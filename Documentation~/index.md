# Unity Agent Coordination

[English](index.md) | [한국어](index.ko.md)

## Purpose

This package separates parallel file ownership from serial Unity Editor validation. Workers claim repository-relative paths and may work concurrently when those paths do not overlap. One canonical Editor owns the validation lease and is the only Editor allowed to compile, run Unity tests, enter Play Mode for validation, or confirm serialized changes for a queued commit.

The package enforces claims, one integrator, one active validation lease, exact Git SHAs, and a clean validation checkout. Agent instructions enforce the remaining operating rules, including when coordination starts, which Editor to select, and when not to launch another Editor.

## Compatibility and Dependencies

- Tested with Unity `6000.3.22f1`.
- No `unity` minimum is declared in `package.json`. Package Manager may consider other Unity versions installable, but they are not guaranteed.
- Required: `com.unity.test-framework` and Unity's registry `com.unity.nuget.newtonsoft-json` package. Unity Package Manager resolves both from `package.json` when this package is installed.
- Optional: `com.coplaydev.unity-mcp`. The bundled adapter compiles for versions `10.1.2` through `10.x` and was tested with `10.1.2`.
- Required for coordination: a Git repository. Extra Git worktrees are optional and should not be created by default for Unity projects.

## Installation

Install the tagged package with Unity Package Manager:

```text
https://github.com/olivecrow/Unity-Agent-Coordination.git#v0.2.0
```

For an embedded package, copy this folder to `Packages/com.olivecrow.unity-agent-coordination` and add:

```json
"com.olivecrow.unity-agent-coordination": "file:com.olivecrow.unity-agent-coordination"
```

Installing the coordination package does not install an MCP implementation. Install Coplay separately to use the bundled adapter, or implement an adapter for the Unity bridge already used by the project.

## Integration Layers

The package has two independent layers:

1. `UnityAgentCoordinationCommandRouter` accepts the shared action schema through C# or JSON and returns a serializable `CoordinationCommandResult`.
2. An MCP-specific adapter exposes that router to an AI agent. The bundled Coplay assembly uses Coplay attributes and is excluded when Coplay is unavailable.

Other MCP implementations can use the same command contract without depending on Coplay. Their adapter must run inside the target Unity Editor, forward the complete command object, preserve the stable error code and response data, and avoid direct access to the state files. See [Integration Protocol](mcp-protocol.md).

MCP does not provide a universal custom-Unity-tool discovery contract. Merely telling an agent to read these instructions cannot make an unsupported MCP expose the command. The agent instructions and the transport adapter are both required.

## Activation Boundary

Start coordination immediately before the first operation that uses or can affect a live shared Unity Editor:

- import, refresh, compilation, Unity Test Runner, Play Mode, build, or live Editor diagnosis;
- scene, prefab, or serialized asset mutation;
- writing C#, asmdef, or assets in the checkout currently observed by Unity, because automatic import may start;
- exact-SHA validation or Editor-owner handoff.

Do not start coordination for Git-only operations, read-only filesystem inspection, documentation, planning, or edits in a checkout that is not open in Unity when no Editor validation is requested.

## Canonical Editor

Before the first Editor-affecting action, inspect running Unity processes and the selected MCP's instances when that capability exists. Then invoke `status` with the intended scopes.

- A live `EditorOwner` record identifies the canonical Editor for the repository.
- Only the canonical Editor may register the integrator and acquire validation.
- Agent instructions must not launch another Editor while any Unity Editor process already exists. The package records ownership but does not block process creation itself.
- If multiple Editors exist and the owner cannot be matched safely, report their project paths, process IDs, MCP instances, and owner state rather than guessing.
- A dirty canonical checkout cannot validate an exact SHA. Wait until it can be made clean within the user's authority, or request an explicit owner handoff.

Use bounded readiness checks. The included agent skill recommends 60 seconds for an existing process to appear in the MCP, 180 seconds for import or compilation readiness, and 100 seconds for owner handoff. Startup dialogs, Safe Mode, licensing, upgrades, and recovery choices require user input and are not transient readiness states.

## Work and Validation Lifecycle

1. Call `status` with the stable `agent_id` and intended repository-relative `scopes`.
2. If `ConflictingClaims` is empty, call `begin_work` as a `worker`. The Editor owning the canonical checkout may register one `integrator`.
3. Claim added paths before editing them. Send `heartbeat` when phase, summary, worktree HEAD, or work kind changes.
4. Recheck `status` before writing Unity-observed files and before every Editor-stateful call. Wait while another validation lease is active.
5. After a candidate commit exists, a worker calls `enqueue_validation` with its exact 40-character SHA, ordered test modes, and manual checks.
6. The integrator puts the canonical checkout at that SHA, confirms it has no tracked or untracked changes, then calls `acquire_validation`.
7. The package refreshes assets, requests compilation, and runs requested `EditMode` and `PlayMode` suites serially.
8. After console and manual checks, the lease owner calls `finish_validation`. Recorded compile or test failures cannot be overridden by `passed=true`.
9. Cancel abandoned requests and call `finish_work` only after every owned request is terminal.

If an explicitly approved separate worktree uses Git LFS, prove that tracked files are materialized before opening Unity or enqueueing validation. A clean `git status` does not distinguish real objects from committed LFS pointer files.

## State and Recovery

All worktrees of the same Git repository share an ID derived from `.git/commondir`. State is stored under:

```text
%LOCALAPPDATA%/Olivecrow/UnityAgentCoordination/<repository-id>
```

Set `UNITY_AGENT_COORDINATOR_ROOT` to override the root for tests or controlled automation. JSON writes use a named mutex and atomic temporary-file replacement.

The canonical Editor heartbeats every 15 seconds. A dead owner process can be replaced immediately; otherwise an owner is stale after 90 seconds. Stale owner recovery preserves worker claims and queued validation. Agent heartbeats expire after 45 minutes, validation leases after 45 minutes, and orphaned queued requests after four hours. Normal Editor exit clears the repository coordination and owner files.

Inspect live state at `Window > AI Agent Coordination`. Clearing all state is a recovery operation, not normal cleanup.

## Agent Guidance

The package includes a Codex skill under `Skills~/unity-agent-coordination`. Other agents can use the same operating contract through project guidance such as `AGENTS.md` or `CLAUDE.md`. See [Agent Setup](agent-setup.md).
