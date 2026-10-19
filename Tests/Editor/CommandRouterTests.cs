using System;
using System.IO;
using NUnit.Framework;

namespace Olivecrow.UnityAgentCoordination.Tests
{
    public sealed class CommandRouterTests
    {
        private string root;
        private CoordinationStore store;
        private CoordinationService service;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "ticket-protocol-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            store = new CoordinationStore(root);
            service = new CoordinationService(store);
        }

        [TearDown]
        public void TearDown() { store.Dispose(); Directory.Delete(root, true); }

        [Test]
        public void PublicJsonLifecycleWorksInAVcsFreeProject()
        {
            CoordinationCommandResult a = CoordinationProtocol.ExecuteJson("{\"action\":\"enqueue\",\"session_id\":\"a\",\"label\":\"씬 검사\"}", service);
            CoordinationCommandResult b = CoordinationProtocol.ExecuteJson("{\"action\":\"enqueue\",\"session_id\":\"b\"}", service);
            Assert.That(a.Success && b.Success, Is.True);
            Assert.That(a.Ticket.Label, Is.EqualTo("씬 검사"));
            Assert.That(b.Queue.NextTicketId, Is.EqualTo(a.Ticket.TicketId));
            Assert.That(Dispatch("acquire", "b", b.Ticket.TicketId).ErrorCode, Is.EqualTo("not_your_turn"));
            Assert.That(Dispatch("acquire", "a", a.Ticket.TicketId).Success, Is.True);
            Assert.That(Dispatch("acquire", "b", b.Ticket.TicketId).ErrorCode, Is.EqualTo("editor_busy"));
            Assert.That(Dispatch("release", "a", a.Ticket.TicketId).Queue.NextTicketId, Is.EqualTo(b.Ticket.TicketId));
            Assert.That(Dispatch("acquire", "b", b.Ticket.TicketId).Success, Is.True);
            CoordinationCommandResult completed = Dispatch("release", "b", b.Ticket.TicketId);
            Assert.That(completed.Queue.Tickets, Is.Empty);
            Assert.That(QueueJson.Deserialize<CoordinationCommandResult>(completed.ToJson()).Success, Is.True);
            Assert.That(Directory.Exists(Path.Combine(root, ".git")), Is.False);
        }

        [Test]
        public void StatusDoesNotRequireSessionRegistration()
        {
            CoordinationCommandResult result = CoordinationProtocol.ExecuteJson("{\"action\":\"status\"}", service);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Queue.Tickets, Is.Empty);
        }

        [Test]
        public void EmptyMalformedAndNonObjectPayloadsReturnStableErrors()
        {
            Assert.That(CoordinationProtocol.ExecuteJson(null, service).ErrorCode, Is.EqualTo("parameters_required"));
            foreach (string json in new[] { "[]", "null", "{", "false" })
                Assert.That(CoordinationProtocol.ExecuteJson(json, service).ErrorCode, Is.EqualTo("invalid_json"), json);
            Assert.That(CoordinationProtocol.ExecuteJson("{}", service).ErrorCode, Is.EqualTo("missing_parameter"));
        }

        [Test]
        public void CommandsRequireOnlyTheirSessionAndTicketFields()
        {
            Assert.That(CoordinationProtocol.ExecuteJson("{\"action\":\"enqueue\"}", service).ErrorCode, Is.EqualTo("missing_parameter"));
            Assert.That(Dispatch("acquire", "a", null).ErrorCode, Is.EqualTo("missing_parameter"));
            Assert.That(Dispatch("release", "a", "missing").ErrorCode, Is.EqualTo("ticket_not_found"));
        }

        [Test]
        public void EditorReadinessIsEnforcedThroughTheJsonProtocol()
        {
            EditorTicket ticket = service.Enqueue("a");
            var command = new CoordinationCommand { Action = "acquire", SessionId = "a", TicketId = ticket.TicketId };
            Assert.That(CoordinationProtocol.ExecuteJson(QueueJson.Serialize(command), service, () => false).ErrorCode, Is.EqualTo("editor_busy"));
            Assert.That(CoordinationProtocol.ExecuteJson(QueueJson.Serialize(command), service, () => true).Success, Is.True);
        }

        [Test]
        public void UnknownActionsDoNotEnterASecondWorkflow()
        {
            CoordinationCommandResult result = Dispatch("unknown", "a", null);
            Assert.That(result.ErrorCode, Is.EqualTo("unknown_action"));
            Assert.That(service.GetStatus().Tickets, Is.Empty);
        }

        private CoordinationCommandResult Dispatch(string action, string session, string ticket)
            => CoordinationProtocol.ExecuteJson(QueueJson.Serialize(new CoordinationCommand
                { Action = action, SessionId = session, TicketId = ticket }), service);
    }
}
