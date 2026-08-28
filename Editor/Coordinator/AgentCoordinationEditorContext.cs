using System;
using System.Diagnostics;
using System.IO;
using Olivecrow.UnityAgentCoordination;
using UnityEditor;
using UnityEngine;

namespace Olivecrow.UnityAgentCoordination.Editor
{
    [InitializeOnLoad]
    public static class AgentCoordinationEditorContext
    {
        private const string SessionKey = "Olivecrow.UnityAgentCoordination.EditorSessionId";
        private const string RelinquishedKey = "Olivecrow.UnityAgentCoordination.EditorOwnershipRelinquished";
        private static readonly TimeSpan EditorLeaseStaleAfter = TimeSpan.FromSeconds(90);
        private static readonly string SessionIdValue;
        private static double nextHeartbeatTime;
        private static bool ownershipRelinquished;

        static AgentCoordinationEditorContext()
        {
            ProjectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            Store = new CoordinationStore(ProjectPath);
            Service = new CoordinationService(Store);
            SessionIdValue = SessionState.GetString(SessionKey, string.Empty);
            if (string.IsNullOrWhiteSpace(SessionIdValue))
            {
                SessionIdValue = Guid.NewGuid().ToString("N");
                SessionState.SetString(SessionKey, SessionIdValue);
            }

            ownershipRelinquished = SessionState.GetBool(RelinquishedKey, false);
            if (!ownershipRelinquished)
            {
                IsCoordinatorEditor = Store.TryAcquireEditor(CreateOwnerRecord(), EditorLeaseStaleAfter);
            }

            EditorApplication.update += UpdateHeartbeat;
            EditorApplication.quitting += OnEditorQuitting;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        public static CoordinationStore Store { get; }
        public static CoordinationService Service { get; }
        public static string ProjectPath { get; }
        public static string SessionId => SessionIdValue;
        public static bool IsCoordinatorEditor { get; private set; }

        public static void RequireCoordinatorEditor()
        {
            if (!IsCoordinatorEditor)
            {
                throw new CoordinationException("not_coordinator_editor",
                    "This Unity Editor does not own repository validation. Retry after the editor-owner heartbeat expires.");
            }
        }

        public static void RelinquishOwnership()
        {
            RequireCoordinatorEditor();
            Store.ReleaseEditorOwnership(SessionIdValue);
            ownershipRelinquished = true;
            IsCoordinatorEditor = false;
            SessionState.SetBool(RelinquishedKey, true);
        }

        private static void UpdateHeartbeat()
        {
            if (EditorApplication.timeSinceStartup < nextHeartbeatTime)
            {
                return;
            }

            nextHeartbeatTime = EditorApplication.timeSinceStartup + 15d;
            if (ownershipRelinquished)
            {
                IsCoordinatorEditor = Store.TryRecoverEditor(CreateOwnerRecord(), EditorLeaseStaleAfter);
                if (IsCoordinatorEditor)
                {
                    ownershipRelinquished = false;
                    SessionState.SetBool(RelinquishedKey, false);
                }

                return;
            }

            if (!IsCoordinatorEditor)
            {
                IsCoordinatorEditor = Store.TryAcquireEditor(CreateOwnerRecord(), EditorLeaseStaleAfter);
                return;
            }

            Store.HeartbeatEditor(SessionIdValue);
            Service.GetStatus(Array.Empty<string>(), null, SessionIdValue);
        }

        private static EditorOwnerRecord CreateOwnerRecord()
        {
            DateTime now = DateTime.UtcNow;
            int processId;
            DateTime startedUtc;
            using (Process process = Process.GetCurrentProcess())
            {
                processId = process.Id;
                try
                {
                    startedUtc = process.StartTime.ToUniversalTime();
                }
                catch
                {
                    startedUtc = now;
                }
            }

            return new EditorOwnerRecord
            {
                SessionId = SessionIdValue,
                ProjectPath = ProjectPath,
                UnityVersion = Application.unityVersion,
                ProcessId = processId,
                StartedUtc = startedUtc,
                HeartbeatUtc = now
            };
        }

        private static void OnBeforeAssemblyReload()
        {
            if (IsCoordinatorEditor)
            {
                Store.HeartbeatEditor(SessionIdValue);
            }
        }

        private static void OnEditorQuitting()
        {
            if (IsCoordinatorEditor)
            {
                Store.ReleaseEditorAndClear(SessionIdValue);
            }
        }
    }
}
