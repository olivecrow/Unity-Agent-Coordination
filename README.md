# Unity Agent Coordination

[English](README.md) | [한국어](README.ko.md)

A FIFO ticket queue for AI access to one Unity Editor: draw a ticket, wait for your turn, use the Editor, and return the ticket. Each Unity project has its own queue.

## Requirements and installation

The Core and Editor assemblies use only Unity and built-in .NET APIs. There are no required UPM dependencies, external executables, version-control metadata, or separate work-registration steps. Git, Unity Version Control, and projects without version control use the same ticket flow.

Install a package `.tgz` through Unity Package Manager, or copy this folder to `Packages/com.olivecrow.unity-agent-coordination` and declare:

```json
"com.olivecrow.unity-agent-coordination": "file:com.olivecrow.unity-agent-coordination"
```

After import and compilation, the package registers its Codex skill in that project's `.agents/skills/unity-agent-coordination`. It preserves user changes, updates unchanged package-owned files, and cleans them up during normal Package Manager removal. See [Agent Setup](Documentation~/agent-setup.md).

## Use

**Tickets are scoped to one active conversation turn.** Return them as soon as the current Editor work finishes, and always before ending the turn with a final response or suspending work to wait for the user's reply, confirmation, or approval, even if the task will continue in the same conversation. Progress messages and tool waits while continuing the current work do not end the turn. The `session_id` may persist across turns; a ticket must not.

1. Choose a unique `session_id` for this AI task and call `enqueue` with `program`, `model`, `reasoning_effort`, and `session_name`. Supply verified values or `Unknown` if unavailable; they are self-reported and optional for older clients. It returns a `ticket_id` and an increasing ticket number. Retrying `enqueue` for that session returns its outstanding ticket; this does not authorize keeping it across turns.
2. Poll `status`. Wait until `queue.next_ticket_id` matches your ticket.
3. Call `acquire` with your session and ticket. Use the Editor only after success; an active ticket blocks every other ticket.
4. In cleanup/finally, finish or safely stop Editor operations you started, then `release` your active ticket or `cancel` your waiting ticket, including on failure and before ending the turn or waiting for user input. The next waiting session may then acquire its ticket.
5. Verify cleanup success and that your ticket is absent from the returned `queue.tickets`. If the response is missing, failed, or ambiguous, use `status` to reconcile before retrying cleanup. Report unresolved cleanup with your session/ticket IDs and error; do not claim release without confirmation.
6. When resuming Editor work, enqueue a fresh ticket and wait in FIFO order again, even in the same conversation. Clean up and verify removal of any ticket you left from an earlier turn before enqueueing again.

Use `cancel` to abandon your own waiting or active ticket. No session can acquire, release, or cancel another session's ticket. Tickets do not expire automatically: a disconnected owner must return/cancel its ticket, or the user can explicitly reset an abandoned queue in `Window > AI Agent Coordination` after confirming that the previous session has stopped.

The package coordinates cooperating agents; it cannot detect conversation turn endings or user-input waits, so agents must explicitly clean up their tickets. It does not intercept unrelated bridge commands or automatically run compilation, tests, builds, or version-control operations. Queue state survives compilation/domain reloads in `Library/UnityAgentCoordination/tickets.json` and is protected by a cross-process mutex and atomic file replacement. Returned tickets are removed; the stored counter remains small.

## AI transport

The public C#/JSON entry point is `UnityAgentCoordinationCommandRouter.ExecuteJson(string)`. Any existing Unity bridge can forward JSON to it. No particular MCP provider is required by the queue.

An optional Coplay adapter exposes `manage_unity_agent_coordination` and `get_unity_agent_coordination`. It compiles only when Coplay `10.1.2` through `10.x` is already installed. The adapter uses Coplay's existing JSON dependency; the Core and Editor assemblies do not depend on it.

See the five-command [protocol](Documentation~/mcp-protocol.md). Optional NUnit tests under `Tests` require a test environment; that framework is not installed as a runtime package dependency.

## Upgrading and validation

The 0.5 protocol replaces the old work-registration, claims, integrator, and commit-validation APIs. Stop existing sessions and return their tickets before upgrading. Previous coordination state is not imported into the new project-local queue.

Reference compilation is checked against Unity `6000.3.22f1`; standalone tests cover the entire JSON ticket lifecycle without version-control files or external executables. Reference compilation and isolated tests do not substitute for a live Unity import/callback or Unity Test Runner check. Other Unity versions have not been qualified.

## License

MIT. See [LICENSE.md](LICENSE.md).
