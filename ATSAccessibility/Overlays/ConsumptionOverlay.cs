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

		protected override string OverlayName => "Consumption";
		protected override string EmptyMessage => "No categories available";

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
					Speech.Say("No items");
					return false;
				}
				return true;
			}

			if (Level == 1) {
				if (_currentCategoryIsRawFood)
					return false;

				RefreshRaces(_items[index]);

				if (_races.Count == 0) {
					Speech.Say("No races");
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
					return $"Consumption control blocked by {effects}";
				return "Consumption control blocked";
			}

			if (_categories.Count > 0)
				return $"Consumption. {GetCategoryAnnouncement(0)}";

			return $"Consumption. {EmptyMessage}";
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
				Speech.Say("Blocked");
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
				string status = ConsumptionReflection.GetRaceNeedsStatus(cat.Race);
				setTo = (status == "all prohibited");
				ConsumptionReflection.SetAllNeedsPermissionForRace(cat.Race, setTo);
			} else {
				// Toggle all needs in category: if not all prohibited, prohibit; otherwise permit
				string status = ConsumptionReflection.GetCategoryStatus(cat.Category, false);
				setTo = (status == "all prohibited");
				ConsumptionReflection.SetAllNeedsPermissionForCategory(cat.Category, setTo);
			}

			SoundManager.PlayButtonClick();
			Speech.Say(setTo ? "Permitted" : "Prohibited");
		}

		private void ToggleItem() {
			if (_items.Count == 0) return;

			if (_isBlocked) {
				Speech.Say("Blocked");
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
				string status = ConsumptionReflection.GetNeedStatus(need);
				setTo = (status == "all prohibited");
				ConsumptionReflection.SetNeedBlanketPermission(need, setTo);
			}

			SoundManager.PlayButtonClick();
			Speech.Say(setTo ? "Permitted" : "Prohibited");
		}

		private void ToggleRace() {
			if (_races.Count == 0 || _items.Count == 0) return;

			if (_isBlocked) {
				Speech.Say("Blocked");
				SoundManager.PlayFailed();
				return;
			}

			var race = _races[CurrentIndex];
			var need = _items[_indices[1]];
			bool setTo = !ConsumptionReflection.IsNeedPermittedForRace(race, need);
			ConsumptionReflection.SetNeedPermissionForRace(race, need, setTo);

			SoundManager.PlayButtonClick();
			Speech.Say(setTo ? "Permitted" : "Prohibited");
		}

		// ========================================
		// ANNOUNCEMENTS
		// ========================================

		private string GetCategoryAnnouncement(int index) {
			if (index < 0 || index >= _categories.Count) return "";
			var cat = _categories[index];

			if (cat.IsRace) {
				string status = ConsumptionReflection.GetRaceNeedsStatus(cat.Race);
				return $"{cat.Name}, {status}";
			}

			string catStatus = ConsumptionReflection.GetCategoryStatus(cat.Category, cat.IsRawFood);
			return $"{cat.Name}, {catStatus}";
		}

		private string GetItemAnnouncement(int index) {
			if (index < 0 || index >= _items.Count) return "";

			string name = (index < _itemNames.Count) ? _itemNames[index] : "Unknown";

			if (_currentCategoryIsRawFood) {
				string id = _items[index] as string;
				bool permitted = (id != null) && ConsumptionReflection.IsRawFoodPermitted(id);
				return $"{name}, {(permitted ? "permitted" : "prohibited")}";
			} else {
				var need = _items[index];
				string status = ConsumptionReflection.GetNeedStatus(need);
				return $"{name}, {status}";
			}
		}

		private string GetRaceAnnouncement(int index) {
			if (index < 0 || index >= _races.Count || _items.Count == 0) return "";

			string name = (index < _raceNames.Count) ? _raceNames[index] : "Unknown";
			var race = _races[index];
			var need = _items[_indices[1]];

			bool permitted = ConsumptionReflection.IsNeedPermittedForRace(race, need);
			var (_, max) = ConsumptionReflection.GetResolveImpact(race, need);

			string impact = permitted ? $"+{max} resolve bonus" : $"{max} rationing penalty";
			return $"{name}, {(permitted ? "permitted" : "prohibited")}, {impact}";
		}

		// ========================================
		// DATA REFRESH
		// ========================================

		private void RefreshCategories() {
			_categories.Clear();

			// First category is always "Raw Food"
			_categories.Add(new CategoryData {
				Category = null,
				Name = "Raw Food",
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
