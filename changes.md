# Changes since v1.1.0

## New features
- W key worker summary now includes building specialty with bonus type and matching races (e.g., "Woodworking Efficiency (Beavers)")
- Building worker menu now shows bonus type when selecting races (e.g., "Woodworking Efficiency")
- Hearth W key now shows firekeeper effect for assigned race (e.g., "1/1: Beaver, +5 Global Resolve")

## Bug fixes
- Type-ahead search no longer rolls back characters when no matches found; keeps full search term

## Internal
- Use StringBuilder in TypeAheadSearch to reduce GC allocations during search
- Refactor race bonus methods to eliminate code duplication and reduce double-lookup overhead
- Update CONTRIBUTING.md with current build workflow and project structure
- Complete game-internals.md with TOC, intro, and all system documentation (Daily Expedition, Custom Games, Payments, World Events, Games History, Stats, Ironman)
- Reorganize codebase into subdirectories: Core/, Overlays/, Reflection/, Handlers/, Utils/, Panels/, Navigators/
