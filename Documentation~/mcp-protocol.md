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
| `status` | any | none | `agent_id`, `scopes` |
| `begin_work` | any agent | `agent_id`, `role`, `worktree_path`, `head_sha` | `scopes`, `summary`, `work_kind`, `phase` |
| `heartbeat` | registered agent | `agent_id` | `head_sha`, `summary`, `work_kind`, `phase` |
| `claim` | registered agent | `agent_id`, non-empty `scopes` | none |
| `release_claims` | registered agent | `agent_id` | `scopes`; omit to release all |
| `enqueue_validation` | worker | `agent_id`, `candidate_sha` | `summary`, `test_modes`, `manual_checks` |
| `acquire_validation` | integrator | `agent_id`, `candidate_sha` | `request_id`; omit for FIFO |
| `finish_validation` | lease owner | `agent_id`, `request_id`, `passed` | `message` |
| `cancel_validation` | request owner or integrator | `agent_id`, `request_id` | `message` |
| `finish_work` | registered agent | `agent_id` | none |
| `relinquish_editor` | coordinator integrator | `agent_id`, `confirm=true` | none |
| `clear_all` | coordinator integrator | `agent_id`, `confirm=true` | none |

`role` is `worker` or `integrator`. Only the Editor whose project path matches the package's current project may register the integrator, and only one integrator may exist.

`scopes` are repository-relative paths. Equal paths and parent/child paths conflict. Absolute paths and `..` segments are rejected. Comparisons are case-insensitive on Windows.

`head_sha` and `candidate_sha` are full 40-character Git commit SHAs. `acquire_validation` compares the queued candidate, the requested candidate, and the actual canonical checkout HEAD. It also rejects tracked or untracked changes.

`test_modes` accepts only `EditMode` and `PlayMode`, executed in array order. `phase` normally uses `inspecting`, `working`, `waiting_validation`, `validating`, `finalizing`, or `blocked` for agent work. Active validation uses `compiling`, `testing`, `awaiting_manual`, or `ready_to_finish`.

## Example Lifecycle

```json
{
  "action": "status",
  "agent_id": "task-42",
  "scopes": ["Assets/MyFeature"]
}
```

```json
{
  "action": "begin_work",
  "agent_id": "task-42",
  "role": "worker",
  "worktree_path": "C:/Projects/Game",
  "head_sha": "0123456789abcdef0123456789abcdef01234567",
  "scopes": ["Assets/MyFeature"],
  "work_kind": "code",
  "phase": "working",
  "summary": "Implement feature"
}
```

After committing, the worker uses `enqueue_validation`. The integrator prepares a clean canonical checkout at the exact SHA and calls `acquire_validation`. When the phase reaches `awaiting_manual` or `ready_to_finish`, the integrator checks the final Console and manual items, calls `finish_validation`, and both agents eventually call `finish_work`.

## Adapter Requirements

An MCP adapter must:

- run inside or safely marshal calls into the target Unity Editor;
- expose all command fields without changing their meaning;
- call only the public command router or public coordination service, never state files;
- serialize `CoordinationCommandResult` fields faithfully;
- identify the Unity project or instance so agents do not send commands to the wrong Editor;
- avoid acquiring validation outside the canonical owner Editor.

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
| `integrator_exists` | Another integrator is active. |
| `validation_busy` | Another serial validation lease is active. |
| `head_mismatch` | The queued SHA differs from the requested validation SHA. |
| `actual_head_mismatch` | The actual Unity checkout HEAD differs from the requested SHA. |
| `tracked_worktree_dirty` | The validation checkout has tracked or untracked changes. |
| `editor_busy` | Unity is compiling, updating assets, or changing Play Mode. |
| `not_coordinator_editor` | Another Unity Editor owns repository validation. |
| `pending_validation` | The agent still owns a queued or running request. |

Never resolve an error by deleting another agent's claim, discarding checkout changes, or editing coordination JSON directly.
