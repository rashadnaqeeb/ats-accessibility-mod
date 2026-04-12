# Translation guide

This file explains how to add a non-English language to the mod. The
infrastructure is in place — no code changes are needed to ship a new language,
only a new `<code>.properties` file next to `en.properties`.

## Shipping a translation

1. Copy `en.properties` to `<code>.properties` where `<code>` is one of the
   language codes the game ships with (see list below — the mod picks the code
   the game reports at runtime, so it must match exactly).
2. Translate each value. Keys, `{0}`/`{1}` placeholders, comments, and blank
   lines stay unchanged.
3. Build: the new file is auto-embedded (csproj glob `Strings\*.properties`).
4. Verify: `Tools\ValidateTranslation.ps1 -Language <code>` reports missing
   keys, extra keys, placeholder drift, and untranslated values.
5. Test: set `ForceLanguage = <code>` under `[Localization]` in the mod's
   BepInEx config, launch, and listen for the translated announcements.
   The game's UI language is independent of this setting.

## Languages the game ships with

Sourced from the game's Addressables catalog
(`Against the Storm_Data\StreamingAssets\aa\catalog.json`) — these are the
codes `LocalizationReflection.GetCurrentLocaCode()` will return at runtime:

| Code       | Language                |
| ---------- | ----------------------- |
| `en`       | English (base / fallback) |
| `cs`       | Czech                   |
| `de`       | German                  |
| `es`       | Spanish (Spain)         |
| `es-LATAM` | Spanish (Latin America) |
| `fr`       | French                  |
| `hu`       | Hungarian               |
| `it`       | Italian                 |
| `ja`       | Japanese                |
| `ko`       | Korean                  |
| `pl`       | Polish                  |
| `pt`       | Portuguese              |
| `ru`       | Russian                 |
| `th`       | Thai                    |
| `tr`       | Turkish                 |
| `ua`       | Ukrainian (note: game uses `ua`, not the ISO `uk`) |
| `zh-CN`    | Chinese (Simplified)    |
| `zh-CT`    | Chinese (Traditional — note: game uses `zh-CT`, not `zh-TW`/`zh-Hant`) |

If you ship a `.properties` for a code the game doesn't recognise, the loader
warns and stays on English — no way for a user to switch to it other than the
`ForceLanguage` config.

## Glossary — brand-critical terms

Accessibility announcements must use **the same rendering of these game terms
as the game's own UI**, or a screen-reader user cross-referencing with a
sighted player (or with a wiki, forum, etc.) will get confused. Before
translating a string containing any of the terms below, look up how the
game's own translation renders it and copy that. Don't freestyle.

### How to look up the game's rendering

The game stores its localized strings in Unity TextAssets addressed as
`Texts/<code>` inside the Addressables bundles. Extracting them from the
bundles directly is possible (AssetStudio / AssetRipper) but annoying — the
mod ships a runtime dumper that uses the game's own `Resources.Load` path to
write each language's JSON to disk.

**Procedure:**

1. In the mod's BepInEx config (`BepInEx/config/com.accessibility.ats.cfg`),
   under `[Localization]`, set `DumpGameLocalization = true`.
2. Launch the game and wait for the main menu to announce. The mod writes
   `<code>.json` for every ships-with-game language to
   `%USERPROFILE%\Documents\ATSAccessibility-Locas\` and speaks a confirmation
   through Tolk. Each file is ~1–2 MB of `{"key":"translated value", …}` JSON.
3. Close the game, set `DumpGameLocalization = false` (the dumper is idempotent
   per launch, but leaving it true is noise).
4. Grep / jq the JSON for brand terms in both `en.json` (to find the key) and
   `<target>.json` (to read that key's translation). Example, finding the
   Russian rendering of "Hearth":

   ```powershell
   # 1. Find the key whose English value is "Hearth"
   jq -r 'to_entries[] | select(.value == "Hearth") | .key' `
     $env:USERPROFILE\Documents\ATSAccessibility-Locas\en.json

   # 2. Read that key's value in ru.json
   jq -r '."<key-from-step-1>"' `
     $env:USERPROFILE\Documents\ATSAccessibility-Locas\ru.json
   ```

   Or without jq: open both JSONs in an editor, grep for the English term in
   `en.json` to get its key, then grep that key in `<target>.json`.

5. Use the translated term consistently when your mod string value contains
   that noun. Don't invent a new rendering.

### Mechanics / meta terms

- Viceroy (the player)
- Smoldering City (the central hub; appears in meta / overworld context)
- Hearth (in-settlement central building; the capitalised "Hearth" is the proper noun)
- Cornerstone (perk picked at reputation milestones)
- Blueprint (building unlock picked during a run)
- Seal (end-game objective on the world map)
- Deed (meta achievement)
- Resolve (villager morale — **noun**, not the verb "to resolve")
- Impatience (the lose-condition meter)
- Reputation (the win-condition meter)
- Hostility (the threat-level meter)
- Blightrot (the corruption mechanic)
- Glade (exploration reward tile)
- Storm / Clearance / Drizzle (the three seasons — usually translated as the common-noun weather word)

### Species

Seven playable species — names are usually transliterated or translated
distinctly by the game, not left in English:

- Humans
- Beavers
- Lizards
- Harpies
- Foxes
- Frogs
- Bats

### Building categories

- Rainpunk (engine / technology family)
- Institution (religious / cultural building family)
- Altar, Hearth, House, Farm, Fishing Hut, Relic, Shrine, Institution — each is
  a building category the mod announces; match the game's capitalisation and
  spelling.

### Do NOT translate

- Keybinding labels (`"Alt+I"`, `"Shift+Space"`, etc.) — these are key names,
  not UI text, and screen readers read them letter-by-letter. They currently
  live in code as plain strings on the `HelpEntry` constructor; the
  translatable value is only the *description* passed via `Strings.Get`.
- `{0}`, `{1}` placeholders — these are filled in at runtime with numbers,
  names, or other dynamic content.

## Tone

Announcements are terse — users are screen-reader veterans who prefer minimal
verbosity. Match this style:

- Just the information (name, state, value) — no filler like "You are now in…"
- No navigation hints ("press Enter to…") — the user already knows how to
  navigate.
- No item counts ("3 of 10") unless the count is the information.

If the source English is short, the translation should be short. Don't expand
a three-word English phrase into a full sentence.

## Plural forms

The mod currently has **zero `Strings.Plural` call sites** — every count-
interpolating string uses one template shared across all counts (e.g. `"{0}
seconds"`). For languages with >2 plural forms (Russian, Polish, Arabic…),
the accepted-awkward approach is to:

- Pick a form that reads reasonably for the common case, or
- Reword to dodge count agreement (e.g. `"Seconds: {0}"` instead of `"{0}
  seconds"`).

### Evidence this matches the game's own approach

Inspection of the dumped loca JSONs shows **Against the Storm itself has no
plural engine** — no CLDR, no ICU MessageFormat, no per-count key variants.
Races carry only a `Name` (singular) and `PluralName` (plural-any), and the
plural is used unconditionally for every count ≥ 2, including counts where
grammatically correct Russian / Polish / etc. demand a different form. Eremite's
own translators use the same two workarounds the list above prescribes:

- *Reword to dodge agreement.* Russian `ConditionalNeedEffect_3BuildingsDestroyed_Desc`
  puts numbers in parenthetical side-notes so nothing has to agree with them.
  Abbreviations (`сек.` instead of `секунд/секунды/секунда`) also sidestep
  declension.
- *Accept minor wrongness.* Polish uses `"Co {0} sekund {1} budynki"` — the
  5+ form for both nouns, knowingly wrong at 2–4. Shipped anyway.

In other words: our two-form `Strings.Plural` matches the plural support
the reference product actually provides. A CLDR resolver isn't worth building
just to exceed the grammatical rigor of the game's own translations.

If a specific translated string sounds broken at certain counts, apply the
same workarounds. Only escalate to adding per-language plural logic if the
affected string is critical enough to be worth the one-off complexity.

## File format quick reference

- UTF-8, no BOM, LF or CRLF line endings both fine.
- `#` or `!` at column 0 = comment.
- `key=value` — the first `=` splits; later `=`s are part of the value.
- Escapes: `\n` `\t` `\r` `\\` `\=` `\s` (= literal space — used for trailing
  spaces that editors would otherwise trim).
- Keys are ASCII with dots as scope separators (`scope.area.name`).

## Related tools

- `Tools\ValidateStrings.ps1` — checks every `Strings.Get("key", args)` call
  in C# code against en.properties for missing keys and arity mismatches. Runs
  automatically during `build.ps1`.
- `Tools\ValidateTranslation.ps1` — checks a translation file against
  en.properties for missing keys, extra keys, placeholder drift, and
  unchanged-from-English values.
- `Tools\VerifyAgainstBaseline.ps1` — golden-file diff used to prove a
  literal-to-key migration preserves byte-identical English output. Not
  relevant once you're translating (English output is already locked in).
