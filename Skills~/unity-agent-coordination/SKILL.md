---
name: unity-agent-coordination
description: Take a FIFO ticket before using or triggering this project's shared Unity Editor, acquire it in order, and return it within the current conversation turn before yielding to the user. Applies to live Editor inspection, import, compilation, tests, Play Mode, builds, serialized edits, and file writes that can trigger import in the open project. Project-local only; no prior work registration or version-control metadata is required. Do not activate for read-only filesystem work, documentation, planning, or edits in a checkout not open in Unity.
---

# Unity Agent Coordination

Coordinate Editor use with one ticket queue in the current Unity project. This skill is installed in `.agents/skills/unity-agent-coordination` by the package. Use the existing `com.olivecrow.unity-agent-coordination` package and its bridge; if absent, stop rather than installing or restoring it. Do not install a global skill or choose/manage an MCP provider.

## When to draw a ticket

Draw a ticket before the first operation that uses or can affect the live Editor: inspection, import/refresh, compilation, tests, Play Mode, builds, scene/prefab edits, or scripts/assets written in the project currently open in Unity. Read-only file inspection, documentation, planning, and file edits outside the live project need no ticket. Do not launch another Editor as a queue workaround.

## Ticket lifetime: one active conversation turn

Hold a ticket only within the current active conversation turn. Return it as soon as the current Editor work finishes, and always clean up before ending the turn with a final response or suspending work to wait for the user's reply, confirmation, or approval. This applies even when the overall task is unfinished and will continue in the same conversation. Progress messages and tool waits while continuing the current work do not end the turn.

The `session_id` may persist across turns; a ticket must not. On a later turn or Editor batch, enqueue a new ticket and wait in FIFO order again. If your own ticket was left over from an earlier turn, clean it up and verify removal before enqueueing again; do not reuse its ownership or queue position.

## Ticket lifecycle

1. Select the existing bridge instance for the intended Unity project. Create a unique `session_id` for this task and retain it throughout the ticket lifecycle.
2. Call `enqueue` with `session_id` and an optional short `label`. Save `ticket.ticket_id`. No separate registration, role, revision value, file claim, or external executable is needed. Retrying enqueue for that session returns its outstanding ticket.
3. Poll `status` with backoff, doing useful independent work while waiting. Do not use or trigger the Editor until `queue.next_ticket_id` equals your ticket and `acquire` succeeds with your session and ticket IDs. `not_your_turn` and `editor_busy` preserve your place while you continue working in this turn. If you end the turn or suspend work for user input, cancel your waiting ticket. Never cancel another session's ticket or reset its queue.
4. While using the Editor, check that `queue.active_ticket_id` is still your ticket immediately before each batch of Editor operations or import-triggering writes. Use the existing bridge for the actual work. The package does not run those operations for you.
5. In a finally/cleanup step, finish or safely stop Editor operations you started, then `release` your active ticket or `cancel` your waiting ticket. Do this on success, failure, abandonment, and before ending the turn or suspending work for user input. Do not acquire a waiting ticket just to return it.
6. Check cleanup success and confirm your ticket is absent from the returned `queue.tickets`. If the response is missing, failed, or ambiguous, call `status` to reconcile before retrying cleanup of your own ticket. If cleanup remains impossible or unconfirmed, stop Editor use and report the `session_id`, `ticket_id`, and cleanup error to the user; do not claim the ticket was returned or the queue is free.

The package cannot detect conversation turn endings or user-input waits; the agent must perform this cleanup explicitly. Tickets survive compilation/domain reload and do not expire automatically. If the owner disappears, report the blocked queue; the user can reset it only after confirming that the old owner has stopped. Do not use timeouts as permission to bypass ownership or edit queue files.

The protocol has only `status`, `enqueue`, `acquire`, `release`, and `cancel`. Call the installed adapter (`manage_unity_agent_coordination` for Coplay) or a bridge to `UnityAgentCoordinationCommandRouter.ExecuteJson(string)`. Queue state is internal to `Library/UnityAgentCoordination`; never read or write it directly.

Read [references/mcp-protocol.md](references/mcp-protocol.md) when constructing an unfamiliar command or adapting a bridge.
