# Editor Ticket Protocol

Each command runs in the target Unity Editor. The queue is scoped to its project folder. No prior registration, revision value, role, file scope, or third-party executable is required.

| Action | Required fields | Result |
| --- | --- | --- |
| `status` | none | `queue.tickets`, `queue.active_ticket_id`, `queue.next_ticket_id` |
| `enqueue` | `session_id` | `ticket` with `ticket_id`, `number`, and `state`; optional `label` |
| `acquire` | `session_id`, `ticket_id` | Own the Editor only after success |
| `release` | `session_id`, `ticket_id` | Return an active ticket and unblock the next one |
| `cancel` | `session_id`, `ticket_id` | Remove your own abandoned waiting/active ticket |

```json
{"action":"enqueue","session_id":"task-a","label":"Inspect scene"}
```

Save the returned `ticket.ticket_id`. Poll `status` with backoff. Only when `queue.next_ticket_id` matches your ticket, send:

```json
{"action":"acquire","session_id":"task-a","ticket_id":"<returned-id>"}
```

After success, perform the Editor operations through the project's existing bridge, then always return the ticket, including after a failed operation:

```json
{"action":"release","session_id":"task-a","ticket_id":"<returned-id>"}
```

Responses contain `success`, `message`, optional `error_code`, and on success a `queue` and optional `ticket`. `next_ticket_id` is omitted while an active ticket exists. `not_your_turn` and `editor_busy` mean keep waiting. `not_ticket_owner` forbids manipulating another session's ticket. Failed acquisition does not remove or reorder a ticket. The owning session can retry `acquire` on its active ticket safely.

One session can hold one outstanding ticket. Retrying `enqueue` returns that ticket; after release/cancel, enqueue issues a new one. Ticket numbers are allocated under the storage lock and determine FIFO order even when timestamps match. At most one ticket is active. There is no automatic expiration, heartbeat, compilation runner, test runner, or separate validation queue.

The Unity router refuses a new acquisition while compiling, importing, building, or transitioning Play Mode. Stable Play Mode is allowed. The queue survives domain reload; it does not automatically return an active ticket during compilation.

An adapter forwards JSON to `UnityAgentCoordinationCommandRouter.ExecuteJson(string)` and preserves its result. The Core protocol can also be called through `CoordinationProtocol.ExecuteJson(json, service, editorReady)`. Adapters must not edit queue files directly. Other bridge commands are not intercepted: agents must honor ownership before every batch of Editor operations.

If a session disappears, its ticket stays in place. The user may use **Reset Abandoned Queue** in the Editor window only after confirming the old owner has stopped. This recovery action is deliberately outside the AI protocol. Corrupt queue files fail closed instead of silently discarding ownership.
