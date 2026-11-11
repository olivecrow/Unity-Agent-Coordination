# Agent Setup

[English](agent-setup.md) | [한국어](agent-setup.ko.md)

## Project-local Codex skill

After package import and compilation, Editor initialization copies `Skills~/unity-agent-coordination` to the Unity project's `.agents/skills/unity-agent-coordination`. No user-global skill or automatic `AGENTS.md` edit is needed. The installer updates unchanged managed files and preserves unmanaged/user-modified skills with a warning. Normal Package Manager removal cleans up unchanged package-owned files. Removal while Unity is closed may leave the project-local copy; its instructions stop if the package is absent.

Remove a previous global copy only after confirming the project-local skill exists. Review and commit or ignore generated project-local files according to the project's normal workflow. This administrative choice does not affect ticket ownership.

After updating the package, verify that the generated project-local skill contains the active-turn cleanup rule. If user modifications prevented the installer from updating it, merge this rule into that copy while preserving those modifications.

## Existing Unity bridge

Use any bridge able to call `UnityAgentCoordinationCommandRouter.ExecuteJson(string)` inside the target Editor. An optional Coplay 10.1.2–10.x adapter exposes `manage_unity_agent_coordination`. The queue does not install or manage an MCP provider.

For other agents, link [the protocol](mcp-protocol.md) from the project's instructions: enqueue with a unique session ID, wait for your ticket, acquire, and use the Editor only within the current active conversation turn. In finally/cleanup, finish or safely stop operations you started, release an active ticket or cancel a waiting ticket, and verify removal before ending the turn or suspending work for the user's reply, confirmation, or approval. This applies on failure and even if the task will continue in the same conversation; keep the session ID if useful, but enqueue a fresh ticket when resuming. Reconcile uncertain cleanup with `status` and report unresolved cleanup without claiming release. Every participant must honor this protocol; other bridge commands are not intercepted, and the package cannot detect turn endings or user-input waits.

The 0.5 command schema contains only `action`, `session_id`, `ticket_id`, and optional `label`. Upgrade the package, bridge, and skill together after old sessions stop; previous work-registration and validation APIs are removed.
