# Release Packaging

## Package Structure

```
release-package/
├── BepInEx/
│   ├── core/                    (from BepInEx distribution)
│   └── plugins/
│       └── ATSAccessibility/
│           ├── ATSAccessibility.dll
│           └── prism.dll
├── doorstop_config.ini          (from BepInEx distribution)
├── winhttp.dll                  (from BepInEx distribution)
├── LICENSE.txt                  (MIT - this project)
├── LICENSE-BepInEx.txt          (LGPL-2.1)
├── LICENSE-Prism.txt            (MPL-2.0)
└── README.md                    (copy from repo root)
```

Prism bundles every supported screen-reader bridge into a single DLL, so no
separate SAPI/NVDA client files need to be shipped.

## Creating a Release

1. **Update the version number** in `ATSAccessibility/Core/Plugin.cs`:
   Change the `ModVersion` constant to the new version. The `BepInPlugin` attribute and the update checker both read from this single constant.
   ```csharp
   public const string ModVersion = "X.X.X";
   ```

2. **Build the mod**:
   ```bash
   dotnet build ATSAccessibility/ATSAccessibility.csproj
   ```

3. **Copy built DLL to release-package**:
   ```bash
   cp "C:/Users/rasha/Documents/ATS-Accessibility-Mod/ATSAccessibility/bin/Debug/net472/ATSAccessibility.dll" "C:/Users/rasha/Documents/ATS-Accessibility-Mod/release-package/BepInEx/plugins/ATSAccessibility/"
   ```

4. **Copy README.md and LICENSE to release-package**:
   ```bash
   cp "C:/Users/rasha/Documents/ATS-Accessibility-Mod/README.md" "C:/Users/rasha/Documents/ATS-Accessibility-Mod/release-package/"
   cp "C:/Users/rasha/Documents/ATS-Accessibility-Mod/LICENSE" "C:/Users/rasha/Documents/ATS-Accessibility-Mod/release-package/LICENSE.txt"
   ```

5. **Create the zip** (use PowerShell on Windows):
   ```bash
   cd "C:/Users/rasha/Documents/ATS-Accessibility-Mod/release-package" && powershell -Command "Compress-Archive -Path * -DestinationPath '../ATSAccessibility-vX.X.X-with-BepInEx.zip' -Force"
   ```

6. **Write the Loadstone release manifest**. The Loadstone installer reads
   `loadstone-release.json` from each release: it freezes
   `loadstone-packages/ats-accessibility.json` (install, launch and uninstall steps) for this
   version and pins the zip's hash. Save the release notes to `../release-notes.md` first (see
   the notes guidance in step 7); they become the notes the installer shows. Loadstone
   is a checkout beside this repo (`../loadstone`, from github.com/rashadnaqeeb/loadstone):
   ```bash
   (cd ../loadstone && cargo build --release -p loadstone-cli)
   ../loadstone/target/release/loadstone package --manifest loadstone-packages/ats-accessibility.json \
       --version vX.X.X --artifact ../ATSAccessibility-vX.X.X-with-BepInEx.zip \
       --notes-file ../release-notes.md --out ../loadstone-release.json
   ../loadstone/target/release/loadstone validate --scope loadstone-packages ../loadstone-release.json
   ```
   The version must be three-part semver (`v1.8.0`, not `v1.8`). Check package files after
   editing them with `../loadstone/target/release/loadstone validate loadstone-packages`.

7. **Tag and publish the release**:
   ```bash
   git tag vX.X.X
   git push origin vX.X.X
   gh release create vX.X.X ../ATSAccessibility-vX.X.X-with-BepInEx.zip ../loadstone-release.json --title "vX.X.X" --notes-file ../release-notes.md
   ```
   Creating the tag locally before pushing ensures it exists in both the local repo and on GitHub. Do not use `gh release create` with `--target` alone, as that only creates the tag on the remote.
   Write the release notes derived from the "Changes since" section in `changes.md`, formatted to match the style of previous GitHub releases (see any prior release for the template with installation instructions, known limitations, etc.). Do not pass `changes.md` itself as the notes file, since it contains the full project history.

8. **Update changes.md** for the next cycle:
   In `changes.md`, rename the `## Changes since vX.X.X` section to `## vX.X.X` and add a new empty `## Changes since vX.X.X` section above it with empty `### New features`, `### Bug fixes`, and `### Internal` subsections.

## Source Locations

Sources:

- BepInEx core files: fresh BepInEx 5.x download (user provides).
- `ATSAccessibility.dll`: build output at `ATSAccessibility/bin/Debug/net472/`.
- `prism.dll`: vendored at `prism/native/win-x64/prism.dll` in this repo (sourced from the [Prism](https://github.com/ethindp/prism) project).
- `LICENSE.txt`: repo root (rename from `LICENSE`).
- `LICENSE-BepInEx.txt`: https://raw.githubusercontent.com/BepInEx/BepInEx/master/LICENSE
- `LICENSE-Prism.txt`: copy from `prism/LICENSES/` (MPL-2.0).
- `README.md`: repo root.

## License Requirements

- **ATSAccessibility**: MIT, must include `LICENSE.txt`.
- **BepInEx**: LGPL-2.1, must include `LICENSE-BepInEx.txt`.
- **Prism**: MPL-2.0, must include `LICENSE-Prism.txt`. See `prism/NOTICE` for additional third-party attributions bundled inside `prism.dll`.
