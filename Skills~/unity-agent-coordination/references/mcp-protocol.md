# Coordination Command Reference

Use the transport adapter that forwards commands to `UnityAgentCoordinationCommandRouter`. The bundled Coplay adapter exposes `manage_unity_agent_coordination`; other adapters may use a different tool name but must preserve this action schema and response fields.

`agent_id` identifies the logical agent. `session_id` identifies one independent work session and should be unique even when the same agent, worktree, and Git HEAD are reused. For backward compatibility, omitting `session_id` makes the package use `agent_id` as the session identity.

| Step | Action | Required values |
|---|---|---|
| Inspect | `status` | Optional `agent_id`, `session_id`, and planned `scopes` |
| Register | `begin_work` | `agent_id`, `role`, `worktree_path`, `head_sha`; optional `session_id`, `scopes`, `work_kind`, `phase`, `summary` |
| Expand scope | `claim` | `agent_id`, added `scopes`; optional `session_id` |
| Release scope | `release_claims` | `agent_id`; optional `session_id` and `scopes` |
| Update | `heartbeat` | `agent_id`; optional `session_id`, new `head_sha`, `work_kind`, `phase`, `summary` |
| Queue Editor use | `enqueue_editor_access` | `agent_id`, non-empty `operations`; optional `session_id`, `scopes`, `base_head_sha`, `request_key`, `summary` |
| Acquire Editor use | `acquire_editor_access` | Request owner `agent_id`; optional `session_id` and `request_id` |
| Finish Editor use | `finish_editor_access` | Lease owner `agent_id`, `request_id`; optional `session_id`, `message` |
| Cancel Editor use | `cancel_editor_access` | Request owner `agent_id`, `request_id`; optional `session_id`, `message` |
| Queue exact validation | `enqueue_validation` | Worker `agent_id`, `candidate_sha`; optional `session_id`, `test_modes`, `manual_checks`, `summary` |
| Acquire exact validation | `acquire_validation` | Integrator `agent_id`, actual HEAD in `candidate_sha`; optional `session_id`, `request_id` |
| Finish validation | `finish_validation` | Lease owner `agent_id`, `request_id`, `passed`; optional `session_id`, `message` |
| Cancel validation | `cancel_validation` | Request or lease owner `agent_id`, `request_id`; optional `session_id`, `message` |
| Finish work | `finish_work` | `agent_id`; optional `session_id` |

Omitting `scopes` from `release_claims` releases every claim owned by that session. `relinquish_editor` and `clear_all` require `confirm=true` and a registered integrator session in the coordinator Editor.

Use `worker` or `integrator` for `role`. Use repository-relative paths for `scopes`. Equal, parent, and child paths conflict across sessions. A path listed on `enqueue_editor_access` must already be covered by that session's claim; omit scopes for path-independent operations such as Console inspection. Use full 40-character Git SHAs. `test_modes` accepts only `EditMode` and `PlayMode`.

## General Editor Access

The general access queue is commit-optional and session-scoped. `operations` is a non-empty, free-form list describing work such as `import`, `compile`, `console`, `editmode_tests`, `playmode`, `scene_edit`, `prefab_edit`, or `build`. `base_head_sha` is optional metadata and is never the request identity or a clean-checkout requirement. Every enqueue creates a new request unless the session supplies an existing `request_key`, in which case the original request ID is returned.

Acquisition is strict FIFO. Only the session that queued the first pending request may acquire it, and acquisition must happen through the coordinator Editor while Unity is idle. Keep the lease alive with `heartbeat`, then finish or cancel it before ending the session. The lease serializes Editor-stateful operations but does not transfer uncommitted files from another worktree into the canonical checkout.

## Exact-SHA Validation

Validation remains a separate queue for reproducible final verification. It requires a clean canonical checkout at the queued 40-character candidate SHA. Deduplication is session-scoped, so different sessions may independently queue the same SHA. The integrator acquires the request and runs compilation plus the declared EditMode and PlayMode suites. General Editor access and exact validation leases are mutually exclusive, but their pending queues remain independent.

Before writing files observed by the canonical Editor or making an Editor-stateful call, refresh `status`. Never access coordination JSON or owner files directly. Feature-detect general access through the action schema or the status state's reported schema version (`State.SchemaVersion >= 2` in the bundled router response); do not send commit-free work to an older exact-SHA validation queue.

Important error codes include `scope_conflict`, `scope_not_claimed`, `session_agent_mismatch`, `integrator_exists`, `request_not_next`, `request_not_queued`, `request_not_cancelable`, `not_request_owner`, `not_lease_owner`, `editor_access_busy`, `validation_busy`, `head_mismatch`, `actual_head_mismatch`, `tracked_worktree_dirty`, `editor_busy`, `not_coordinator_editor`, `pending_editor_access`, and `pending_validation`. Preserve the error code when reporting or adapting the transport.

The complete portable contract is in `Documentation~/mcp-protocol.md`.
