using Olivecrow.UnityAgentCoordination.Editor;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace Olivecrow.UnityAgentCoordination.CoplayMcp
{
    [McpForUnityTool("manage_unity_agent_coordination",
        Description = "Coordinates shared Unity Editor ownership and serial validation. Use only for Editor-affecting work such as import, compilation, tests, Play Mode, serialization, or exact-SHA validation. Do not use for Git-only tasks such as status, diff, staging, commit, merge, or push. Call action=status first, then begin_work before touching the shared Editor or Unity-imported files. Workers claim paths and enqueue commit SHAs; only the integrator in the coordinator Editor may acquire compilation and tests. Never edit the state files directly.",
        AutoRegister = true, Group = "core")]
    public static class ManageUnityAgentCoordination
    {
        public sealed class Parameters
        {
            [ToolParameter("Action: status, begin_work, heartbeat, claim, release_claims, enqueue_validation, acquire_validation, finish_validation, cancel_validation, finish_work, relinquish_editor, or clear_all.")]
            public string action { get; set; }

            [ToolParameter("Stable ID for the current AI agent or task.", Required = false)]
            public string agent_id { get; set; }

            [ToolParameter("worker or integrator.", Required = false)]
            public string role { get; set; }

            [ToolParameter("Absolute Git worktree path.", Required = false)]
            public string worktree_path { get; set; }

            [ToolParameter("40-character Git commit SHA.", Required = false)]
            public string head_sha { get; set; }

            [ToolParameter("Short current task or validation summary.", Required = false)]
            public string summary { get; set; }

            [ToolParameter("Short work category such as code, asset, scene, diagnosis, documentation, or integration.", Required = false)]
            public string work_kind { get; set; }

            [ToolParameter("Current work phase such as inspecting, working, waiting_validation, validating, finalizing, or blocked.", Required = false)]
            public string phase { get; set; }

            [ToolParameter("Repository-relative paths to inspect for conflicts or claim.", Required = false)]
            public string[] scopes { get; set; }

            [ToolParameter("Validation request ID. acquire_validation may omit it to take the oldest queued request.", Required = false)]
            public string request_id { get; set; }

            [ToolParameter("Candidate commit SHA for enqueue_validation, or exact tested HEAD for acquire_validation.", Required = false)]
            public string candidate_sha { get; set; }

            [ToolParameter("Automated suites to run serially: EditMode and/or PlayMode.", Required = false)]
            public string[] test_modes { get; set; }

            [ToolParameter("Manual checks the integrator must perform after compilation and tests.", Required = false)]
            public string[] manual_checks { get; set; }

            [ToolParameter("Final validation outcome for finish_validation.", Required = false)]
            public bool passed { get; set; }

            [ToolParameter("Completion, failure, or cancellation note.", Required = false)]
            public string message { get; set; }

            [ToolParameter("Must be true for relinquish_editor and clear_all.", Required = false,
                DefaultValue = "false")]
            public bool confirm { get; set; }
        }

        public static object HandleCommand(JObject parameters)
        {
            CoordinationCommandResult result = UnityAgentCoordinationCommandRouter.Execute(parameters);
            return result.Success
                ? new SuccessResponse(result.Message, result.Data)
                : new ErrorResponse(result.ErrorCode, new { message = result.Message });
        }
    }
}
