using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Olivecrow.UnityAgentCoordination.Editor
{
    public sealed class CoordinationCommandResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("error_code", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorCode { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public object Data { get; set; }

        public string ToJson(Formatting formatting = Formatting.None)
        {
            return JsonConvert.SerializeObject(this, formatting);
        }
    }

    /// <summary>
    /// Transport-neutral command surface for Unity agent coordination integrations.
    /// MCP packages and other local automation bridges should adapt their payloads to this API.
    /// </summary>
    public static class UnityAgentCoordinationCommandRouter
    {
        public static CoordinationCommandResult ExecuteJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return Failure("parameters_required", "A JSON command object is required.");
            }

            try
            {
                JToken token = JToken.Parse(json);
                if (token.Type != JTokenType.Object)
                {
                    return Failure("invalid_json", "The command payload must be a JSON object.");
                }

                return Execute((JObject)token);
            }
            catch (JsonException exception)
            {
                return Failure("invalid_json", exception.Message);
            }
        }

        public static CoordinationCommandResult Execute(JObject parameters)
        {
            if (parameters == null)
            {
                return Failure("parameters_required", "A command object is required.");
            }

            try
            {
                string action = RequiredString(parameters, "action").ToLowerInvariant();
                CoordinationService service = AgentCoordinationEditorContext.Service;
                object data;
                switch (action)
                {
                    case "status":
                        data = service.GetStatus(StringArray(parameters, "scopes"),
                            OptionalString(parameters, "agent_id"), AgentCoordinationEditorContext.SessionId);
                        break;
                    case "begin_work":
                        data = service.BeginWork(RequiredString(parameters, "agent_id"),
                            RequiredString(parameters, "role"), RequiredString(parameters, "worktree_path"),
                            RequiredString(parameters, "head_sha"), OptionalString(parameters, "summary"),
                            StringArray(parameters, "scopes"), OptionalString(parameters, "work_kind"),
                            OptionalString(parameters, "phase"));
                        break;
                    case "heartbeat":
                        data = service.Heartbeat(RequiredString(parameters, "agent_id"),
                            OptionalString(parameters, "head_sha"), OptionalString(parameters, "summary"),
                            OptionalString(parameters, "phase"), OptionalString(parameters, "work_kind"));
                        break;
                    case "claim":
                        data = service.Claim(RequiredString(parameters, "agent_id"),
                            StringArray(parameters, "scopes"));
                        break;
                    case "release_claims":
                        service.ReleaseClaims(RequiredString(parameters, "agent_id"),
                            StringArray(parameters, "scopes"));
                        data = service.GetStatus(Array.Empty<string>(), OptionalString(parameters, "agent_id"),
                            AgentCoordinationEditorContext.SessionId);
                        break;
                    case "enqueue_validation":
                        data = new
                        {
                            request_id = service.EnqueueValidation(RequiredString(parameters, "agent_id"),
                                RequiredString(parameters, "candidate_sha"), OptionalString(parameters, "summary"),
                                StringArray(parameters, "test_modes"), StringArray(parameters, "manual_checks"))
                        };
                        break;
                    case "acquire_validation":
                        data = ValidationRunner.AcquireAndStart(RequiredString(parameters, "agent_id"),
                            OptionalString(parameters, "request_id"), RequiredString(parameters, "candidate_sha"));
                        break;
                    case "finish_validation":
                        if (ParameterToken(parameters, "passed") == null)
                        {
                            throw new CoordinationException("missing_parameter", "The passed value is required.");
                        }

                        data = service.FinishValidation(RequiredString(parameters, "agent_id"),
                            RequiredString(parameters, "request_id"), parameters.Value<bool>("passed"),
                            OptionalString(parameters, "message"));
                        break;
                    case "cancel_validation":
                        service.CancelValidationRequest(RequiredString(parameters, "agent_id"),
                            RequiredString(parameters, "request_id"), OptionalString(parameters, "message"));
                        data = service.GetStatus(Array.Empty<string>(), OptionalString(parameters, "agent_id"),
                            AgentCoordinationEditorContext.SessionId);
                        break;
                    case "finish_work":
                        service.FinishWork(RequiredString(parameters, "agent_id"));
                        data = service.GetStatus(Array.Empty<string>(), null,
                            AgentCoordinationEditorContext.SessionId);
                        break;
                    case "relinquish_editor":
                        RelinquishEditor(parameters, service);
                        data = service.GetStatus(Array.Empty<string>(), null,
                            AgentCoordinationEditorContext.SessionId);
                        break;
                    case "clear_all":
                        ClearAll(parameters, service);
                        data = service.GetStatus(Array.Empty<string>(), null,
                            AgentCoordinationEditorContext.SessionId);
                        break;
                    default:
                        throw new CoordinationException("unknown_action", $"Unknown action: {action}");
                }

                return new CoordinationCommandResult
                {
                    Success = true,
                    Message = $"{action} completed",
                    Data = data
                };
            }
            catch (CoordinationException exception)
            {
                return Failure(exception.Code, exception.Message);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return Failure("internal_error", exception.Message);
            }
        }

        private static void ClearAll(JObject parameters, CoordinationService service)
        {
            if (!parameters.Value<bool?>("confirm").GetValueOrDefault())
            {
                throw new CoordinationException("confirmation_required", "clear_all requires confirm=true.");
            }

            AgentCoordinationEditorContext.RequireCoordinatorEditor();
            string agentId = RequiredString(parameters, "agent_id");
            CoordinationSnapshot snapshot = service.GetStatus(Array.Empty<string>(), agentId,
                AgentCoordinationEditorContext.SessionId);
            if (!snapshot.State.Agents.Any(agent => agent.AgentId == agentId &&
                                                   agent.Role == CoordinationRoles.Integrator))
            {
                throw new CoordinationException("integrator_required",
                    "Only the registered integrator can clear all coordination state.");
            }

            service.ClearAll();
        }

        private static void RelinquishEditor(JObject parameters, CoordinationService service)
        {
            if (!parameters.Value<bool?>("confirm").GetValueOrDefault())
            {
                throw new CoordinationException("confirmation_required",
                    "relinquish_editor requires confirm=true.");
            }

            AgentCoordinationEditorContext.RequireCoordinatorEditor();
            string agentId = RequiredString(parameters, "agent_id");
            CoordinationSnapshot snapshot = service.GetStatus(Array.Empty<string>(), agentId,
                AgentCoordinationEditorContext.SessionId);
            if (snapshot.State.ActiveValidation != null)
            {
                throw new CoordinationException("active_validation",
                    "Finish or cancel the active validation before relinquishing Editor ownership.");
            }

            if (!snapshot.State.Agents.Any(agent => agent.AgentId == agentId &&
                                                   agent.Role == CoordinationRoles.Integrator))
            {
                throw new CoordinationException("integrator_required",
                    "Only the registered integrator can relinquish Editor ownership.");
            }

            service.FinishWork(agentId);
            AgentCoordinationEditorContext.RelinquishOwnership();
        }

        private static string RequiredString(JObject parameters, string name)
        {
            string value = OptionalString(parameters, name);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new CoordinationException("missing_parameter", $"The {name} value is required.");
            }

            return value;
        }

        private static string OptionalString(JObject parameters, string name)
        {
            return ParameterToken(parameters, name)?.Value<string>()?.Trim();
        }

        private static IEnumerable<string> StringArray(JObject parameters, string name)
        {
            JToken token = ParameterToken(parameters, name);
            if (token == null || token.Type == JTokenType.Null)
            {
                return Array.Empty<string>();
            }

            if (token.Type == JTokenType.Array)
            {
                return token.Values<string>().Where(value => value != null).ToArray();
            }

            string single = token.Value<string>();
            return string.IsNullOrWhiteSpace(single) ? Array.Empty<string>() : new[] { single };
        }

        private static JToken ParameterToken(JObject parameters, string snakeCaseName)
        {
            JToken token = parameters[snakeCaseName];
            if (token != null)
            {
                return token;
            }

            string[] parts = snakeCaseName.Split('_');
            if (parts.Length == 1)
            {
                return null;
            }

            string camelCaseName = parts[0] + string.Concat(parts.Skip(1).Select(part =>
                part.Length == 0 ? string.Empty : char.ToUpperInvariant(part[0]) + part.Substring(1)));
            return parameters[camelCaseName];
        }

        private static CoordinationCommandResult Failure(string errorCode, string message)
        {
            return new CoordinationCommandResult
            {
                Success = false,
                ErrorCode = errorCode,
                Message = message
            };
        }
    }
}
