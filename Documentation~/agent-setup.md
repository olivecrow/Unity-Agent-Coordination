# Agent Setup

[English](agent-setup.md) | [한국어](agent-setup.ko.md)

The package needs both an agent operating rule and a callable Unity transport adapter. Documentation alone does not register an MCP tool.

## Codex

After Unity imports and compiles this package, its Editor initialization copies the bundled skill and supporting resources into `<UnityProject>/.agents/skills/unity-agent-coordination`. Codex discovers this repository-scoped folder automatically when working in that project. No user-global installation or automatic `AGENTS.md` edit is needed. See [Codex skill locations](https://developers.openai.com/codex/skills#where-codex-loads-local-skills).

The `.package-owned` record tracks the installed file hashes. Subsequent Editor initialization updates unmodified files without rewriting identical content. Existing unmanaged skills and user-modified files are left intact with a Console warning. Normal removal through Unity Package Manager deletes only unchanged package-owned files and retains unrelated files. If the package is removed while Unity is closed, or cleanup is blocked by user modifications, remove the leftover project-local folder manually when appropriate; its instructions stop if the package is absent.

To migrate a previous global installation, first confirm the project-local copy exists, then remove or archive the `unity-agent-coordination` folder from the user skill directory. Leave other global skills intact. Generated project-local files remain visible to Git: review and commit or ignore them before requesting clean exact-SHA validation.

Automatic registration is available starting with `v0.4.0`. Tags `v0.2.0` and `v0.3.0` predate it: for those versions, copy `Skills~/unity-agent-coordination` into the project's `.agents/skills` manually. If Codex does not show a newly registered skill, restart it.

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

## Project Package Scope

The project-local skill uses the package already installed in that Unity project. It never adds or upgrades dependencies, restores a removed package, or writes a user-global skill. It stops if the package is absent. Keep the existing package source and version unchanged when applying coordination.

Installing this package does not install or select an MCP provider. The agent uses the project's existing Unity MCP adapter. Adding or replacing an MCP implementation remains a separate user-authorized change.

## Adapter Check

Before relying on the instructions, verify that the selected Unity MCP exposes a coordination adapter:

- Coplay should expose `manage_unity_agent_coordination` and the optional status resource after reconnecting.
- Another MCP should expose an equivalent tool that forwards the same action payload to `UnityAgentCoordinationCommandRouter`.
- If the MCP offers only generic Editor C# execution, create a small bridge that invokes the router and returns its serialized result.
- If no route exists, stop and add an adapter. Do not read or write `%LOCALAPPDATA%` state directly.

The command schema is documented in [Integration Protocol](mcp-protocol.md).

Adapters should expose the full session and Editor access fields. Older installed package versions may not recognize `session_id` or the `*_editor_access` actions; agents must feature-detect them from the action schema or the status state's reported schema version (`State.SchemaVersion` in the bundled router response) and must not silently treat the validation queue as a general pre-commit access queue.
