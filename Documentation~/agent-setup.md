# Agent Setup

[English](agent-setup.md) | [한국어](agent-setup.ko.md)

The package needs both an agent operating rule and a callable Unity transport adapter. Documentation alone does not register an MCP tool.

## Codex

Copy or link `Skills~/unity-agent-coordination` into a Codex skill location, then mention the skill from the project's `AGENTS.md`. The included skill defines when coordination applies, the canonical Editor policy, validation lifecycle, bounded waits, and cleanup.

## Claude Code and Other Agents

Add a short routing rule to the project's agent instructions and link this package documentation. For example:

```markdown
For work that uses or can trigger the shared Unity Editor, read the Unity Agent
Coordination package instructions before the first Editor-affecting action. Call
the coordination adapter with action=status, honor path claims and the active
validation lease, and call finish_work when done. Do not activate coordination
for Git-only or filesystem-only work. Never edit coordination state files.
```

Use the host's native guidance file, such as `AGENTS.md`, `CLAUDE.md`, or an equivalent project instruction file. The wording may change, but preserve the activation boundary and cleanup contract.

## Adapter Check

Before relying on the instructions, verify that the selected Unity MCP exposes a coordination adapter:

- Coplay should expose `manage_unity_agent_coordination` and the optional status resource after reconnecting.
- Another MCP should expose an equivalent tool that forwards the same action payload to `UnityAgentCoordinationCommandRouter`.
- If the MCP offers only generic Editor C# execution, create a small bridge that invokes the router and returns its serialized result.
- If no route exists, stop and add an adapter. Do not read or write `%LOCALAPPDATA%` state directly.

The command schema is documented in [Integration Protocol](mcp-protocol.md).
