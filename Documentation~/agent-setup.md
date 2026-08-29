# Agent Setup

[English](agent-setup.md) | [한국어](agent-setup.ko.md)

The package needs both an agent operating rule and a callable Unity transport adapter. Documentation alone does not register an MCP tool.

## Codex

Copy or link `Skills~/unity-agent-coordination` into a Codex skill location, then mention the skill from the project's `AGENTS.md`. The included skill defines when coordination applies, package bootstrap, session identity, commit-optional Editor access, exact-SHA validation, bounded waits, and cleanup.

## Claude Code and Other Agents

Add a short routing rule to the project's agent instructions and link this package documentation. For example:

```markdown
For work that uses or can trigger the shared Unity Editor, read the Unity Agent
Coordination package instructions before the first Editor-affecting action. Call
the coordination adapter with action=status, register a unique session, honor path
claims, and queue Editor access before stateful operations. Use exact-SHA validation
only for clean committed candidates and call finish_work when done. Do not activate
coordination for Git-only or filesystem-only work. Never edit coordination state files.
```

Use the host's native guidance file, such as `AGENTS.md`, `CLAUDE.md`, or an equivalent project instruction file. The wording may change, but preserve the activation boundary and cleanup contract.

## Package Bootstrap

The bundled Codex skill checks the package only after a task crosses the Editor-affecting activation boundary. If `com.olivecrow.unity-agent-coordination` is already installed or declared, the agent uses its existing source and version without replacing or upgrading it. If an embedded package folder already exists, the skill adds its local `file:` dependency. Otherwise it adds the tagged public Git dependency:

```json
"com.olivecrow.unity-agent-coordination": "https://github.com/olivecrow/Unity-Agent-Coordination.git#v0.2.0"
```

The agent preserves unrelated `Packages/manifest.json` entries and leaves `Packages/packages-lock.json` to Unity Package Manager. Package installation is a one-time bootstrap exception because the coordination API is not available beforehand. The agent still inspects running Unity processes before the dependency edit, never launches a second Editor, and waits for package resolution before calling `status` as its first coordination action.

Installing this package does not install or select an MCP provider. The agent uses the project's existing Unity MCP adapter. Adding or replacing an MCP implementation remains a separate user-authorized change.

## Adapter Check

Before relying on the instructions, verify that the selected Unity MCP exposes a coordination adapter:

- Coplay should expose `manage_unity_agent_coordination` and the optional status resource after reconnecting.
- Another MCP should expose an equivalent tool that forwards the same action payload to `UnityAgentCoordinationCommandRouter`.
- If the MCP offers only generic Editor C# execution, create a small bridge that invokes the router and returns its serialized result.
- If no route exists, stop and add an adapter. Do not read or write `%LOCALAPPDATA%` state directly.

The command schema is documented in [Integration Protocol](mcp-protocol.md).

Adapters should expose the full session and Editor access fields. Older installed package versions may not recognize `session_id` or the `*_editor_access` actions; agents must feature-detect them from the action schema or the status state's reported schema version (`State.SchemaVersion` in the bundled router response) and must not silently treat the validation queue as a general pre-commit access queue.
