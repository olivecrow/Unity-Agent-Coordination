using System;
using System.Linq;
using System.IO;

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

        public EditorTicket Enqueue(string sessionId, string label = null, string program = null, string model = null,
            string reasoningEffort = null, string sessionName = null, string notificationToken = null)
        {
            string session = Required(sessionId, "session_id");
            if (notificationToken != null && (notificationToken.Length != 32 || !notificationToken.All(Uri.IsHexDigit)))
                throw new CoordinationException("invalid_notification", "notification_token must be 32 hexadecimal characters.");
            return store.Change(state =>
            {
                EditorTicket existing = state.Tickets.FirstOrDefault(ticket => ticket.SessionId == session);
                if (existing != null) return existing;
                if (notificationToken != null && state.Tickets.Any(ticket => string.Equals(ticket.NotificationToken, notificationToken, StringComparison.OrdinalIgnoreCase)))
                    throw new CoordinationException("notification_in_use", "The notification token is already registered by another ticket.");
                if (state.NextNumber == long.MaxValue) throw new CoordinationException("queue_full", "Ticket numbering is exhausted.");
                var ticket = new EditorTicket
                {
                    TicketId = Guid.NewGuid().ToString("N"), Number = state.NextNumber++,
                    SessionId = session, Label = label ?? string.Empty,
                    Program = program, Model = model, ReasoningEffort = reasoningEffort, SessionName = sessionName,
                    NotificationToken = notificationToken,
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
            EditorTicket result = store.Change(state =>
            {
                EditorTicket ticket = FindTicket(state, id, session);
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
            ClearNotification(result);
            return result;
        }

        public EditorTicket Release(string sessionId, string ticketId)
        {
            string session = Required(sessionId, "session_id");
            string id = Required(ticketId, "ticket_id");
            EditorTicket result = store.Change(state =>
            {
                EditorTicket ticket = FindTicket(state, id, session);
                if (ticket.State != TicketStates.Active)
                    throw new CoordinationException("ticket_not_active", "Acquire the ticket before returning it, or cancel a waiting ticket.");
                state.Tickets.Remove(ticket);
                ticket.State = TicketStates.Released;
                return ticket;
            });
            ClearNotification(result);
            NotifyNext();
            return result;
        }

        public EditorTicket Cancel(string sessionId, string ticketId)
            => CancelTicket(ticketId, Required(sessionId, "session_id"), allowActive: true);

        // For an operator's explicit recovery action in the Editor window, not the AI protocol.
        public EditorTicket CancelFromEditor(string ticketId, bool allowActive)
            => CancelTicket(ticketId, session: null, allowActive: allowActive);

        private EditorTicket CancelTicket(string ticketId, string session, bool allowActive)
        {
            string id = Required(ticketId, "ticket_id");
            EditorTicket result = store.Change(state =>
            {
                EditorTicket ticket = FindTicket(state, id, session);
                if (!allowActive && ticket.State == TicketStates.Active)
                    throw new CoordinationException("ticket_became_active", "The ticket is now active. Review its current state before canceling it.");
                state.Tickets.Remove(ticket);
                ticket.State = TicketStates.Canceled;
                return ticket;
            });
            ClearNotification(result);
            NotifyNext();
            return result;
        }

        // A notification is only a hint; clients must recheck the queue and acquire normally.
        private void NotifyNext()
        {
            try
            {
                QueueSnapshot snapshot = GetStatus();
                EditorTicket next = snapshot.Tickets.FirstOrDefault(ticket => ticket.TicketId == snapshot.NextTicketId);
                if (next == null || string.IsNullOrEmpty(next.NotificationToken)) return;
                string path = NotificationPath(next.NotificationToken);
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary, QueueJson.Serialize(new TicketNotification { TicketId = next.TicketId, Token = next.NotificationToken }));
                    if (!File.Exists(path)) File.Move(temporary, path);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is CoordinationException)
            {
                // Delivery is best-effort. It must never turn a successful return into a failure.
            }
        }

        private string NotificationPath(string token)
            => Path.Combine(Path.GetDirectoryName(store.StatePath), "notify-" + token + ".json");

        private void ClearNotification(EditorTicket ticket)
        {
            if (ticket == null || string.IsNullOrEmpty(ticket.NotificationToken)) return;
            try { File.Delete(NotificationPath(ticket.NotificationToken)); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        // For an operator's explicit recovery action in the Editor window, not the AI protocol.
        public void ResetQueue()
        {
            EditorTicket[] removed = store.Change(state => { var tickets = state.Tickets.ToArray(); state.Tickets.Clear(); return tickets; });
            foreach (EditorTicket ticket in removed) ClearNotification(ticket);
        }

        private static EditorTicket FindTicket(CoordinationState state, string id, string session)
        {
            EditorTicket ticket = state.Tickets.FirstOrDefault(item => item.TicketId == id);
            if (ticket == null) throw new CoordinationException("ticket_not_found", "This ticket is no longer in the queue.");
            if (session != null && ticket.SessionId != session) throw new CoordinationException("not_ticket_owner", "Only the session that drew the ticket may use or return it.");
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
