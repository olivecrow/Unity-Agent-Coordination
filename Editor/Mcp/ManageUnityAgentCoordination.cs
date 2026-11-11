using Olivecrow.UnityAgentCoordination.Editor;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace Olivecrow.UnityAgentCoordination.CoplayMcp
{
    [McpForUnityTool("manage_unity_agent_coordination",
        Description = "FIFO tickets for this Unity Editor, held only within one active conversation turn. Enqueue with a unique session_id, wait until next_ticket_id matches your ticket, then acquire. After current Editor work, including on failure, finish or safely stop operations you started and release your active ticket or cancel your waiting ticket before ending the turn or pausing for user input or approval, even if the same conversation will continue. Verify success and removal from queue.tickets; use status to reconcile an uncertain cleanup result and report unresolved cleanup without claiming release. Enqueue a fresh ticket when resuming; clean up your own leftover ticket first. Turn endings are not detected automatically. No prior work registration or revision metadata is needed.",
        AutoRegister = true, Group = "core")]
    public static class ManageUnityAgentCoordination
    {
        public sealed class Parameters
        {
            [ToolParameter("status, enqueue, acquire, release, or cancel.")] public string action { get; set; }
            [ToolParameter("Unique ID for this AI work session; required except for status. May persist across turns, but tickets must be released or canceled before yielding to the user.", Required = false)] public string session_id { get; set; }
            [ToolParameter("Ticket returned by enqueue for the current turn; required for acquire, release, or cancel. Enqueue a fresh ticket when resuming in a later turn.", Required = false)] public string ticket_id { get; set; }
            [ToolParameter("Optional short task description for enqueue.", Required = false)] public string label { get; set; }
        }

        public static object HandleCommand(JObject parameters)
            => JObject.Parse(UnityAgentCoordinationCommandRouter.ExecuteJson(parameters?.ToString()).ToJson());
    }
}
