# Changelog

[English](CHANGELOG.md) | [한국어](CHANGELOG.ko.md)

All notable changes to this package are documented here.

## Unreleased

- Added explicit NUnit and Newtonsoft JSON precompiled references to the Editor test assembly so public router signatures compile in consuming tests.
- Confirmed `com.unity.nuget.newtonsoft-json` as a required UPM dependency that is resolved automatically with the package.
- Updated the bundled Codex skill to reuse existing package installations and bootstrap the tagged Git UPM dependency only when the package is absent.
- Documented that package bootstrap does not install or replace the project's Unity MCP provider.

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
