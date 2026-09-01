# AGENTS.md

This file is the authoritative working context for coding agents in this repository.

## Product intent

Chris Ruler is a tiny Windows desktop overlay used as a visual guide over applications such as spreadsheets.

It is **not** a measurement tool. The core UX is a thin always-on-top rectangular frame with a transparent/click-through center, so the user can keep working in the application underneath.

## MVP requirements

Preserve these unless the task explicitly changes them:

1. Windows 10 and Windows 11 desktop support.
2. C# / .NET 8 + WPF.
3. Always-on-top overlay.
4. Window can be moved.
5. Window can be resized from every edge and corner.
6. Visible frame should stay thin, approximately 2–4 px unless a later setting changes it.
7. The center must be visually transparent.
8. Mouse input through the transparent center must reach the application underneath.
9. Border/resize interaction must still work reliably.
10. Portable use is preferred; normal use should not require an installer.

## Engineering rules

- Keep the architecture small and boring.
- Prefer built-in .NET/WPF APIs.
- Use Win32 interop only where WPF cannot provide the required window/input behavior cleanly.
- Do not add third-party dependencies without a concrete need.
- Do not add telemetry, analytics, accounts, networking, auto-update, services, tray agents, or background daemons unless explicitly requested.
- Do not broaden the feature set during refactors.
- Avoid premature abstractions and elaborate patterns.
- Keep startup fast and runtime memory reasonable for a tiny utility.
- Handle DPI/scaling and multiple monitors deliberately; do not assume 100% scaling or a single display.
- Avoid requiring administrator privileges.
- The app must fail safely: no global hooks, system-wide input interception, registry dependence, or persistent system modifications for the MVP.

## Window/input behavior

This is the highest-risk part of the project.

The desired behavior is **not** a completely click-through window. The center should pass input through, while the thin frame must remain interactive for moving/resizing.

When implementing native hit testing:

- keep the hit-test logic localized and documented,
- preserve standard resize hit zones on edges/corners,
- return transparent/pass-through behavior only for the intended center region,
- account for DPI when calculating frame thickness and hit regions,
- verify behavior near monitor boundaries and with negative virtual-screen coordinates.

Do not solve this with a global mouse hook unless an explicit later requirement makes it unavoidable.

## Packaging

Target a portable executable suitable for copying to another Windows PC and running without an installer.

Use the simplest publish strategy that satisfies this goal. If a self-contained single-file build is used, document the resulting size and any trade-offs rather than adding a custom installer.

Do not sign, obfuscate, pack, or modify security settings as part of normal development.

## Workflow

Before changing code:

1. Read this file.
2. Read `docs/PROJECT_PLAN.md` and `docs/WORKFLOW.md`.
3. Inspect the current repository state; do not assume earlier tasks were merged.
4. Make only the changes required by the current task.

For every implementation task:

1. work on a dedicated branch,
2. keep commits focused,
3. build the solution,
4. run available automated checks,
5. perform relevant static/manual review,
6. update documentation only when behavior, architecture, packaging, or project status actually changed,
7. open a PR with a concise summary and explicit test status.

Never claim a real-device/manual Windows test was performed unless it actually was.

## Definition of done for code tasks

A task is done only when:

- requested behavior is implemented,
- the project builds cleanly,
- relevant automated checks pass,
- obvious warnings or dead code introduced by the change are resolved,
- no unrelated feature creep is present,
- PR notes clearly separate automated verification from manual verification still required.

## Current delivery sequence

Follow the phases in `docs/PROJECT_PLAN.md`.

Do not jump ahead to polish or optional features before the basic overlay has passed a real-machine test.
