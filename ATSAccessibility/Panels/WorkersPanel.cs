using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Panels {
	/// <summary>
	/// Virtual speech-only panel for navigating worker profession counts by race.
	/// Level 0 = categories ("All", then each present race).
	/// Level 1 = each assigned profession with its worker count, sorted by count descending then name ascending.
	/// </summary>
	public class WorkersPanel: MenuBase {
		private class ProfessionItem {
			public string Name { get; set; }
			public int Count { get; set; }
		}

		private class WorkerCategory {
			public string Name { get; set; }
			public string RaceName { get; set; }  // null for "All"
			public List<ProfessionItem> Items { get; set; } = new List<ProfessionItem>();
		}

		private List<WorkerCategory> _categories = new List<WorkerCategory>();

		// Cached reflection metadata
		private static PropertyInfo _villRacesProperty;
		private static FieldInfo _professionModelField;
		private static FieldInfo _professionDisplayNameField;
		private static bool _typesCached;

		// Compatibility aliases for readability
		private int _currentCategoryIndex { get => _indices[0]; set => _indices[0] = value; }
		private int _currentItemIndex { get => _indices[1]; set => _indices[1] = value; }

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "Workers panel";
		protected override string EmptyMessage => "No workers present";

		protected override int GetItemCount() {
			if (Level == 0)
				return _categories.Count;

			if (_currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count)
				return _categories[_currentCategoryIndex].Items.Count;

			return 0;
		}

		protected override string GetLabel(int index) {
			if (Level == 0) {
				if (index >= 0 && index < _categories.Count)
					return _categories[index].Name;
				return null;
			}

			if (_currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count) {
				var items = _categories[_currentCategoryIndex].Items;
				if (index >= 0 && index < items.Count)
					return $"{items[index].Name}, {items[index].Count}";
			}
			return null;
		}

		protected override string GetSearchName(int index) {
			if (Level == 0) {
				if (index >= 0 && index < _categories.Count)
					return _categories[index].Name;
				return null;
			}

			if (_currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count) {
				var items = _categories[_currentCategoryIndex].Items;
				if (index >= 0 && index < items.Count)
					return items[index].Name;
			}
			return null;
		}

		protected override void RefreshData() {
			_categories.Clear();
			EnsureTypes();

			var races = StatsReader.GetPresentRaces();
			if (races.Count == 0) return;

			// Build per-race profession counts
			var raceData = new List<(string raceName, string displayName, Dictionary<string, int> professions)>();
			var allProfessions = new Dictionary<string, int>();

			foreach (var raceName in races) {
				var villagers = GetVillagersForRace(raceName);
				var professions = new Dictionary<string, int>();

				foreach (var villager in villagers) {
					string profession = GetVillagerProfession(villager);
					if (string.IsNullOrEmpty(profession)) continue;

					if (professions.ContainsKey(profession))
						professions[profession]++;
					else
						professions[profession] = 1;

					if (allProfessions.ContainsKey(profession))
						allProfessions[profession]++;
					else
						allProfessions[profession] = 1;
				}

				string displayName = GetRaceDisplayName(raceName);
				raceData.Add((raceName, displayName, professions));
			}

			// Build "All" category
			if (allProfessions.Count > 0) {
				var allCategory = new WorkerCategory {
					Name = "All",
					RaceName = null,
					Items = BuildSortedItems(allProfessions)
				};
				_categories.Add(allCategory);
			}

			// Build per-race categories
			foreach (var (raceName, displayName, professions) in raceData) {
				if (professions.Count == 0) continue;

				var category = new WorkerCategory {
					Name = displayName,
					RaceName = raceName,
					Items = BuildSortedItems(professions)
				};
				_categories.Add(category);
			}

			Debug.Log($"[ATSAccessibility] Workers panel refreshed: {_categories.Count} categories");
		}

		protected override EnterAction OnEnter(int index) {
			if (Level == 0) {
				if (_currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count
					&& _categories[_currentCategoryIndex].Items.Count > 0)
					return EnterAction.DrillDown;

				Speech.Say("No items in this category");
				return EnterAction.None;
			}
			return EnterAction.None;
		}

		protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (keyCode == KeyCode.LeftArrow && Level == 0)
				return false; // Pass to InfoPanelMenu to close child panel
			return null;
		}

		protected override string GetOpenAnnouncement() {
			if (_categories.Count == 0) return EmptyMessage;
			return GetLabel(0);
		}

		protected override void OnClosed() {
			_categories.Clear();
			InputBlocker.BlockCancelOnce = true;
			Speech.Say($"{OverlayName} closed");
		}

		/// <summary>
		/// Bridge for InfoPanelMenu which calls ProcessKeyEvent(KeyCode).
		/// </summary>
		public bool ProcessKeyEvent(KeyCode keyCode) =>
			ProcessKey(keyCode, default(KeyboardManager.KeyModifiers));

		// ========================================
		// HELPERS
		// ========================================

		private static List<ProfessionItem> BuildSortedItems(Dictionary<string, int> professions) {
			return professions
				.Select(kvp => new ProfessionItem { Name = kvp.Key, Count = kvp.Value })
				.OrderByDescending(p => p.Count)
				.ThenBy(p => p.Name)
				.ToList();
		}

		// ========================================
		// REFLECTION HELPERS
		// ========================================

		private static void EnsureTypes() {
			if (_typesCached) return;

			var villService = GameReflection.GetVillagersService();
			if (villService != null) {
				var type = villService.GetType();
				_villRacesProperty = type.GetProperty("Races", GameReflection.PublicInstance);

				// Cache profession field lookups from a sample villager
				if (_villRacesProperty != null) {
					try {
						var racesDict = _villRacesProperty.GetValue(villService);
						if (racesDict != null) {
							var enumerator = (racesDict as System.Collections.IEnumerable)?.GetEnumerator();
							if (enumerator != null && enumerator.MoveNext()) {
								var kvp = enumerator.Current;
								var valueProp = kvp.GetType().GetProperty("Value");
								var list = valueProp?.GetValue(kvp) as System.Collections.IEnumerable;
								if (list != null) {
									foreach (var villager in list) {
										_professionModelField = villager.GetType().GetField("professionModel", GameReflection.PublicInstance);
										if (_professionModelField != null) {
											var model = _professionModelField.GetValue(villager);
											if (model != null)
												_professionDisplayNameField = model.GetType().GetField("displayName", GameReflection.PublicInstance);
										}
										break;  // Only need one sample
									}
								}
							}
						}
					} catch (Exception ex) {
						Debug.LogWarning($"[ATSAccessibility] WorkersPanel profession field caching failed: {ex.Message}");
					}
				}
			}

			_typesCached = true;
		}

		private static System.Collections.IEnumerable GetVillagersForRace(string raceName) {
			try {
				var villService = GameReflection.GetVillagersService();
				if (villService == null || _villRacesProperty == null) return Array.Empty<object>();

				var racesDict = _villRacesProperty.GetValue(villService);
				if (racesDict == null) return Array.Empty<object>();

				var indexer = racesDict.GetType().GetProperty("Item");
				var villagerList = indexer?.GetValue(racesDict, new object[] { raceName }) as System.Collections.IEnumerable;
				return villagerList ?? (System.Collections.IEnumerable)Array.Empty<object>();
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] WorkersPanel.GetVillagersForRace failed: {ex.Message}");
			}
			return Array.Empty<object>();
		}

		private static string GetVillagerProfession(object villager) {
			try {
				if (_professionModelField == null)
					_professionModelField = villager.GetType().GetField("professionModel", GameReflection.PublicInstance);

				var professionModel = _professionModelField?.GetValue(villager);
				if (professionModel != null) {
					if (_professionDisplayNameField == null)
						_professionDisplayNameField = professionModel.GetType().GetField("displayName", GameReflection.PublicInstance);

					var locaText = _professionDisplayNameField?.GetValue(professionModel);
					return GameReflection.GetLocaText(locaText) ?? "Worker";
				}
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] WorkersPanel.GetVillagerProfession failed: {ex.Message}"); }
			return "Worker";
		}

		private static string GetRaceDisplayName(string raceName) {
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
			} catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] WorkersPanel.GetRaceDisplayName failed: {ex.Message}"); }
			return raceName;
		}
	}
}
