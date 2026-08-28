# Unity Agent Coordination

[English](README.md) | [한국어](README.ko.md)

An Editor-only Unity Package Manager package for coordinating multiple AI agents around one shared Unity Editor. Workers claim non-overlapping repository paths, while one canonical Editor serializes compilation, Unity Test Framework runs, Play Mode work, serialized asset changes, and exact-commit validation.

The coordination engine does not depend on a particular MCP implementation. It exposes a public C#/JSON command API, and MCP integrations are adapters around that API. A Coplay Unity MCP adapter is included, but it is compiled only when a compatible Coplay package is installed.

## Compatibility

- Tested with Unity `6000.3.22f1`.
- The package manifest intentionally does not declare a minimum Unity version. Unity Package Manager may therefore allow installation on other versions, but those versions are not guaranteed until tested.
- Requires Unity Test Framework and Unity's registry `com.unity.nuget.newtonsoft-json` package. Both are declared in `package.json` and are resolved automatically by Unity Package Manager.
- Coplay Unity MCP is optional. The bundled adapter targets `com.coplaydev.unity-mcp` versions `10.1.2` through `10.x` and was tested with `10.1.2`.

## Installation

In Unity Package Manager, select **Install package from git URL** and enter:

```text
https://github.com/olivecrow/Unity-Agent-Coordination.git#v0.2.0
```

For an embedded copy, place the package at `Packages/com.olivecrow.unity-agent-coordination` and add this dependency to the consuming project's `Packages/manifest.json`:

```json
"com.olivecrow.unity-agent-coordination": "file:com.olivecrow.unity-agent-coordination"
```

## MCP and Agent Integration

- **Coplay Unity MCP:** install a compatible `com.coplaydev.unity-mcp` package. The optional assembly registers `manage_unity_agent_coordination` and `get_unity_agent_coordination` automatically when the transport connects.
- **Other Unity MCP implementations:** add a thin Editor adapter that forwards a JSON object to `UnityAgentCoordinationCommandRouter.Execute(JObject)` or `ExecuteJson(string)` and returns the result unchanged.
- **Codex:** install or reference `Skills~/unity-agent-coordination/SKILL.md`. When an Editor-affecting task activates the skill, it keeps any existing package source and version or adds the tagged `v0.2.0` Git UPM dependency when the package is absent.
- **Claude Code and other agents:** link the package instructions from the project's agent guidance file and expose the command router through the selected Unity MCP adapter.

MCP itself does not define a universal mechanism for discovering arbitrary Unity package tools. An adapter is required unless the selected MCP already provides a generic way to invoke Editor C# APIs. Agents must never emulate the adapter by editing the package's state files.

See [Agent Setup](Documentation~/agent-setup.md) and the [Integration Protocol](Documentation~/mcp-protocol.md) for the exact contract.

## Package Assemblies

- `Olivecrow.UnityAgentCoordination.Core`: repository identity, path claims, heartbeats, validation queue, atomic state, and owner lease data.
- `Olivecrow.UnityAgentCoordination.Editor`: canonical Editor ownership, Git checkout checks, compilation, tests, the Editor status window, and the public transport-neutral command router.
- `Olivecrow.UnityAgentCoordination.CoplayMcp`: optional Coplay adapter, excluded from compilation when Coplay Unity MCP is absent or outside the supported range.
- Editor tests: isolated Git fixtures for repository identity, conflicting claims, serial validation, status formatting, and stale owner recovery.

Open `Window > AI Agent Coordination` to inspect current work, claims, owner state, and the validation queue.

## Development and Validation

The package includes Editor tests under `Tests`. After changing it, validate JSON and assembly definitions, Unity compilation, EditMode tests, and the optional adapter both with and without Coplay installed. The current release was tested against Unity `6000.3.22f1`; no broader Unity-version claim is made.

## License

This project is licensed under the MIT License. See [LICENSE.md](LICENSE.md).
