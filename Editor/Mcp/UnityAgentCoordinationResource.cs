using Olivecrow.UnityAgentCoordination.Editor;
using MCPForUnity.Editor.Resources;
using Newtonsoft.Json.Linq;

namespace Olivecrow.UnityAgentCoordination.CoplayMcp
{
    [McpForUnityResource("get_unity_agent_coordination", Description = "This Unity Editor's active ticket and FIFO waiting tickets.")]
    public static class UnityAgentCoordinationResource
    {
        public static object HandleCommand(JObject parameters)
            => JObject.Parse(UnityAgentCoordinationCommandRouter.Execute(new CoordinationCommand { Action = "status" }).ToJson());
    }
}
