# Editor Ticket Protocol

Each command runs in the target Unity Editor. The queue is scoped to its project folder. No prior registration, revision value, role, file scope, or third-party executable is required.

## Lifetime: one active conversation turn

A ticket may be held only within the current active conversation turn. Return it as soon as the current Editor work finishes, and always clean up before ending the turn with a final response or suspending work to wait for the user's reply, confirmation, or approval. An unfinished task or continuation in the same conversation does not justify retaining a ticket. Progress messages and tool waits while continuing the current work do not end the turn.

Before yielding, finish or safely stop Editor operations you started, then `release` your active ticket or `cancel` your waiting ticket, including on failure. Do not acquire a waiting ticket just to return it. Verify cleanup success and that your ticket is absent from the returned `queue.tickets`. If the response is missing, failed, or ambiguous, use `status` to reconcile before retrying cleanup of your own ticket. If cleanup remains impossible or unconfirmed, stop Editor use and report the `session_id`, `ticket_id`, and cleanup error; do not claim the ticket was returned or the queue is free.

The `session_id` may persist across turns; the ticket must not. When Editor work resumes, enqueue a fresh ticket and wait in FIFO order again. If your own ticket remains from an earlier turn, clean it up and verify removal before enqueueing again; do not reuse its ownership or queue position. The package cannot detect conversation turn endings or user-input waits, so the agent must perform this cleanup explicitly.

## Commands

| Action | Required fields | Result |
| --- | --- | --- |
| `status` | none | `queue.tickets`, `queue.active_ticket_id`, `queue.next_ticket_id` |
| `enqueue` | `session_id` | `ticket` with `ticket_id`, `number`, and `state`; optional `label` |
| `acquire` | `session_id`, `ticket_id` | Own the Editor only after success |
| `release` | `session_id`, `ticket_id` | Return an active ticket and unblock the next one |
| `cancel` | `session_id`, `ticket_id` | Remove your waiting ticket before yielding, or your own abandoned ticket |

```json
{"action":"enqueue","session_id":"task-a","label":"Inspect scene"}
```

Save the returned `ticket.ticket_id`. Poll `status` with backoff. Only when `queue.next_ticket_id` matches your ticket, send:

```json
{"action":"acquire","session_id":"task-a","ticket_id":"<returned-id>"}
```

After success, perform the current Editor work through the project's existing bridge, then return the active ticket in cleanup, including after a failed operation and before ending the turn or waiting for user input:

```json
{"action":"release","session_id":"task-a","ticket_id":"<returned-id>"}
```

If the turn ends or work pauses for user input while the ticket is still waiting, cancel it instead:

```json
{"action":"cancel","session_id":"task-a","ticket_id":"<returned-id>"}
```

Responses contain `success`, `message`, optional `error_code`, and on success a `queue` and optional `ticket`. `next_ticket_id` is omitted while an active ticket exists. `not_your_turn` and `editor_busy` mean keep waiting while continuing this turn; cancel a waiting ticket before yielding to the user. `not_ticket_owner` forbids manipulating another session's ticket. Failed acquisition does not remove or reorder a ticket. During the same turn, the owning session can retry `acquire` on its active ticket safely.

One session can hold one outstanding ticket. Retrying `enqueue` returns that ticket; this retry behavior is not permission to retain it across turns. After release/cancel, enqueue issues a new one. Ticket numbers are allocated under the storage lock and determine FIFO order even when timestamps match. At most one ticket is active. There is no automatic expiration, heartbeat, compilation runner, test runner, or separate validation queue.

The Unity router refuses a new acquisition while compiling, importing, building, or transitioning Play Mode. Stable Play Mode is allowed. The queue survives domain reload; it does not automatically return an active ticket during compilation.

An adapter forwards JSON to `UnityAgentCoordinationCommandRouter.ExecuteJson(string)` and preserves its result. The Core protocol can also be called through `CoordinationProtocol.ExecuteJson(json, service, editorReady)`. Adapters must not edit queue files directly. Other bridge commands are not intercepted: agents must honor ownership before every batch of Editor operations.

If a session disappears, its ticket stays in place. The user may use **Reset Abandoned Queue** in the Editor window only after confirming the old owner has stopped. This recovery action is deliberately outside the AI protocol. Corrupt queue files fail closed instead of silently discarding ownership.
