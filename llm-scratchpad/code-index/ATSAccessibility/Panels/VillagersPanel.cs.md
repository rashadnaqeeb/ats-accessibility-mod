# VillagersPanel.cs
Virtual speech-only panel for navigating villager information.

Navigation model (3 levels via MenuBase):
- Level 0 (Categories): Shared Needs (if any), then each race
- Level 1 (Details): Resolve, Needs, Favoring
- Level 2 (Sub-details): Resolve breakdown

## class VillagersPanel: MenuBase (line 18)

### Fields
- private List<RaceCategory> _categories (line 48)
- private static MethodInfo _villGetDefaultProfessionAmountMethod (line 51)
- private static MethodInfo _villGetHomelessAmountMethod (line 52)
- private static bool _typesCached (line 53)

### Properties
- protected override string OverlayName { get; } (line 71)
- protected override string EmptyMessage { get; } (line 73)
- protected override int SearchItemCount { get; } (line 257)
  Returns item count appropriate to current Level.

### Methods
- public bool ProcessKeyEvent(KeyCode keyCode) (line 62)
  Bridge method for InfoPanelMenu; returns false immediately if not open.
- protected override int GetItemCount() (line 75)
  Switch on Level: 2 = sub-detail count, 1 = detail count, 0 = category count.
- protected override string GetLabel(int index) (line 92)
  Switch on Level: 2 = sub-detail string, 1 = detail label, 0 = race display name.
- protected override void RefreshData() (line 111)
  Builds RaceCategory list by calling StatsReader and GetRaceNeeds for each present race. Also detects needs shared across 2+ races and prepends a Shared Needs category.
- protected override EnterAction OnEnter(int index) (line 155)
  Level 0: DrillDown if race has details. Level 1: Action for Favoring items, DrillDown for items with sub-details, else re-announces. Level 2: re-announces.
- protected override void OnAction(int index) (line 188)
  Level 1 only: calls PerformFavoringAction() for Favoring detail items.
- protected override bool CanDrillDown(int index) (line 200)
  Level 0: true if race has details. Level 1: true if detail has sub-details. Level 2: false.
- protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 218)
  Level 0 LeftArrow and Escape both return false to pass to parent (InfoPanelMenu).
- protected override void OnClosed() (line 228)
- protected override void AnnounceCurrentItem() (line 232)
  Dispatches to AnnounceCategory, AnnounceDetail, or AnnounceSubDetail based on Level.
- protected override string GetOpenAnnouncement() (line 246)
  Delegates to BuildCategoryAnnouncement(0).
- protected override string GetSearchName(int index) (line 276)
  Returns appropriate name string for each Level.
- private void PerformFavoringAction() (line 299)
  Toggles favoring for the current race. Checks if already favored (toggle off), cooldown, minimum 2 races with villagers, and non-zero population before calling FavorRace.
- private void UpdateFavoringLabel() (line 354)
  Refreshes the Favoring detail label in all categories after a favoring state change.
- private void PlayFavoringSound(string raceName) (line 367)
  Looks up the race's favoringStartSound via reflection and plays it via SoundManager.
- private string BuildCategoryAnnouncement(int index) (line 392)
  Returns "{Name}[, favored]. {population} villagers, {free} free, {homeless} homeless". Shared Needs category returns just the name.
- private void AnnounceCategory() (line 406)
- private void AnnounceDetail() (line 414)
  Adds "Press right for breakdown" hint for Resolve items with sub-details.
- private void AnnounceSubDetail() (line 432)
- private void BuildSharedNeedsCategory(Dictionary<string, List<SharedNeedRaceInfo>> needRaces, List<string> needOrder) (line 448)
  Finds needs appearing in 2+ races, sums satisfaction counts across races, inserts a Shared Needs category at index 0 if any exist.
- private void BuildRaceDetails(RaceCategory category, string raceName, List<NeedInfo> needs) (line 487)
  Builds the Details list for a race: Resolve with breakdown sub-details, each Need as a detail, and a Favoring detail.
- private string GetFavoringLabel(string raceName) (line 523)
  Returns context-appropriate label: active/cooldown/available.
- private static void EnsureTypes() (line 538)
  Caches MethodInfo for VillagersService.GetDefaultProfessionAmount and GetHomelessAmount.
- private string GetRaceDisplayName(string raceName) (line 554)
  Fetches localized race name via Settings.GetRace reflection.
- private int GetFreeWorkers(string raceName) (line 572)
  Calls cached VillagersService.GetDefaultProfessionAmount via reflection.
- private int GetHomeless(string raceName) (line 582)
  Calls cached VillagersService.GetHomelessAmount via reflection.
- private List<NeedInfo> GetRaceNeeds(string raceName) (line 604)
  Gets the race's needs array via reflection, filtering to visible needs and resolving display names via localization.
- private int GetNeedSatisfiedCount(string raceName, object needModel) (line 646)
  Calls NeedsService.CountVillagersWithFulfilled(NeedModel, RaceModel) via reflection.
