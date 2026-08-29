using System;
using System.Collections.Generic;
using System.Linq;
using Olivecrow.UnityAgentCoordination;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Olivecrow.UnityAgentCoordination.Editor
{
    public sealed class AgentCoordinationWindow : EditorWindow
    {
        private const double RefreshIntervalSeconds = 1d;
        private Vector2 scrollPosition;
        private CoordinationSnapshot snapshot;
        private string errorMessage;
        private bool showDetails;
        private double nextRefreshTime;

        [MenuItem("Window/AI Agent Coordination")]
        public static void Open()
        {
            GetWindow<AgentCoordinationWindow>("AI Agent Coordination");
        }

        private void OnEnable()
        {
            Refresh();
            EditorApplication.update += AutoRefresh;
        }

        private void OnDisable()
        {
            EditorApplication.update -= AutoRefresh;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Unity AI Agent Coordination", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Canonical Editor",
                AgentCoordinationEditorContext.IsCoordinatorEditor ? "This Editor" : "Another Editor");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh"))
                {
                    Refresh();
                }

                GUI.enabled = AgentCoordinationEditorContext.IsCoordinatorEditor;
                if (GUILayout.Button("Clear All Coordination State"))
                {
                    if (EditorUtility.DisplayDialog("Clear Coordination State",
                        "Clear all active sessions, path claims, Editor access requests, and validation requests?",
                        "Clear", "Cancel"))
                    {
                        AgentCoordinationEditorContext.Service.ClearAll();
                        Refresh();
                    }
                }

                GUI.enabled = true;
            }

            if (!string.IsNullOrWhiteSpace(errorMessage))
            {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Error);
                return;
            }

            if (snapshot?.State == null)
            {
                EditorGUILayout.HelpBox("Reading coordination state.", MessageType.Info);
                return;
            }

            EditorGUILayout.Space();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawAgents(snapshot.State.Agents, snapshot.State.Claims);
            EditorGUILayout.Space();
            DrawEditorAccess(snapshot.State.EditorAccessRequests, snapshot.State.ActiveEditorAccess);
            EditorGUILayout.Space();
            DrawValidation(snapshot.State.ValidationRequests, snapshot.State.ActiveValidation);
            EditorGUILayout.Space();
            showDetails = EditorGUILayout.Foldout(showDetails, "Diagnostic Details", true);
            if (showDetails)
            {
                string json = JsonConvert.SerializeObject(snapshot, Formatting.Indented);
                EditorGUILayout.SelectableLabel(json, EditorStyles.textArea,
                    GUILayout.MinHeight(180f), GUILayout.ExpandHeight(true));
            }

            EditorGUILayout.EndScrollView();
        }

        private static void DrawAgents(IReadOnlyCollection<AgentRecord> agents,
            IReadOnlyCollection<ScopeClaim> claims)
        {
            EditorGUILayout.LabelField($"Work ({agents.Count})", EditorStyles.boldLabel);
            if (agents.Count == 0)
            {
                EditorGUILayout.HelpBox("No work is currently registered.", MessageType.Info);
                return;
            }

            DateTime now = DateTime.UtcNow;
            foreach (AgentRecord agent in agents.OrderBy(item => item.StartedUtc))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    string kind = string.IsNullOrWhiteSpace(agent.WorkKind) ? "General" : agent.WorkKind;
                    EditorGUILayout.LabelField($"[{agent.Role}] {kind} · {agent.AgentId}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Session", agent.SessionId);
                    EditorGUILayout.LabelField("Phase", FormatPhase(agent.Phase));
                    EditorGUILayout.LabelField("Summary",
                        string.IsNullOrWhiteSpace(agent.Summary) ? "-" : agent.Summary,
                        EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("Started",
                        $"{agent.StartedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} ({FormatElapsed(agent.StartedUtc, now)} elapsed)");
                    EditorGUILayout.LabelField("Last Update",
                        $"{agent.HeartbeatUtc.ToLocalTime():HH:mm:ss} ({FormatElapsed(agent.HeartbeatUtc, now)} ago)");

                    string[] ownedScopes = claims.Where(claim => claim.SessionId == agent.SessionId)
                        .Select(claim => claim.Scope).OrderBy(value => value).ToArray();
                    if (ownedScopes.Length > 0)
                    {
                        EditorGUILayout.LabelField("Scopes", string.Join(", ", ownedScopes),
                            EditorStyles.wordWrappedLabel);
                    }
                }
            }
        }

        private static void DrawEditorAccess(IReadOnlyCollection<EditorAccessRequest> requests,
            ActiveEditorAccess activeEditorAccess)
        {
            EditorAccessRequest[] pending = requests
                .Where(request => request.State == EditorAccessStates.Queued ||
                                  request.State == EditorAccessStates.Active)
                .OrderBy(request => request.EnqueuedUtc).ToArray();
            EditorGUILayout.LabelField($"Editor Access Queue ({pending.Length})", EditorStyles.boldLabel);
            if (pending.Length == 0)
            {
                EditorGUILayout.HelpBox("No Editor access is queued or active.", MessageType.Info);
                return;
            }

            DateTime now = DateTime.UtcNow;
            for (int index = 0; index < pending.Length; index++)
            {
                EditorAccessRequest request = pending[index];
                bool isActive = activeEditorAccess != null &&
                                activeEditorAccess.RequestId == request.RequestId;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        $"{index + 1}. {(isActive ? "Active" : "Queued")} · {request.AgentId}",
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Session", request.SessionId);
                    EditorGUILayout.LabelField("Summary",
                        string.IsNullOrWhiteSpace(request.Summary) ? "-" : request.Summary,
                        EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("Operations", string.Join(", ", request.Operations),
                        EditorStyles.wordWrappedLabel);
                    if (request.Scopes.Count > 0)
                    {
                        EditorGUILayout.LabelField("Scopes", string.Join(", ", request.Scopes),
                            EditorStyles.wordWrappedLabel);
                    }

                    if (!string.IsNullOrWhiteSpace(request.BaseHeadSha))
                    {
                        string shortHead = request.BaseHeadSha.Length > 12
                            ? request.BaseHeadSha.Substring(0, 12)
                            : request.BaseHeadSha;
                        EditorGUILayout.LabelField("Observed HEAD", shortHead);
                    }

                    EditorGUILayout.LabelField("Enqueued",
                        $"{request.EnqueuedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} ({FormatElapsed(request.EnqueuedUtc, now)} elapsed)");
                }
            }
        }

        private static void DrawValidation(IReadOnlyCollection<ValidationRequest> requests,
            ActiveValidation activeValidation)
        {
            int pendingCount = requests.Count(request => request.State == ValidationStates.Queued ||
                                                         request.State == ValidationStates.Running);
            EditorGUILayout.LabelField($"Validation Queue ({pendingCount})", EditorStyles.boldLabel);
            ValidationRequest[] pending = requests
                .Where(request => request.State == ValidationStates.Queued ||
                                  request.State == ValidationStates.Running)
                .OrderBy(request => request.EnqueuedUtc).ToArray();
            if (pending.Length == 0)
            {
                EditorGUILayout.HelpBox("No validation is queued or running.", MessageType.Info);
                return;
            }

            DateTime now = DateTime.UtcNow;
            for (int index = 0; index < pending.Length; index++)
            {
                ValidationRequest request = pending[index];
                bool isActive = activeValidation != null && activeValidation.RequestId == request.RequestId;
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        $"{index + 1}. {(isActive ? "Running" : "Queued")} · {request.AgentId} · {FormatPhase(request.Phase)}",
                        EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Session", request.SessionId);
                    EditorGUILayout.LabelField("Summary",
                        string.IsNullOrWhiteSpace(request.Summary) ? "-" : request.Summary,
                        EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("Enqueued",
                        $"{request.EnqueuedUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} ({FormatElapsed(request.EnqueuedUtc, now)} elapsed)");
                    if (request.TestModes.Count > 0)
                    {
                        EditorGUILayout.LabelField("Automated", string.Join(" → ", request.TestModes));
                    }

                    if (request.ManualChecks.Count > 0)
                    {
                        EditorGUILayout.LabelField("Manual Checks", string.Join(", ", request.ManualChecks),
                            EditorStyles.wordWrappedLabel);
                    }
                }
            }
        }

        internal static string FormatPhase(string phase)
        {
            switch (phase)
            {
                case AgentWorkPhases.Inspecting:
                    return "Inspecting";
                case AgentWorkPhases.Working:
                    return "Working";
                case AgentWorkPhases.WaitingEditorAccess:
                    return "Waiting for Editor Access";
                case AgentWorkPhases.UsingEditor:
                    return "Using Editor";
                case AgentWorkPhases.WaitingValidation:
                    return "Waiting for Validation";
                case AgentWorkPhases.Validating:
                    return "Validating";
                case AgentWorkPhases.Finalizing:
                    return "Finalizing";
                case AgentWorkPhases.Blocked:
                    return "Blocked";
                case ValidationPhases.Compiling:
                    return "Compiling";
                case ValidationPhases.Testing:
                    return "Testing";
                case ValidationPhases.AwaitingManual:
                    return "Awaiting Manual Checks";
                case ValidationPhases.ReadyToFinish:
                    return "Ready to Finish";
                default:
                    return string.IsNullOrWhiteSpace(phase) ? "Unspecified" : phase;
            }
        }

        internal static string FormatElapsed(DateTime startUtc, DateTime nowUtc)
        {
            TimeSpan elapsed = nowUtc - startUtc;
            if (elapsed < TimeSpan.Zero)
            {
                elapsed = TimeSpan.Zero;
            }

            if (elapsed.TotalDays >= 1d)
            {
                return $"{(int)elapsed.TotalDays}d {elapsed.Hours}h";
            }

            if (elapsed.TotalHours >= 1d)
            {
                return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m";
            }

            if (elapsed.TotalMinutes >= 1d)
            {
                return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
            }

            return $"{Math.Max(0, (int)elapsed.TotalSeconds)}s";
        }

        private void AutoRefresh()
        {
            if (EditorApplication.timeSinceStartup < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = EditorApplication.timeSinceStartup + RefreshIntervalSeconds;
            Refresh();
        }

        private void Refresh()
        {
            try
            {
                snapshot = AgentCoordinationEditorContext.Service.GetStatus(
                    Array.Empty<string>(), null, AgentCoordinationEditorContext.SessionId);
                errorMessage = null;
            }
            catch (Exception exception)
            {
                errorMessage = exception.Message;
            }

            Repaint();
        }
    }
}
