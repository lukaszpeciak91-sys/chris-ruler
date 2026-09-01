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

## Project documents

- [`AGENTS.md`](AGENTS.md) — authoritative implementation rules for Codex/agents
- [`docs/PROJECT_PLAN.md`](docs/PROJECT_PLAN.md) — staged delivery plan and acceptance criteria
- [`docs/WORKFLOW.md`](docs/WORKFLOW.md) — development and PR pipeline

## Current phase

**Phase 0 — documentation and project setup.**

After that we build the smallest functional foundation, perform a general pre-test cleanup/hardening pass, and then test the executable on a real Windows machine.
