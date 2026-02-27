# LLM Scratchpad - Current Status

## Working Branch
`claude-mod-cleanup` (based off `master` at commit 161130d)

## Prompts Completed
1. `prompts/sanity-checks-setup.md` — done
2. `prompts/information-gathering-and-checking.md` — done
3. `prompts/code-directory-construction.md` — done (150 index files for 150 source files)

## Prompts Remaining
- `prompts/large-file-handling.md` — done
- `prompts/input-handling.md` — done (no changes needed, system already well-designed)
- `prompts/string-builder.md` — done (not a string builder mod; ~10% string building, well distributed)
- `prompts/low-level-cleanup.md` — done (5 commits: PortReflection strider/crew consolidation, ToggleBuildingSleep to base class, SettlementKeyHandler priority dedup, GetDirection dedup across 4 files, BuildMode/MoveMode shared helpers to NavigationUtils)
- `prompts/high-level-cleanup.md` — in progress
- `prompts/finalization.md` — next after high-level-cleanup

## Code Index
- `llm-scratchpad/code-index/` — 154 .md files mirroring ATSAccessibility/ structure (4 new from splits)
- BuildingReflection.cs: 10132 → 6831 → 10125 → 6845 lines (extracted Hearth/Relic/Port, absorbed then re-extracted construction)
- ConstructionReflection.cs: 3313 lines (new — building system, placement, range, lake, supply chain from BuildingReflection)
- GameReflection.cs: 7413 → 4096 → 3159 lines (construction → BuildingReflection, map/glade/seal → MapReflection)
- MapReflection.cs: 444 → 1381 lines (absorbed glade info, harvest, seal/guidepost from GameReflection)

## Documentation Created
- `llm-docs/CLAUDE.md` — overview of llm-docs contents
- `llm-docs/game-api-reference.md` — reflected game API catalog (33 sections, ~1500 lines)
- `llm-docs/game-overview.md` — game mechanics, screens, controls, modding info
- `llm-docs/mod-coverage-map.md` — complete mod coverage: overlays, handlers, navigators, panels

## CLAUDE.md Changes Applied
- Removed machine-specific paths (build commands, debug log)
- Removed stale file counts from directory tree
- Updated Core/ description (added HelpCollector, IHelpProvider, SceneConstants)
- Fixed Overlays/ description (not all extend MenuBase)
- Updated Handlers/ description (added TutorialTooltipHandler)
- Updated Panels/ description (added HelpOverlay)
- Fixed ReflectionHelper overload count claim
- Added game overview section
- Added llm-docs reference to Key Locations

## Notes
- Game: Against the Storm (roguelite city-builder by Eremite Games)
- Mod: BepInEx 5 accessibility mod with screen reader support via Tolk + HarmonyX
- No clarification needed from user — all information was verifiable from codebase and web sources
