using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Olivecrow.UnityAgentCoordination
{
    public static class CoordinationSchema
    {
        public const int CurrentVersion = 2;
    }

    public static class CoordinationRoles
    {
        public const string Worker = "worker";
        public const string Integrator = "integrator";

        public static bool IsValid(string role)
        {
            return role == Worker || role == Integrator;
        }
    }

    public static class ValidationStates
    {
        public const string Queued = "queued";
        public const string Running = "running";
        public const string Passed = "passed";
        public const string Failed = "failed";
        public const string Canceled = "canceled";
    }

    public static class EditorAccessStates
    {
        public const string Queued = "queued";
        public const string Active = "active";
        public const string Completed = "completed";
        public const string Canceled = "canceled";
        public const string Expired = "expired";
    }

    public static class ValidationPhases
    {
        public const string Compiling = "compiling";
        public const string Testing = "testing";
        public const string AwaitingManual = "awaiting_manual";
        public const string ReadyToFinish = "ready_to_finish";
    }

    public static class AgentWorkPhases
    {
        public const string Inspecting = "inspecting";
        public const string Working = "working";
        public const string WaitingEditorAccess = "waiting_editor_access";
        public const string UsingEditor = "using_editor";
        public const string WaitingValidation = "waiting_validation";
        public const string Validating = "validating";
        public const string Finalizing = "finalizing";
        public const string Blocked = "blocked";
    }

    [Serializable]
    public sealed class CoordinationState
    {
        public int SchemaVersion { get; set; } = CoordinationSchema.CurrentVersion;
        public string RepositoryId { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public List<AgentRecord> Agents { get; set; } = new List<AgentRecord>();
        public List<ScopeClaim> Claims { get; set; } = new List<ScopeClaim>();
        public List<EditorAccessRequest> EditorAccessRequests { get; set; } = new List<EditorAccessRequest>();
        public ActiveEditorAccess ActiveEditorAccess { get; set; }
        public List<ValidationRequest> ValidationRequests { get; set; } = new List<ValidationRequest>();
        public ActiveValidation ActiveValidation { get; set; }

        [JsonIgnore]
        public bool IsEmpty => Agents.Count == 0 && Claims.Count == 0 &&
                               EditorAccessRequests.Count == 0 && ActiveEditorAccess == null &&
                               ValidationRequests.Count == 0 && ActiveValidation == null;
    }

    [Serializable]
    public sealed class AgentRecord
    {
        public string AgentId { get; set; }
        public string SessionId { get; set; }
        public string Role { get; set; }
        public string WorktreePath { get; set; }
        public string HeadSha { get; set; }
        public string WorkKind { get; set; }
        public string Phase { get; set; }
        public string Summary { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime HeartbeatUtc { get; set; }
    }

    [Serializable]
    public sealed class ScopeClaim
    {
        public string AgentId { get; set; }
        public string SessionId { get; set; }
        public string Scope { get; set; }
        public DateTime ClaimedUtc { get; set; }
    }

    [Serializable]
    public sealed class EditorAccessRequest
    {
        public string RequestId { get; set; }
        public string RequestKey { get; set; }
        public string AgentId { get; set; }
        public string SessionId { get; set; }
        public string BaseHeadSha { get; set; }
        public string Summary { get; set; }
        public string State { get; set; }
        public List<string> Operations { get; set; } = new List<string>();
        public List<string> Scopes { get; set; } = new List<string>();
        public string Message { get; set; }
        public DateTime EnqueuedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    [Serializable]
    public sealed class ActiveEditorAccess
    {
        public string RequestId { get; set; }
        public string AgentId { get; set; }
        public string SessionId { get; set; }
        public DateTime AcquiredUtc { get; set; }
        public DateTime HeartbeatUtc { get; set; }
    }

    [Serializable]
    public sealed class ValidationRequest
    {
        public string RequestId { get; set; }
        public string AgentId { get; set; }
        public string SessionId { get; set; }
        public string CandidateSha { get; set; }
        public string Summary { get; set; }
        public string State { get; set; }
        public string Phase { get; set; }
        public List<string> TestModes { get; set; } = new List<string>();
        public List<string> ManualChecks { get; set; } = new List<string>();
        public int CurrentTestIndex { get; set; } = -1;
        public string CurrentTestMode { get; set; }
        public string TestJobId { get; set; }
        public List<string> CompilationErrors { get; set; } = new List<string>();
        public List<string> TestFailures { get; set; } = new List<string>();
        public string Message { get; set; }
        public DateTime EnqueuedUtc { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }

    [Serializable]
    public sealed class ActiveValidation
    {
        public string RequestId { get; set; }
        public string IntegratorAgentId { get; set; }
        public string IntegratorSessionId { get; set; }
        public string TestedHeadSha { get; set; }
        public string Phase { get; set; }
        public DateTime AcquiredUtc { get; set; }
        public DateTime HeartbeatUtc { get; set; }
    }

    [Serializable]
    public sealed class EditorOwnerRecord
    {
        public string SessionId { get; set; }
        public string ProjectPath { get; set; }
        public string UnityVersion { get; set; }
        public int ProcessId { get; set; }
        public DateTime StartedUtc { get; set; }
        public DateTime HeartbeatUtc { get; set; }
    }

    [Serializable]
    public sealed class CoordinationSnapshot
    {
        public CoordinationState State { get; set; }
        public EditorOwnerRecord EditorOwner { get; set; }
        public List<ScopeClaim> ConflictingClaims { get; set; } = new List<ScopeClaim>();
        public bool IsCoordinatorEditor { get; set; }
        public bool HasActiveWork => State != null && !State.IsEmpty;
    }

    public sealed class CoordinationException : Exception
    {
        public CoordinationException(string code, string message) : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }

    public static class CoordinationModelExtensions
    {
        public static EditorAccessRequest FindEditorAccessRequest(this CoordinationState state,
            string requestId)
        {
            return state.EditorAccessRequests.FirstOrDefault(item =>
                string.Equals(item.RequestId, requestId, StringComparison.Ordinal));
        }

        public static ValidationRequest FindRequest(this CoordinationState state, string requestId)
        {
            return state.ValidationRequests.FirstOrDefault(item =>
                string.Equals(item.RequestId, requestId, StringComparison.Ordinal));
        }
    }
}
