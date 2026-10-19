using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Olivecrow.UnityAgentCoordination.Tests
{
    public sealed class CoordinationServiceTests
    {
        private string root;
        private CoordinationStore store;
        private CoordinationService service;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "editor-ticket-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            store = new CoordinationStore(root);
            service = new CoordinationService(store, () => new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc));
        }

        [TearDown]
        public void TearDown() { store.Dispose(); Directory.Delete(root, true); }

        [Test]
        public void EmptyStatusDoesNotCreateStateFiles()
        {
            Assert.That(service.GetStatus().Tickets, Is.Empty);
            Assert.That(File.Exists(store.StatePath), Is.False);
        }

        [Test]
        public void FullCycleNeedsOnlySessionAndTicketWithoutVcsOrExecutables()
        {
            Directory.CreateDirectory(Path.Combine(root, ".plastic"));
            string previousPath = Environment.GetEnvironmentVariable("PATH");
            try
            {
                Environment.SetEnvironmentVariable("PATH", string.Empty);
                EditorTicket ticket = service.Enqueue("ai-task");
                Assert.That(service.Acquire("ai-task", ticket.TicketId).State, Is.EqualTo(TicketStates.Active));
                Assert.That(service.Release("ai-task", ticket.TicketId).State, Is.EqualTo(TicketStates.Released));
                Assert.That(service.GetStatus().Tickets, Is.Empty);
                Assert.That(Directory.Exists(Path.Combine(root, ".git")), Is.False);
            }
            finally { Environment.SetEnvironmentVariable("PATH", previousPath); }
        }

        [Test]
        public void DistinctSessionsReceiveDistinctOrderedTicketsEvenAtTheSameTime()
        {
            EditorTicket a = service.Enqueue("session-a");
            EditorTicket b = service.Enqueue("session-b");
            Assert.That(a.TicketId, Is.Not.EqualTo(b.TicketId));
            Assert.That(b.Number, Is.GreaterThan(a.Number));
            Assert.That(a.IssuedUtc, Is.EqualTo(b.IssuedUtc));
            Assert.That(service.GetStatus().NextTicketId, Is.EqualTo(a.TicketId));
        }

        [Test]
        public void EnqueueRetryReturnsTheSessionsOutstandingTicket()
        {
            EditorTicket ticket = service.Enqueue("session-a", "first label");
            Assert.That(service.Enqueue("session-a", "retry label").TicketId, Is.EqualTo(ticket.TicketId));
            service.Acquire("session-a", ticket.TicketId);
            Assert.That(service.Enqueue("session-a").TicketId, Is.EqualTo(ticket.TicketId));
            Assert.That(service.GetStatus().Tickets, Has.Count.EqualTo(1));
        }

        [Test]
        public void LaterTicketCannotJumpTheQueue()
        {
            service.Enqueue("a");
            EditorTicket b = service.Enqueue("b");
            Assert.That(Assert.Throws<CoordinationException>(() => service.Acquire("b", b.TicketId)).Code,
                Is.EqualTo("not_your_turn"));
        }

        [Test]
        public void ActiveTicketBlocksOthersUntilItIsReturned()
        {
            EditorTicket a = service.Enqueue("a");
            EditorTicket b = service.Enqueue("b");
            service.Acquire("a", a.TicketId);
            Assert.That(service.GetStatus().NextTicketId, Is.Null);
            Assert.That(Assert.Throws<CoordinationException>(() => service.Acquire("b", b.TicketId)).Code,
                Is.EqualTo("editor_busy"));
            service.Release("a", a.TicketId);
            Assert.That(service.GetStatus().NextTicketId, Is.EqualTo(b.TicketId));
            Assert.That(service.Acquire("b", b.TicketId).State, Is.EqualTo(TicketStates.Active));
        }

        [Test]
        public void AnotherSessionCannotAcquireReturnOrCancelYourTicket()
        {
            EditorTicket a = service.Enqueue("a");
            Assert.That(Assert.Throws<CoordinationException>(() => service.Acquire("b", a.TicketId)).Code, Is.EqualTo("not_ticket_owner"));
            service.Acquire("a", a.TicketId);
            Assert.That(Assert.Throws<CoordinationException>(() => service.Release("b", a.TicketId)).Code, Is.EqualTo("not_ticket_owner"));
            Assert.That(Assert.Throws<CoordinationException>(() => service.Cancel("b", a.TicketId)).Code, Is.EqualTo("not_ticket_owner"));
            Assert.That(service.GetStatus().ActiveTicketId, Is.EqualTo(a.TicketId));
        }

        [Test]
        public void ReadinessBlocksNewAcquisitionButAllowsTheOwnersRetry()
        {
            EditorTicket a = service.Enqueue("a");
            Assert.That(Assert.Throws<CoordinationException>(() => service.Acquire("a", a.TicketId, () => false)).Code, Is.EqualTo("editor_busy"));
            Assert.That(service.GetStatus().NextTicketId, Is.EqualTo(a.TicketId));
            service.Acquire("a", a.TicketId, () => true);
            Assert.That(service.Acquire("a", a.TicketId, () => false).State, Is.EqualTo(TicketStates.Active));
        }

        [Test]
        public void WaitingTicketMustBeCanceledInsteadOfReturned()
        {
            EditorTicket a = service.Enqueue("a");
            Assert.That(Assert.Throws<CoordinationException>(() => service.Release("a", a.TicketId)).Code, Is.EqualTo("ticket_not_active"));
            service.Cancel("a", a.TicketId);
            Assert.That(service.GetStatus().Tickets, Is.Empty);
        }

        [Test]
        public void CancelingAnAbandonedActiveTicketLetsTheNextSessionProceed()
        {
            EditorTicket a = service.Enqueue("a");
            EditorTicket b = service.Enqueue("b");
            service.Acquire("a", a.TicketId);
            service.Cancel("a", a.TicketId);
            Assert.That(service.Acquire("b", b.TicketId).State, Is.EqualTo(TicketStates.Active));
        }

        [Test]
        public void CompletedSessionCanDrawANewTicketWithoutASeparateWorkLifecycle()
        {
            EditorTicket a = service.Enqueue("a");
            service.Acquire("a", a.TicketId);
            service.Release("a", a.TicketId);
            EditorTicket next = service.Enqueue("a");
            Assert.That(next.TicketId, Is.Not.EqualTo(a.TicketId));
            Assert.That(next.Number, Is.GreaterThan(a.Number));
            Assert.That(service.GetStatus().Tickets, Has.Count.EqualTo(1));
        }

        [Test]
        public void ReloadingTheServicePreservesTheActiveTicketAndWaitingOrder()
        {
            EditorTicket a = service.Enqueue("a");
            EditorTicket b = service.Enqueue("b");
            service.Acquire("a", a.TicketId);
            store.Dispose();
            store = new CoordinationStore(root);
            service = new CoordinationService(store);
            Assert.That(service.GetStatus().ActiveTicketId, Is.EqualTo(a.TicketId));
            service.Release("a", a.TicketId);
            Assert.That(service.Acquire("b", b.TicketId).State, Is.EqualTo(TicketStates.Active));
        }

        [Test]
        public void ProjectFoldersHaveIndependentQueues()
        {
            service.Enqueue("same-session");
            using (var otherStore = new CoordinationStore(Path.Combine(root, "other-project")))
            {
                var other = new CoordinationService(otherStore);
                Assert.That(other.GetStatus().Tickets, Is.Empty);
                EditorTicket ticket = other.Enqueue("same-session");
                Assert.That(other.Acquire("same-session", ticket.TicketId).State, Is.EqualTo(TicketStates.Active));
            }
            Assert.That(service.GetStatus().ActiveTicketId, Is.Null);
        }

        [Test]
        public void ConcurrentStoreInstancesIssueUniqueSequentialNumbers()
        {
            Parallel.For(0, 24, index =>
            {
                using (var concurrent = new CoordinationStore(root))
                    new CoordinationService(concurrent).Enqueue("session-" + index);
            });
            long[] numbers = service.GetStatus().Tickets.Select(ticket => ticket.Number).ToArray();
            Assert.That(numbers, Is.EqualTo(Enumerable.Range(1, 24).Select(number => (long)number).ToArray()));
        }

        [Test]
        public void CorruptStateFailsClosedAndIsNotOverwritten()
        {
            service.Enqueue("a");
            File.WriteAllText(store.StatePath, "broken json");
            Assert.That(Assert.Throws<CoordinationException>(() => service.Enqueue("b")).Code, Is.EqualTo("state_corrupt"));
            Assert.That(File.ReadAllText(store.StatePath), Is.EqualTo("broken json"));
        }

        [Test]
        public void MultipleActiveTicketsAreRejectedInsteadOfGrantingConcurrentAccess()
        {
            service.Enqueue("a");
            var state = new CoordinationState { NextNumber = 3 };
            state.Tickets.Add(new EditorTicket { TicketId = "one", Number = 1, SessionId = "a", State = TicketStates.Active, IssuedUtc = DateTime.UtcNow });
            state.Tickets.Add(new EditorTicket { TicketId = "two", Number = 2, SessionId = "b", State = TicketStates.Active, IssuedUtc = DateTime.UtcNow });
            File.WriteAllText(store.StatePath, QueueJson.Serialize(state));
            Assert.That(Assert.Throws<CoordinationException>(() => service.GetStatus()).Code, Is.EqualTo("state_corrupt"));
        }

        [Test]
        public void OperatorResetClearsAnAbandonedQueue()
        {
            EditorTicket a = service.Enqueue("a");
            service.Enqueue("b");
            service.Acquire("a", a.TicketId);
            service.ResetQueue();
            Assert.That(service.GetStatus().Tickets, Is.Empty);
            EditorTicket c = service.Enqueue("c");
            Assert.That(service.Acquire("c", c.TicketId).State, Is.EqualTo(TicketStates.Active));
        }
    }
}
