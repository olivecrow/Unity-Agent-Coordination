using System;
using System.IO;
using NUnit.Framework;

namespace Olivecrow.UnityAgentCoordination.Tests
{
    public sealed class CoordinationServiceTests
    {
        private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private string previousStateRoot;
        private string fixtureRoot;
        private string repositoryRoot;
        private string workerWorktree;
        private CoordinationStore store;
        private CoordinationService service;

        [SetUp]
        public void SetUp()
        {
            fixtureRoot = Path.Combine(Path.GetTempPath(), "UnityAgentCoordinationTests",
                Guid.NewGuid().ToString("N"));
            repositoryRoot = Path.Combine(fixtureRoot, "repository");
            workerWorktree = Path.Combine(fixtureRoot, "worker");
            Directory.CreateDirectory(Path.Combine(repositoryRoot, ".git", "refs", "heads"));
            File.WriteAllText(Path.Combine(repositoryRoot, ".git", "HEAD"), "ref: refs/heads/main\n");
            File.WriteAllText(Path.Combine(repositoryRoot, ".git", "refs", "heads", "main"), Sha + "\n");

            string workerGit = Path.Combine(repositoryRoot, ".git", "worktrees", "worker");
            Directory.CreateDirectory(workerGit);
            Directory.CreateDirectory(workerWorktree);
            File.WriteAllText(Path.Combine(workerWorktree, ".git"), "gitdir: " + workerGit + "\n");
            File.WriteAllText(Path.Combine(workerGit, "commondir"), "../..\n");
            File.WriteAllText(Path.Combine(workerGit, "HEAD"), Sha + "\n");

            previousStateRoot = Environment.GetEnvironmentVariable("UNITY_AGENT_COORDINATOR_ROOT");
            Environment.SetEnvironmentVariable("UNITY_AGENT_COORDINATOR_ROOT", Path.Combine(fixtureRoot, "state"));
            store = new CoordinationStore(repositoryRoot);
            service = new CoordinationService(store);
        }

        [TearDown]
        public void TearDown()
        {
            Environment.SetEnvironmentVariable("UNITY_AGENT_COORDINATOR_ROOT", previousStateRoot);
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, true);
            }
        }

        [Test]
        public void WorktreesShareRepositoryIdentity()
        {
            Assert.That(RepositoryIdentity.ResolveRepositoryId(repositoryRoot),
                Is.EqualTo(RepositoryIdentity.ResolveRepositoryId(workerWorktree)));
            Assert.That(RepositoryIdentity.TryReadHeadSha(workerWorktree), Is.EqualTo(Sha));
        }

        [Test]
        public void ParentAndChildScopesConflictAcrossAgents()
        {
            service.BeginWork("worker-a", CoordinationRoles.Worker, workerWorktree, Sha, "A",
                new[] { "Assets/Feature" });
            string secondWorktree = CreateAdditionalWorktree("worker-b");

            CoordinationException exception = Assert.Throws<CoordinationException>(() =>
                service.BeginWork("worker-b", CoordinationRoles.Worker, secondWorktree, Sha, "B",
                    new[] { "Assets/Feature/Subfolder" }));

            Assert.That(exception.Code, Is.EqualTo("scope_conflict"));
        }

        [Test]
        public void WorkMetadataIsRecordedAndHeartbeatUpdatesTheCurrentPhase()
        {
            AgentRecord agent = service.BeginWork("worker", CoordinationRoles.Worker, workerWorktree, Sha,
                "Inspect package structure", new[] { "Packages/Feature" }, "package", AgentWorkPhases.Inspecting);

            Assert.That(agent.WorkKind, Is.EqualTo("package"));
            Assert.That(agent.Phase, Is.EqualTo(AgentWorkPhases.Inspecting));
            Assert.That(agent.Summary, Is.EqualTo("Inspect package structure"));

            agent = service.Heartbeat("worker", null, "Implement Editor UI", AgentWorkPhases.Working);

            Assert.That(agent.WorkKind, Is.EqualTo("package"));
            Assert.That(agent.Phase, Is.EqualTo(AgentWorkPhases.Working));
            Assert.That(agent.Summary, Is.EqualTo("Implement Editor UI"));
        }

        [Test]
        public void ValidationIsSerialAndStateDisappearsAfterAllWorkFinishes()
        {
            service.BeginWork("worker", CoordinationRoles.Worker, workerWorktree, Sha, "candidate",
                new[] { "Assets/Feature" });
            service.BeginWork("integrator", CoordinationRoles.Integrator, repositoryRoot, Sha, "validate",
                Array.Empty<string>());
            string requestId = service.EnqueueValidation("worker", Sha, "candidate",
                Array.Empty<string>(), Array.Empty<string>());

            ValidationRequest request = service.AcquireValidation("integrator", requestId, Sha);
            Assert.That(request.State, Is.EqualTo(ValidationStates.Running));
            Assert.Throws<CoordinationException>(() =>
                service.AcquireValidation("integrator", requestId, Sha));

            service.MarkCompilationFinished(requestId);
            request = service.FinishValidation("integrator", requestId, true, "ok");
            Assert.That(request.State, Is.EqualTo(ValidationStates.Passed));

            service.FinishWork("worker");
            service.FinishWork("integrator");
            Assert.That(store.HasCoordinationState, Is.False);
        }

        [Test]
        public void ValidationLifecycleUpdatesWorkerAndIntegratorPhases()
        {
            service.BeginWork("worker", CoordinationRoles.Worker, workerWorktree, Sha, "candidate",
                new[] { "Assets/Feature" }, "code", AgentWorkPhases.Working);
            service.BeginWork("integrator", CoordinationRoles.Integrator, repositoryRoot, Sha, "validate",
                Array.Empty<string>(), "validation", AgentWorkPhases.Inspecting);

            string requestId = service.EnqueueValidation("worker", Sha, "candidate",
                Array.Empty<string>(), Array.Empty<string>());
            Assert.That(service.GetStatus(Array.Empty<string>(), null, null).State.Agents
                .Find(agent => agent.AgentId == "worker").Phase, Is.EqualTo(AgentWorkPhases.WaitingValidation));

            service.AcquireValidation("integrator", requestId, Sha);
            Assert.That(service.GetStatus(Array.Empty<string>(), null, null).State.Agents
                .Find(agent => agent.AgentId == "integrator").Phase, Is.EqualTo(AgentWorkPhases.Validating));

            service.MarkCompilationFinished(requestId);
            service.FinishValidation("integrator", requestId, true, "ok");
            CoordinationState state = service.GetStatus(Array.Empty<string>(), null, null).State;
            Assert.That(state.Agents.Find(agent => agent.AgentId == "worker").Phase,
                Is.EqualTo(AgentWorkPhases.Finalizing));
            Assert.That(state.Agents.Find(agent => agent.AgentId == "integrator").Phase,
                Is.EqualTo(AgentWorkPhases.Finalizing));
        }

        [Test]
        public void StatusPrunesStaleAgentClaimsAndQueuedValidation()
        {
            service.BeginWork("worker", CoordinationRoles.Worker, workerWorktree, Sha, "abandoned",
                new[] { "Assets/Feature" });
            service.EnqueueValidation("worker", Sha, "abandoned", Array.Empty<string>(), Array.Empty<string>());
            store.Mutate(state =>
            {
                state.Agents[0].HeartbeatUtc = DateTime.UtcNow.AddMinutes(-46);
                return true;
            });

            CoordinationSnapshot snapshot = service.GetStatus(Array.Empty<string>(), null, null);

            Assert.That(snapshot.State.Agents, Is.Empty);
            Assert.That(snapshot.State.Claims, Is.Empty);
            Assert.That(snapshot.State.ValidationRequests, Is.Empty);
            Assert.That(store.HasCoordinationState, Is.False);
        }

        [Test]
        public void EditorReleaseClearsOwnerAndCoordinationFiles()
        {
            string sessionId = Guid.NewGuid().ToString("N");
            Assert.That(store.TryAcquireEditor(new EditorOwnerRecord
            {
                SessionId = sessionId,
                ProjectPath = repositoryRoot,
                UnityVersion = "test",
                ProcessId = 1,
                StartedUtc = DateTime.UtcNow,
                HeartbeatUtc = DateTime.UtcNow
            }, TimeSpan.FromMinutes(1)), Is.True);
            service.BeginWork("worker", CoordinationRoles.Worker, workerWorktree, Sha, "work",
                new[] { "Assets/Feature" });

            store.ReleaseEditorAndClear(sessionId);

            Assert.That(File.Exists(store.EditorOwnerPath), Is.False);
            Assert.That(File.Exists(store.StatePath), Is.False);
        }

        [Test]
        public void EditorOwnershipHandoffPreservesWorkerCoordinationState()
        {
            string sessionId = Guid.NewGuid().ToString("N");
            Assert.That(store.TryAcquireEditor(new EditorOwnerRecord
            {
                SessionId = sessionId,
                ProjectPath = repositoryRoot,
                UnityVersion = "test",
                ProcessId = 1,
                StartedUtc = DateTime.UtcNow,
                HeartbeatUtc = DateTime.UtcNow
            }, TimeSpan.FromMinutes(1)), Is.True);
            service.BeginWork("worker", CoordinationRoles.Worker, workerWorktree, Sha, "work",
                new[] { "Assets/Feature" });

            store.ReleaseEditorOwnership(sessionId);

            Assert.That(File.Exists(store.EditorOwnerPath), Is.False);
            Assert.That(File.Exists(store.StatePath), Is.True);
            Assert.That(store.ReadState().Agents, Has.Count.EqualTo(1));
        }

        private string CreateAdditionalWorktree(string name)
        {
            string path = Path.Combine(fixtureRoot, name);
            string gitPath = Path.Combine(repositoryRoot, ".git", "worktrees", name);
            Directory.CreateDirectory(path);
            Directory.CreateDirectory(gitPath);
            File.WriteAllText(Path.Combine(path, ".git"), "gitdir: " + gitPath + "\n");
            File.WriteAllText(Path.Combine(gitPath, "commondir"), "../..\n");
            File.WriteAllText(Path.Combine(gitPath, "HEAD"), Sha + "\n");
            return path;
        }
    }
}
