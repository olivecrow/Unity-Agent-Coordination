# Agent Setup

[English](agent-setup.md) | [한국어](agent-setup.ko.md)

## Project-local Codex skill

After package import and compilation, Editor initialization copies `Skills~/unity-agent-coordination` to the Unity project's `.agents/skills/unity-agent-coordination`. No user-global skill or automatic `AGENTS.md` edit is needed. The installer updates unchanged managed files and preserves unmanaged/user-modified skills with a warning. Normal Package Manager removal cleans up unchanged package-owned files. Removal while Unity is closed may leave the project-local copy; its instructions stop if the package is absent.

Remove a previous global copy only after confirming the project-local skill exists. Review and commit or ignore generated project-local files according to the project's normal workflow. This administrative choice does not affect ticket ownership.

## Existing Unity bridge

Use any bridge able to call `UnityAgentCoordinationCommandRouter.ExecuteJson(string)` inside the target Editor. An optional Coplay 10.1.2–10.x adapter exposes `manage_unity_agent_coordination`. The queue does not install or manage an MCP provider.

For other agents, link [the protocol](mcp-protocol.md) from the project's instructions: enqueue with a unique session ID, wait for your ticket, acquire, use the Editor, and release in a finally/cleanup step. Every participant must honor this protocol; other bridge commands are not intercepted.

The 0.5 command schema contains only `action`, `session_id`, `ticket_id`, and optional `label`. Upgrade the package, bridge, and skill together after old sessions stop; previous work-registration and validation APIs are removed.
