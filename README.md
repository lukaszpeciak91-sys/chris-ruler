# Chris Ruler

A small portable Windows desktop utility for using an on-screen guide while working in applications such as spreadsheets.

Chris Ruler is **not a pixel-measuring ruler**. Its purpose is to create a thin, always-on-top frame that helps the user visually follow a row or area while still being able to interact with the application underneath.

## MVP

The first testable version should:

- run on Windows 10 and Windows 11,
- stay always on top,
- be movable,
- be resizable from all sides and corners,
- show only a thin visible frame (target: roughly 2–4 px),
- keep the center visually transparent,
- allow mouse interaction through the transparent center to the application underneath,
- remain simple and unobtrusive.

## Technical direction

- C# / .NET 8
- WPF for the desktop UI
- minimal Win32 interop where required for window/input behavior
- portable distribution preferred; no installer should be required for normal use

The implementation should stay intentionally small. Do not introduce additional frameworks, services, databases, telemetry, accounts, network access, or background processes unless a future requirement clearly needs them.

## Build and publish

The application requires the .NET 8 SDK and a Windows-compatible build environment.

```powershell
dotnet restore
dotnet build
dotnet build -c Release
dotnet publish src/ChrisRuler/ChrisRuler.csproj -p:PublishProfile=win-x64
```

The publish profile creates a self-contained, single-file Windows x64 executable in
`src/ChrisRuler/bin/publish/win-x64`. It can be copied and run without installing the
.NET runtime. Self-contained publishing makes the output substantially larger than a
framework-dependent build; the exact size should be recorded when the publish is run
in a .NET-capable environment.

## Project documents

- [`AGENTS.md`](AGENTS.md) — authoritative implementation rules for Codex/agents
- [`docs/PROJECT_PLAN.md`](docs/PROJECT_PLAN.md) — staged delivery plan and acceptance criteria
- [`docs/WORKFLOW.md`](docs/WORKFLOW.md) — development and PR pipeline

## Current phase

**Phase 3 — pre-test hardening complete; manual Windows validation pending.**

The ruler uses native hit testing for frame movement and resizing. Its center is removed
from the native window region so input can reach an unrelated application underneath.
The native interaction path has been statically reviewed for DPI changes, signed virtual-
screen coordinates, region ownership/failures, minimum geometry, and window lifecycle.
Cross-process click-through, resize, and multi-monitor behavior still require the first
manual Windows test before further features or polish are considered.
