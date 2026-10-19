using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

namespace Olivecrow.UnityAgentCoordination
{
    public sealed class CoordinationStore : IDisposable
    {
        private readonly object gate = new object();
        private readonly Mutex mutex;

        public CoordinationStore(string projectPath)
        {
            if (string.IsNullOrWhiteSpace(projectPath)) throw new ArgumentException("A project folder is required.", nameof(projectPath));
            ProjectPath = Path.GetFullPath(projectPath);
            StatePath = Path.Combine(ProjectPath, "Library", "UnityAgentCoordination", "tickets.json");
            mutex = new Mutex(false, ProjectIdentity.LockName(ProjectPath));
        }

        public string ProjectPath { get; }
        public string StatePath { get; }

        internal T Read<T>(Func<CoordinationState, T> read) => Locked(() => read(Load()));

        internal T Change<T>(Func<CoordinationState, T> change)
        {
            return Locked(() =>
            {
                CoordinationState state = Load();
                T result = change(state);
                Save(state);
                return result;
            });
        }

        private CoordinationState Load()
        {
            if (!File.Exists(StatePath)) return new CoordinationState();
            CoordinationState state;
            try { state = QueueJson.Deserialize<CoordinationState>(File.ReadAllText(StatePath, Encoding.UTF8)); }
            catch (SerializationException exception)
            {
                throw new CoordinationException("state_corrupt", "Cannot read the ticket queue: " + exception.Message);
            }

            if (state == null || state.Version != CoordinationState.CurrentVersion || state.NextNumber < 1 ||
                state.Tickets == null || state.Tickets.Any(ticket => ticket == null ||
                    string.IsNullOrWhiteSpace(ticket.TicketId) || string.IsNullOrWhiteSpace(ticket.SessionId) ||
                    ticket.Number < 1 || ticket.Number >= state.NextNumber || ticket.IssuedUtc == default(DateTime) ||
                    (ticket.State != TicketStates.Waiting && ticket.State != TicketStates.Active)) ||
                state.Tickets.Select(ticket => ticket.TicketId).Distinct().Count() != state.Tickets.Count ||
                state.Tickets.Select(ticket => ticket.SessionId).Distinct().Count() != state.Tickets.Count ||
                state.Tickets.Select(ticket => ticket.Number).Distinct().Count() != state.Tickets.Count ||
                state.Tickets.Count(ticket => ticket.State == TicketStates.Active) > 1)
            {
                throw new CoordinationException("state_corrupt", "The ticket queue has invalid or unsupported state. Inspect it before resetting the queue.");
            }

            return state;
        }

        private void Save(CoordinationState state)
        {
            string directory = Path.GetDirectoryName(StatePath);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, "tickets-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(QueueJson.Serialize(state));
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(StatePath)) File.Replace(temporary, StatePath, null);
                else File.Move(temporary, StatePath);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        private T Locked<T>(Func<T> operation)
        {
            lock (gate)
            {
                bool acquired = false;
                try
                {
                    try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(15)); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) throw new CoordinationException("queue_busy", "The queue storage is busy; retry later.");
                    return operation();
                }
                finally { if (acquired) mutex.ReleaseMutex(); }
            }
        }

        public void Dispose() => mutex.Dispose();
    }
}
