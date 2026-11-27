using Olivecrow.UnityAgentCoordination.Editor;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace Olivecrow.UnityAgentCoordination.CoplayMcp
{
    [McpForUnityTool("manage_unity_agent_coordination",
        Description = "FIFO tickets for this Unity Editor, held within one active conversation turn. " +
            "Before each enqueue, look up program, model, reasoning_effort, and session_name in the available host or session metadata. " +
            "Send all four fields with a unique session_id. Use Unknown only for a value you could not verify after checking available sources. " +
            "Verify the returned metadata and wait until next_ticket_id matches before acquire. " +
            "Finish or safely stop your Editor operations and release your active ticket or cancel your waiting ticket before ending the turn or pausing for input or approval, including on failure and when the conversation will continue. " +
            "Verify removal from queue.tickets. Use status to reconcile uncertain cleanup and report unresolved errors. " +
            "Clean up your own leftover ticket before enqueueing a fresh one when resuming. Turn endings are not detected automatically.",
        AutoRegister = true, Group = "core")]
    public static class ManageUnityAgentCoordination
    {
        public sealed class Parameters
        {
            [ToolParameter("status, enqueue, acquire, release, or cancel.")] public string action { get; set; }
            [ToolParameter("Unique ID for this AI work session; required except for status. May persist across turns, but tickets must be released or canceled before yielding to the user.", Required = false)] public string session_id { get; set; }
            [ToolParameter("Ticket returned by enqueue for the current turn; required for acquire, release, or cancel. Enqueue a fresh ticket when resuming in a later turn.", Required = false)] public string ticket_id { get; set; }
            [ToolParameter("Optional short task description for enqueue.", Required = false)] public string label { get; set; }
            [ToolParameter("Required on enqueue for agents. Verify the host program running this session. Use Unknown only after checking available sources.", Required = false)] public string program { get; set; }
            [ToolParameter("Required on enqueue for agents. Look up the actual running model. Use Unknown only after checking available sources.", Required = false)] public string model { get; set; }
            [ToolParameter("Required on enqueue for agents. Look up the current reasoning effort or level. Use Unknown only after checking available sources.", Required = false)] public string reasoning_effort { get; set; }
            [ToolParameter("Required on enqueue for agents. Look up the actual conversation title. Use Unknown only after checking available sources.", Required = false)] public string session_name { get; set; }
            [ToolParameter("Optional 32-hex token from the local file watcher. Notifications are hints: recheck status and acquire.", Required = false)] public string notification_token { get; set; }
        }

        public static object HandleCommand(JObject parameters)
            => JObject.Parse(UnityAgentCoordinationCommandRouter.ExecuteJson(parameters?.ToString()).ToJson());
    }
}
