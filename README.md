# Chris Ruler

Chris Ruler is a tiny, always-on-top Windows row-focus guide for spreadsheets and similar applications. Its graphite frame masks neighboring rows while the transparent center exposes one row and passes clicks, typing, and other mouse input to the application underneath.

## Run it

1. Download the **`ChrisRuler-win-x64-folder`** ZIP from the release or CI artifacts.
2. Extract the ZIP completely.
3. Keep every extracted file together and run `ChrisRuler.exe`.

No installer, administrator access, or separately installed .NET runtime is required. Chris Ruler supports 64-bit Windows 10 and Windows 11.

## Controls

- **Move:** when unlocked, drag any substantial graphite part of the frame.
- **Resize:** when unlocked, drag any outer edge or corner. Align the two thin blue calibration lines with the top and bottom of one spreadsheet row; they brighten while resizing.
- **Lock / Unlock:** select the padlock to protect the chosen size and position from accidental dragging or resizing. Row navigation and Close remain available while locked.
- **Up / Down:** select the arrow buttons to move exactly one calibrated row. **Alt+Up** and **Alt+Down** use the same movement.
- **Close:** select the × button.

The center is intentionally absent from the overlay's native input area. It stays transparent and click-through; calibration lines are visual-only.

Chris Ruler remembers its last position and size in `%LOCALAPPDATA%\ChrisRuler\window.json`. A missing or damaged file is ignored. If the saved guide is fully outside the current monitor layout, the guide starts at a sensible visible default instead. Lock state is not saved.

## Security note

Current builds are unsigned. Windows may show an unverified-publisher or reputation warning, and corporate controls such as AppLocker or WDAC may block the program. Chris Ruler does not bypass those protections. The conventional self-contained folder build is preferred because it passed the project's real-machine antivirus A/B test; the complete folder must remain together.

## Build and publish

The .NET 8 SDK and a Windows-compatible build environment are required.

```powershell
dotnet restore
dotnet build -c Release
dotnet publish src/ChrisRuler/ChrisRuler.csproj -p:PublishProfile=win-x64-folder
```

The preferred output is `src/ChrisRuler/bin/publish/win-x64-folder`. It is self-contained, untrimmed, and does not bundle or extract runtime files. CI uploads it as `ChrisRuler-win-x64-folder` and includes `ChrisRuler.exe.sha256.txt`.

The optional `win-x64` profile produces a larger self-contained single executable that extracts native runtime libraries at startup. It remains available only for packaging diagnostics; it is not the normal distribution path. Neither profile uses an installer, packer, obfuscator, custom loader, post-build rewriting, or ad-hoc signing.

## Project status and documents

Chris Ruler is in **Phase 6 — MVP polish and release candidate**. Core move, resize, and cross-process center click-through behavior plus the self-contained folder package have passed earlier real-machine testing. Persistence and the combined release-candidate behavior still require the Windows checks listed in the release PR.

- [`AGENTS.md`](AGENTS.md) — authoritative implementation rules
- [`docs/PROJECT_PLAN.md`](docs/PROJECT_PLAN.md) — delivery plan and acceptance criteria
- [`docs/WORKFLOW.md`](docs/WORKFLOW.md) — development and PR workflow
