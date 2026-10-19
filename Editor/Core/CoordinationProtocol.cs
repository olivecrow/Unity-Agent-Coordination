using System;
using System.IO;
using System.Runtime.Serialization;

namespace Olivecrow.UnityAgentCoordination
{
    [DataContract]
    public sealed class CoordinationCommand
    {
        [DataMember(Name = "action")] public string Action { get; set; }
        [DataMember(Name = "session_id")] public string SessionId { get; set; }
        [DataMember(Name = "ticket_id")] public string TicketId { get; set; }
        [DataMember(Name = "label")] public string Label { get; set; }
    }

    [DataContract]
    public sealed class CoordinationCommandResult
    {
        [DataMember(Name = "success")] public bool Success { get; set; }
        [DataMember(Name = "error_code", EmitDefaultValue = false)] public string ErrorCode { get; set; }
        [DataMember(Name = "message")] public string Message { get; set; }
        [DataMember(Name = "ticket", EmitDefaultValue = false)] public EditorTicket Ticket { get; set; }
        [DataMember(Name = "queue", EmitDefaultValue = false)] public QueueSnapshot Queue { get; set; }
        public string ToJson() => QueueJson.Serialize(this);
    }

    public static class CoordinationProtocol
    {
        public static CoordinationCommandResult ExecuteJson(string json, CoordinationService service, Func<bool> editorReady = null)
        {
            if (string.IsNullOrWhiteSpace(json)) return Failure("parameters_required", "A JSON command object is required.");
            if (!json.TrimStart().StartsWith("{", StringComparison.Ordinal)) return Failure("invalid_json", "A JSON command object is required.");
            CoordinationCommand command;
            try { command = QueueJson.Deserialize<CoordinationCommand>(json); }
            catch (SerializationException exception) { return Failure("invalid_json", exception.Message); }
            return Execute(command, service, editorReady);
        }

        public static CoordinationCommandResult Execute(CoordinationCommand command, CoordinationService service, Func<bool> editorReady = null)
        {
            if (command == null) return Failure("parameters_required", "A command object is required.");
            if (string.IsNullOrWhiteSpace(command.Action)) return Failure("missing_parameter", "action is required.");
            try
            {
                EditorTicket ticket = null;
                switch (command.Action.Trim().ToLowerInvariant())
                {
                    case "status": break;
                    case "enqueue": ticket = service.Enqueue(command.SessionId, command.Label); break;
                    case "acquire": ticket = service.Acquire(command.SessionId, command.TicketId, editorReady); break;
                    case "release": ticket = service.Release(command.SessionId, command.TicketId); break;
                    case "cancel": ticket = service.Cancel(command.SessionId, command.TicketId); break;
                    default: return Failure("unknown_action", "Use status, enqueue, acquire, release, or cancel.");
                }
                return new CoordinationCommandResult { Success = true, Message = "OK", Ticket = ticket, Queue = service.GetStatus() };
            }
            catch (CoordinationException exception) { return Failure(exception.Code, exception.Message); }
            catch (IOException exception) { return Failure("storage_error", exception.Message); }
            catch (UnauthorizedAccessException exception) { return Failure("storage_error", exception.Message); }
        }

        private static CoordinationCommandResult Failure(string code, string message)
            => new CoordinationCommandResult { Success = false, ErrorCode = code, Message = message };
    }
}
