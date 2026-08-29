# Integration Protocol

[English](mcp-protocol.md) | [한국어](mcp-protocol.ko.md)

## Transport-Neutral API

The stable integration surface is in the `Olivecrow.UnityAgentCoordination.Editor` assembly:

```csharp
CoordinationCommandResult result =
    UnityAgentCoordinationCommandRouter.ExecuteJson(jsonCommand);

string responseJson = result.ToJson();
```

Adapters that already receive a Newtonsoft `JObject` may call `Execute(JObject)` instead. The result contains:

```json
{
  "success": true,
  "message": "status completed",
  "data": {}
}
```

Failures set `success` to `false`, include a stable `error_code`, and omit `data` when there is no result. Adapters should preserve these fields rather than converting errors to free-form text.

Both `snake_case` and equivalent `camelCase` parameter names are accepted. Action names are lowercase snake case.

## Actions

| Action | Caller | Required fields | Optional fields |
|---|---|---|---|
| `status` | any | none | `agent_id`, `session_id`, `scopes` |
| `begin_work` | any agent | `agent_id`, `role`, `worktree_path`, `head_sha` | `session_id`, `scopes`, `summary`, `work_kind`, `phase` |
| `heartbeat` | registered session | `agent_id` | `session_id`, `head_sha`, `summary`, `work_kind`, `phase` |
| `claim` | registered session | `agent_id`, non-empty `scopes` | `session_id` |
| `release_claims` | registered session | `agent_id` | `session_id`, `scopes`; omit scopes to release all |
| `enqueue_editor_access` | registered session | `agent_id`, non-empty `operations` | `session_id`, `request_key`, `base_head_sha`, `summary`, `scopes` |
| `acquire_editor_access` | request owner | `agent_id` | `session_id`, `request_id`; omit request ID for FIFO |
| `finish_editor_access` | lease owner | `agent_id`, `request_id` | `session_id`, `message` |
| `cancel_editor_access` | request owner or integrator | `agent_id`, `request_id` | `session_id`, `message` |
| `enqueue_validation` | worker session | `agent_id`, `candidate_sha` | `session_id`, `summary`, `test_modes`, `manual_checks` |
| `acquire_validation` | integrator session | `agent_id`, `candidate_sha` | `session_id`, `request_id`; omit request ID for FIFO |
| `finish_validation` | lease owner | `agent_id`, `request_id`, `passed` | `session_id`, `message` |
| `cancel_validation` | request owner or integrator | `agent_id`, `request_id` | `session_id`, `message` |
| `finish_work` | registered session | `agent_id` | `session_id` |
| `relinquish_editor` | coordinator integrator | `agent_id`, `confirm=true` | `session_id` |
| `clear_all` | coordinator integrator | `agent_id`, `confirm=true` | `session_id` |

`role` is `worker` or `integrator`. `agent_id` identifies the logical agent or task. `session_id` identifies one concrete work session and must be unique within the repository; omitting it uses `agent_id` for backward compatibility. Multiple sessions may share the same `agent_id`, worktree HEAD, and base commit. Validation deduplication is also session-scoped, so two sessions may independently queue the same candidate SHA. Only the Editor whose project path matches the package's current project may register the integrator, and only one integrator session may exist.

`scopes` are repository-relative paths. Equal paths and parent/child paths conflict. Absolute paths and `..` segments are rejected. Comparisons are case-insensitive on Windows. Every scope supplied to `enqueue_editor_access` must already be covered by that session's claim; path-independent requests may omit scopes.

`head_sha`, optional `base_head_sha`, and `candidate_sha` are full 40-character Git commit SHAs. `head_sha` describes the session's current HEAD. `base_head_sha` is optional Editor-request diagnostics and is never a queue identity or clean-checkout requirement. Only `candidate_sha` drives exact validation: `acquire_validation` compares the queued candidate, requested candidate, and actual canonical checkout HEAD and rejects tracked or untracked changes.

`operations` accepts one or more free-form operation names so adapters can describe import, compilation, Console inspection, tests, Play Mode, builds, scene or prefab edits, or provider-specific work without changing the protocol. The package grants a lease but does not execute these operations. Supplying `request_key` makes `enqueue_editor_access` idempotent within one session; without it, every call creates a distinct request even at the same HEAD.

`test_modes` accepts only `EditMode` and `PlayMode`, executed in array order. `phase` normally uses `inspecting`, `working`, `waiting_editor_access`, `using_editor`, `waiting_validation`, `validating`, `finalizing`, or `blocked` for agent work. Active validation uses `compiling`, `testing`, `awaiting_manual`, or `ready_to_finish`.

## Example Lifecycle

```json
{
  "action": "status",
  "agent_id": "codex",
  "session_id": "task-42",
  "scopes": ["Assets/MyFeature"]
}
```

```json
{
  "action": "begin_work",
  "agent_id": "codex",
  "session_id": "task-42",
  "role": "worker",
  "worktree_path": "C:/Projects/Game",
  "head_sha": "0123456789abcdef0123456789abcdef01234567",
  "scopes": ["Assets/MyFeature"],
  "work_kind": "code",
  "phase": "working",
  "summary": "Implement feature"
}
```

Before using the Editor, the same session can queue access without creating a commit:

```json
{
  "action": "enqueue_editor_access",
  "agent_id": "codex",
  "session_id": "task-42",
  "request_key": "compile-after-code-edit-1",
  "operations": ["import", "compile", "console"],
  "scopes": ["Assets/MyFeature"],
  "summary": "Compile uncommitted feature work"
}
```

The request owner calls `acquire_editor_access` when it reaches the FIFO head, heartbeats while performing the declared operations through the existing Unity bridge, then calls `finish_editor_access`. An Editor access lease allows a dirty canonical checkout but cannot make changes from another worktree visible.

After committing, the worker may separately use `enqueue_validation`. The integrator prepares a clean canonical checkout at the exact SHA and calls `acquire_validation`. Editor access and validation leases are mutually exclusive. When validation reaches `awaiting_manual` or `ready_to_finish`, the integrator checks the final Console and manual items, calls `finish_validation`, and every session eventually calls `finish_work`.

## Adapter Requirements

An MCP adapter must:

- run inside or safely marshal calls into the target Unity Editor;
- expose all command fields without changing their meaning;
- call only the public command router or public coordination service, never state files;
- serialize `CoordinationCommandResult` fields faithfully;
- identify the Unity project or instance so agents do not send commands to the wrong Editor;
- avoid acquiring Editor access or validation outside the canonical owner Editor.

The bundled Coplay adapter exposes:

- tool: `manage_unity_agent_coordination`;
- resource: `get_unity_agent_coordination`;
- assembly: `Olivecrow.UnityAgentCoordination.CoplayMcp`;
- version gate: `com.coplaydev.unity-mcp` `[10.1.2,11.0.0)`.

It refreshes Coplay tool and resource discovery for connected HTTP or stdio transports. Coplay-specific resources such as its instance, Editor state, and window resources are useful for selecting the canonical Editor but are not part of this package's portable protocol.

## Common Errors

| Error code | Meaning |
|---|---|
| `parameters_required` | No command object was supplied. |
| `invalid_json` | The JSON is malformed or is not an object. |
| `missing_parameter` | A required field is blank or absent. |
| `scope_conflict` | Another agent owns an equal, parent, or child scope. |
| `scope_not_claimed` | An Editor access scope is not covered by the requesting session's claims. |
| `integrator_exists` | Another integrator is active. |
| `operation_required` | An Editor access request has no operations. |
| `editor_access_busy` | Another Editor access lease is active. |
| `request_not_next` | A different Editor access request is ahead in FIFO order. |
| `request_not_cancelable` | The Editor access request is already terminal. |
| `editor_access_not_active` | The request does not own the current Editor access lease. |
| `pending_editor_access` | The session still owns a queued or active Editor access request. |
| `validation_busy` | Another serial validation lease is active. |
| `head_mismatch` | The queued SHA differs from the requested validation SHA. |
| `actual_head_mismatch` | The actual Unity checkout HEAD differs from the requested SHA. |
| `tracked_worktree_dirty` | The validation checkout has tracked or untracked changes. |
| `editor_busy` | Unity is compiling, updating assets, or changing Play Mode. |
| `not_coordinator_editor` | Another Unity Editor owns repository validation. |
| `pending_validation` | The agent still owns a queued or running request. |

Never resolve an error by deleting another agent's claim, discarding checkout changes, or editing coordination JSON directly.
