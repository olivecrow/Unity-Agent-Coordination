using Olivecrow.UnityAgentCoordination.Editor;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Resources;
using Newtonsoft.Json.Linq;

namespace Olivecrow.UnityAgentCoordination.CoplayMcp
{
    [McpForUnityResource("get_unity_agent_coordination",
        Description = "Current AI work sessions, path claims, commit-optional Editor access queue, exact-SHA validation queue, active leases, and canonical Editor owner.")]
    public static class UnityAgentCoordinationResource
    {
        public static object HandleCommand(JObject parameters)
        {
            JObject command = parameters == null ? new JObject() : (JObject)parameters.DeepClone();
            command["action"] = "status";
            CoordinationCommandResult result = UnityAgentCoordinationCommandRouter.Execute(command);
            return result.Success
                ? new SuccessResponse(result.Message, result.Data)
                : new ErrorResponse(result.ErrorCode, new { message = result.Message });
        }
    }
}
