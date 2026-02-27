# EncyclopediaNavigator.cs
Provides keyboard navigation for the game's WikiPopup (encyclopedia).
Supports 3-panel navigation: Categories, Articles, and Content.
Does not extend MenuBase because Left/Right switches between panels with
fundamentally different content types, and the Content panel uses a text
reader rather than item navigation — neither maps to MenuBase's level model.

## class EncyclopediaNavigator: IKeyHandler, ISearchable, IHelpProvider (line 17)

### Fields
- public enum WikiPanel { Categories = 0, Articles = 1, Content = 2 } (line 18)
- private object _wikiPopup (line 20)
- private WikiPanel _currentPanel (line 21)
- private List<object> _categoryButtons (line 24)
- private int _categoryIndex (line 25)
- private List<object> _articleSlots (line 28)
- private int _articleIndex (line 29)
- private List<string> _contentLines (line 32)
- private int _contentLineIndex (line 33)
- private object _currentCategoryPanel (line 36)
- private readonly TypeAheadSearch _search (line 39)
- private static readonly List<HelpEntry> _helpEntries (line 45)

### Properties
- public HelpBehavior HelpBehavior { get; } (line 47)
- public string HelpContextName { get; } (line 48)
- public bool IsActive { get; } (line 55)

### Methods
- public IReadOnlyList<HelpEntry> GetHelpEntries() (line 49)
- public IReadOnlyList<string> GetPassthroughKeys() (line 50)
- public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) (line 61)
- public void OnWikiPopupShown(object popup) (line 107)
- public void OnWikiPopupHidden() (line 122)
- public void NavigatePanel(int direction) (line 138)
  Moves between the three panels (Left/Right). Rebuilds article/content data when entering those panels, resets index only when moving forward.
- public void NavigateElementToFirst() (line 178)
- public void NavigateElementToLast() (line 195)
- public void NavigateElement(int direction) (line 212)
- public bool ActivateCurrentElement() (line 231)
  On Categories/Articles: activates item and auto-advances to next panel. On Content: re-reads the current line.
- private void RebuildCategories() (line 252)
- private void NavigateCategories(int direction) (line 288)
- private bool ActivateCategory() (line 300)
  Clicks the category button and auto-advances to the Articles panel.
- private void RebuildArticles() (line 324)
- private void NavigateArticles(int direction) (line 355)
- private bool ActivateArticle() (line 367)
  Checks lock state; if unlocked, clicks button and auto-advances to the Content panel.
- private void RebuildContent() (line 395)
  Dispatcher: routes to BuildRaceContent, BuildBuildingContent, BuildRelicContent, or generic UI text extraction.
- private void BuildRaceContent(object raceSlot) (line 443)
  Extracts data directly from RaceModel via reflection for proper ordering.
- private void BuildBuildingContent(object buildingSlot) (line 523)
  Extracts data directly from BuildingModel via reflection.
- private void BuildWorkshopRecipes(object workshop) (line 596)
- private void BuildUpgradeInfo(object building) (line 626)
- private void BuildRelicContent(object relicSlot) (line 662)
  Builds structured content for a glade event (relic) article.
- private void BuildRelicDynamicEffects(object relic) (line 697)
- private void BuildRelicStaticEffects(object relic) (line 728)
- private void BuildRelicDecisions(object relic) (line 746)
- private void BuildRelicSinglePath(object relic) (line 814)
- private string IntToRoman(int num) (line 874)
- private string FormatGoodsSets(Array goodsSets, string separator = ", + ", string altSeparator = " OR ") (line 890)
  Each GoodsSet is one required input slot (joined by separator); goods within a set are alternatives (joined by altSeparator).
- private string FormatUpgradeCost(Array requiredGoods) (line 914)
- private void AddIfNotEmpty(string text) (line 921)
- private string FormatMinSec(float totalSeconds) (line 929)
- private string ExtractPreviewContent() (line 940)
  Fallback for article types without structured reflection support — collects all TMP_Text content from the Preview transform.
- private Transform FindChildRecursive(Transform parent, string name) (line 978)
- private void NavigateContentLines(int direction) (line 989)
  Clamps rather than wraps (does not wrap around in the Content panel).
- int ISearchable.SearchItemCount { get; } (line 1005)
  Returns 0 for Content panel (search disabled there).
- int ISearchable.SearchCurrentIndex { get; } (line 1015)
- string ISearchable.GetSearchLabel(int index) (line 1025)
- void ISearchable.SearchMoveTo(int index) (line 1040)
- private void AnnounceCurrentPanel() (line 1057)
- private void AnnounceCurrentElement() (line 1068)
- private void AnnounceCategoryElement() (line 1082)
- private void AnnounceArticleElement() (line 1098)
- private void AnnounceContentElement() (line 1117)
