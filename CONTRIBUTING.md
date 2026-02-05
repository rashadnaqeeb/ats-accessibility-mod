# Contributing to ATS Accessibility Mod

## Prerequisites

- .NET SDK 6.0+ (for `dotnet build`)
- Against the Storm (Steam version)
- BepInEx 5.x installed in your game folder
- PowerShell (for build script)

## Quick Start

```powershell
# Clone and build+deploy in one step
git clone https://github.com/rashadnaqeeb/ats-accessibility-mod.git
cd ats-accessibility-mod
powershell -ExecutionPolicy Bypass -File "build.ps1"
```

The build script compiles and deploys the DLL to your game's BepInEx plugins folder automatically.

For debug builds: `powershell -ExecutionPolicy Bypass -File "build.ps1" -Configuration Debug`

### Custom Game Path

If your game isn't in the default Steam location:
```bash
cp Directory.Build.props.template Directory.Build.props
```
Edit `Directory.Build.props` and set your installation path.

## Project Structure

```
ATSAccessibility/           # All source code
  Navigators/               # Building-specific panels (extend BuildingSectionNavigator)
  *Overlay.cs               # Popup navigation (extend MenuBase)
  *Reflection.cs            # Game API access (one per system)
game-source/                # Decompiled game code (read-only reference)
```

## Development Workflow

1. Read `CLAUDE.md` for architecture and patterns
2. Make changes
3. Build and test in-game
4. **Update `changes.md`** with a one-line summary (required for every commit)
5. Commit (include `changes.md` in the same commit)

## Debugging

Check the Player log for `[ATSAccessibility]` output:
```
C:\Users\<you>\AppData\LocalLow\Eremite Games\Against the Storm\Player.log
```

## Key Patterns (see CLAUDE.md for details)

- **MenuBase**: Base class for all navigable overlays. Handles keyboard input, search, multi-level navigation.
- **PopupRouter**: Routes game popup events to overlays. Register in `AccessibilityCore.Start()`.
- **IKeyHandler**: Priority chain for keyboard input. Return `true` to consume, `false` to pass through.
- **ReflectionHelper**: Null-safe accessors for game internals. Cache types, never cache service instances.

## Code Style

- **Tabs** for indentation (not spaces)
- **K&R braces** (opening brace on same line)
- Run `dotnet format` if you have whitespace issues
- See `.editorconfig` for full rules

## Announcement Style

Keep it concise. No item counts, no navigation hints, no redundant context.

```csharp
// Good
Speech.Say("Lumber Mill");
Speech.Say("Planks recipe, active");

// Avoid
Speech.Say("Lumber Mill, 1 of 5 buildings, press Enter to open");
```

## Pull Requests

1. Branch from `master`
2. Test in-game with a screen reader (or verify log output)
3. Include `changes.md` update
4. Submit PR with clear description

## Questions?

Open an issue on GitHub.
