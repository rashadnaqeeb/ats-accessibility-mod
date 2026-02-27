# MapReflection.cs
Reflection-based access to map objects: fields (tiles), glades, relics, villagers, service dictionary properties, glade info state, location markers, relic highlights, harvest marking, farm range helpers, and seal/guidepost support. Used by MapNavigator, MapScanner, HarvestMarkHandler, and RangeInfoHelper. Unlike other reflection files, many caches are lazily populated from the first encountered runtime object rather than from a known type name.

## class MapReflection (line 18)

### Section: FIELD (tile) PROPERTIES (line 19)
**Cached fields** (line 23-25): `_fieldTypeProperty`, `_fieldIsTraversableProperty`, `_fieldCached`

- private static void `EnsureFieldCached(object field)` (line 27)
  Lazy-caches PropertyInfo from the first encountered field object's runtime type.
- public static string `GetFieldTypeName(object field)` (line 44)
  Returns displayName, falls back to name, then ToString.
- public static bool `GetFieldIsTraversable(object field)` (line 79)

### Section: GLADE FIELDS (line 91)
**Cached fields** (line 95-101): `_gladeWasDiscoveredField`, `_gladeDangerLevelField`, `_gladeFieldsField`, `_gladeHasRewardChaseField`, `_gladeRewardChaseEndField`, `_gladeRelicsField`, `_gladeCached`

- private static void `EnsureGladeCached(object glade)` (line 103)
- public static void `EnsureGladeCachedFromList(object allGlades)` (line 124)
  Triggers caching from first non-null glade in a list; call before scan loops.
- public static bool `GetGladeWasDiscovered(object glade)` (line 137)
- public static string `GetGladeDangerLevelRaw(object glade)` (line 146)
  Returns raw enum string ("None", "Dangerous", "Forbidden"); consumers map to display names.
- public static IList `GetGladeFields(object glade)` (line 152)
- public static bool `GetGladeHasRewardChase(object glade)` (line 157)
- public static float `GetGladeRewardChaseEnd(object glade)` (line 162)
- public static IList `GetGladeRelics(object glade)` (line 167)
- public static Vector2Int `GetGladeFirstField(object glade)` (line 175)
  Returns first field position from a glade; used to determine glade position.

### Section: GLADE RELIC FIELDS (line 183)
**Cached fields** (line 187-190): `_relicIsRewardChaseField`, `_relicNameField`, `_relicPositionField`, `_relicCached`

- public static void `EnsureRelicCached(object relic)` (line 195)
  Lazy-caches GladeRelicState fields from first encountered relic instance.
- public static bool `IsRewardChaseRelic(object relic)` (line 209)
- public static string `GetRelicName(object relic)` (line 214)
- public static Vector2Int `GetRelicPosition(object relic)` (line 219)

### Section: VILLAGER PROPERTIES (line 226)
**Cached fields** (line 230-233): `_villagerActorStateProperty`, `_actorStatePositionField`, `_villagerRaceProperty`, `_villagerCached`

- private static void `EnsureVillagerCached(object villager)` (line 235)
  Chains into ActorState to cache position field.
- public static Vector3 `GetVillagerPosition(object villager)` (line 255)
- public static string `GetVillagerRace(object villager)` (line 269)

### Section: SERVICE DICTIONARY PROPERTIES (line 275)
**Cached fields** (line 279-284): `_naturalResourcesProperty`, `_depositsProperty`, `_oresProperty`, `_springsProperty`, `_lakesProperty`, `_buildingsProperty`
Lazy-cached from service instance type on first call.

- public static IDictionary `GetNaturalResources(object resourcesService)` (line 286)
- public static IDictionary `GetDeposits(object depositsService)` (line 293)
- public static IDictionary `GetOres(object oreService)` (line 300)
- public static IDictionary `GetSprings(object springsService)` (line 307)
- public static IDictionary `GetLakes(object lakesService)` (line 314)
- public static IDictionary `GetBuildings(object buildingsService)` (line 321)

### Section: DISPLAY NAME HELPERS (line 328)

- public static string `GetObjectDisplayName(object obj)` (line 336)
  Gets display name via Model.displayName (or Model.name fallback). Works for NaturalResource, ResourceDeposit, Ore, Spring, Lake.
- public static string `GetBuildingDisplayName(object building)` (line 375)
  Gets display name via BuildingModel.displayName, then falls back to GetObjectDisplayName.
- public static Vector2Int `GetBuildingPosition(object building)` (line 407)
  Gets building position from its Field property.
- public static string `GetResourceSizeType(object resourceState)` (line 426)
  Returns "Small", "Large", or "Gigantic" from Model.type on deposit/lake state.

### Section: GLADE INFO STATE (line 445)
**Cached fields** (line 450-456): `_ssEffectsProperty`, `_effectsGladeInfoOwnersField`, `_effectsRevealedGrassLocationsField`, `_effectsRevealedSpringsLocationsField`, `_effectsRevealedRelicsLocationsField`, `_effectsDangerousGladeInfoBlocksField`, `_gladeInfoTypesCached`
Uses assembly-based caching (IStateService, EffectsState types).

- private static void `EnsureGladeInfoTypes()` (line 458)
- public static object `GetEffectsState()` (line 502)
  Gets EffectsState from StateService; contains glade info owners and revealed locations.
- public static bool `HasGladeInfo()` (line 518)
  True when gladeInfoOwners list is non-empty (full glade info active).
- public static bool `HasDangerousGladeInfo()` (line 536)
  True when NOT blocked. In Cursed Royal Woodlands returns false (all glade markers hidden).
- public static List<Vector2Int> `GetRevealedGrassLocations()` (line 553)
  From Human's locate fertile soil ability.
- public static List<Vector2Int> `GetRevealedSpringsLocations()` (line 568)
- public static List<Vector2Int> `GetRevealedRelicLocations()` (line 583)
  From dig site/archaeology abilities.
- public static string `GetLocationMarkerType(int x, int y)` (line 599)
  Returns "grass marker", "spring marker", or "relic marker" if found at position, null otherwise.

### Section: GLADE CONTENTS (line 617)
**Cached fields** (line 618-624): `_gladeContentsDepositsField`, `_gladeContentsRelicsField`, `_gladeContentsBuildingsField`, `_gladeContentsSpringsField`, `_gladeContentsLakesField`, `_gladeContentsOreField`, `_gladeContentsFieldsCached`

- private static void `EnsureGladeContentsFields(object glade)` (line 626)
- public static string `GetGladeContentsSummary(object glade)` (line 648)
  Formatted summary (e.g., "2 deposits, 1 relic"). Shared by MapScanner and MapNavigator.

### Section: LOCATION MARKER EVENT SUBSCRIPTION (line 692)
**Cached fields** (line 697-700): `_esOnGrassLocationRequestedProperty`, `_esOnSpringsLocationRequestedProperty`, `_esOnRelicLocationRequestedProperty`, `_locationEventTypesCached`
Uses assembly-based caching (IEffectsService type).

- private static void `EnsureLocationEventTypes()` (line 702)
- public static IDisposable `SubscribeToGrassLocationRequested(Action callback)` (line 734)
  Subscribes to grass location revealed event (Human's locate fertile soil).
- public static IDisposable `SubscribeToSpringsLocationRequested(Action callback)` (line 754)
- public static IDisposable `SubscribeToRelicLocationRequested(Action callback)` (line 774)
  Subscribes to relic location revealed event (dig site/archaeology).

### Section: RELICS HIGHLIGHT SYSTEM (line 791)
**Cached fields** (line 796-799): `_gsRelicsServiceProperty`, `_rsOnRelicsHighlightRequestedProperty`, `_rsFindRelicForMethod`, `_relicsHighlightTypesCached`
**Instance state** (line 802): `_highlightedRelics` (Dictionary<Vector2Int, string>) tracks highlighted relic positions.

- private static void `EnsureRelicsHighlightTypes()` (line 804)
- public static object `GetRelicsService()` (line 841)
- public static IDisposable `SubscribeToRelicsHighlightRequested(Action<string, Vector2Int> callback)` (line 857)
  Subscribes to relic highlight events (Short Range Scanner). Handler calls FindRelicFor to resolve name/position, stores in _highlightedRelics, then invokes callback.
- public static Dictionary<Vector2Int, string> `GetHighlightedRelics()` (line 904)
- public static string `GetHighlightedRelicAt(int x, int y)` (line 912)
  Returns relic name if highlighted at position, null otherwise.
- public static void `ClearHighlightedRelics()` (line 922)

### Section: NaturalResource MARKED STATE (line 926)
**Cached fields** (line 927-929): `_naturalResourceStateProperty`, `_nrsIsMarkedField`, `_naturalResourceMarkedCached`

- private static void `EnsureNaturalResourceMarkedCache(object resource)` (line 931)
  Caches State property and isMarked field from NaturalResource runtime type.
- public static bool `IsNaturalResourceMarked(object resource)` (line 952)
  Checks if a NaturalResource is marked for woodcutting/harvesting.

### Section: HARVEST MARK/UNMARK REFLECTION (line 965)
**Cached fields** (line 969-973): `_naturalResourceMarkMethod`, `_naturalResourceUnmarkMethod`, `_nrsIsGladeEdgeField`, `_resourcesNaturalResourcesProperty`, `_harvestReflectionCached`

- private static void `EnsureHarvestReflectionCache(object resource)` (line 975)
  Caches Mark/Unmark methods and isGladeEdge field. Chains into EnsureNaturalResourceMarkedCache.
- private static void `EnsureResourcesNaturalResourcesProperty(object resourcesService)` (line 995)
- public static object `GetNaturalResourceAt(Vector2Int pos)` (line 1010)
  Gets the NaturalResource at a map position from ResourcesService dictionary.
- public static bool `HasNaturalResourceAt(Vector2Int pos)` (line 1031)
- public static bool `MarkNaturalResourceAt(Vector2Int pos)` (line 1039)
  Marks resource for harvesting. Returns true on success.
- public static bool `UnmarkNaturalResourceAt(Vector2Int pos)` (line 1058)
  Unmarks resource. Returns true on success.
- public static bool `IsNaturalResourceGladeEdge(Vector2Int pos)` (line 1076)
  Checks if a NaturalResource at position is on a glade edge.
- public static List<Vector2Int> `GetAllNaturalResourcePositions()` (line 1097)
  Returns all NaturalResource positions from ResourcesService dictionary.

### Section: FARM RANGE HELPERS (line 1119)
**Cached fields** (line 1123-1124): `_farmModelWorkAreaField`, `_farmModelFieldsCached`
Uses assembly-based caching (FarmModel type).

- private static void `EnsureFarmModelFields()` (line 1126)
- public static Vector2Int `GetFarmModelWorkArea(object farmModel)` (line 1150)
  Gets work area size from a FarmModel.
- public static bool `IsFieldGrass(object field)` (line 1169)
  Checks if a Field is of type Grass (fertile soil). Compares Type property toString to "Grass".
- public static bool `IsInUnrevealedGlade(int x, int y)` (line 1189)
  Checks GladesService.IsGlade then glade.IsDiscovered. Returns true if glade exists but is not discovered.

### Section: SEAL / GUIDEPOST SUPPORT (line 1217)
**Cached fields** (line 1221-1228): `_gsGameSealServiceProperty`, `_gameSealServiceIsSealedBiomeMethod`, `_gameSealServiceGetSealFieldMethod`, `_bsSealsProperty`, `_biomeServiceDifficultyProperty`, `_difficultyInGameSealField`, `_sealModelSizeField`, `_sealTypesCached`
Uses assembly-based caching (IGameSealService, IBuildingsService, IBiomeService, DifficultyModel, BuildingModel types).

- private static void `EnsureSealTypes()` (line 1230)
- public static object `GetGameSealService()` (line 1282)
- public static bool `IsSealedBiome()` (line 1290)
  Checks if current biome is a sealed biome (Sealed Forest).
- public static Vector2Int `GetSealField()` (line 1301)
  Gets seal field location from GameSealService. Used when seal not yet discovered.
- public static IDictionary `GetSeals()` (line 1319)
  Gets Seals dictionary from BuildingsService.
- public static Vector2Int `GetSealSize()` (line 1336)
  Gets seal size via BiomeService.Difficulty.inGameSeal.size chain.
- public static Vector2Int `GetGuidepostTargetField()` (line 1361)
  Gets target field for guidepost direction. Prefers discovered seal's Field, falls back to GameSealService.GetSealField().
