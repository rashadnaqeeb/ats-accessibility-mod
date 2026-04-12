# Translation guide

Audience: an LLM agent (or human) producing a `<code>.properties` file next to
`en.properties`. Infrastructure is already in place — no code changes ship a
new language.

## Before you start

The mod stores each ships-with-game language's official text as JSON in
`%USERPROFILE%\Documents\ATSAccessibility-Locas\`. You need `en.json` and
`<target>.json` there to look up the game's own rendering of brand terms.

**Expect these files to already exist** from a prior session — check first.
Only if the folder is missing or lacks your target: set
`DumpGameLocalization = true` under `[Localization]` in
`BepInEx/config/com.accessibility.ats.cfg`, launch the game once to the main
menu, then set it back to `false`. Do not commit the JSONs — they're
copyrighted game content.

## Languages the game ships with

These are the codes `LocalizationReflection.GetCurrentLocaCode()` returns at
runtime; your filename must match exactly.

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
| `ua`       | Ukrainian (note: game uses `ua`, not ISO `uk`) |
| `zh-CN`    | Chinese (Simplified)    |
| `zh-CT`    | Chinese (Traditional — game uses `zh-CT`, not `zh-TW`/`zh-Hant`) |

## Workflow — how to fit this in a context window

`en.properties` is ~4400 lines / ~160 KB. A single-pass translation will blow
your context. Do it in three phases.

### Phase 1 — resolve the glossary (main agent, once)

For every term in the **Glossary** section below, use `en.json` to find the
key whose value matches the English term, then read that key's value in
`<target>.json`. Record every result as a locked rendering. Example for
Russian "Hearth":

```powershell
# 1. Find the key whose English value is "Hearth"
jq -r 'to_entries[] | select(.value == "Hearth") | .key' `
  $env:USERPROFILE\Documents\ATSAccessibility-Locas\en.json

# 2. Read that key's value in ru.json
jq -r '."<key-from-step-1>"' `
  $env:USERPROFILE\Documents\ATSAccessibility-Locas\ru.json
```

Without jq: open both JSONs, grep the English term in `en.json`, then grep
the key in `<target>.json`. If a term has no exact match (game uses a longer
phrase, or the term only appears as part of compounds), note the closest
rendering and move on.

Output of this phase: a flat table `term → official translation` that every
subsequent worker receives verbatim.

### Phase 2 — translate in chunks (fan out to subagents)

`en.properties` is keyed by `scope.area.name`. Split the file along top-level
scopes into the following seven chunks. Sizes are approximate key counts.

| # | Chunk                                                                                   | ~keys |
| - | --------------------------------------------------------------------------------------- | ----- |
| 1 | `common.*` + `core.*` + `dialog.*` + `menu.*` + `reflection.*`                          |   201 |
| 2 | `nav.*` (all building navigators)                                                       |   340 |
| 3 | `handler.*` (key handlers / mode controllers)                                           |   258 |
| 4 | `util.*` (speech / formatters / readers / scanners)                                     |   271 |
| 5 | `panel.*` (info panels + menu hubs)                                                     |   200 |
| 6 | `overlay.*` A–O (`altar` through `orders`, alphabetical by second-level scope)          |   332 |
| 7 | `overlay.*` P–W (`payments` through `world_tutorials`)                                  |   317 |

Dispatch one subagent per chunk with the locked glossary from Phase 1, the
**Tone**, **Plural forms**, **Do NOT translate**, and **File format**
sections below, and its chunk. Keys, comments, blank lines, placeholders,
and escapes are preserved byte-for-byte; only values are translated.
Subagents must not invent glossary renderings — any brand term the glossary
didn't cover is flagged, not guessed.

The mechanics of splitting, dispatching, and collecting results — and how
the orchestrator handles unresolved-term flags — live in the `translate`
skill (`.claude/skills/translate/SKILL.md`).

### Phase 3 — reassemble and validate (main agent)

Reassemble the translated chunks into `<code>.properties` preserving
`en.properties`'s original line order, then run:

```powershell
Tools\ValidateTranslation.ps1 -Language <code>
```

It reports missing keys, extra keys, placeholder drift, and values still
identical to English. Fix drift in place; escalate back to Phase 1 only for
brand-critical terms still rendered in English (most unchanged values are
legitimate — numbers, proper nouns, intentional English retentions).

## Glossary — brand-critical terms

Accessibility announcements must use **the same rendering as the game's own
UI**, or a screen-reader user cross-referencing with a sighted player (or
wiki) will get confused. Resolve each term below in Phase 1 before
translating.

### Mechanics / meta terms

- Viceroy (the player)
- Smoldering City (central hub, overworld / meta context)
- Capital (the meta-progression city; "Capital upgrade" is a term of art)
- Hearth (in-settlement central building — proper noun when capitalised)
- Cornerstone (perk picked at reputation milestones — per run)
- Perk (permanent meta-progression upgrade — distinct from Cornerstone)
- Blueprint (building unlock picked during a run)
- Seal (end-game objective on the world map)
- Deed (meta achievement)
- Order (world-map objective issued by the Queen)
- Queen (NPC who issues Orders)
- Mystery / Forest Mystery (content event type)
- Newcomer (villager arriving from the Smoldering City)
- Trader / Trade Route (travelling NPC + its world-map path)
- Amber (trade currency)
- Biome (the run's setting — Marshlands, Coniferous, etc.)
- Resolve (villager morale — **noun**, not the verb "to resolve")
- Impatience (lose-condition meter)
- Reputation (win-condition meter)
- Hostility (threat-level meter)
- Blightrot (the corruption mechanic)
- Cyst (a blightrot node)
- Glade (exploration reward tile; also Small / Dangerous / Forbidden Glade)
- Storm / Clearance / Drizzle (the three seasons — usually the common-noun
  weather word)
- Ironman (a game mode)

### Species

Seven playable species — names are usually translated or transliterated
distinctly, not left in English:

- Humans, Beavers, Lizards, Harpies, Foxes, Frogs, Bats

### Building categories

- Rainpunk (engine / technology family)
- Institution (religious / cultural building family)
- Altar, Hearth, House, Farm, Fishing Hut, Relic, Shrine — each is a building
  category the mod announces; match the game's capitalisation and spelling.

## Do NOT translate

- Keybinding labels (`"Alt+I"`, `"Shift+Space"`, etc.) — these are key names,
  not UI text, and screen readers read them letter-by-letter. They live in
  code as plain strings on the `HelpEntry` constructor; the translatable
  value is only the *description* passed via `Strings.Get`.
- `{0}`, `{1}` placeholders — runtime-filled with numbers, names, or other
  dynamic content.
- Keys (everything left of the first `=`), comments (`#` / `!` at column 0),
  and blank lines — preserve byte-for-byte.

## Tone

Announcements are terse — users are screen-reader veterans who prefer minimal
verbosity:

- Just the information (name, state, value). No filler like "You are now
  in…".
- No navigation hints ("press Enter to…") — users know how to navigate.
- No counts ("3 of 10") unless the count *is* the information.
- If the English source is short, the translation should be short. Don't
  expand three words into a sentence.

## Plural forms

The mod has zero `Strings.Plural` call sites — every count-interpolating
string uses one template across all counts (e.g. `"{0} seconds"`). For
languages with >2 plural forms (Russian, Polish, Arabic…):

- Pick a form that reads reasonably for the common case, or
- Reword to dodge agreement (e.g. `"Seconds: {0}"` instead of `"{0}
  seconds"`).

This matches the game's own approach — Against the Storm has no plural
engine either, and Eremite's translators use the same two workarounds. Don't
escalate to per-language plural logic unless a specific string is critical
enough to be worth the one-off complexity.

## File format quick reference

- UTF-8, no BOM. LF or CRLF both fine.
- `#` or `!` at column 0 = comment.
- `key=value` — the first `=` splits; later `=`s are part of the value.
- Escapes: `\n` `\t` `\r` `\\` `\=` `\s` (= literal space, for trailing
  spaces that editors would otherwise trim).
- Keys are ASCII with dots as scope separators (`scope.area.name`).

## After translating — shipping the file

1. Place `<code>.properties` next to `en.properties`. The csproj glob
   (`Strings\*.properties`) auto-embeds it on next build.
2. Build: `powershell -ExecutionPolicy Bypass -File build.ps1`.
3. Test: set `ForceLanguage = <code>` under `[Localization]` in
   `BepInEx/config/com.accessibility.ats.cfg`, launch, listen. The game's UI
   language is independent of this setting.

If you ship a `.properties` for a code the game doesn't recognise, the
loader warns and stays on English — the only way to select it is
`ForceLanguage`.

## Related tool

- `Tools\ValidateTranslation.ps1` — missing keys, extra keys, placeholder
  drift, unchanged-from-English values. Run after Phase 3.
