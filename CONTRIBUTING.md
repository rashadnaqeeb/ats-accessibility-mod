# Contributing to ATS Accessibility Mod

## Prerequisites

- .NET SDK (for `dotnet build`)
- Against the Storm (Steam version)
- BepInEx 5.x installed in your game folder
- A code editor (VS Code, Visual Studio, Rider)

## Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/rashadnaqeeb/ats-accessibility-mod.git
   cd ats-accessibility-mod
   ```

2. Configure your game path (if not using default Steam location):
   ```bash
   cp Directory.Build.props.template Directory.Build.props
   ```
   Edit `Directory.Build.props` and set your game installation path.

   Default path: `C:\Program Files (x86)\Steam\steamapps\common\Against the Storm`

3. Build:
   ```bash
   dotnet build ATSAccessibility/ATSAccessibility.csproj
   ```

## Deploy for Testing

Copy the built DLL to your game's BepInEx plugins folder:

```bash
cp ATSAccessibility/bin/Debug/net472/ATSAccessibility.dll "/path/to/Against the Storm/BepInEx/plugins/ATSAccessibility/"
```

The plugin folder should also contain `Tolk.dll` and `SAAPI64.dll` (screen reader bridge libraries).

## Debugging

Check the game's log file for `[ATSAccessibility]` output:
```
%APPDATA%\..\LocalLow\Eremite Games\Against the Storm\Player.log
```

All mod logging is prefixed with `[ATSAccessibility]`.

## Architecture

See [CLAUDE.md](CLAUDE.md) for detailed architecture documentation, including:

- Code organization and key files
- Design patterns (IKeyHandler, TwoLevelPanel, BuildingSectionNavigator, etc.)
- Reflection patterns for accessing game internals
- Announcement style guidelines

## Coding Guidelines

### Key Patterns

- **Reflection classes** (`*Reflection.cs`): Cache type metadata (Type, PropertyInfo, MethodInfo), never cache service instances (they're destroyed on scene change)
- **Overlays** (`*Overlay.cs`): Implement `IKeyHandler` for popup/panel navigation
- **Key handlers**: Return `true` to consume a key, `false` to pass it to the game

### Announcement Style

Keep announcements concise. Users are experienced screen reader users.

```csharp
// Good
Speech.Say("Lumber Mill");
Speech.Say("Planks recipe, active");

// Avoid
Speech.Say("Lumber Mill, 1 of 5 buildings, press Enter to open");
```

### Conventions

- Prefix all log messages with `[ATSAccessibility]`
- Use `NavigationUtils.WrapIndex()` for circular navigation
- Always null-check reflection results

## Pull Requests

1. Create a feature branch from `master`
2. Make your changes
3. Test in-game with a screen reader
4. Submit a PR with a clear description of what changed and why

## Questions?

Open an issue on GitHub if you have questions or run into problems.
