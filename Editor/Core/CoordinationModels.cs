using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Olivecrow.UnityAgentCoordination
{
    public static class TicketStates
    {
        public const string Waiting = "waiting";
        public const string Active = "active";
        public const string Released = "released";
        public const string Canceled = "canceled";
    }

    [DataContract]
    public sealed class EditorTicket
    {
        [DataMember(Name = "ticket_id")] public string TicketId { get; set; }
        [DataMember(Name = "number")] public long Number { get; set; }
        [DataMember(Name = "session_id")] public string SessionId { get; set; }
        [DataMember(Name = "label")] public string Label { get; set; }
        [DataMember(Name = "state")] public string State { get; set; }
        [DataMember(Name = "issued_utc")] public DateTime IssuedUtc { get; set; }
    }

    [DataContract]
    public sealed class CoordinationState
    {
        public const int CurrentVersion = 3;
        [DataMember(Name = "version")] public int Version { get; set; } = CurrentVersion;
        [DataMember(Name = "next_number")] public long NextNumber { get; set; } = 1;
        [DataMember(Name = "tickets")] public List<EditorTicket> Tickets { get; set; } = new List<EditorTicket>();
    }

    [DataContract]
    public sealed class QueueSnapshot
    {
        [DataMember(Name = "tickets")] public List<EditorTicket> Tickets { get; set; }
        [DataMember(Name = "active_ticket_id", EmitDefaultValue = false)] public string ActiveTicketId { get; set; }
        [DataMember(Name = "next_ticket_id", EmitDefaultValue = false)] public string NextTicketId { get; set; }
    }

    public sealed class CoordinationException : Exception
    {
        public CoordinationException(string code, string message) : base(message) { Code = code; }
        public string Code { get; }
    }
}
