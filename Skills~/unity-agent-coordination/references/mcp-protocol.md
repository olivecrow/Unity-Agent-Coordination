# Coordination Command Reference

Use the transport adapter that forwards commands to `UnityAgentCoordinationCommandRouter`. The bundled Coplay adapter exposes `manage_unity_agent_coordination`; other adapters may use a different tool name but must preserve this action schema and response fields.

| Step | Action | Required values |
|---|---|---|
| Inspect | `status` | optional `agent_id`, planned `scopes` |
| Register | `begin_work` | `agent_id`, `role`, `worktree_path`, `head_sha`; optional `scopes`, `work_kind`, `phase`, `summary` |
| Expand scope | `claim` | `agent_id`, added `scopes` |
| Update | `heartbeat` | `agent_id`; optional new `head_sha`, `work_kind`, `phase`, `summary` |
| Queue | `enqueue_validation` | worker `agent_id`, `candidate_sha`; optional `test_modes`, `manual_checks` |
| Acquire | `acquire_validation` | integrator `agent_id`, actual HEAD in `candidate_sha`; optional `request_id` |
| Finish validation | `finish_validation` | lease owner, `request_id`, `passed`; optional `message` |
| Finish work | `finish_work` | `agent_id` |

Omitting `scopes` from `release_claims` releases every claim owned by that agent. `relinquish_editor` and `clear_all` require `confirm=true` and a registered integrator in the coordinator Editor.

Use `worker` or `integrator` for `role`. Use repository-relative paths for `scopes`. Use full 40-character Git SHAs. `test_modes` accepts only `EditMode` and `PlayMode`.

Before writing files observed by the canonical Editor or making an Editor-stateful call, refresh `status`. Wait while a validation lease is active. Never access coordination JSON or owner files directly.

Important error codes include `scope_conflict`, `integrator_exists`, `validation_busy`, `head_mismatch`, `actual_head_mismatch`, `tracked_worktree_dirty`, `editor_busy`, `not_coordinator_editor`, and `pending_validation`. Preserve the error code when reporting or adapting the transport.

The complete portable contract is in `Documentation~/mcp-protocol.md`.
