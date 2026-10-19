using Olivecrow.UnityAgentCoordination.Editor;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace Olivecrow.UnityAgentCoordination.CoplayMcp
{
    [McpForUnityTool("manage_unity_agent_coordination",
        Description = "FIFO tickets for this Unity Editor. Enqueue with a unique session_id, wait until next_ticket_id matches your ticket, acquire it, use the Editor, and release it. Cancel your own abandoned ticket. No prior work registration or revision metadata is needed.",
        AutoRegister = true, Group = "core")]
    public static class ManageUnityAgentCoordination
    {
        public sealed class Parameters
        {
            [ToolParameter("status, enqueue, acquire, release, or cancel.")] public string action { get; set; }
            [ToolParameter("Unique ID for this AI work session; required except for status.", Required = false)] public string session_id { get; set; }
            [ToolParameter("Ticket returned by enqueue; required for acquire, release, or cancel.", Required = false)] public string ticket_id { get; set; }
            [ToolParameter("Optional short task description for enqueue.", Required = false)] public string label { get; set; }
        }

        public static object HandleCommand(JObject parameters)
            => JObject.Parse(UnityAgentCoordinationCommandRouter.ExecuteJson(parameters?.ToString()).ToJson());
    }
}
