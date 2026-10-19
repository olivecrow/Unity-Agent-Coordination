using System;
using System.Linq;

namespace Olivecrow.UnityAgentCoordination
{
    public sealed class CoordinationService
    {
        private readonly CoordinationStore store;
        private readonly Func<DateTime> utcNow;

        public CoordinationService(CoordinationStore store, Func<DateTime> utcNow = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public QueueSnapshot GetStatus() => store.Read(Snapshot);

        public EditorTicket Enqueue(string sessionId, string label = null)
        {
            string session = Required(sessionId, "session_id");
            return store.Change(state =>
            {
                EditorTicket existing = state.Tickets.FirstOrDefault(ticket => ticket.SessionId == session);
                if (existing != null) return existing;
                if (state.NextNumber == long.MaxValue) throw new CoordinationException("queue_full", "Ticket numbering is exhausted.");
                var ticket = new EditorTicket
                {
                    TicketId = Guid.NewGuid().ToString("N"), Number = state.NextNumber++,
                    SessionId = session, Label = label ?? string.Empty,
                    State = TicketStates.Waiting, IssuedUtc = utcNow()
                };
                state.Tickets.Add(ticket);
                return ticket;
            });
        }

        public EditorTicket Acquire(string sessionId, string ticketId, Func<bool> editorReady = null)
        {
            string session = Required(sessionId, "session_id");
            string id = Required(ticketId, "ticket_id");
            return store.Change(state =>
            {
                EditorTicket ticket = OwnedTicket(state, session, id);
                if (ticket.State == TicketStates.Active) return ticket;
                if (state.Tickets.Any(item => item.State == TicketStates.Active))
                    throw new CoordinationException("editor_busy", "Another session is using the Editor.");
                if (state.Tickets.OrderBy(item => item.Number).First().TicketId != id)
                    throw new CoordinationException("not_your_turn", "An earlier ticket is waiting.");
                if (editorReady != null && !editorReady())
                    throw new CoordinationException("editor_busy", "The Editor is compiling, importing, building, or changing Play Mode.");
                ticket.State = TicketStates.Active;
                return ticket;
            });
        }

        public EditorTicket Release(string sessionId, string ticketId)
        {
            string session = Required(sessionId, "session_id");
            string id = Required(ticketId, "ticket_id");
            return store.Change(state =>
            {
                EditorTicket ticket = OwnedTicket(state, session, id);
                if (ticket.State != TicketStates.Active)
                    throw new CoordinationException("ticket_not_active", "Acquire the ticket before returning it, or cancel a waiting ticket.");
                state.Tickets.Remove(ticket);
                ticket.State = TicketStates.Released;
                return ticket;
            });
        }

        public EditorTicket Cancel(string sessionId, string ticketId)
        {
            string session = Required(sessionId, "session_id");
            string id = Required(ticketId, "ticket_id");
            return store.Change(state =>
            {
                EditorTicket ticket = OwnedTicket(state, session, id);
                state.Tickets.Remove(ticket);
                ticket.State = TicketStates.Canceled;
                return ticket;
            });
        }

        // For an operator's explicit recovery action in the Editor window, not the AI protocol.
        public void ResetQueue()
        {
            store.Change(state => { state.Tickets.Clear(); return true; });
        }

        private static EditorTicket OwnedTicket(CoordinationState state, string session, string id)
        {
            EditorTicket ticket = state.Tickets.FirstOrDefault(item => item.TicketId == id);
            if (ticket == null) throw new CoordinationException("ticket_not_found", "This ticket is no longer in the queue.");
            if (ticket.SessionId != session) throw new CoordinationException("not_ticket_owner", "Only the session that drew the ticket may use or return it.");
            return ticket;
        }

        private static QueueSnapshot Snapshot(CoordinationState state)
        {
            var tickets = state.Tickets.OrderBy(ticket => ticket.Number).ToList();
            string active = tickets.FirstOrDefault(ticket => ticket.State == TicketStates.Active)?.TicketId;
            return new QueueSnapshot
            {
                Tickets = tickets, ActiveTicketId = active,
                NextTicketId = active == null ? tickets.FirstOrDefault()?.TicketId : null
            };
        }

        private static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new CoordinationException("missing_parameter", name + " is required.");
            return value.Trim();
        }
    }
}
