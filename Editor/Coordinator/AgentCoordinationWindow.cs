using System;
using UnityEditor;
using UnityEngine;

namespace Olivecrow.UnityAgentCoordination.Editor
{
    public sealed class AgentCoordinationWindow : EditorWindow
    {
        private Vector2 scroll;
        private QueueSnapshot snapshot;
        private string error;
        private double nextRefresh;

        [MenuItem("Window/AI Agent Coordination")]
        public static void Open() => GetWindow<AgentCoordinationWindow>("Editor Tickets");

        private void OnEnable() { Refresh(); EditorApplication.update += UpdateQueue; }
        private void OnDisable() { EditorApplication.update -= UpdateQueue; }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Unity Editor Tickets", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Project", AgentCoordinationEditorContext.ProjectPath, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Draw a ticket, wait for your turn, use the Editor, then return it.", EditorStyles.wordWrappedLabel);
            if (GUILayout.Button("Refresh")) Refresh();
            using (new EditorGUI.DisabledScope(!AgentCoordinationEditorContext.IsReady))
            {
                if (GUILayout.Button("Reset Abandoned Queue") && EditorUtility.DisplayDialog("Reset Editor Tickets",
                    "This cancels every ticket, including the active ticket. Confirm that its session has stopped using the Editor first.", "Reset", "Cancel"))
                {
                    try { AgentCoordinationEditorContext.Service.ResetQueue(); Refresh(); }
                    catch (Exception exception) { error = exception.Message; }
                }
            }
            if (error != null) { EditorGUILayout.HelpBox(error, MessageType.Error); return; }
            if (snapshot == null || snapshot.Tickets.Count == 0)
            {
                EditorGUILayout.HelpBox("The queue is empty.", MessageType.Info);
                return;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (EditorTicket ticket in snapshot.Tickets)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"#{ticket.Number} · {ticket.State}", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField("Session", ticket.SessionId);
                    EditorGUILayout.LabelField("Ticket", ticket.TicketId);
                    EditorGUILayout.LabelField("Task", ticket.Label, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("Issued", ticket.IssuedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void UpdateQueue()
        {
            if (EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + 1;
            Refresh();
        }

        private void Refresh()
        {
            try { snapshot = AgentCoordinationEditorContext.Service.GetStatus(); error = null; }
            catch (Exception exception) { error = exception.Message; }
            Repaint();
        }
    }
}
