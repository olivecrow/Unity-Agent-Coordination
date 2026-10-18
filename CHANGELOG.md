# Changelog

[English](CHANGELOG.md) | [한국어](CHANGELOG.ko.md)

All notable changes to this package are documented here.

## 0.4.0 - 2026-10-18

- Automatically register the Codex skill in the consuming project's `.agents/skills/unity-agent-coordination` after Unity imports the package.
- Track managed file hashes, update only unmodified files, and remove only package-owned files during normal Package Manager removal. Preserve user modifications and unmanaged skills with a warning.
- Scope the skill to the consuming project and its installed package; remove user-global skill installation and automatic package bootstrap.
- Add filesystem regression tests covering installation, repeated registration, updates, removal, and user changes.
- Document reference-assembly compilation and isolated tests separately from live Editor callback and Unity Test Runner validation.

## 0.3.0 - 2026-08-29

- Added a commit-optional Editor access queue for imports, compilation, Console inspection, tests, Play Mode, builds, serialized edits, and custom operations.
- Added explicit `session_id` support so one logical agent and one HEAD can own multiple independent work sessions; omitted session IDs remain backward-compatible with `agent_id`.
- Added FIFO Editor access leases, optional request idempotency keys, heartbeat expiry, cancellation, and mutual exclusion with exact-SHA validation.
- Required path-specific Editor access requests to stay within the requesting session's existing claims while allowing scope-free operations such as Console inspection.
- Migrated persisted coordination state to schema version 2 and exposed Editor access sessions and requests in the status window and transport-neutral API.
- Added explicit NUnit and Newtonsoft JSON precompiled references to the Editor test assembly so public router signatures compile in consuming tests.
- Confirmed `com.unity.nuget.newtonsoft-json` as a required UPM dependency that is resolved automatically with the package.
- Updated the bundled Codex skill to reuse existing package installations and bootstrap the tagged Git UPM dependency only when the package is absent.
- Documented that package installation does not install or replace the project's Unity MCP provider.

## 0.2.0 - 2026-08-28

- Renamed the package and assemblies under the `Olivecrow.UnityAgentCoordination` namespace.
- Added a public transport-neutral C#/JSON command router.
- Removed Coplay Unity MCP from mandatory package dependencies.
- Moved Coplay registration into an optional version-defined adapter supporting HTTP and stdio transports.
- Changed the package UI and default documentation to English and added separate Korean documentation.
- Removed the package manifest's minimum Unity version and documented Unity `6000.3.22f1` as the tested version instead.
- Changed the default state root to `%LOCALAPPDATA%/Olivecrow/UnityAgentCoordination`.

## 0.1.1

- Added structured work kind, phase, summary, start time, and heartbeat time.
- Improved the Editor window with live work and validation-queue views.
- Added stale-agent cleanup for claims and queued validation.
- Clarified that Unity projects use one existing canonical Editor and do not create extra worktrees by default.

## 0.1.0

- Added repository-wide path claims and agent heartbeats shared across Git worktrees.
- Added a serial validation queue for compilation and EditMode/PlayMode tests.
- Added exact-SHA validation, owner handoff, atomic cleanup, and stale owner recovery.
- Added the first MCP adapter and agent operating instructions.
