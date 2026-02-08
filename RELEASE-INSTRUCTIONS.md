# Release Packaging

## Package Structure

```
release-package/
├── BepInEx/
│   ├── core/                    (from BepInEx distribution)
│   └── plugins/
│       └── ATSAccessibility/
│           ├── ATSAccessibility.dll
│           ├── Tolk.dll
│           └── SAAPI64.dll
├── nvdaControllerClient64.dll   (MUST be in game root for NVDA detection)
├── doorstop_config.ini          (from BepInEx distribution)
├── winhttp.dll                  (from BepInEx distribution)
├── LICENSE.txt                  (MIT - this project)
├── LICENSE-BepInEx.txt          (LGPL-2.1)
├── LICENSE-Tolk.txt             (LGPL-3.0)
└── README.md                    (copy from repo root)
```

## Creating a Release

1. **Build the mod**:
   ```bash
   dotnet build ATSAccessibility/ATSAccessibility.csproj
   ```

2. **Copy built DLL to release-package**:
   ```bash
   cp "C:/Users/rasha/Documents/ATS-Accessibility-Mod/ATSAccessibility/bin/Debug/net472/ATSAccessibility.dll" "C:/Users/rasha/Documents/ATS-Accessibility-Mod/release-package/BepInEx/plugins/ATSAccessibility/"
   ```

3. **Copy README.md and LICENSE to release-package**:
   ```bash
   cp "C:/Users/rasha/Documents/ATS-Accessibility-Mod/README.md" "C:/Users/rasha/Documents/ATS-Accessibility-Mod/release-package/"
   cp "C:/Users/rasha/Documents/ATS-Accessibility-Mod/LICENSE" "C:/Users/rasha/Documents/ATS-Accessibility-Mod/release-package/LICENSE.txt"
   ```

4. **Create the zip** (use PowerShell on Windows):
   ```bash
   cd "C:/Users/rasha/Documents/ATS-Accessibility-Mod/release-package" && powershell -Command "Compress-Archive -Path * -DestinationPath '../ATSAccessibility-vX.X.X-with-BepInEx.zip' -Force"
   ```

5. **Tag and publish the release**:
   ```bash
   git tag vX.X.X
   git push origin vX.X.X
   gh release create vX.X.X ../ATSAccessibility-vX.X.X-with-BepInEx.zip --title "vX.X.X" --notes "release notes here"
   ```
   Creating the tag locally before pushing ensures it exists in both the local repo and on GitHub. Do not use `gh release create` with `--target` alone, as that only creates the tag on the remote.
   Use the `--notes` flag with release notes derived from the "Changes since" section in `changes.md`, formatted to match the style of previous GitHub releases (see any prior release for the template with installation instructions, known limitations, etc.). Do not pass `changes.md` directly as `--notes-file` since it now contains the full project history.

6. **Update changes.md** for the next cycle:
   In `changes.md`, rename the `## Changes since vX.X.X` section to `## vX.X.X` and add a new empty `## Changes since vX.X.X` section above it with empty `### New features`, `### Bug fixes`, and `### Internal` subsections.

## Source Locations

| File | Source |
|------|--------|
| BepInEx core files | Fresh BepInEx 5.x download (user provides) |
| ATSAccessibility.dll | Build output: `ATSAccessibility/bin/Debug/net472/` |
| Tolk.dll, SAAPI64.dll | Deployed game folder: `/c/Program Files (x86)/Steam/steamapps/common/Against the Storm/BepInEx/plugins/ATSAccessibility/` |
| nvdaControllerClient64.dll | NVDA releases: https://download.nvaccess.org/releases/stable/ (controllerClient.zip) - goes in package ROOT |
| LICENSE.txt | Repo root (rename from LICENSE) |
| LICENSE-BepInEx.txt | https://raw.githubusercontent.com/BepInEx/BepInEx/master/LICENSE |
| LICENSE-Tolk.txt | https://raw.githubusercontent.com/ndarilek/tolk/master/LICENSE.txt |
| README.md | Repo root |

## License Requirements

- **ATSAccessibility**: MIT - must include LICENSE.txt
- **BepInEx**: LGPL-2.1 - must include LICENSE-BepInEx.txt
- **Tolk**: LGPL-3.0 - must include LICENSE-Tolk.txt
