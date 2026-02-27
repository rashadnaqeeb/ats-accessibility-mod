# LLM Scratchpad - Current Status

## Working Branch
`claude-mod-cleanup` (based off `master` at commit 161130d)

## Prompts Completed
1. `prompts/sanity-checks-setup.md` — done
2. `prompts/information-gathering-and-checking.md` — done

## Prompts Remaining
- `prompts/code-directory-construction.md`
- (subsequent prompts TBD from reading each prompt)

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
