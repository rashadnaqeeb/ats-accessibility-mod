using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the ConsumptionPopup (consumption control).
	/// Three-level navigation: categories -> items -> races.
	/// Pattern B: Right drills down, Enter does nothing at any level.
	/// Space toggles at all levels.
	/// </summary>
	public class ConsumptionOverlay: MenuBase {
		// Category data
		private class CategoryData {
			public object Category;     // NeedCategoryModel object (null for raw food/race)
			public string Name;
			public bool IsRawFood;
			public bool IsRace;
			public object Race;         // RaceModel object (only when IsRace)
		}

		// State
		private bool _isBlocked;

		// Level 0: Categories
		private List<CategoryData> _categories = new List<CategoryData>();

		// Level 1: Items (raw food IDs or need objects)
		private List<object> _items = new List<object>();
		private List<string> _itemNames = new List<string>();

		// Level 2: Races (for selected need)
		private List<object> _races = new List<object>();
		private List<string> _raceNames = new List<string>();

		// Track whether current category is raw food (for level 1 behavior)
		private bool _currentCategoryIsRawFood;

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("overlay.consumption.title");
		protected override string EmptyMessage => Strings.Get("common.no_categories_available");

		protected override int GetItemCount() {
			switch (Level) {
				case 0: return _categories.Count;
				case 1: return _items.Count;
				case 2: return _races.Count;
				default: return 0;
			}
		}

		protected override string GetLabel(int index) {
			switch (Level) {
				case 0: return GetCategoryAnnouncement(index);
				case 1: return GetItemAnnouncement(index);
				case 2: return GetRaceAnnouncement(index);
				default: return null;
			}
		}

		protected override void RefreshData() {
			_isBlocked = ConsumptionReflection.IsBlocked();
			RefreshCategories();
		}

		protected override EnterAction OnEnter(int index) => EnterAction.None;

		protected override void OnSpace(int index) {
			switch (Level) {
				case 0: ToggleCategory(); break;
				case 1: ToggleItem(); break;
				case 2: ToggleRace(); break;
			}
		}

		/// <summary>
		/// Pattern B: Right drills down, Enter does nothing.
		/// CanDrillDown also loads data for the next level and validates it.
		/// </summary>
		protected override bool CanDrillDown(int index) {
			if (Level == 0) {
				if (index < 0 || index >= _categories.Count || _categories[index].IsRace)
					return false;

				_currentCategoryIsRawFood = _categories[index].IsRawFood;
				RefreshItems(_categories[index]);

				if (_items.Count == 0) {
					Speech.Say(Strings.Get("common.no_items"));
					return false;
				}
				return true;
			}

			if (Level == 1) {
				if (_currentCategoryIsRawFood)
					return false;

				RefreshRaces(_items[index]);

				if (_races.Count == 0) {
					Speech.Say(Strings.Get("overlay.consumption.no_races"));
					return false;
				}
				return true;
			}

			return false;
		}

		protected override void OnDrillDown(int index) {
			// Data already loaded in CanDrillDown
		}

		protected override void OnGoBack() {
			if (Level == 2) {
				_races.Clear();
				_raceNames.Clear();
			} else if (Level == 1) {
				_items.Clear();
				_itemNames.Clear();
			}
		}

		protected override string GetOpenAnnouncement() {
			if (_isBlocked) {
				string effects = ConsumptionReflection.GetBlockingEffectsList();
				if (!string.IsNullOrEmpty(effects))
					return Strings.Get("overlay.consumption.blocked_with", effects);
				return Strings.Get("overlay.consumption.blocked");
			}

			if (_categories.Count > 0)
				return Strings.Get("overlay.consumption.open", GetCategoryAnnouncement(0));

			return Strings.Get("overlay.consumption.open", EmptyMessage);
		}

		protected override void OnClosed() {
			_categories.Clear();
			_items.Clear();
			_itemNames.Clear();
			_races.Clear();
			_raceNames.Clear();
		}

		// ========================================
		// SEARCH
		// ========================================

		protected override int SearchItemCount {
			get {
				switch (Level) {
					case 0: return _categories.Count;
					case 1: return _itemNames.Count;
					case 2: return _raceNames.Count;
					default: return 0;
				}
			}
		}

		protected override string GetSearchName(int index) {
			switch (Level) {
				case 0:
					return (index >= 0 && index < _categories.Count) ? _categories[index].Name : null;
				case 1:
					return (index >= 0 && index < _itemNames.Count) ? _itemNames[index] : null;
				case 2:
					return (index >= 0 && index < _raceNames.Count) ? _raceNames[index] : null;
				default:
					return null;
			}
		}

		// ========================================
		// TOGGLE ACTIONS
		// ========================================

		private void ToggleCategory() {
			if (_categories.Count == 0) return;

			if (_isBlocked) {
				Speech.Say(Strings.Get("overlay.consumption.toggle.blocked"));
				SoundManager.PlayFailed();
				return;
			}

			var cat = _categories[_indices[0]];
			bool setTo;

			if (cat.IsRawFood) {
				// Toggle all raw foods: only permit if all prohibited; mixed -> prohibit
				setTo = ConsumptionReflection.IsAllRawFoodProhibited();
				ConsumptionReflection.SetAllRawFoodPermission(setTo);
			} else if (cat.IsRace) {
				// Toggle all needs for this race: if not all prohibited, prohibit; otherwise permit
				setTo = ConsumptionReflection.GetRaceNeedsStatus(cat.Race) == ConsumptionStatus.AllProhibited;
				ConsumptionReflection.SetAllNeedsPermissionForRace(cat.Race, setTo);
			} else {
				// Toggle all needs in category: if not all prohibited, prohibit; otherwise permit
				setTo = ConsumptionReflection.GetCategoryStatus(cat.Category, false) == ConsumptionStatus.AllProhibited;
				ConsumptionReflection.SetAllNeedsPermissionForCategory(cat.Category, setTo);
			}

			SoundManager.PlayButtonClick();
			Speech.Say(Strings.Get(setTo ? "overlay.consumption.toggle.permitted" : "overlay.consumption.toggle.prohibited"));
		}

		private void ToggleItem() {
			if (_items.Count == 0) return;

			if (_isBlocked) {
				Speech.Say(Strings.Get("overlay.consumption.toggle.blocked"));
				SoundManager.PlayFailed();
				return;
			}

			bool setTo;

			if (_currentCategoryIsRawFood) {
				// Toggle individual raw food
				string id = _items[CurrentIndex] as string;
				if (id == null) return;

				setTo = !ConsumptionReflection.IsRawFoodPermitted(id);
				ConsumptionReflection.SetRawFoodPermission(id, setTo);
			} else {
				// Blanket toggle for a need (all races)
				var need = _items[CurrentIndex];
				setTo = ConsumptionReflection.GetNeedStatus(need) == ConsumptionStatus.AllProhibited;
				ConsumptionReflection.SetNeedBlanketPermission(need, setTo);
			}

			SoundManager.PlayButtonClick();
			Speech.Say(Strings.Get(setTo ? "overlay.consumption.toggle.permitted" : "overlay.consumption.toggle.prohibited"));
		}

		private void ToggleRace() {
			if (_races.Count == 0 || _items.Count == 0) return;

			if (_isBlocked) {
				Speech.Say(Strings.Get("overlay.consumption.toggle.blocked"));
				SoundManager.PlayFailed();
				return;
			}

			var race = _races[CurrentIndex];
			var need = _items[_indices[1]];
			bool setTo = !ConsumptionReflection.IsNeedPermittedForRace(race, need);
			ConsumptionReflection.SetNeedPermissionForRace(race, need, setTo);

			SoundManager.PlayButtonClick();
			Speech.Say(Strings.Get(setTo ? "overlay.consumption.toggle.permitted" : "overlay.consumption.toggle.prohibited"));
		}

		// ========================================
		// ANNOUNCEMENTS
		// ========================================

		private string GetCategoryAnnouncement(int index) {
			if (index < 0 || index >= _categories.Count) return "";
			var cat = _categories[index];

			if (cat.IsRace) {
				var status = ConsumptionReflection.GetRaceNeedsStatus(cat.Race);
				return Strings.Get("overlay.consumption.category_row", cat.Name, FormatStatus(status));
			}

			var catStatus = ConsumptionReflection.GetCategoryStatus(cat.Category, cat.IsRawFood);
			return Strings.Get("overlay.consumption.category_row", cat.Name, FormatStatus(catStatus));
		}

		private static string FormatStatus(ConsumptionStatus status) {
			switch (status) {
				case ConsumptionStatus.AllPermitted: return Strings.Get("overlay.consumption.status.all_permitted");
				case ConsumptionStatus.AllProhibited: return Strings.Get("overlay.consumption.status.all_prohibited");
				case ConsumptionStatus.Mixed: return Strings.Get("overlay.consumption.status.mixed");
				default: return Strings.Get("overlay.consumption.status.unknown");
			}
		}

		private string GetItemAnnouncement(int index) {
			if (index < 0 || index >= _items.Count) return "";

			string name = (index < _itemNames.Count) ? _itemNames[index] : Strings.Get("common.unknown");

			if (_currentCategoryIsRawFood) {
				string id = _items[index] as string;
				bool permitted = (id != null) && ConsumptionReflection.IsRawFoodPermitted(id);
				return Strings.Get("overlay.consumption.item_row", name, Strings.Get(permitted ? "overlay.consumption.status.permitted" : "overlay.consumption.status.prohibited"));
			} else {
				var need = _items[index];
				var status = ConsumptionReflection.GetNeedStatus(need);
				return Strings.Get("overlay.consumption.item_row", name, FormatStatus(status));
			}
		}

		private string GetRaceAnnouncement(int index) {
			if (index < 0 || index >= _races.Count || _items.Count == 0) return "";

			string name = (index < _raceNames.Count) ? _raceNames[index] : Strings.Get("common.unknown");
			var race = _races[index];
			var need = _items[_indices[1]];

			bool permitted = ConsumptionReflection.IsNeedPermittedForRace(race, need);
			var (_, max) = ConsumptionReflection.GetResolveImpact(race, need);

			string impact = permitted ? Strings.Get("overlay.consumption.impact.bonus", max) : Strings.Get("overlay.consumption.impact.penalty", max);
			return Strings.Get("overlay.consumption.race_row", name, Strings.Get(permitted ? "overlay.consumption.status.permitted" : "overlay.consumption.status.prohibited"), impact);
		}

		// ========================================
		// DATA REFRESH
		// ========================================

		private void RefreshCategories() {
			_categories.Clear();

			// First category is always "Raw Food"
			_categories.Add(new CategoryData {
				Category = null,
				Name = Strings.Get("overlay.consumption.category.raw_food"),
				IsRawFood = true
			});

			// Add dynamic need categories
			var categories = ConsumptionReflection.GetCategories();
			foreach (var cat in categories) {
				_categories.Add(new CategoryData {
					Category = cat,
					Name = ConsumptionReflection.GetCategoryName(cat),
					IsRawFood = false
				});
			}

			// Add per-race master toggles at the bottom
			var races = ConsumptionReflection.GetAllRevealedRaces();
			foreach (var race in races) {
				_categories.Add(new CategoryData {
					Name = ConsumptionReflection.GetRaceName(race),
					IsRace = true,
					Race = race
				});
			}
		}

		private void RefreshItems(CategoryData category) {
			_items.Clear();
			_itemNames.Clear();

			if (category.IsRawFood) {
				var foods = ConsumptionReflection.GetRawFoods();
				foreach (var id in foods) {
					_items.Add(id);
					_itemNames.Add(ConsumptionReflection.GetRawFoodName(id));
				}
			} else {
				var needs = ConsumptionReflection.GetNeedsForCategory(category.Category);
				foreach (var need in needs) {
					_items.Add(need);
					_itemNames.Add(ConsumptionReflection.GetNeedName(need));
				}
			}
		}

		private void RefreshRaces(object need) {
			_races.Clear();
			_raceNames.Clear();

			var races = ConsumptionReflection.GetRacesForNeed(need);
			foreach (var race in races) {
				_races.Add(race);
				_raceNames.Add(ConsumptionReflection.GetRaceName(race));
			}
		}
	}
}
