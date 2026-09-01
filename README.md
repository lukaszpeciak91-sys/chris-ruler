# Chris Ruler

A small portable Windows desktop utility for using an on-screen guide while working in applications such as spreadsheets.

Chris Ruler is **not a pixel-measuring ruler**. Its purpose is to create a translucent, always-on-top ruler-style frame that helps the user visually follow a row or area while still being able to interact with the application underneath.

## MVP

The first testable version should:

- run on Windows 10 and Windows 11,
- stay always on top,
- be movable,
- be resizable from all sides and corners,
- show a practical translucent blue frame with a 26 DIP top ruler, 14 DIP bottom ruler, and 14 DIP sides,
- keep the center visually transparent,
- allow mouse interaction through the transparent center to the application underneath,
- provide compact Close and Next Row controls without blocking the center,
- provide a broad integrated drag area across the top ruler bar,
- show clear unitless guide ticks, with a longer orientation mark every fifth tick,
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

The main publish profile creates an uncompressed, untrimmed, self-contained single-file
Windows x64 executable in `src/ChrisRuler/bin/publish/win-x64`. It can be copied and run
without installing the .NET runtime. Native runtime libraries are bundled and extracted
by the standard .NET app host when the application starts. No packer, obfuscator, custom
loader, post-build executable rewriting, or ad-hoc signing is used. Self-contained
publishing makes the output substantially larger than a framework-dependent build.

### CI build for testers

GitHub Actions verifies the Release build on Windows and publishes the
`ChrisRuler-win-x64` artifact. The artifact contains the portable `ChrisRuler.exe` and a
SHA-256 text file; CI also prints the hash in its log. A second, clearly separated
`ChrisRuler-win-x64-folder-diagnostic` artifact is a conventional self-contained folder
publish with no single-file bundle or native extraction. It is intended only to compare
antivirus results and must be kept with all of its files.

Both builds are currently unsigned. Windows may show an unverified-publisher or
reputation warning, and corporate policy may still block them; the application does not
attempt to suppress or bypass those controls. Trusted Authenticode signing is the proper
long-term way to establish publisher identity and reputation if distribution broadens,
but it cannot guarantee zero antivirus detections. A self-signed certificate should not
be presented as a SmartScreen or executable-reputation solution.

### Antivirus packaging diagnostic

During real-machine testing, Avast Premium Security has repeatedly quarantined the
GitHub Actions build as the heuristic detection `Win64:Malware-gen`. This detection has
not been proven resolved. The two CI artifacts are temporarily produced for controlled
A/B testing: `ChrisRuler-win-x64` preserves the current single-file/self-extracting
baseline, while `ChrisRuler-win-x64-folder-diagnostic` contains an untrimmed,
uncompressed, self-contained folder publish. The comparison is intended to determine
whether single-file packaging contributes to the detection, rather than to bypass or
weaken antivirus controls.

If a build is reported as malicious, a developer should verify its SHA-256 and submit
the suspected false positive through that antivirus vendor's official sample/false-
positive process. Uploading a binary to a vendor or third-party analysis service must be
an explicit human decision. Users should not be told to disable protection or create
broad exclusions. Compare the main and folder diagnostic results before deciding whether
single-file packaging should remain the default. Manual Windows overlay testing and
external antivirus rescanning are still required; a successful build alone does not
establish that a detection has been resolved. Do not disable antivirus protection to run
either diagnostic variant.

## Project documents

- [`AGENTS.md`](AGENTS.md) — authoritative implementation rules for Codex/agents
- [`docs/PROJECT_PLAN.md`](docs/PROJECT_PLAN.md) — staged delivery plan and acceptance criteria
- [`docs/WORKFLOW.md`](docs/WORKFLOW.md) — development and PR pipeline

## Current phase

**Phase 5 — first real-machine UX corrections in progress; revised design requires manual Windows validation.**

The ruler uses a thicker, 70%-opaque blue frame with native hit testing for frame movement and resizing. Its center is removed
from the native window region so input can reach an unrelated application underneath. The top bar is an integrated drag surface, while a narrow outer band and the corners retain resizing.
The 120 × 54 DIP minimum window size keeps the controls separated and the bars fully
visible while allowing a 92 × 14 DIP transparent center for short spreadsheet rows.
The top-right Close button exits normally, while the adjacent Next Row button moves the
ruler down by its current height and stops at the bottom of the virtual desktop. Subtle
horizontal ticks alternate regular marks with a longer mark every fifth interval; they are visual guides only and do not represent a measurement scale.
The native interaction path has been statically reviewed for DPI changes, signed virtual-
screen coordinates, region ownership/failures, minimum geometry, and window lifecycle.
Cross-process click-through, resize, and multi-monitor behavior still require the first
manual Windows test before further features or polish are considered.
