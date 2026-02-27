# WorkersPanel.cs
Virtual speech-only panel for navigating worker profession counts by race.
Level 0 = categories ("All", then each present race).
Level 1 = each assigned profession with its worker count, sorted by count descending then name ascending.

## class WorkersPanel: MenuBase (line 16)

### Fields
- private List<WorkerCategory> _categories (line 28)
- private static PropertyInfo _villRacesProperty (line 31)
  Cached PropertyInfo for VillagersService.Races.
- private static FieldInfo _professionModelField (line 32)
  Cached FieldInfo for villager.professionModel.
- private static FieldInfo _professionDisplayNameField (line 33)
  Cached FieldInfo for professionModel.displayName.
- private static bool _typesCached (line 34)
- private int _currentCategoryIndex (line 37)
  Compatibility alias mapping to _indices[0].
- private int _currentItemIndex (line 38)
  Compatibility alias mapping to _indices[1].

### Properties
- protected override string OverlayName { get; } (line 44)
- protected override string EmptyMessage { get; } (line 45)

### Methods
- protected override int GetItemCount() (line 47)
  Level 0: category count. Level 1: item count for current category.
- protected override string GetLabel(int index) (line 57)
  Level 0: category name. Level 1: "{ProfessionName}, {count}".
- protected override string GetSearchName(int index) (line 72)
  Level 0: category name. Level 1: profession name only (without count).
- protected override void RefreshData() (line 87)
  Iterates villagers per race via reflection to build profession-count dictionaries, creates "All" category plus per-race categories, sorted by count desc then name asc.
- protected override EnterAction OnEnter(int index) (line 146)
  Level 0: DrillDown if category has items. Level 1: None.
- protected override EscapeAction OnEscape() (line 158)
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 160)
  Level 0 LeftArrow returns false to pass to InfoPanelMenu.
- protected override string GetOpenAnnouncement() (line 166)
- protected override void OnClosed() (line 171)
- public bool ProcessKeyEvent(KeyCode keyCode) (line 180)
  Bridge method for InfoPanelMenu; wraps ProcessKey with default modifiers.
- private static List<ProfessionItem> BuildSortedItems(Dictionary<string, int> professions) (line 187)
  Converts profession dict to a list sorted by count descending, then name ascending.
- private static void EnsureTypes() (line 199)
  Caches VillagersService.Races property, then samples one villager to cache professionModel and displayName field infos.
- private static System.Collections.IEnumerable GetVillagersForRace(string raceName) (line 239)
  Uses cached Races property indexer to get the villager list for a race. Returns empty on failure.
- private static string GetVillagerProfession(object villager) (line 256)
  Reads villager.professionModel.displayName via cached fields (with lazy field caching as fallback). Returns "Worker" on failure.
- private static string GetRaceDisplayName(string raceName) (line 273)
  Fetches localized race name via Settings.GetRace reflection.
