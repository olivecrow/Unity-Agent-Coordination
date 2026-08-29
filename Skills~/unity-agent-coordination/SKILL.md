---
name: unity-agent-coordination
description: Coordinate session-scoped access to a shared Unity Editor with a commit-optional FIFO access queue and a separate clean exact-SHA validation queue. When activated, reuse an installed com.olivecrow.unity-agent-coordination package or bootstrap its tagged Git UPM dependency before Editor work. Use for import or compilation, Unity Test Runner, Play Mode, builds, live Editor diagnosis, scene or prefab serialization, writes in the checkout currently open in Unity that can trigger automatic import, and workflows that must acquire the canonical Editor or validation lease. Do not use for Git-only, read-only, planning, documentation, or filesystem-only work that does not use or affect a live Unity Editor.
---

# Unity Agent Coordination

Use exactly one canonical Unity Editor as the access and validation authority while independent work sessions claim non-conflicting paths. This skill governs session-scoped Editor leases and exact-SHA validation; it is not a general Git wrapper. Additional Git worktrees are optional and must not be created by default for Unity projects.

Use the installed transport adapter as the exclusive API for coordination state. The bundled Coplay adapter names its tool `manage_unity_agent_coordination`; another MCP may expose an equivalent name while preserving the same action schema. Never read or write LocalAppData state files directly.

## Activation Boundary

Activate immediately before the first operation that will:

- import, refresh, compile, run Unity tests, enter Play Mode, build, or inspect live Editor state;
- mutate scenes, prefabs, or serialized assets through Unity;
- write C#, asmdef, or assets in the exact checkout currently open in Unity, where automatic import can start;
- enqueue or acquire commit-optional Editor access, enqueue or acquire exact-SHA validation, or transfer canonical Editor ownership.

Do not activate for Git-only work; read-only code or asset inspection; documentation, planning, or administration; or filesystem-only edits in a checkout not open in Unity when no Editor validation is requested. If Editor involvement becomes necessary later, activate immediately before that first Editor-affecting step.

## Package Bootstrap

After activation, locate the Unity project root by its `Assets`, `Packages`, and `ProjectSettings` directories. Check whether Unity already resolves `com.olivecrow.unity-agent-coordination` or `Packages/manifest.json` already declares it.

- If the package is already installed or declared, use that source and version as-is. Do not replace a file, registry, or Git dependency and do not upgrade it merely because this skill was activated.
- If no dependency is declared but `Packages/com.olivecrow.unity-agent-coordination/package.json` exists with the matching package name, add the existing embedded package as `"file:com.olivecrow.unity-agent-coordination"`.
- Otherwise add this exact dependency under the existing `dependencies` object in `Packages/manifest.json`:

```json
"com.olivecrow.unity-agent-coordination": "https://github.com/olivecrow/Unity-Agent-Coordination.git#v0.2.0"
```

Preserve every unrelated manifest entry and the project's formatting, then validate the JSON. Never edit `Packages/packages-lock.json` by hand; let Unity Package Manager resolve it. If the manifest is invalid, conflicted, or cannot be changed without overwriting unrelated work, stop and report the blocker.

Inspect OS Unity processes before changing the manifest and inspect the selected MCP's Unity instances at the same time when available. Do not launch a second Editor. When the target project is already open, the dependency edit may immediately trigger package resolution, import, and compilation; wait within the readiness budget before continuing. When no Editor is open, record the dependency before any authorized launch and wait for resolution after startup. Stop on Git/network failure, package-resolution failure, an upgrade prompt, Safe Mode, or a compatibility error instead of substituting another package or editing coordination state directly.

Package bootstrap is the only pre-coordination exception: the coordination API cannot exist before the package is available. Include `Packages/manifest.json` in the first reported scope when it was changed. Once the package and an adapter are available, `status` must be the first coordination action. Installing this package does not install an MCP provider; use an adapter exposed by the project's existing Unity MCP, and do not install or replace that provider unless the user separately requests it.

## First Action

1. Complete the package bootstrap and process checks above.
2. Select the transport instance matching the target repository, create a stable logical `agent_id`, and create a unique `session_id` for this work session.
3. Discover the installed coordination adapter. If none exists after package resolution, stop before the Editor-affecting operation and report that the current Unity MCP needs an adapter.
4. Call `action=status` with the stable `agent_id`, unique `session_id`, and intended repository-relative `scopes`.
5. Match the returned live `EditorOwner` to the selected Unity instance. Only that Editor may grant access or validate.

With Coplay, use `manage_unity_agent_coordination` directly. If Coplay exposes the registered command only through `batch_execute`, use that wrapper with the same tool name and payload. Coplay resources such as its instances, Editor state, and window resources help with instance selection, but are not portable requirements for other adapters.

## Canonical Editor and Bounded Readiness

- Do not automatically launch another Unity Editor while any Editor process already exists. Inspect and report the existing processes first.
- If a live owner exists, match its project path and process ID to the transport instance when possible. That owner is the only Editor access and validation authority.
- If no owner exists but an Editor for the repository is running, wait for package initialization and adapter registration.
- If multiple Editors exist and ownership cannot be determined, report process IDs, project paths, transport instances, and owner state. Do not guess.
- Launch one Editor only when no Editor exists, Editor work is actually required, and the host's approval rules permit it.

Use bounded polling. Unless the user supplies another limit, allow up to 60 seconds for an existing process to appear in the transport, 180 seconds for import or compilation readiness, and 100 seconds for owner handoff. Startup dialogs, Safe Mode, licensing, upgrades, recovery choices, and package-resolution prompts are blockers requiring user input. Do not click through them automatically.

Every timeout report includes elapsed time, Unity process ID and project path, transport instance, `EditorOwner`, last readiness or modal state, and safe commands already attempted.

## Interpret Status and Register Work

- Continue only when `ConflictingClaims` is empty.
- Equal, parent, and child scopes conflict. Narrow the scope or wait for its owner; never remove another agent's claim.
- An active Editor access or validation lease serializes compilation, tests, Play Mode, scene mutation, and other Editor-stateful work. In the canonical checkout either lease also pauses C#, asmdef, and asset writes owned by other sessions when those writes can trigger import.

Call `begin_work` with:

- `role=worker` for ordinary work, or `role=integrator` only in the canonical Editor checkout;
- the stable logical `agent_id` and unique `session_id`; if the installed adapter does not expose `session_id`, use one unique task-scoped `agent_id` as the backward-compatible session identity;
- absolute `worktree_path`, current 40-character `head_sha`, exact `scopes`, and a short `summary`;
- a short `work_kind` and a current `phase` such as `inspecting`, `working`, `waiting_editor_access`, `using_editor`, `waiting_validation`, `validating`, `finalizing`, or `blocked`.

Call `claim` before expanding into more paths. Include `session_id` in every action. Send `heartbeat` when phase, summary, work kind, or HEAD changes and while holding a lease. Immediately before each batch of Unity-observed file writes and before every Editor-stateful call, refresh `status` and wait if another Editor access or validation lease is active.

## Editor Access Queue

Use the general Editor access queue for import, refresh, compilation, Console inspection, EditMode or PlayMode tests outside exact validation, Play Mode, builds, scene or prefab edits, live diagnosis, and provider-specific stateful operations. A candidate commit is not required and a dirty canonical checkout is allowed.

1. Call `enqueue_editor_access` with `agent_id`, `session_id`, one or more free-form `operations`, already-claimed relevant `scopes`, and a short summary. Omit scopes for path-independent operations such as Console inspection. `base_head_sha` is optional diagnostic metadata, not the request identity. Add a stable `request_key` only when retries must be idempotent; otherwise every call creates a distinct request even at the same HEAD.
2. Poll `status`. When the request is the FIFO head and no lease is active, its owner session calls `acquire_editor_access`. Never acquire another session's request.
3. Confirm the canonical Editor's current checkout actually contains the work to inspect. The lease does not copy uncommitted changes from another worktree.
4. Perform only the declared Editor operations through the existing Unity bridge and keep calling `heartbeat` during long work.
5. Call `finish_editor_access` immediately after the Editor work, or `cancel_editor_access` if the queued or active request is abandoned.

Feature-detect this queue from the adapter action schema or the status state's reported schema version (`State.SchemaVersion >= 2` in the bundled router response). An older installed package may expose only claims and exact-SHA validation. Do not silently upgrade an existing package, and do not misuse its validation queue for commit-free access; report the missing general queue and follow the older package's claim-and-wait contract.

## Worker Contract

Workers edit only claimed paths. They may compile, run tests, enter Play Mode, build, diagnose, or mutate the shared Editor only while their own general Editor access lease is active. They never mutate the Editor during another lease, use the exact-SHA validation runner directly, manipulate state files, or acquire another session's request.

After the candidate is committed and the user has authorized the required Git operation, call `enqueue_validation` with the exact `candidate_sha`, ordered `test_modes` (`EditMode`, `PlayMode`), and `manual_checks`. Poll status for the terminal result. If no candidate commit can be created within current authority, stop before enqueueing.

For an explicitly approved additional worktree using Git LFS, verify materialization before Unity or enqueueing: inspect tracked paths, run bounded `git lfs pull` and `git lfs checkout`, check `git lfs status`, and reject files that still begin with the Git LFS pointer header. A clean `git status` is not proof of materialization.

## Integrator Contract

Only one integrator session may exist. Authorized Git integration happens outside this protocol. Before acquiring validation, prove that the canonical Unity checkout:

- is at the candidate commit;
- has no tracked or untracked changes;
- is not compiling, updating assets, changing Play Mode, using another Editor access lease, or validating another request.

Clear the Unity Console immediately before acquiring. Call `acquire_validation` with the queued `request_id` or omit it for FIFO, and pass the exact current HEAD as `candidate_sha`. The package requests compilation and runs the requested Unity test modes serially.

If the canonical checkout is dirty, stop exact-SHA validation. Do not open a second Editor as a workaround. An owner handoff requires explicit user approval and no active Editor access or validation. The old integrator then calls `relinquish_editor` with `confirm=true`, completes the approved transition, and waits for the new canonical Editor to acquire ownership before validation continues.

When validation reaches `awaiting_manual` or `ready_to_finish`, read the final Console through the available Unity bridge and perform the listed manual checks. Call `finish_validation` with the lease-owning `agent_id`, `request_id`, `passed`, and concise evidence.

## Cleanup

- Call `cancel_editor_access` or `cancel_validation` when abandoning a queued or active request.
- Call `release_claims` when paths are no longer being edited.
- Call `finish_work` after all owned requests are terminal. Pending requests intentionally prevent cleanup.
- When the last worker and integrator finish, the empty coordination file is deleted automatically.

Read [references/mcp-protocol.md](references/mcp-protocol.md) before constructing an unfamiliar action, adapting another MCP, or recovering from a protocol error.
