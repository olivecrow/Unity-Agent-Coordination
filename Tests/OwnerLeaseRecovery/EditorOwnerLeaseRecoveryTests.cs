using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Olivecrow.UnityAgentCoordination.Tests
{
    public sealed class EditorOwnerLeaseRecoveryTests
    {
        private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private string previousStateRoot;
        private string fixtureRoot;
        private string repositoryRoot;
        private CoordinationStore store;
        private CoordinationService service;

        [SetUp]
        public void SetUp()
        {
            fixtureRoot = Path.Combine(Path.GetTempPath(), "UnityAgentOwnerLeaseTests",
                Guid.NewGuid().ToString("N"));
            repositoryRoot = Path.Combine(fixtureRoot, "repository");
            Directory.CreateDirectory(Path.Combine(repositoryRoot, ".git", "refs", "heads"));
            File.WriteAllText(Path.Combine(repositoryRoot, ".git", "HEAD"), "ref: refs/heads/main\n");
            File.WriteAllText(Path.Combine(repositoryRoot, ".git", "refs", "heads", "main"), Sha + "\n");

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
        public void RecoverEditor_ReplacesExitedProcessImmediatelyAndPreservesCoordination()
        {
            service.BeginWork("worker", CoordinationRoles.Worker, repositoryRoot, Sha, "queued work",
                new[] { "Assets/Feature" });
            Assert.That(store.TryAcquireEditor(CreateOwner("exited", int.MaxValue,
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow), TimeSpan.FromSeconds(90)), Is.True);

            Assert.That(store.TryRecoverEditor(CreateCurrentProcessOwner("recovered"),
                TimeSpan.FromSeconds(90)), Is.True);

            Assert.That(store.ReadEditorOwner().SessionId, Is.EqualTo("recovered"));
            Assert.That(store.HasCoordinationState, Is.True);
            Assert.That(store.ReadState().Agents, Has.Count.EqualTo(1));
        }

        [Test]
        public void RecoverEditor_ReplacesLiveProcessAfterHeartbeatExpires()
        {
            EditorOwnerRecord currentProcessOwner = CreateCurrentProcessOwner("unresponsive");
            currentProcessOwner.HeartbeatUtc = DateTime.UtcNow.AddMinutes(-2);
            Assert.That(store.TryAcquireEditor(currentProcessOwner, TimeSpan.FromSeconds(90)), Is.True);

            Assert.That(store.TryRecoverEditor(CreateCurrentProcessOwner("recovered"),
                TimeSpan.FromSeconds(90)), Is.True);

            Assert.That(store.ReadEditorOwner().SessionId, Is.EqualTo("recovered"));
        }

        [Test]
        public void RecoverEditor_DoesNotReplaceHealthyOwner()
        {
            Assert.That(store.TryAcquireEditor(CreateCurrentProcessOwner("healthy"),
                TimeSpan.FromSeconds(90)), Is.True);

            Assert.That(store.TryRecoverEditor(CreateCurrentProcessOwner("contender"),
                TimeSpan.FromSeconds(90)), Is.False);

            Assert.That(store.ReadEditorOwner().SessionId, Is.EqualTo("healthy"));
        }

        [Test]
        public void RecoverEditor_DoesNotClaimEmptyHandoffWindow()
        {
            Assert.That(store.TryRecoverEditor(CreateCurrentProcessOwner("relinquished"),
                TimeSpan.FromSeconds(90)), Is.False);
            Assert.That(File.Exists(store.EditorOwnerPath), Is.False);
        }

        private static EditorOwnerRecord CreateCurrentProcessOwner(string sessionId)
        {
            using (Process process = Process.GetCurrentProcess())
            {
                return CreateOwner(sessionId, process.Id, process.StartTime.ToUniversalTime(), DateTime.UtcNow);
            }
        }

        private static EditorOwnerRecord CreateOwner(string sessionId, int processId, DateTime startedUtc,
            DateTime heartbeatUtc)
        {
            return new EditorOwnerRecord
            {
                SessionId = sessionId,
                ProjectPath = "test",
                UnityVersion = "test",
                ProcessId = processId,
                StartedUtc = startedUtc,
                HeartbeatUtc = heartbeatUtc
            };
        }
    }
}
