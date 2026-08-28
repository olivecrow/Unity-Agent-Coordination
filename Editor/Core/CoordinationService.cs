using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Olivecrow.UnityAgentCoordination
{
    public sealed class CoordinationService
    {
        private static readonly TimeSpan AgentStaleAfter = TimeSpan.FromMinutes(45);
        private static readonly TimeSpan QueueStaleAfter = TimeSpan.FromHours(4);
        private static readonly TimeSpan ValidationStaleAfter = TimeSpan.FromMinutes(45);
        private readonly CoordinationStore store;

        public CoordinationService(CoordinationStore store)
        {
            this.store = store;
        }

        public CoordinationStore Store => store;

        public CoordinationSnapshot GetStatus(IEnumerable<string> proposedScopes, string requestingAgentId,
            string editorSessionId)
        {
            List<string> normalizedScopes = NormalizeScopes(proposedScopes).ToList();
            CoordinationState state = store.Mutate(current =>
            {
                PruneStale(current);
                return current;
            });
            EditorOwnerRecord owner = store.ReadEditorOwner();
            return new CoordinationSnapshot
            {
                State = state,
                EditorOwner = owner,
                IsCoordinatorEditor = owner != null && owner.SessionId == editorSessionId,
                ConflictingClaims = state.Claims.Where(claim =>
                        claim.AgentId != requestingAgentId &&
                        normalizedScopes.Any(scope => ScopesOverlap(scope, claim.Scope)))
                    .ToList()
            };
        }

        public AgentRecord BeginWork(string agentId, string role, string worktreePath, string headSha,
            string summary, IEnumerable<string> scopes, string workKind = null, string phase = null)
        {
            RequireValue(agentId, "agent_id");
            RequireValue(role, "role");
            RequireValue(worktreePath, "worktree_path");
            if (!CoordinationRoles.IsValid(role))
            {
                throw new CoordinationException("invalid_role", "role must be worker or integrator.");
            }

            string fullWorktreePath = Path.GetFullPath(worktreePath);
            if (RepositoryIdentity.ResolveRepositoryId(fullWorktreePath) != store.RepositoryId)
            {
                throw new CoordinationException("different_repository",
                    "worktree_path is not part of the same Git repository as the current Unity project.");
            }

            string normalizedHead = NormalizeSha(headSha);
            List<string> normalizedScopes = NormalizeScopes(scopes).ToList();
            return store.Mutate(state =>
            {
                PruneStale(state);
                AgentRecord existing = FindAgent(state, agentId, false);
                if (existing != null && existing.Role != role)
                {
                    throw new CoordinationException("role_changed", "The role of a registered agent cannot change.");
                }

                if (role == CoordinationRoles.Integrator)
                {
                    if (RepositoryIdentity.NormalizePath(fullWorktreePath) !=
                        RepositoryIdentity.NormalizePath(store.ProjectPath))
                    {
                        throw new CoordinationException("integrator_wrong_worktree",
                            "An integrator must register from the project worktree opened by Unity Editor.");
                    }

                    AgentRecord otherIntegrator = state.Agents.FirstOrDefault(agent =>
                        agent.Role == CoordinationRoles.Integrator && agent.AgentId != agentId);
                    if (otherIntegrator != null)
                    {
                        throw new CoordinationException("integrator_exists",
                            $"An integrator is already active: {otherIntegrator.AgentId}");
                    }
                }

                EnsureScopesAvailable(state, agentId, normalizedScopes);
                DateTime now = DateTime.UtcNow;
                AgentRecord agentRecord = existing ?? new AgentRecord
                {
                    AgentId = agentId,
                    StartedUtc = now
                };
                agentRecord.Role = role;
                agentRecord.WorktreePath = fullWorktreePath;
                agentRecord.HeadSha = normalizedHead;
                agentRecord.WorkKind = NormalizeOptionalText(workKind,
                    existing?.WorkKind ?? (role == CoordinationRoles.Integrator ? "integration" : "development"));
                agentRecord.Phase = NormalizeOptionalText(phase,
                    existing?.Phase ?? AgentWorkPhases.Working);
                agentRecord.Summary = summary ?? string.Empty;
                agentRecord.HeartbeatUtc = now;
                if (existing == null)
                {
                    state.Agents.Add(agentRecord);
                }

                AddClaims(state, agentId, normalizedScopes, now);
                return agentRecord;
            });
        }

        public AgentRecord Heartbeat(string agentId, string headSha, string summary, string phase = null,
            string workKind = null)
        {
            return store.Mutate(state =>
            {
                PruneStale(state);
                AgentRecord agent = FindAgent(state, agentId, true);
                if (!string.IsNullOrWhiteSpace(headSha))
                {
                    agent.HeadSha = NormalizeSha(headSha);
                }

                if (summary != null)
                {
                    agent.Summary = summary;
                }

                if (!string.IsNullOrWhiteSpace(phase))
                {
                    agent.Phase = phase.Trim();
                }

                if (!string.IsNullOrWhiteSpace(workKind))
                {
                    agent.WorkKind = workKind.Trim();
                }

                agent.HeartbeatUtc = DateTime.UtcNow;
                if (state.ActiveValidation != null && state.ActiveValidation.IntegratorAgentId == agentId)
                {
                    state.ActiveValidation.HeartbeatUtc = DateTime.UtcNow;
                }

                return agent;
            });
        }

        public List<ScopeClaim> Claim(string agentId, IEnumerable<string> scopes)
        {
            List<string> normalizedScopes = NormalizeScopes(scopes).ToList();
            if (normalizedScopes.Count == 0)
            {
                throw new CoordinationException("scope_required", "At least one scope is required.");
            }

            return store.Mutate(state =>
            {
                PruneStale(state);
                FindAgent(state, agentId, true);
                EnsureScopesAvailable(state, agentId, normalizedScopes);
                AddClaims(state, agentId, normalizedScopes, DateTime.UtcNow);
                return state.Claims.Where(item => item.AgentId == agentId).ToList();
            });
        }

        public void ReleaseClaims(string agentId, IEnumerable<string> scopes)
        {
            List<string> normalizedScopes = NormalizeScopes(scopes).ToList();
            store.Mutate(state =>
            {
                PruneStale(state);
                FindAgent(state, agentId, true);
                state.Claims.RemoveAll(claim => claim.AgentId == agentId &&
                    (normalizedScopes.Count == 0 || normalizedScopes.Contains(claim.Scope)));
                return true;
            });
        }

        public string EnqueueValidation(string agentId, string candidateSha, string summary,
            IEnumerable<string> testModes, IEnumerable<string> manualChecks)
        {
            string normalizedCandidate = NormalizeSha(candidateSha);
            List<string> modes = NormalizeTestModes(testModes).ToList();
            List<string> checks = NormalizeTextList(manualChecks).ToList();
            return store.Mutate(state =>
            {
                PruneStale(state);
                AgentRecord agent = FindAgent(state, agentId, true);
                if (agent.Role != CoordinationRoles.Worker)
                {
                    throw new CoordinationException("worker_required", "Only a worker can enqueue validation.");
                }

                ValidationRequest duplicate = state.ValidationRequests.FirstOrDefault(request =>
                    request.AgentId == agentId && request.CandidateSha == normalizedCandidate &&
                    (request.State == ValidationStates.Queued || request.State == ValidationStates.Running));
                if (duplicate != null)
                {
                    return duplicate.RequestId;
                }

                DateTime now = DateTime.UtcNow;
                ValidationRequest request = new ValidationRequest
                {
                    RequestId = Guid.NewGuid().ToString("N"),
                    AgentId = agentId,
                    CandidateSha = normalizedCandidate,
                    Summary = summary ?? agent.Summary ?? string.Empty,
                    State = ValidationStates.Queued,
                    TestModes = modes,
                    ManualChecks = checks,
                    EnqueuedUtc = now,
                    UpdatedUtc = now
                };
                state.ValidationRequests.Add(request);
                agent.Phase = AgentWorkPhases.WaitingValidation;
                agent.HeartbeatUtc = now;
                return request.RequestId;
            });
        }

        public ValidationRequest AcquireValidation(string integratorAgentId, string requestId,
            string testedHeadSha)
        {
            string normalizedHead = NormalizeSha(testedHeadSha);
            return store.Mutate(state =>
            {
                PruneStale(state);
                AgentRecord integrator = FindAgent(state, integratorAgentId, true);
                if (integrator.Role != CoordinationRoles.Integrator)
                {
                    throw new CoordinationException("integrator_required", "Only an integrator can acquire a validation lease.");
                }

                if (state.ActiveValidation != null)
                {
                    throw new CoordinationException("validation_busy",
                        $"Validation is already active: {state.ActiveValidation.RequestId}");
                }

                ValidationRequest request = string.IsNullOrWhiteSpace(requestId)
                    ? state.ValidationRequests.Where(item => item.State == ValidationStates.Queued)
                        .OrderBy(item => item.EnqueuedUtc).FirstOrDefault()
                    : state.FindRequest(requestId);
                if (request == null || request.State != ValidationStates.Queued)
                {
                    throw new CoordinationException("request_not_queued", "No queued validation request was found.");
                }

                if (request.CandidateSha != normalizedHead)
                {
                    throw new CoordinationException("head_mismatch",
                        $"Requested SHA ({request.CandidateSha}) does not match tested SHA ({normalizedHead}).");
                }

                DateTime now = DateTime.UtcNow;
                request.State = ValidationStates.Running;
                request.Phase = ValidationPhases.Compiling;
                request.UpdatedUtc = now;
                request.CurrentTestIndex = -1;
                request.CurrentTestMode = null;
                request.TestJobId = null;
                request.CompilationErrors.Clear();
                request.TestFailures.Clear();
                integrator.Phase = AgentWorkPhases.Validating;
                integrator.HeartbeatUtc = now;
                state.ActiveValidation = new ActiveValidation
                {
                    RequestId = request.RequestId,
                    IntegratorAgentId = integratorAgentId,
                    TestedHeadSha = normalizedHead,
                    Phase = ValidationPhases.Compiling,
                    AcquiredUtc = now,
                    HeartbeatUtc = now
                };
                return request;
            });
        }

        public void MarkCompilationStarted(string requestId)
        {
            MutateActiveRequest(requestId, (state, request) =>
            {
                request.Phase = ValidationPhases.Compiling;
                state.ActiveValidation.Phase = ValidationPhases.Compiling;
            });
        }

        public void AddCompilationErrors(string requestId, IEnumerable<string> errors)
        {
            List<string> values = NormalizeTextList(errors).ToList();
            MutateActiveRequest(requestId, (_, request) =>
            {
                foreach (string error in values.Where(error => !request.CompilationErrors.Contains(error)))
                {
                    request.CompilationErrors.Add(error);
                }
            });
        }

        public string MarkCompilationFinished(string requestId)
        {
            return MutateActiveRequest(requestId, (state, request) =>
            {
                if (request.CompilationErrors.Count > 0)
                {
                    SetPhase(state, request, ValidationPhases.ReadyToFinish);
                    request.Message = "Compilation errors were recorded.";
                    return null;
                }

                return StartNextTestOrAdvance(state, request);
            });
        }

        public void SetTestJobId(string requestId, string jobId)
        {
            MutateActiveRequest(requestId, (_, request) => request.TestJobId = jobId);
        }

        public void AddTestFailures(string requestId, IEnumerable<string> failures)
        {
            List<string> values = NormalizeTextList(failures).ToList();
            MutateActiveRequest(requestId, (_, request) =>
            {
                foreach (string failure in values.Where(failure => !request.TestFailures.Contains(failure)))
                {
                    request.TestFailures.Add(failure);
                }
            });
        }

        public string CompleteCurrentTest(string requestId)
        {
            return MutateActiveRequest(requestId, StartNextTestOrAdvance);
        }

        public ValidationRequest FinishValidation(string integratorAgentId, string requestId, bool passed,
            string message)
        {
            return store.Mutate(state =>
            {
                PruneStale(state);
                ActiveValidation active = RequireActive(state, requestId);
                if (active.IntegratorAgentId != integratorAgentId)
                {
                    throw new CoordinationException("not_lease_owner", "Only the integrator holding the validation lease can finish it.");
                }

                ValidationRequest request = state.FindRequest(requestId);
                if (request.Phase != ValidationPhases.ReadyToFinish &&
                    request.Phase != ValidationPhases.AwaitingManual)
                {
                    throw new CoordinationException("validation_not_ready", "Automated validation has not finished yet.");
                }

                bool effectivePass = passed && request.CompilationErrors.Count == 0 && request.TestFailures.Count == 0;
                request.State = effectivePass ? ValidationStates.Passed : ValidationStates.Failed;
                request.Phase = null;
                request.Message = message ?? (effectivePass ? "Validation passed" : "Validation failed");
                request.UpdatedUtc = DateTime.UtcNow;
                AgentRecord integrator = FindAgent(state, integratorAgentId, false);
                if (integrator != null)
                {
                    integrator.Phase = AgentWorkPhases.Finalizing;
                    integrator.HeartbeatUtc = DateTime.UtcNow;
                }

                AgentRecord requester = FindAgent(state, request.AgentId, false);
                if (requester != null)
                {
                    requester.Phase = effectivePass ? AgentWorkPhases.Finalizing : AgentWorkPhases.Working;
                    requester.HeartbeatUtc = DateTime.UtcNow;
                }

                state.ActiveValidation = null;
                return request;
            });
        }

        public void CancelValidationRequest(string agentId, string requestId, string message)
        {
            store.Mutate(state =>
            {
                PruneStale(state);
                AgentRecord agent = FindAgent(state, agentId, true);
                ValidationRequest request = state.FindRequest(requestId);
                if (request == null)
                {
                    throw new CoordinationException("request_missing", "The validation request was not found.");
                }

                bool ownsRequest = request.AgentId == agentId;
                bool ownsLease = state.ActiveValidation != null &&
                                 state.ActiveValidation.IntegratorAgentId == agentId &&
                                 state.ActiveValidation.RequestId == requestId;
                if (!ownsRequest && !ownsLease && agent.Role != CoordinationRoles.Integrator)
                {
                    throw new CoordinationException("not_request_owner", "This agent cannot cancel the validation request.");
                }

                request.State = ValidationStates.Canceled;
                request.Phase = null;
                request.Message = message ?? "Canceled";
                request.UpdatedUtc = DateTime.UtcNow;
                AgentRecord requester = FindAgent(state, request.AgentId, false);
                if (requester != null)
                {
                    requester.Phase = AgentWorkPhases.Working;
                    requester.HeartbeatUtc = DateTime.UtcNow;
                }

                if (state.ActiveValidation != null && state.ActiveValidation.RequestId == requestId)
                {
                    AgentRecord integrator = FindAgent(state, state.ActiveValidation.IntegratorAgentId, false);
                    if (integrator != null)
                    {
                        integrator.Phase = AgentWorkPhases.Finalizing;
                        integrator.HeartbeatUtc = DateTime.UtcNow;
                    }

                    state.ActiveValidation = null;
                }

                return true;
            });
        }

        public void FinishWork(string agentId)
        {
            store.Mutate(state =>
            {
                PruneStale(state);
                FindAgent(state, agentId, true);
                if (state.ActiveValidation != null && state.ActiveValidation.IntegratorAgentId == agentId)
                {
                    throw new CoordinationException("active_validation", "Finish or cancel the active validation first.");
                }

                bool hasPendingRequest = state.ValidationRequests.Any(request => request.AgentId == agentId &&
                    (request.State == ValidationStates.Queued || request.State == ValidationStates.Running));
                if (hasPendingRequest)
                {
                    throw new CoordinationException("pending_validation", "A queued or running validation request still exists.");
                }

                state.Claims.RemoveAll(claim => claim.AgentId == agentId);
                state.Agents.RemoveAll(agent => agent.AgentId == agentId);
                state.ValidationRequests.RemoveAll(request => request.AgentId == agentId &&
                    request.State != ValidationStates.Queued && request.State != ValidationStates.Running);
                return true;
            });
        }

        public void ClearAll()
        {
            store.ClearCoordinationState();
        }

        public static bool ScopesOverlap(string left, string right)
        {
            string a = NormalizeScope(left);
            string b = NormalizeScope(right);
            return a == "." || b == "." || a == b ||
                   a.StartsWith(b + "/", StringComparison.Ordinal) ||
                   b.StartsWith(a + "/", StringComparison.Ordinal);
        }

        private T MutateActiveRequest<T>(string requestId, Func<CoordinationState, ValidationRequest, T> mutation)
        {
            return store.Mutate(state =>
            {
                ActiveValidation active = RequireActive(state, requestId);
                ValidationRequest request = state.FindRequest(requestId);
                active.HeartbeatUtc = DateTime.UtcNow;
                request.UpdatedUtc = DateTime.UtcNow;
                return mutation(state, request);
            });
        }

        private void MutateActiveRequest(string requestId, Action<CoordinationState, ValidationRequest> mutation)
        {
            MutateActiveRequest(requestId, (state, request) =>
            {
                mutation(state, request);
                return true;
            });
        }

        private static string StartNextTestOrAdvance(CoordinationState state, ValidationRequest request)
        {
            request.CurrentTestIndex++;
            request.TestJobId = null;
            if (request.CurrentTestIndex < request.TestModes.Count)
            {
                request.CurrentTestMode = request.TestModes[request.CurrentTestIndex];
                SetPhase(state, request, ValidationPhases.Testing);
                return request.CurrentTestMode;
            }

            request.CurrentTestMode = null;
            SetPhase(state, request, request.ManualChecks.Count > 0
                ? ValidationPhases.AwaitingManual
                : ValidationPhases.ReadyToFinish);
            return null;
        }

        private static void SetPhase(CoordinationState state, ValidationRequest request, string phase)
        {
            request.Phase = phase;
            state.ActiveValidation.Phase = phase;
        }

        private static ActiveValidation RequireActive(CoordinationState state, string requestId)
        {
            if (state.ActiveValidation == null || state.ActiveValidation.RequestId != requestId)
            {
                throw new CoordinationException("validation_not_active", "This is not the active validation lease.");
            }

            if (state.FindRequest(requestId) == null)
            {
                throw new CoordinationException("request_missing", "The active validation request record is missing.");
            }

            return state.ActiveValidation;
        }

        private static void PruneStale(CoordinationState state)
        {
            DateTime now = DateTime.UtcNow;
            HashSet<string> staleAgents = new HashSet<string>(state.Agents
                .Where(agent => now - agent.HeartbeatUtc > AgentStaleAfter)
                .Select(agent => agent.AgentId));
            if (staleAgents.Count > 0)
            {
                state.Agents.RemoveAll(agent => staleAgents.Contains(agent.AgentId));
                state.Claims.RemoveAll(claim => staleAgents.Contains(claim.AgentId));
                state.ValidationRequests.RemoveAll(request => staleAgents.Contains(request.AgentId) &&
                    request.State == ValidationStates.Queued);
            }

            if (state.ActiveValidation != null &&
                (now - state.ActiveValidation.HeartbeatUtc > ValidationStaleAfter ||
                 !state.Agents.Any(agent => agent.AgentId == state.ActiveValidation.IntegratorAgentId)))
            {
                ValidationRequest activeRequest = state.FindRequest(state.ActiveValidation.RequestId);
                if (activeRequest != null)
                {
                    activeRequest.State = ValidationStates.Failed;
                    activeRequest.Phase = null;
                    activeRequest.Message = "The validation lease heartbeat expired.";
                    activeRequest.UpdatedUtc = now;
                }

                state.ActiveValidation = null;
            }

            state.ValidationRequests.RemoveAll(request =>
                request.State == ValidationStates.Queued && now - request.UpdatedUtc > QueueStaleAfter);
            state.ValidationRequests.RemoveAll(request =>
                request.State != ValidationStates.Queued && request.State != ValidationStates.Running &&
                !state.Agents.Any(agent => agent.AgentId == request.AgentId));
        }

        private static AgentRecord FindAgent(CoordinationState state, string agentId, bool required)
        {
            AgentRecord agent = state.Agents.FirstOrDefault(item => item.AgentId == agentId);
            if (required && agent == null)
            {
                throw new CoordinationException("agent_missing", "Register the work with begin_work first.");
            }

            return agent;
        }

        private static void EnsureScopesAvailable(CoordinationState state, string agentId,
            IEnumerable<string> scopes)
        {
            ScopeClaim conflict = state.Claims.FirstOrDefault(claim => claim.AgentId != agentId &&
                scopes.Any(scope => ScopesOverlap(scope, claim.Scope)));
            if (conflict != null)
            {
                throw new CoordinationException("scope_conflict",
                    $"Scope '{conflict.Scope}' is already claimed by {conflict.AgentId}.");
            }
        }

        private static void AddClaims(CoordinationState state, string agentId, IEnumerable<string> scopes,
            DateTime now)
        {
            foreach (string scope in scopes)
            {
                if (state.Claims.All(item => item.AgentId != agentId || item.Scope != scope))
                {
                    state.Claims.Add(new ScopeClaim { AgentId = agentId, Scope = scope, ClaimedUtc = now });
                }
            }
        }

        private static IEnumerable<string> NormalizeScopes(IEnumerable<string> scopes)
        {
            return (scopes ?? Enumerable.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(NormalizeScope).Distinct(StringComparer.Ordinal);
        }

        private static string NormalizeScope(string scope)
        {
            RequireValue(scope, "scope");
            string normalized = scope.Replace('\\', '/').Trim().Trim('/');
            if (normalized.Length == 0)
            {
                normalized = ".";
            }

            if (Path.IsPathRooted(scope) || normalized.Split('/').Any(part => part == ".."))
            {
                throw new CoordinationException("invalid_scope", "scope must be a repository-relative path.");
            }

            return Environment.OSVersion.Platform == PlatformID.Win32NT
                ? normalized.ToLowerInvariant()
                : normalized;
        }

        private static IEnumerable<string> NormalizeTestModes(IEnumerable<string> modes)
        {
            foreach (string mode in NormalizeTextList(modes))
            {
                if (!string.Equals(mode, "EditMode", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(mode, "PlayMode", StringComparison.OrdinalIgnoreCase))
                {
                    throw new CoordinationException("invalid_test_mode", "test_modes only accepts EditMode or PlayMode.");
                }

                yield return string.Equals(mode, "EditMode", StringComparison.OrdinalIgnoreCase)
                    ? "EditMode"
                    : "PlayMode";
            }
        }

        private static IEnumerable<string> NormalizeTextList(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()).Distinct(StringComparer.Ordinal);
        }

        private static string NormalizeOptionalText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string NormalizeSha(string sha)
        {
            RequireValue(sha, "sha");
            string value = sha.Trim().ToLowerInvariant();
            if (value.Length != 40 || value.Any(character =>
                    character < '0' || character > '9' && character < 'a' || character > 'f'))
            {
                throw new CoordinationException("invalid_sha", "SHA must be a 40-character Git commit SHA.");
            }

            return value;
        }

        private static void RequireValue(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new CoordinationException("missing_parameter", $"The {name} value is required.");
            }
        }
    }
}
