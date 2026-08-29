using Olivecrow.UnityAgentCoordination.Editor;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace Olivecrow.UnityAgentCoordination.CoplayMcp
{
    [McpForUnityTool("manage_unity_agent_coordination",
        Description = "Coordinates shared Unity Editor access and exact-SHA validation. Use session-scoped work records and the commit-optional Editor access queue for import, compilation, tests, Play Mode, serialization, builds, or diagnosis. Use the validation queue only for clean exact-commit verification. Call status first, then begin_work. Never edit the state files directly.",
        AutoRegister = true, Group = "core")]
    public static class ManageUnityAgentCoordination
    {
        public sealed class Parameters
        {
            [ToolParameter("Action: status, begin_work, heartbeat, claim, release_claims, enqueue_editor_access, acquire_editor_access, finish_editor_access, cancel_editor_access, enqueue_validation, acquire_validation, finish_validation, cancel_validation, finish_work, relinquish_editor, or clear_all.")]
            public string action { get; set; }

            [ToolParameter("Stable ID for the current AI agent or task.", Required = false)]
            public string agent_id { get; set; }

            [ToolParameter("Unique work-session ID. Omit for backward compatibility to use agent_id as the session ID.", Required = false)]
            public string session_id { get; set; }

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

            [ToolParameter("Current work phase such as inspecting, working, waiting_editor_access, using_editor, waiting_validation, validating, finalizing, or blocked.", Required = false)]
            public string phase { get; set; }

            [ToolParameter("Repository-relative paths to inspect for conflicts or claim.", Required = false)]
            public string[] scopes { get; set; }

            [ToolParameter("Editor access or validation request ID. An acquire action may omit it to take its oldest queued request.", Required = false)]
            public string request_id { get; set; }

            [ToolParameter("Optional idempotency key for enqueue_editor_access within one session.", Required = false)]
            public string request_key { get; set; }

            [ToolParameter("Optional 40-character HEAD observed for an Editor access request. It is metadata, not a lease identity or clean-checkout requirement.", Required = false)]
            public string base_head_sha { get; set; }

            [ToolParameter("Editor operations requested, such as import, compile, console, editmode_tests, playmode, scene_edit, prefab_edit, or build.", Required = false)]
            public string[] operations { get; set; }

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
