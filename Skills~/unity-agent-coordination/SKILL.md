---
name: unity-agent-coordination
description: Coordinate access to a shared Unity Editor when work requires Editor-stateful operations or exact-SHA Editor validation. Use for import or compilation, Unity Test Runner, Play Mode, builds, live Editor diagnosis, scene or prefab serialization, writes in the checkout currently open in Unity that can trigger automatic import, and workflows that must acquire the canonical Editor or validation lease. Do not use for Git-only, read-only, planning, documentation, or filesystem-only work that does not use or affect a live Unity Editor.
---

# Unity Agent Coordination

Use exactly one canonical Unity Editor as the integration and validation authority while workers claim non-conflicting paths. This skill governs Editor ownership and serial Unity validation; it is not a general Git wrapper. Additional Git worktrees are optional and must not be created by default for Unity projects.

Use the installed transport adapter as the exclusive API for coordination state. The bundled Coplay adapter names its tool `manage_unity_agent_coordination`; another MCP may expose an equivalent name while preserving the same action schema. Never read or write LocalAppData state files directly.

## Activation Boundary

Activate immediately before the first operation that will:

- import, refresh, compile, run Unity tests, enter Play Mode, build, or inspect live Editor state;
- mutate scenes, prefabs, or serialized assets through Unity;
- write C#, asmdef, or assets in the exact checkout currently open in Unity, where automatic import can start;
- enqueue or acquire exact-SHA validation, or transfer canonical Editor ownership.

Do not activate for Git-only work; read-only code or asset inspection; documentation, planning, or administration; or filesystem-only edits in a checkout not open in Unity when no Editor validation is requested. If Editor involvement becomes necessary later, activate immediately before that first Editor-affecting step.

## First Action

1. Inspect OS Unity processes and the selected MCP's Unity instances when available. Run independent checks together.
2. Select the instance matching the target repository and create one stable `agent_id` for this task.
3. Verify that a coordination adapter is callable. If none exists, stop before the Editor-affecting operation and report that an adapter is required.
4. Call `action=status` with the stable `agent_id` and intended repository-relative `scopes`.
5. Match the returned live `EditorOwner` to the selected Unity instance. Only that Editor may validate.

With Coplay, use `manage_unity_agent_coordination` directly. If Coplay exposes the registered command only through `batch_execute`, use that wrapper with the same tool name and payload. Coplay resources such as its instances, Editor state, and window resources help with instance selection, but are not portable requirements for other adapters.

## Canonical Editor and Bounded Readiness

- Do not automatically launch another Unity Editor while any Editor process already exists. Inspect and report the existing processes first.
- If a live owner exists, match its project path and process ID to the transport instance when possible. That owner is the only validator.
- If no owner exists but an Editor for the repository is running, wait for package initialization and adapter registration.
- If multiple Editors exist and ownership cannot be determined, report process IDs, project paths, transport instances, and owner state. Do not guess.
- Launch one Editor only when no Editor exists, Editor work is actually required, and the host's approval rules permit it.

Use bounded polling. Unless the user supplies another limit, allow up to 60 seconds for an existing process to appear in the transport, 180 seconds for import or compilation readiness, and 100 seconds for owner handoff. Startup dialogs, Safe Mode, licensing, upgrades, recovery choices, and package-resolution prompts are blockers requiring user input. Do not click through them automatically.

Every timeout report includes elapsed time, Unity process ID and project path, transport instance, `EditorOwner`, last readiness or modal state, and safe commands already attempted.

## Interpret Status and Register Work

- Continue only when `ConflictingClaims` is empty.
- Equal, parent, and child scopes conflict. Narrow the scope or wait for its owner; never remove another agent's claim.
- An active validation lease serializes compilation, tests, Play Mode, scene mutation, and other Editor-stateful work. In the canonical checkout it also pauses C#, asmdef, and asset writes that can trigger import.

Call `begin_work` with:

- `role=worker` for ordinary work, or `role=integrator` only in the canonical Editor checkout;
- absolute `worktree_path`, current 40-character `head_sha`, exact `scopes`, and a short `summary`;
- a short `work_kind` and a current `phase` such as `inspecting`, `working`, `waiting_validation`, `validating`, `finalizing`, or `blocked`.

Call `claim` before expanding into more paths. Send `heartbeat` when phase, summary, work kind, or HEAD changes. Immediately before each batch of Unity-observed file writes and before every Editor-stateful call, refresh `status` and wait if validation is active.

## Worker Contract

Workers edit only claimed paths. They do not compile, run tests, enter Play Mode, mutate the shared Editor during validation, use the validation runner directly, manipulate state files, or acquire another worker's request.

After the candidate is committed and the user has authorized the required Git operation, call `enqueue_validation` with the exact `candidate_sha`, ordered `test_modes` (`EditMode`, `PlayMode`), and `manual_checks`. Poll status for the terminal result. If no candidate commit can be created within current authority, stop before enqueueing.

For an explicitly approved additional worktree using Git LFS, verify materialization before Unity or enqueueing: inspect tracked paths, run bounded `git lfs pull` and `git lfs checkout`, check `git lfs status`, and reject files that still begin with the Git LFS pointer header. A clean `git status` is not proof of materialization.

## Integrator Contract

Only one integrator may exist. Authorized Git integration happens outside this protocol. Before acquiring validation, prove that the canonical Unity checkout:

- is at the candidate commit;
- has no tracked or untracked changes;
- is not compiling, updating assets, changing Play Mode, or validating another request.

Clear the Unity Console immediately before acquiring. Call `acquire_validation` with the queued `request_id` or omit it for FIFO, and pass the exact current HEAD as `candidate_sha`. The package requests compilation and runs the requested Unity test modes serially.

If the canonical checkout is dirty, stop. Do not open a second Editor as a workaround. An owner handoff requires explicit user approval and no active validation. The old integrator then calls `relinquish_editor` with `confirm=true`, completes the approved transition, and waits for the new canonical Editor to acquire ownership before validation continues.

When validation reaches `awaiting_manual` or `ready_to_finish`, read the final Console through the available Unity bridge and perform the listed manual checks. Call `finish_validation` with the lease-owning `agent_id`, `request_id`, `passed`, and concise evidence.

## Cleanup

- Call `cancel_validation` when abandoning a queued or active request.
- Call `release_claims` when paths are no longer being edited.
- Call `finish_work` after all owned requests are terminal. Pending requests intentionally prevent cleanup.
- When the last worker and integrator finish, the empty coordination file is deleted automatically.

Read [references/mcp-protocol.md](references/mcp-protocol.md) before constructing an unfamiliar action, adapting another MCP, or recovering from a protocol error.
