# Chris Ruler

A small portable Windows desktop row-focus guide for applications such as spreadsheets.

Chris Ruler is **not a measurement tool**. Its high-opacity graphite frame masks neighboring spreadsheet rows, while its transparent center shows the active row and passes input through to the application underneath.

## MVP

The first testable version should:

- run on Windows 10 and Windows 11,
- stay always on top,
- be movable,
- be resizable from all sides and corners,
- show an intentional graphite row mask with a 30 DIP top bar, 22 DIP bottom bar, and 9 DIP sides,
- keep the center visually transparent,
- allow mouse interaction through the transparent center to the application underneath,
- provide compact Lock, Up, Down, Minimize, and Close controls without blocking the center,
- provide a compact scratchpad text field and a button that copies its contents to the clipboard,
- provide a compact color selector with several restrained accent themes,
- provide broad integrated drag areas across the frame,
- show two thin visual-only accent-colored row-alignment lines,
- avoid ticks, scales, or other measurement styling,
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
dotnet publish src/ChrisRuler/ChrisRuler.csproj -p:PublishProfile=win-x64-folder
```

The currently preferred, real-machine-tested path is the untrimmed, self-contained folder
publish in `src/ChrisRuler/bin/publish/win-x64-folder`. This is the variant that passed the antivirus A/B test. Copy and
distribute the complete folder; it does not require an installed .NET runtime. The alternative
`win-x64` profile creates a self-contained single file that extracts native runtime
libraries at startup. Neither path uses a packer, obfuscator, custom loader, post-build
executable rewriting, or ad-hoc signing.

### CI build for testers

GitHub Actions verifies the Release build on Windows and publishes the
`ChrisRuler-win-x64-folder` artifact. The artifact contains the portable `ChrisRuler.exe` and a
SHA-256 text file; CI also prints the hash in its log. A clearly separated `ChrisRuler-win-x64-single-file-diagnostic` artifact retains the
single-file build for packaging diagnosis only.

Both builds are currently unsigned. Windows may show an unverified-publisher or
reputation warning, and corporate policy may still block them; the application does not
attempt to suppress or bypass those controls. Trusted Authenticode signing is the proper
long-term way to establish publisher identity and reputation if distribution broadens,
but it cannot guarantee zero antivirus detections. A self-signed certificate should not
be presented as a SmartScreen or executable-reputation solution.

### Antivirus packaging result

During real-machine A/B testing, Avast Premium Security quarantined the single-file build
as `Win64:Malware-gen`, while the conventional self-contained folder build ran without a
detection. The folder artifact is therefore the preferred tested distribution path. This
result does not guarantee acceptance by other antivirus or corporate security policies.

If a build is reported as malicious, a developer should verify its SHA-256 and submit
the suspected false positive through that antivirus vendor's official sample/false-
positive process. Uploading a binary to a vendor or third-party analysis service must be
an explicit human decision. Users should not be told to disable protection or create
broad exclusions. Do not disable antivirus protection to run either variant.

## Project documents

- [`AGENTS.md`](AGENTS.md) — authoritative implementation rules for Codex/agents
- [`docs/PROJECT_PLAN.md`](docs/PROJECT_PLAN.md) — staged delivery plan and acceptance criteria
- [`docs/WORKFLOW.md`](docs/WORKFLOW.md) — development and PR pipeline

## Current phase

**Phase 6 — MVP polish and release candidate; combined behavior requires manual Windows validation.**

The guide uses an approximately 88%-opaque graphite frame with restrained blue accents.
Its 30 DIP top, 22 DIP bottom, and 9 DIP side bars intentionally mask neighboring rows.
The center is removed from the native window region so input can reach an unrelated
application underneath. The bars are integrated drag surfaces, while a narrow outer band
and the corners retain resizing. The 120 × 66 DIP minimum keeps a usable transparent
active-row center between the masks. The top-right Close button exits normally. The adjacent Minimize button uses normal Windows
taskbar behavior and restores to the same bounds. The Up and Down buttons move the guide by exactly the transparent-center height and
stop at the virtual desktop boundaries. The navigation step therefore matches the row
height selected by resizing the guide. Alt+Up and Alt+Down invoke the same row navigation.
The application architecture supports multiple rulers in one process. These global shortcuts are
registered once for the process and routed to the last ruler the user interacted with; that ruler
remains the target after focus returns to the underlying application.
Locking the guide protects that selected row height by disabling mouse dragging and resizing;
button and keyboard row navigation, the scratchpad and its Copy button, Minimize, Close, and center click-through remain available until
the guide is unlocked. A compact selector to the left of the scratchpad offers Blue, Green, Amber, Red, and Purple
accents while retaining the graphite frame; the choice applies only to that running ruler window and is not persisted.
The selector remains available while the guide is locked. Two static, approximately 1 DIP accent-colored lines at the inner mask edges are a visual alignment
aid only. The UI contains no scale or tick marks.

Chris Ruler saves its last valid position and size to `%LOCALAPPDATA%\ChrisRuler\window.json`.
Invalid, inaccessible, or fully off-screen settings are ignored, and Lock state is not saved.

The baseline cross-process click-through and resize behavior has passed real-machine
testing. This revised frame geometry and visual treatment still require a focused manual
Windows regression test, including DPI and multi-monitor coverage.
