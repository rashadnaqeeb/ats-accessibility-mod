using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Panels {
	/// <summary>
	/// Virtual speech-only panel for navigating villager information.
	///
	/// Navigation model (3 levels via MenuBase):
	/// - Level 0 (Categories): Shared Needs (if any), then each race - Up/Down to navigate, Enter/Right to enter details
	/// - Level 1 (Details): Resolve, Needs, Favoring - Up/Down to navigate, Left to return
	/// - Level 2 (Sub-details): Resolve breakdown - Right to expand
	/// </summary>
	public class VillagersPanel: MenuBase {
		// ========================================
		// DETAIL ITEM TYPES
		// ========================================

		private enum DetailType {
			Resolve,
			Need,
			Favoring
		}

		private class DetailItem {
			public DetailType Type { get; set; }
			public string Label { get; set; }
			public List<string> SubDetails { get; set; } = new List<string>();
		}

		private class RaceCategory {
			public string RaceName { get; set; }
			public string DisplayName { get; set; }
			public int Population { get; set; }
			public int FreeWorkers { get; set; }
			public int Homeless { get; set; }
			public List<DetailItem> Details { get; set; } = new List<DetailItem>();
		}

		// ========================================
		// STATE
		// ========================================

		private List<RaceCategory> _categories = new List<RaceCategory>();

		// Cached reflection metadata
		private static MethodInfo _villGetDefaultProfessionAmountMethod;
		private static MethodInfo _villGetHomelessAmountMethod;
		private static bool _typesCached;

		// ========================================
		// PUBLIC API
		// ========================================

		// ========================================
		// MENUBASE ABSTRACT IMPLEMENTATIONS
		// ========================================

		protected override string OverlayName => "Villagers";

		protected override string EmptyMessage => "No villagers present";

		protected override int GetItemCount() {
			switch (Level) {
				case 2: {
						if (_indices[0] >= _categories.Count) return 0;
						var category = _categories[_indices[0]];
						if (_indices[1] >= category.Details.Count) return 0;
						return category.Details[_indices[1]].SubDetails.Count;
					}
				case 1: {
						if (_indices[0] >= _categories.Count) return 0;
						return _categories[_indices[0]].Details.Count;
					}
				default:
					return _categories.Count;
			}
		}

		protected override string GetLabel(int index) {
			switch (Level) {
				case 2: {
						if (_indices[0] >= _categories.Count) return null;
						var category = _categories[_indices[0]];
						if (_indices[1] >= category.Details.Count) return null;
						var detail = category.Details[_indices[1]];
						return index < detail.SubDetails.Count ? detail.SubDetails[index] : null;
					}
				case 1: {
						if (_indices[0] >= _categories.Count) return null;
						var category = _categories[_indices[0]];
						return index < category.Details.Count ? category.Details[index].Label : null;
					}
				default:
					return index < _categories.Count ? _categories[index].DisplayName : null;
			}
		}

		protected override void RefreshData() {
			_categories.Clear();
			EnsureTypes();

			var races = StatsReader.GetPresentRaces();

			// Track needs per race for shared needs detection
			var needRaces = new Dictionary<string, List<SharedNeedRaceInfo>>();
			var needOrder = new List<string>();

			foreach (var raceName in races) {
				var category = new RaceCategory {
					RaceName = raceName,
					DisplayName = GetRaceDisplayName(raceName),
					Population = StatsReader.GetRaceCount(raceName),
					FreeWorkers = GetFreeWorkers(raceName),
					Homeless = GetHomeless(raceName)
				};

				var needs = GetRaceNeeds(raceName);
				BuildRaceDetails(category, raceName, needs);
				_categories.Add(category);

				// Collect needs for shared detection
				foreach (var need in needs) {
					if (!needRaces.ContainsKey(need.name)) {
						needRaces[need.name] = new List<SharedNeedRaceInfo>();
						needOrder.Add(need.name);
					}
					needRaces[need.name].Add(new SharedNeedRaceInfo {
						RaceName = raceName,
						DisplayName = category.DisplayName,
						NeedModel = need.model,
						Population = category.Population
					});
				}
			}

			// Build shared needs category (needs appearing in 2+ races)
			BuildSharedNeedsCategory(needRaces, needOrder);

			Debug.Log($"[ATSAccessibility] Villagers panel refreshed: {_categories.Count} categories");
		}

		protected override EnterAction OnEnter(int index) {
			switch (Level) {
				case 0: {
						var cat = _categories[index];
						if (cat.Details.Count > 0)
							return EnterAction.DrillDown;
						Speech.Say("No details for this race");
						return EnterAction.None;
					}
				case 1: {
						var category = _categories[_indices[0]];
						if (index < category.Details.Count) {
							var detail = category.Details[index];
							if (detail.Type == DetailType.Favoring)
								return EnterAction.Action;
							if (detail.SubDetails.Count > 0)
								return EnterAction.DrillDown;
						}
						// Re-announce for items with no sub-details and no action
						AnnounceDetail();
						return EnterAction.None;
					}
				default:
					// Level 2: re-announce at deepest level
					AnnounceSubDetail();
					return EnterAction.None;
			}
		}

		// ========================================
		// MENUBASE VIRTUAL OVERRIDES
		// ========================================

		protected override void OnAction(int index) {
			if (Level == 1) {
				var category = _categories[_indices[0]];
				if (index < category.Details.Count) {
					var detail = category.Details[index];
					if (detail.Type == DetailType.Favoring) {
						PerformFavoringAction();
					}
				}
			}
		}

		protected override bool CanDrillDown(int index) {
			switch (Level) {
				case 0: {
						if (index < _categories.Count)
							return _categories[index].Details.Count > 0;
						return false;
					}
				case 1: {
						var category = _categories[_indices[0]];
						if (index < category.Details.Count)
							return category.Details[index].SubDetails.Count > 0;
						return false;
					}
				default:
					return false;
			}
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (keyCode == KeyCode.LeftArrow && Level == 0)
				return false; // Pass to parent (InfoPanelMenu) to close this panel

			if (keyCode == KeyCode.Escape)
				return false; // Pass to parent to handle panel closing

			return null;
		}

		protected override void OnClosed() {
			_categories.Clear();
		}

		protected override void AnnounceCurrentItem() {
			switch (Level) {
				case 0:
					AnnounceCategory();
					break;
				case 1:
					AnnounceDetail();
					break;
				case 2:
					AnnounceSubDetail();
					break;
			}
		}

		protected override string GetOpenAnnouncement() {
			if (_categories.Count == 0)
				return EmptyMessage;

			return BuildCategoryAnnouncement(0);
		}

		// ========================================
		// SEARCH OVERRIDES
		// ========================================

		protected override int SearchItemCount {
			get {
				switch (Level) {
					case 2: {
							if (_indices[0] >= _categories.Count) return 0;
							var category = _categories[_indices[0]];
							if (_indices[1] >= category.Details.Count) return 0;
							return category.Details[_indices[1]].SubDetails.Count;
						}
					case 1: {
							if (_indices[0] >= _categories.Count) return 0;
							return _categories[_indices[0]].Details.Count;
						}
					default:
						return _categories.Count;
				}
			}
		}

		protected override string GetSearchName(int index) {
			switch (Level) {
				case 2: {
						if (_indices[0] >= _categories.Count) return null;
						var category = _categories[_indices[0]];
						if (_indices[1] >= category.Details.Count) return null;
						var detail = category.Details[_indices[1]];
						return index < detail.SubDetails.Count ? detail.SubDetails[index] : null;
					}
				case 1: {
						if (_indices[0] >= _categories.Count) return null;
						var category = _categories[_indices[0]];
						return index < category.Details.Count ? category.Details[index].Label : null;
					}
				default:
					return index < _categories.Count ? _categories[index].DisplayName : null;
			}
		}

		// ========================================
		// FAVORING
		// ========================================

		private void PerformFavoringAction() {
			if (_indices[0] >= _categories.Count) return;

			var category = _categories[_indices[0]];
			string raceName = category.RaceName;
			if (raceName == null) return;  // Shared needs category has no favoring

			// If this race is already favored, toggle it off
			if (GameReflection.IsFavored(raceName)) {
				if (GameReflection.StopFavoringRace()) {
					Speech.Say($"{category.DisplayName} no longer favored");
					UpdateFavoringLabel();
				} else {
					Speech.Say("Failed to stop favoring");
				}
				return;
			}

			// Check cooldown before stopping - game greys out the button during cooldown,
			// but we need to tell the player verbally and avoid cancelling existing favoring
			if (GameReflection.IsFavoringOnCooldown()) {
				float cooldown = GameReflection.GetFavorCooldownLeft();
				Speech.Say($"Favoring on cooldown, {Mathf.CeilToInt(cooldown)} seconds remaining");
				return;
			}

			// Stop any existing favoring on a different race first
			GameReflection.StopFavoringRace();

			// Check if there are other races to penalize (need at least 2 races with villagers)
			int racesWithVillagers = 0;
			foreach (var cat in _categories) {
				if (cat.Population > 0) racesWithVillagers++;
			}
			if (racesWithVillagers < 2) {
				Speech.Say("Need at least two races with villagers to use favoring");
				return;
			}

			// Check if this race has any villagers
			if (category.Population == 0) {
				Speech.Say("Cannot favor a race with no villagers");
				return;
			}

			// Start favoring
			if (GameReflection.FavorRace(raceName)) {
				PlayFavoringSound(raceName);
				Speech.Say($"{category.DisplayName} now favored. Other races penalized");
				UpdateFavoringLabel();
			} else {
				Speech.Say("Failed to favor race");
			}
		}

		private void UpdateFavoringLabel() {
			// Update the Favoring label for all categories to reflect new state
			foreach (var category in _categories) {
				var favoringItem = category.Details.Find(d => d.Type == DetailType.Favoring);
				if (favoringItem != null) {
					favoringItem.Label = GetFavoringLabel(category.RaceName);
				}
			}
		}

		/// <summary>
		/// Play the race-specific favoring sound (matches game's FavoringButton behavior).
		/// </summary>
		private void PlayFavoringSound(string raceName) {
			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return;

				var getRaceMethod = settings.GetType().GetMethod("GetRace", GameReflection.PublicInstance);
				var raceModel = getRaceMethod?.Invoke(settings, new object[] { raceName });
				if (raceModel == null) return;

				var soundField = raceModel.GetType().GetField("favoringStartSound", GameReflection.PublicInstance);
				var soundRef = soundField?.GetValue(raceModel);
				if (soundRef == null) return;

				var getNextMethod = soundRef.GetType().GetMethod("GetNext", GameReflection.PublicInstance);
				var soundModel = getNextMethod?.Invoke(soundRef, null);
				SoundManager.PlaySoundEffect(soundModel);
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] PlayFavoringSound failed: {ex.Message}");
			}
		}

		// ========================================
		// ANNOUNCEMENTS
		// ========================================

		private string BuildCategoryAnnouncement(int index) {
			if (index >= _categories.Count) return null;

			var category = _categories[index];

			if (category.RaceName == null) {
				// Shared needs category - just the name
				return category.DisplayName;
			}

			string favoredStatus = GameReflection.IsFavored(category.RaceName) ? ", favored" : "";
			return $"{category.DisplayName}{favoredStatus}. {category.Population} villagers, {category.FreeWorkers} free, {category.Homeless} homeless";
		}

		private void AnnounceCategory() {
			string message = BuildCategoryAnnouncement(CurrentIndex);
			if (message != null) {
				Speech.Say(message);
				Debug.Log($"[ATSAccessibility] Villagers category: {_categories[CurrentIndex].DisplayName}");
			}
		}

		private void AnnounceDetail() {
			if (_indices[0] >= _categories.Count) return;
			var category = _categories[_indices[0]];
			int detailIndex = Level == 1 ? CurrentIndex : _indices[1];
			if (detailIndex >= category.Details.Count) return;

			var detail = category.Details[detailIndex];
			string message = detail.Label;

			// Add type-specific suffix if expandable
			if (detail.Type == DetailType.Resolve && detail.SubDetails.Count > 0) {
				message += ". Press right for breakdown";
			}

			Speech.Say(message);
			Debug.Log($"[ATSAccessibility] Villagers detail: {message}");
		}

		private void AnnounceSubDetail() {
			if (_indices[0] >= _categories.Count) return;
			var category = _categories[_indices[0]];
			if (_indices[1] >= category.Details.Count) return;
			var detail = category.Details[_indices[1]];
			if (CurrentIndex >= detail.SubDetails.Count) return;

			string message = detail.SubDetails[CurrentIndex];
			Speech.Say(message);
			Debug.Log($"[ATSAccessibility] Villagers sub-detail: {message}");
		}

		// ========================================
		// DATA BUILDING
		// ========================================

		private void BuildSharedNeedsCategory(Dictionary<string, List<SharedNeedRaceInfo>> needRaces, List<string> needOrder) {
			var sharedCategory = new RaceCategory {
				RaceName = null,
				DisplayName = "Shared Needs",
				Population = 0,
				FreeWorkers = 0,
				Homeless = 0
			};

			bool firstNeed = true;
			foreach (var needName in needOrder) {
				var races = needRaces[needName];
				if (races.Count < 2) continue;

				int totalSatisfied = 0;
				int totalPopulation = 0;
				var raceNames = new List<string>();

				foreach (var info in races) {
					totalSatisfied += GetNeedSatisfiedCount(info.RaceName, info.NeedModel);
					totalPopulation += info.Population;
					raceNames.Add(info.DisplayName.ToLowerInvariant());
				}

				string prefix = firstNeed ? "Needs: " : "";
				firstNeed = false;
				string racesStr = string.Join(",", raceNames);

				sharedCategory.Details.Add(new DetailItem {
					Type = DetailType.Need,
					Label = $"{prefix}{needName}, {racesStr}, {totalSatisfied} of {totalPopulation} satisfied"
				});
			}

			if (sharedCategory.Details.Count > 0) {
				_categories.Insert(0, sharedCategory);
			}
		}

		private void BuildRaceDetails(RaceCategory category, string raceName, List<NeedInfo> needs) {
			// 1. Resolve with breakdown
			var (resolve, threshold, settling) = StatsReader.GetResolveSummary(raceName);
			var resolveBreakdown = StatsReader.GetResolveBreakdown(raceName);

			category.Details.Add(new DetailItem {
				Type = DetailType.Resolve,
				Label = $"Resolve: {Mathf.FloorToInt(resolve)} of {threshold}, settling to {settling}",
				SubDetails = resolveBreakdown
			});

			// 2. Needs (each need as separate item, first one gets "Needs:" prefix)
			bool firstNeed = true;
			foreach (var need in needs) {
				string needName = need.name;
				int satisfied = GetNeedSatisfiedCount(raceName, need.model);
				int total = category.Population;

				string prefix = firstNeed ? "Needs: " : "";
				firstNeed = false;

				category.Details.Add(new DetailItem {
					Type = DetailType.Need,
					Label = $"{prefix}{needName}, {satisfied} of {total} satisfied"
				});
			}

			// Note: "Other effects" are already included in the resolve breakdown above

			// 3. Favoring option
			category.Details.Add(new DetailItem {
				Type = DetailType.Favoring,
				Label = GetFavoringLabel(raceName)
			});
		}

		private string GetFavoringLabel(string raceName) {
			if (GameReflection.IsFavored(raceName)) {
				return "Favoring: Active. Press Enter to stop";
			} else if (GameReflection.IsFavoringOnCooldown()) {
				float cooldown = GameReflection.GetFavorCooldownLeft();
				return $"Favoring: On cooldown, {Mathf.CeilToInt(cooldown)} seconds";
			} else {
				return "Favoring: Press Enter to favor this race";
			}
		}

		// ========================================
		// REFLECTION HELPERS
		// ========================================

		private static void EnsureTypes() {
			if (_typesCached) return;

			var villService = GameReflection.GetVillagersService();
			if (villService != null) {
				var type = villService.GetType();
				_villGetDefaultProfessionAmountMethod = type.GetMethod("GetDefaultProfessionAmount",
					GameReflection.PublicInstance, null, new Type[] { typeof(string) }, null);
				_villGetHomelessAmountMethod = type.GetMethod("GetHomelessAmount",
					GameReflection.PublicInstance, null, new Type[] { typeof(string) }, null);
			}

			_typesCached = true;
			Debug.Log("[ATSAccessibility] VillagersPanel types cached");
		}

		private string GetRaceDisplayName(string raceName) {
			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return raceName;

				var getRaceMethod = settings.GetType().GetMethod("GetRace", GameReflection.PublicInstance);
				if (getRaceMethod != null) {
					var raceModel = getRaceMethod.Invoke(settings, new object[] { raceName });
					if (raceModel != null) {
						var displayNameField = raceModel.GetType().GetField("displayName", GameReflection.PublicInstance);
						var locaText = displayNameField?.GetValue(raceModel);
						return GameReflection.GetLocaText(locaText) ?? raceName;
					}
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetRaceDisplayName failed: {ex.Message}"); }
			return raceName;
		}

		private int GetFreeWorkers(string raceName) {
			try {
				var villService = GameReflection.GetVillagersService();
				if (villService != null && _villGetDefaultProfessionAmountMethod != null) {
					return (int)_villGetDefaultProfessionAmountMethod.Invoke(villService, new object[] { raceName });
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetFreeWorkers failed: {ex.Message}"); }
			return 0;
		}

		private int GetHomeless(string raceName) {
			try {
				var villService = GameReflection.GetVillagersService();
				if (villService != null && _villGetHomelessAmountMethod != null) {
					return (int)_villGetHomelessAmountMethod.Invoke(villService, new object[] { raceName });
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] GetHomeless failed: {ex.Message}"); }
			return 0;
		}

		private class NeedInfo {
			public string name;
			public object model;
		}

		private class SharedNeedRaceInfo {
			public string RaceName;
			public string DisplayName;
			public object NeedModel;
			public int Population;
		}

		private List<NeedInfo> GetRaceNeeds(string raceName) {
			var result = new List<NeedInfo>();
			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return result;

				var getRaceMethod = settings.GetType().GetMethod("GetRace", GameReflection.PublicInstance);
				if (getRaceMethod == null) return result;

				var raceModel = getRaceMethod.Invoke(settings, new object[] { raceName });
				if (raceModel == null) return result;

				// Get needs array
				var needsField = raceModel.GetType().GetField("needs", GameReflection.PublicInstance);
				var needsArray = needsField?.GetValue(raceModel) as Array;
				if (needsArray == null) return result;

				foreach (var need in needsArray) {
					if (need == null) continue;

					// Check if visible
					var isVisibleField = need.GetType().GetField("isVisible", GameReflection.PublicInstance);
					bool isVisible = (bool)(isVisibleField?.GetValue(need) ?? true);
					if (!isVisible) continue;

					// Get display name via effect.displayName
					var effectField = need.GetType().GetField("effect", GameReflection.PublicInstance);
					var effect = effectField?.GetValue(need);
					if (effect == null) continue;

					var displayNameField = effect.GetType().GetField("displayName", GameReflection.PublicInstance);
					var locaText = displayNameField?.GetValue(effect);
					string name = GameReflection.GetLocaText(locaText) ?? "Unknown";

					result.Add(new NeedInfo { name = name, model = need });
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetRaceNeeds failed: {ex.Message}");
			}
			return result;
		}

		private int GetNeedSatisfiedCount(string raceName, object needModel) {
			try {
				// Get NeedsService via GameServices
				var gameServices = GameReflection.GetGameServices();
				if (gameServices == null) return 0;

				var needsServiceProp = gameServices.GetType().GetProperty("NeedsService", GameReflection.PublicInstance);
				var needsService = needsServiceProp?.GetValue(gameServices);
				if (needsService == null) return 0;

				// Get RaceModel
				var settings = GameReflection.GetSettings();
				var getRaceMethod = settings?.GetType().GetMethod("GetRace", GameReflection.PublicInstance);
				var raceModel = getRaceMethod?.Invoke(settings, new object[] { raceName });
				if (raceModel == null) return 0;

				// Call CountVillagersWithFulfilled(NeedModel, RaceModel)
				var method = needsService.GetType().GetMethod("CountVillagersWithFulfilled",
					new Type[] { needModel.GetType(), raceModel.GetType() });
				if (method != null) {
					return (int)method.Invoke(needsService, new object[] { needModel, raceModel });
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetNeedSatisfiedCount failed: {ex.Message}");
			}
			return 0;
		}

	}
}
