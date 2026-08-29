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
        public void SameAgentAndHeadCanRegisterIndependentSessions()
        {
            string secondWorktree = CreateAdditionalWorktree("worker-session-b");

            AgentRecord first = service.BeginWork("codex", CoordinationRoles.Worker, workerWorktree, Sha,
                "First session", new[] { "Assets/FeatureA" }, sessionId: "session-a");
            AgentRecord second = service.BeginWork("codex", CoordinationRoles.Worker, secondWorktree, Sha,
                "Second session", new[] { "Assets/FeatureB" }, sessionId: "session-b");

            Assert.That(first.AgentId, Is.EqualTo(second.AgentId));
            Assert.That(first.HeadSha, Is.EqualTo(second.HeadSha));
            Assert.That(first.SessionId, Is.Not.EqualTo(second.SessionId));
            Assert.That(service.GetStatus(Array.Empty<string>(), null, null).State.Agents,
                Has.Count.EqualTo(2));

            string firstValidation = service.EnqueueValidation("codex", Sha, "First candidate",
                Array.Empty<string>(), Array.Empty<string>(), "session-a");
            string secondValidation = service.EnqueueValidation("codex", Sha, "Second candidate",
                Array.Empty<string>(), Array.Empty<string>(), "session-b");
            Assert.That(firstValidation, Is.Not.EqualTo(secondValidation));

            CoordinationException exception = Assert.Throws<CoordinationException>(() =>
                service.Claim("codex", new[] { "Assets/FeatureA/Subfolder" }, "session-b"));
            Assert.That(exception.Code, Is.EqualTo("scope_conflict"));
        }

        [Test]
        public void LegacyStateUsesAgentIdsAsSessionIdsWhenRead()
        {
            Directory.CreateDirectory(store.StateRoot);
            File.WriteAllText(store.StatePath,
                "{\"SchemaVersion\":1,\"Agents\":[{\"AgentId\":\"legacy-worker\"}]," +
                "\"Claims\":[{\"AgentId\":\"legacy-worker\",\"Scope\":\"assets/legacy\"}]," +
                "\"ValidationRequests\":[{\"RequestId\":\"legacy-request\",\"AgentId\":\"legacy-worker\"}]}");

            CoordinationState state = store.ReadState();

            Assert.That(state.SchemaVersion, Is.EqualTo(CoordinationSchema.CurrentVersion));
            Assert.That(state.Agents[0].SessionId, Is.EqualTo("legacy-worker"));
            Assert.That(state.Claims[0].SessionId, Is.EqualTo("legacy-worker"));
            Assert.That(state.ValidationRequests[0].SessionId, Is.EqualTo("legacy-worker"));
        }

        [Test]
        public void EditorAccessScopesMustAlreadyBeClaimedByTheSession()
        {
            service.BeginWork("codex", CoordinationRoles.Worker, workerWorktree, Sha, "work",
                new[] { "Assets/Claimed" }, sessionId: "session-a");

            CoordinationException exception = Assert.Throws<CoordinationException>(() =>
                service.EnqueueEditorAccess("codex", null, "edit scene", new[] { "scene_edit" },
                    new[] { "Assets/Other" }, sessionId: "session-a"));

            Assert.That(exception.Code, Is.EqualTo("scope_not_claimed"));
            Assert.DoesNotThrow(() => service.EnqueueEditorAccess("codex", null, "inspect console",
                new[] { "console" }, Array.Empty<string>(), sessionId: "session-a"));
        }

        [Test]
        public void EditorAccessQueueIsSessionScopedFifoAndDoesNotRequireACommit()
        {
            string secondWorktree = CreateAdditionalWorktree("worker-session-b");
            service.BeginWork("codex", CoordinationRoles.Worker, workerWorktree, Sha, "First session",
                new[] { "Assets/FeatureA" }, sessionId: "session-a");
            service.BeginWork("codex", CoordinationRoles.Worker, secondWorktree, Sha, "Second session",
                new[] { "Assets/FeatureB" }, sessionId: "session-b");

            string firstRequest = service.EnqueueEditorAccess("codex", null, "Compile uncommitted work",
                new[] { "import", "compile", "console" }, new[] { "Assets/FeatureA" },
                sessionId: "session-a");
            string idempotentRetry = service.EnqueueEditorAccess("codex", null,
                "Compile uncommitted work", new[] { "compile" }, Array.Empty<string>(), "compile-a",
                "session-a");
            string sameRetry = service.EnqueueEditorAccess("codex", Sha,
                "Changed retry payload", new[] { "playmode" }, Array.Empty<string>(), "compile-a",
                "session-a");
            string secondRequest = service.EnqueueEditorAccess("codex", Sha, "Open Play Mode",
                new[] { "playmode" }, new[] { "Assets/FeatureB" }, sessionId: "session-b");

            Assert.That(firstRequest, Is.Not.EqualTo(secondRequest));
            Assert.That(idempotentRetry, Is.EqualTo(sameRetry));
            Assert.That(service.GetStatus(Array.Empty<string>(), null, null).State
                .FindEditorAccessRequest(firstRequest).BaseHeadSha, Is.Null);

            CoordinationException outOfOrder = Assert.Throws<CoordinationException>(() =>
                service.AcquireEditorAccess("codex", secondRequest, "session-b"));
            Assert.That(outOfOrder.Code, Is.EqualTo("request_not_next"));

            EditorAccessRequest active = service.AcquireEditorAccess("codex", firstRequest, "session-a");
            Assert.That(active.State, Is.EqualTo(EditorAccessStates.Active));
            service.FinishEditorAccess("codex", firstRequest, "compiled", "session-a");
            CoordinationException completedCancel = Assert.Throws<CoordinationException>(() =>
                service.CancelEditorAccessRequest("codex", firstRequest, "too late", "session-a"));
            Assert.That(completedCancel.Code, Is.EqualTo("request_not_cancelable"));

            active = service.AcquireEditorAccess("codex", idempotentRetry, "session-a");
            Assert.That(active.RequestId, Is.EqualTo(idempotentRetry));
            service.CancelEditorAccessRequest("codex", idempotentRetry, "not needed", "session-a");

            active = service.AcquireEditorAccess("codex", secondRequest, "session-b");
            Assert.That(active.SessionId, Is.EqualTo("session-b"));
            service.FinishEditorAccess("codex", secondRequest, null, "session-b");

            service.FinishWork("codex", "session-a");
            service.FinishWork("codex", "session-b");
            Assert.That(store.HasCoordinationState, Is.False);
        }

        [Test]
        public void EditorAccessAndExactShaValidationLeasesAreMutuallyExclusive()
        {
            string validationWorktree = CreateAdditionalWorktree("validation-worker");
            service.BeginWork("access-worker", CoordinationRoles.Worker, workerWorktree, Sha, "access",
                new[] { "Assets/Access" });
            service.BeginWork("validation-worker", CoordinationRoles.Worker, validationWorktree, Sha,
                "validation", new[] { "Assets/Validation" });
            service.BeginWork("integrator", CoordinationRoles.Integrator, repositoryRoot, Sha, "integrate",
                Array.Empty<string>());

            string accessRequest = service.EnqueueEditorAccess("access-worker", null, "compile",
                new[] { "compile" }, new[] { "Assets/Access" });
            string validationRequest = service.EnqueueValidation("validation-worker", Sha, "validate",
                Array.Empty<string>(), Array.Empty<string>());
            service.AcquireEditorAccess("access-worker", accessRequest);

            CoordinationException validationBlocked = Assert.Throws<CoordinationException>(() =>
                service.AcquireValidation("integrator", validationRequest, Sha));
            Assert.That(validationBlocked.Code, Is.EqualTo("editor_access_busy"));

            service.FinishEditorAccess("access-worker", accessRequest, null);
            service.AcquireValidation("integrator", validationRequest, Sha);
            string secondAccessRequest = service.EnqueueEditorAccess("access-worker", null, "console",
                new[] { "console" }, Array.Empty<string>());

            CoordinationException accessBlocked = Assert.Throws<CoordinationException>(() =>
                service.AcquireEditorAccess("access-worker", secondAccessRequest));
            Assert.That(accessBlocked.Code, Is.EqualTo("validation_busy"));
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
