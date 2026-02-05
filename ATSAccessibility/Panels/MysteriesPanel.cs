using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Panels {
	/// <summary>
	/// Virtual speech panel for settlement modifiers.
	/// Five categories: Positive Mysteries, Negative Mysteries, Effects, Cornerstones, Perks.
	/// Level 0 = categories, Level 1 = items within a category.
	/// Cross-category item navigation flows between categories on Up/Down at boundaries.
	/// </summary>
	public class MysteriesPanel: MenuBase {
		/// <summary>
		/// The type of item in a category, used for formatting announcements.
		/// </summary>
		private enum ItemType {
			Mystery,      // Seasonal effects with active/inactive status
			Effect,       // Biome/difficulty/embark effects
			Cornerstone,  // Active cornerstones
			Perk          // Perks with stacks
		}

		/// <summary>
		/// Represents a single modifier item.
		/// </summary>
		private class MysteryItem {
			public string Name { get; set; }
			public string Description { get; set; }
			public string Season { get; set; }
			public bool IsPositive { get; set; }
			public bool IsActive { get; set; }
			public bool IsConditional { get; set; }
			public string ConditionText { get; set; }
			public ItemType Type { get; set; } = ItemType.Mystery;
			public int Stacks { get; set; } = 1;  // For perks
		}

		/// <summary>
		/// Represents a category in the left panel.
		/// </summary>
		private class Category {
			public string Name { get; set; }
			public ItemType Type { get; set; } = ItemType.Mystery;
			public List<MysteryItem> Items { get; set; } = new List<MysteryItem>();
		}

		private List<Category> _categories = new List<Category>();

		// Cached reflection for SeasonalEffectState fields (these are public fields, not properties!)
		private static FieldInfo _sesModelField = null;
		private static FieldInfo _sesSeasonField = null;
		private static FieldInfo _sesIsActiveField = null;
		private static FieldInfo _sesIsPositiveField = null;
		private static bool _sesFieldsCached = false;

		// Cached reflection for EffectModel DisplayName/Description (for modifiers)
		private static PropertyInfo _effectDisplayNameProperty = null;
		private static PropertyInfo _effectDescriptionProperty = null;
		private static PropertyInfo _effectIsPositiveProperty = null;
		private static bool _modelFieldsCached = false;

		// Cached reflection for NeedCategoryCondition fields (for conditional mysteries)
		private static FieldInfo _conditionCategoryField = null;
		private static FieldInfo _conditionAmountField = null;
		private static FieldInfo _categoryDisplayNameField = null;
		private static bool _conditionFieldsCached = false;

		// Compatibility aliases for readability
		private int _currentCategoryIndex { get => _indices[0]; set => _indices[0] = value; }
		private int _currentItemIndex { get => _indices[1]; set => _indices[1] = value; }

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "Modifiers";
		protected override string EmptyMessage => "No modifiers active";

		protected override int GetItemCount() {
			if (Level == 0)
				return _categories.Count;

			if (_currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count)
				return _categories[_currentCategoryIndex].Items.Count;

			return 0;
		}

		protected override string GetLabel(int index) {
			if (Level == 0) {
				if (index >= 0 && index < _categories.Count) {
					var cat = _categories[index];
					return $"{cat.Name}, {cat.Items.Count}";
				}
				return null;
			}

			return BuildItemAnnouncement(index);
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

			// Build exclusion sets for perks category
			var mysteryNames = new HashSet<string>();
			var cornerstoneNames = new HashSet<string>();
			var effectNames = new HashSet<string>();

			// Category 1-2: Get all mysteries split by positive/negative
			// Also collects mystery model names AND wrapped effect names for exclusion
			var (positiveMysteries, negativeMysteries) = GetMysteriesByType(mysteryNames);

			_categories.Add(new Category {
				Name = "Positive Mysteries",
				Type = ItemType.Mystery,
				Items = positiveMysteries
			});

			_categories.Add(new Category {
				Name = "Negative Mysteries",
				Type = ItemType.Mystery,
				Items = negativeMysteries
			});

			// Category 3: Effects (biome, difficulty, embark, events)
			// Excludes IsPerk=true effects (those show under Perks)
			// Also collects effect names for exclusion from perks
			_categories.Add(new Category {
				Name = "Effects",
				Type = ItemType.Effect,
				Items = GetActiveEffects(effectNames)
			});

			// Category 4: Cornerstones
			// Also collect cornerstone names for exclusion from perks
			var cornerstones = GameReflection.GetActiveCornerstones();
			if (cornerstones != null) {
				foreach (var name in cornerstones) {
					if (!string.IsNullOrEmpty(name))
						cornerstoneNames.Add(name);
				}
			}

			_categories.Add(new Category {
				Name = "Cornerstones",
				Type = ItemType.Cornerstone,
				Items = GetCornerstoneItems(cornerstones)
			});

			// Category 5: Perks (exclude mysteries + cornerstones + effects)
			_categories.Add(new Category {
				Name = "Perks",
				Type = ItemType.Perk,
				Items = GetActivePerks(mysteryNames, cornerstoneNames, effectNames)
			});

			Debug.Log($"[ATSAccessibility] Modifiers panel refreshed: PosMyst={_categories[0].Items.Count}, NegMyst={_categories[1].Items.Count}, Effects={_categories[2].Items.Count}, Cornerstones={_categories[3].Items.Count}, Perks={_categories[4].Items.Count}");
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

			// Cross-category item navigation at Level 1 (yield to search when active)
			if (Level == 1 && !_search.IsSearchActive && (keyCode == KeyCode.UpArrow || keyCode == KeyCode.DownArrow)) {
				NavigateItemAcrossCategories(keyCode == KeyCode.DownArrow ? 1 : -1);
				return true;
			}

			return null;
		}

		protected override string GetOpenAnnouncement() {
			// Check if any category has items
			bool hasAnyItems = false;
			foreach (var cat in _categories) {
				if (cat.Items.Count > 0) {
					hasAnyItems = true;
					break;
				}
			}
			if (!hasAnyItems) return EmptyMessage;

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
		// CROSS-CATEGORY ITEM NAVIGATION
		// ========================================

		/// <summary>
		/// Navigate items, flowing into the next/previous category at boundaries.
		/// Announces category name when crossing into a new category.
		/// </summary>
		private void NavigateItemAcrossCategories(int direction) {
			int itemCount = GetItemCount();
			if (itemCount == 0) return;

			int newIndex = _currentItemIndex + direction;

			if (newIndex >= itemCount) {
				// Past end of category - find next non-empty category
				int originalCategory = _currentCategoryIndex;
				do {
					_currentCategoryIndex = (_currentCategoryIndex + 1) % _categories.Count;
				} while (_categories[_currentCategoryIndex].Items.Count == 0 && _currentCategoryIndex != originalCategory);

				if (_categories[_currentCategoryIndex].Items.Count == 0) return;
				_currentItemIndex = 0;
				AnnounceCategoryAndItem();
			} else if (newIndex < 0) {
				// Before start of category - find previous non-empty category
				int originalCategory = _currentCategoryIndex;
				do {
					_currentCategoryIndex = (_currentCategoryIndex - 1 + _categories.Count) % _categories.Count;
				} while (_categories[_currentCategoryIndex].Items.Count == 0 && _currentCategoryIndex != originalCategory);

				if (_categories[_currentCategoryIndex].Items.Count == 0) return;
				_currentItemIndex = _categories[_currentCategoryIndex].Items.Count - 1;
				AnnounceCategoryAndItem();
			} else {
				_currentItemIndex = newIndex;
				AnnounceCurrentItem();
			}
		}

		/// <summary>
		/// Announce category name followed by current item when crossing category boundaries.
		/// </summary>
		private void AnnounceCategoryAndItem() {
			if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count) return;

			string categoryName = _categories[_currentCategoryIndex].Name;
			string itemText = BuildItemAnnouncement(_currentItemIndex);

			if (itemText != null)
				Speech.Say($"{categoryName}. {itemText}");
			else
				Speech.Say($"{categoryName}, empty");
		}

		// ========================================
		// ITEM ANNOUNCEMENT
		// ========================================

		/// <summary>
		/// Build the announcement text for an item at the given index.
		/// Returns null if the index is out of range.
		/// </summary>
		private string BuildItemAnnouncement(int itemIndex) {
			if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count) return null;

			var category = _categories[_currentCategoryIndex];
			if (itemIndex < 0 || itemIndex >= category.Items.Count) return null;

			var item = category.Items[itemIndex];
			var parts = new List<string>();

			switch (item.Type) {
				case ItemType.Mystery:
					// Mysteries format: "Active/Inactive, Name, Season. Description Condition"
					string status = item.IsActive ? "Active" : "Inactive";
					parts.Add($"{status}, {item.Name}, {item.Season}.");

					if (!string.IsNullOrEmpty(item.Description))
						parts.Add(item.Description);

					// Show condition text if present (hostility level, need categories, etc.)
					if (!string.IsNullOrEmpty(item.ConditionText))
						parts.Add(item.ConditionText);
					break;

				case ItemType.Effect:
				case ItemType.Cornerstone:
					// Effects/Cornerstones format: "Name. Description"
					parts.Add(item.Name + ".");
					if (!string.IsNullOrEmpty(item.Description))
						parts.Add(item.Description);
					break;

				case ItemType.Perk:
					// Perks format: "Name x3. Description" or "Name. Description" if stacks=1
					if (item.Stacks > 1)
						parts.Add($"{item.Name} x{item.Stacks}.");
					else
						parts.Add(item.Name + ".");

					if (!string.IsNullOrEmpty(item.Description))
						parts.Add(item.Description);
					break;
			}

			return string.Join(" ", parts);
		}

		// ========================================
		// MYSTERY DATA LOADING
		// ========================================

		/// <summary>
		/// Get all mysteries split by positive/negative.
		/// Also collects mystery model names AND wrapped effect names in outMysteryNames for exclusion from perks.
		/// </summary>
		private (List<MysteryItem> positive, List<MysteryItem> negative) GetMysteriesByType(HashSet<string> outMysteryNames) {
			var positive = new List<MysteryItem>();
			var negative = new List<MysteryItem>();
			var effectsDict = GameReflection.GetSeasonalEffectsDictionary();

			if (effectsDict == null) {
				Debug.Log("[ATSAccessibility] SeasonalEffects dictionary is null");
				return (positive, negative);
			}

			EnsureSeasonalEffectStateFields();

			foreach (DictionaryEntry entry in effectsDict) {
				var state = entry.Value;
				if (state == null) continue;

				// Collect the model name (internal name) for exclusion from perks
				string modelName = _sesModelField?.GetValue(state)?.ToString();
				if (!string.IsNullOrEmpty(modelName)) {
					outMysteryNames.Add(modelName);

					// Also collect the wrapped effect's internal name
					// This is what actually gets added to PerksService when the mystery is active
					object model = GameReflection.GetSimpleSeasonalEffectModel(modelName);
					if (model == null)
						model = GameReflection.GetConditionalSeasonalEffectModel(modelName);

					if (model != null) {
						string wrappedEffectName = GameReflection.GetSeasonalEffectWrappedEffectName(model);
						if (!string.IsNullOrEmpty(wrappedEffectName))
							outMysteryNames.Add(wrappedEffectName);
					}
				}

				var item = CreateMysteryItem(entry.Key?.ToString(), state);
				if (item == null) continue;

				// Sort by isPositive from the item
				if (item.IsPositive)
					positive.Add(item);
				else
					negative.Add(item);
			}

			return (positive, negative);
		}

		/// <summary>
		/// Get active effects (biome, difficulty, embark, events) via EffectsService.GetAllConditions().
		/// Excludes effects where IsPerk=true (those show under Perks category instead).
		/// Also collects internal effect names in outEffectNames for exclusion from perks.
		/// </summary>
		private List<MysteryItem> GetActiveEffects(HashSet<string> outEffectNames) {
			var items = new List<MysteryItem>();

			var conditions = GameReflection.GetAllConditions();
			if (conditions == null) return items;

			EnsureModelFields();

			// Track effect names to avoid duplicates
			var seenDisplayNames = new HashSet<string>();

			foreach (var effectModel in conditions) {
				if (effectModel == null) continue;

				// Skip effects that are perks - they'll show under Perks category
				if (GameReflection.GetEffectIsPerk(effectModel))
					continue;

				// Collect the internal effect name for exclusion from perks
				string internalName = GameReflection.GetEffectName(effectModel);
				if (!string.IsNullOrEmpty(internalName))
					outEffectNames.Add(internalName);

				var item = CreateEffectItem(effectModel);
				if (item != null && !seenDisplayNames.Contains(item.Name)) {
					seenDisplayNames.Add(item.Name);
					items.Add(item);
				}
			}

			return items;
		}

		/// <summary>
		/// Get active cornerstones as modifier items.
		/// </summary>
		private List<MysteryItem> GetCornerstoneItems(List<string> cornerstones) {
			var items = new List<MysteryItem>();

			if (cornerstones == null) return items;

			EnsureModelFields();

			foreach (var effectName in cornerstones) {
				if (string.IsNullOrEmpty(effectName)) continue;

				var item = CreateCornerstoneItem(effectName);
				if (item != null)
					items.Add(item);
			}

			return items;
		}

		/// <summary>
		/// Get active perks as modifier items, excluding items shown in other categories.
		/// Excludes: hidden perks, mysteries (by model name), cornerstones, effects.
		/// </summary>
		private List<MysteryItem> GetActivePerks(HashSet<string> mysteryNames, HashSet<string> cornerstoneNames, HashSet<string> effectNames) {
			var items = new List<MysteryItem>();

			var sortedPerks = GameReflection.GetSortedPerks();
			if (sortedPerks == null) return items;

			EnsureModelFields();

			foreach (var perkState in sortedPerks) {
				if (perkState == null) continue;

				var (name, stacks, hidden) = GameReflection.GetPerkInfo(perkState);
				if (string.IsNullOrEmpty(name) || hidden) continue;

				// Skip mysteries - they're shown in Positive/Negative Mysteries categories
				if (mysteryNames.Contains(name)) continue;

				// Skip cornerstones - they're shown in Cornerstones category
				if (cornerstoneNames.Contains(name)) continue;

				// Skip effects - they're shown in Effects category
				if (effectNames.Contains(name)) continue;

				var item = CreatePerkItem(name, stacks);
				if (item != null)
					items.Add(item);
			}

			return items;
		}

		// ========================================
		// REFLECTION HELPERS
		// ========================================

		/// <summary>
		/// Cache reflection fields for SeasonalEffectState.
		/// </summary>
		private void EnsureSeasonalEffectStateFields() {
			if (_sesFieldsCached) return;

			var assembly = GameReflection.GameAssembly;
			if (assembly == null) {
				_sesFieldsCached = true;
				return;
			}

			try {
				var sesType = assembly.GetType("Eremite.Model.State.SeasonalEffectState");
				if (sesType != null) {
					// Use GetField instead of GetProperty - these are public fields!
					_sesModelField = sesType.GetField("model",
						BindingFlags.Public | BindingFlags.Instance);
					_sesSeasonField = sesType.GetField("season",
						BindingFlags.Public | BindingFlags.Instance);
					_sesIsActiveField = sesType.GetField("isActive",
						BindingFlags.Public | BindingFlags.Instance);
					_sesIsPositiveField = sesType.GetField("isPositive",
						BindingFlags.Public | BindingFlags.Instance);

					Debug.Log("[ATSAccessibility] Cached SeasonalEffectState fields");
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] SeasonalEffectState field caching failed: {ex.Message}");
			}

			_sesFieldsCached = true;
		}

		/// <summary>
		/// Cache reflection fields for EffectModel (used by modifiers).
		/// Note: Seasonal effect models use runtime type reflection in CreateMysteryItem().
		/// </summary>
		private void EnsureModelFields() {
			if (_modelFieldsCached) return;

			var assembly = GameReflection.GameAssembly;
			if (assembly == null) {
				_modelFieldsCached = true;
				return;
			}

			try {
				// EffectModel for modifiers
				var effectModelType = assembly.GetType("Eremite.Model.EffectModel");
				if (effectModelType != null) {
					_effectDisplayNameProperty = effectModelType.GetProperty("DisplayName",
						BindingFlags.Public | BindingFlags.Instance);
					_effectDescriptionProperty = effectModelType.GetProperty("Description",
						BindingFlags.Public | BindingFlags.Instance);
					_effectIsPositiveProperty = effectModelType.GetProperty("isPositive",
						BindingFlags.Public | BindingFlags.Instance);
				}

				Debug.Log("[ATSAccessibility] Cached EffectModel fields");
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] Model field caching failed: {ex.Message}");
			}

			_modelFieldsCached = true;
		}

		/// <summary>
		/// Get isActive from SeasonalEffectState.
		/// </summary>
		private bool GetIsActive(object state) {
			if (state == null || _sesIsActiveField == null) return false;

			try {
				return (bool)(_sesIsActiveField.GetValue(state) ?? false);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Create a MysteryItem from a SeasonalEffectState.
		/// </summary>
		private MysteryItem CreateMysteryItem(string key, object state) {
			if (state == null) return null;

			try {
				// Get model name and other state fields (using field access, not property access)
				string modelName = _sesModelField?.GetValue(state)?.ToString() ?? key;
				object seasonEnum = _sesSeasonField?.GetValue(state);
				bool isPositive = (bool)(_sesIsPositiveField?.GetValue(state) ?? false);
				bool isActive = GetIsActive(state);

				// Convert season enum to string
				string season = seasonEnum?.ToString() ?? "";

				// Try simple model first, then conditional
				object model = GameReflection.GetSimpleSeasonalEffectModel(modelName);
				bool isConditional = false;
				string conditionText = "";

				if (model == null) {
					model = GameReflection.GetConditionalSeasonalEffectModel(modelName);
					if (model != null)
						isConditional = true;
				}

				// Get condition text for both simple and conditional models
				// (both can have hostility level requirements)
				if (model != null) {
					conditionText = GetConditionText(model);
				}

				string displayName = modelName;
				string description = "";

				if (model != null) {
					// Get DisplayName and Description from the model's actual runtime type
					// (works for both SimpleSeasonalEffectModel and ConditionalSeasonalEffectModel)
					var modelType = model.GetType();

					var displayNameProp = modelType.GetProperty("DisplayName",
						BindingFlags.Public | BindingFlags.Instance);
					var nameObj = displayNameProp?.GetValue(model);
					if (nameObj != null)
						displayName = nameObj.ToString();

					var descriptionProp = modelType.GetProperty("Description",
						BindingFlags.Public | BindingFlags.Instance);
					var descObj = descriptionProp?.GetValue(model);
					if (descObj != null)
						description = descObj.ToString();
				}

				return new MysteryItem {
					Name = displayName,
					Description = description,
					Season = season,
					IsPositive = isPositive,
					IsActive = isActive,
					IsConditional = isConditional,
					ConditionText = conditionText
				};
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CreateMysteryItem failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Ensure NeedCategoryCondition reflection fields are cached.
		/// Called lazily on first conditional mystery.
		/// </summary>
		private void EnsureConditionFields(object firstCondition) {
			if (_conditionFieldsCached || firstCondition == null) return;

			try {
				var condType = firstCondition.GetType();
				_conditionCategoryField = condType.GetField("category",
					BindingFlags.Public | BindingFlags.Instance);
				_conditionAmountField = condType.GetField("amount",
					BindingFlags.Public | BindingFlags.Instance);

				// Get category type for displayName field
				if (_conditionCategoryField != null) {
					var category = _conditionCategoryField.GetValue(firstCondition);
					if (category != null) {
						var catType = category.GetType();
						_categoryDisplayNameField = catType.GetField("displayName",
							BindingFlags.Public | BindingFlags.Instance);

					}
				}

				Debug.Log("[ATSAccessibility] Cached NeedCategoryCondition fields");
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] EnsureConditionFields failed: {ex.Message}");
			}

			_conditionFieldsCached = true;
		}

		/// <summary>
		/// Get the condition text for a seasonal effect model.
		/// Includes hostility level requirement and need category conditions.
		/// Works for both SimpleSeasonalEffectModel and ConditionalSeasonalEffectModel.
		/// </summary>
		private string GetConditionText(object seasonalEffectModel) {
			if (seasonalEffectModel == null) return "";

			try {
				var parts = new List<string>();

				// Check hostility level requirement (both Simple and Conditional models have this)
				int hostilityLevel = GameReflection.GetSeasonalEffectHostilityLevel(seasonalEffectModel);
				if (hostilityLevel > 0) {
					parts.Add($"Hostility level {hostilityLevel}");
				}

				// Check need category conditions (ConditionalSeasonalEffectModel only)
				var conditionsField = seasonalEffectModel.GetType().GetField("conditions",
					BindingFlags.Public | BindingFlags.Instance);
				var conditions = conditionsField?.GetValue(seasonalEffectModel) as Array;

				if (conditions != null && conditions.Length > 0) {
					bool firstCondition = true;

					foreach (var condition in conditions) {
						if (condition == null) continue;

						// Cache condition fields on first iteration
						if (firstCondition) {
							EnsureConditionFields(condition);
							firstCondition = false;
						}

						var category = _conditionCategoryField?.GetValue(condition);
						var amount = _conditionAmountField?.GetValue(condition);

						if (category != null) {
							var displayName = _categoryDisplayNameField?.GetValue(category);
							string text = GameReflection.GetLocaText(displayName) ?? "";

							if (!string.IsNullOrEmpty(text))
								parts.Add($"{text} x{amount}");
						}
					}
				}

				return parts.Count > 0 ? "requires " + string.Join(", ", parts) : "";
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetConditionText failed: {ex.Message}");
				return "";
			}
		}

		/// <summary>
		/// Create a MysteryItem from an EffectModel object (for effects from GetAllConditions).
		/// </summary>
		private MysteryItem CreateEffectItem(object effectModel) {
			if (effectModel == null) return null;

			try {
				EnsureModelFields();

				string displayName = "";
				string description = "";

				var nameObj = _effectDisplayNameProperty?.GetValue(effectModel);
				if (nameObj != null)
					displayName = nameObj.ToString();

				var descObj = _effectDescriptionProperty?.GetValue(effectModel);
				if (descObj != null)
					description = descObj.ToString();

				// Fall back to internal name if display name is missing or has broken localization
				if (string.IsNullOrEmpty(displayName) || displayName.Contains("Missing key")) {
					displayName = GameReflection.GetEffectName(effectModel);
					if (string.IsNullOrEmpty(displayName)) return null;
				}

				return new MysteryItem {
					Name = displayName,
					Description = description,
					Type = ItemType.Effect,
					IsActive = true
				};
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CreateEffectItem failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Create a MysteryItem from a cornerstone effect name.
		/// </summary>
		private MysteryItem CreateCornerstoneItem(string effectName) {
			if (string.IsNullOrEmpty(effectName)) return null;

			try {
				object model = GameReflection.GetEffectModel(effectName);

				string displayName = effectName;
				string description = "";

				if (model != null) {
					EnsureModelFields();

					var nameObj = _effectDisplayNameProperty?.GetValue(model);
					if (nameObj != null)
						displayName = nameObj.ToString();

					var descObj = _effectDescriptionProperty?.GetValue(model);
					if (descObj != null)
						description = descObj.ToString();
				}

				return new MysteryItem {
					Name = displayName,
					Description = description,
					Type = ItemType.Cornerstone,
					IsActive = true
				};
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CreateCornerstoneItem failed: {ex.Message}");
				return null;
			}
		}

		/// <summary>
		/// Create a MysteryItem from a perk name and stack count.
		/// </summary>
		private MysteryItem CreatePerkItem(string effectName, int stacks) {
			if (string.IsNullOrEmpty(effectName)) return null;

			try {
				object model = GameReflection.GetEffectModel(effectName);

				string displayName = effectName;
				string description = "";

				if (model != null) {
					EnsureModelFields();

					var nameObj = _effectDisplayNameProperty?.GetValue(model);
					if (nameObj != null)
						displayName = nameObj.ToString();

					var descObj = _effectDescriptionProperty?.GetValue(model);
					if (descObj != null)
						description = descObj.ToString();
				}

				return new MysteryItem {
					Name = displayName,
					Description = description,
					Type = ItemType.Perk,
					Stacks = stacks,
					IsActive = true
				};
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CreatePerkItem failed: {ex.Message}");
				return null;
			}
		}
	}
}
