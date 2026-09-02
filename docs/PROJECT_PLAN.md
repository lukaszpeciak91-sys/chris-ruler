# Project Plan

This plan defines the delivery order for the first usable Chris Ruler build.

The rule is simple: **preserve the real-machine-tested interaction model while refining Chris Ruler into a focused row guide.**

## Phase 0 — Documentation and repository baseline

Goal: give every Codex/agent session enough durable context to work without repeatedly re-explaining the project.

Deliverables:

- `README.md`
- `AGENTS.md`
- `docs/PROJECT_PLAN.md`
- `docs/WORKFLOW.md`

Exit criteria:

- product purpose is explicit,
- MVP scope is explicit,
- technical direction is explicit,
- risks and acceptance criteria are documented,
- development pipeline is documented.

## Phase 1 — Foundation

Goal: create the smallest runnable Windows project with the correct technical base.

Expected scope:

- .NET 8 WPF solution/project,
- one main overlay window,
- borderless transparent window setup,
- always-on-top behavior,
- a visible frame that can mask neighboring rows,
- sane app metadata,
- basic project structure,
- debug and release builds,
- initial portable publish configuration/documentation.

Do not add settings UI, tray UI, persistence, keyboard shortcuts, themes, telemetry, updater, or other optional features in this phase.

Exit criteria:

- solution builds cleanly,
- application launches,
- overlay renders as a substantial row-focus frame,
- no installer is required for the intended published build.

## Phase 2 — Core interaction

Goal: make the overlay behave like the requested tool.

Expected scope:

- move the frame reliably,
- resize from all four edges and all four corners,
- keep the center transparent,
- make the center click-through,
- keep frame interaction available while center input passes through,
- preserve always-on-top behavior,
- handle minimum practical dimensions.

Important validation targets:

- Excel/spreadsheet cells remain clickable through the center,
- dragging/resizing the ruler does not randomly activate content underneath,
- interaction remains predictable at different Windows DPI/scaling values.

Exit criteria:

- all core behaviors are implemented,
- automated/build verification passes,
- code is ready for a hardening review before first real-machine test.

## Phase 3 — Pre-test cleanup and hardening

Goal: remove obvious defects before spending time on manual testing.

Review checklist:

- native hit testing and Win32 interop correctness,
- DPI awareness,
- multi-monitor coordinates, including negative virtual-screen coordinates,
- minimum size and edge/corner resize behavior,
- startup/shutdown lifecycle,
- exceptions and failure paths,
- accidental focus/input problems,
- dependency count,
- Release warnings,
- portable publish output,
- no admin requirement,
- no persistent system modification,
- no unnecessary background work.

Exit criteria:

- clean Release build,
- publish succeeds,
- no known blocker remains for manual testing,
- PR notes include a compact manual test checklist.

## Phase 4 — First real-machine test

Goal: validate actual Windows behavior rather than relying only on code review/builds.

Test at minimum:

1. launch the portable build,
2. confirm frame visibility,
3. move it,
4. resize every edge and corner,
5. click and type in a spreadsheet through the center,
6. verify frame stays above the spreadsheet,
7. try at least two window sizes,
8. check current display scaling,
9. if available, move it between displays / scaling environments,
10. close and relaunch it.

Record every issue as an observable symptom, reproduction steps, and expected behavior.

## Phase 5 — Fix test findings (current)

Goal: fix only issues revealed by the real-machine test or obvious MVP blockers found during review.

The proven baseline can be moved and resized around a spreadsheet, preserves cross-process input through its transparent active-row center, and runs successfully from the self-contained folder publish. Current UX work removes measurement styling and uses an intentional 30 DIP top, 22 DIP bottom, and 9 DIP side mask to obscure neighboring rows.

Use focused branches/PRs. Avoid bundling unrelated polish into bug fixes.

Exit criteria:

- blocking test findings are resolved,
- regression checks pass,
- another short real-machine retest confirms the fixes.

## Phase 6 — MVP polish and release candidate

Only after the core interaction is proven stable, consider small usability improvements such as:

- border color,
- border thickness,
- simple orientation/size presets,
- position reset,
- optional close/control affordance,
- lightweight persistence of last position/size.

These are **not requirements yet**. Add them only when they solve a real usability need without compromising the tiny portable nature of the app.

Release-candidate exit criteria:

- tested portable build,
- documented build/publish instructions,
- no known critical interaction bugs,
- README accurately describes current behavior and limitations.

## Main risks

### 1. Partial click-through behavior

WPF transparency alone does not guarantee the required input behavior. The center must pass input to the underlying app while the frame remains interactive. Native hit testing is likely the key technical area.

### 2. DPI and scaling

A frame that is 3 px at one scale can become visually or interactively wrong at another. Coordinate spaces and native hit testing must be DPI-aware.

### 3. Multi-monitor behavior

Virtual desktop coordinates may be negative and monitors may use different scaling. Avoid assumptions based on the primary monitor.

### 4. Corporate/work PC restrictions

A portable executable avoids installation, but an unsigned custom executable can still be blocked or warned about by Windows SmartScreen, Defender, AppLocker, WDAC, or company endpoint-security policy. The application must not try to bypass those controls.

### 5. Overengineering

This is a small utility. Complexity is itself a risk. Every dependency and subsystem must justify its existence.
