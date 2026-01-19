using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Virtual speech panel for forest mysteries (seasonal effects) and modifiers.
    /// Three categories: Positive Mysteries, Negative Mysteries, Modifiers.
    /// </summary>
    public class MysteriesPanel
    {
        /// <summary>
        /// Represents a single mystery or modifier item.
        /// </summary>
        private class MysteryItem
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Season { get; set; }
            public bool IsPositive { get; set; }
            public bool IsActive { get; set; }
            public bool IsConditional { get; set; }
            public string ConditionText { get; set; }
        }

        /// <summary>
        /// Represents a category in the left panel.
        /// </summary>
        private class Category
        {
            public string Name { get; set; }
            public List<MysteryItem> Items { get; set; } = new List<MysteryItem>();
        }

        private bool _isOpen = false;
        private List<Category> _categories = new List<Category>();
        private int _currentCategoryIndex = 0;
        private int _currentItemIndex = 0;
        private bool _focusOnItems = false;

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

        /// <summary>
        /// Whether the mysteries panel is currently open.
        /// </summary>
        public bool IsOpen => _isOpen;

        /// <summary>
        /// Open the mysteries panel and announce the first category.
        /// Toggle behavior - if already open, close it.
        /// </summary>
        public void Open()
        {
            if (_isOpen)
            {
                Close();
                return;
            }

            // Build category list
            RefreshCategories();

            // Check if all categories are empty
            bool hasAnyItems = false;
            foreach (var cat in _categories)
            {
                if (cat.Items.Count > 0)
                {
                    hasAnyItems = true;
                    break;
                }
            }

            if (!hasAnyItems)
            {
                Speech.Say("No mysteries or modifiers active");
                return;
            }

            _isOpen = true;
            _currentCategoryIndex = 0;
            _currentItemIndex = 0;
            _focusOnItems = false;

            AnnounceCurrentCategory();
            Debug.Log("[ATSAccessibility] Mysteries panel opened");
        }

        /// <summary>
        /// Close the mysteries panel.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            InputBlocker.BlockCancelOnce = true;  // Block the Cancel action that will fire this frame
            _categories.Clear();
            Speech.Say("Mysteries panel closed");
            Debug.Log("[ATSAccessibility] Mysteries panel closed");
        }

        /// <summary>
        /// Process a key event for the mysteries panel.
        /// Returns true if the key was handled.
        /// </summary>
        public bool ProcessKeyEvent(KeyCode keyCode)
        {
            if (!_isOpen) return false;

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    if (_focusOnItems)
                        NavigateItem(-1);
                    else
                        NavigateCategory(-1);
                    return true;

                case KeyCode.DownArrow:
                    if (_focusOnItems)
                        NavigateItem(1);
                    else
                        NavigateCategory(1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.RightArrow:
                    EnterItems();
                    return true;

                case KeyCode.LeftArrow:
                    if (_focusOnItems)
                    {
                        ReturnToCategories();
                        return true;
                    }
                    return false;  // At root level, let parent handle

                case KeyCode.Escape:
                    Close();
                    return true;

                default:
                    return true;  // Consume all other keys while panel is open
            }
        }

        /// <summary>
        /// Navigate categories with Up/Down.
        /// </summary>
        private void NavigateCategory(int direction)
        {
            if (_categories.Count == 0) return;

            _currentCategoryIndex = NavigationUtils.WrapIndex(_currentCategoryIndex, direction, _categories.Count);
            _currentItemIndex = 0;  // Reset item index when changing category
            AnnounceCurrentCategory();
        }

        /// <summary>
        /// Navigate items within current category.
        /// </summary>
        private void NavigateItem(int direction)
        {
            var category = _categories[_currentCategoryIndex];
            if (category.Items.Count == 0) return;

            _currentItemIndex = NavigationUtils.WrapIndex(_currentItemIndex, direction, category.Items.Count);
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Enter items view (Enter key).
        /// </summary>
        private void EnterItems()
        {
            var category = _categories[_currentCategoryIndex];

            if (category.Items.Count == 0)
            {
                Speech.Say("No items in this category");
                return;
            }

            _focusOnItems = true;
            _currentItemIndex = 0;
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Return to categories (Left arrow).
        /// </summary>
        private void ReturnToCategories()
        {
            if (_focusOnItems)
            {
                _focusOnItems = false;
                AnnounceCurrentCategory();
            }
        }

        /// <summary>
        /// Announce the current category (name and count).
        /// </summary>
        private void AnnounceCurrentCategory()
        {
            if (_currentCategoryIndex < 0 || _currentCategoryIndex >= _categories.Count) return;

            var category = _categories[_currentCategoryIndex];
            int count = category.Items.Count;

            string message = $"{category.Name}, {count}";
            Speech.Say(message);
            Debug.Log($"[ATSAccessibility] Mysteries category {_currentCategoryIndex + 1}/{_categories.Count}: {message}");
        }

        /// <summary>
        /// Announce the current item.
        /// For mysteries: "Active/Inactive, Name, Season. Description. Condition (if conditional)"
        /// For modifiers: "Name. Description" (no positive/negative)
        /// </summary>
        private void AnnounceCurrentItem()
        {
            var category = _categories[_currentCategoryIndex];
            if (_currentItemIndex < 0 || _currentItemIndex >= category.Items.Count) return;

            var item = category.Items[_currentItemIndex];
            var parts = new List<string>();

            // Modifiers category uses different format (no active/inactive status)
            if (category.Name == "Modifiers")
            {
                // Format: "Name. Description"
                parts.Add(item.Name + ".");
                if (!string.IsNullOrEmpty(item.Description))
                    parts.Add(item.Description);
            }
            else
            {
                // Mysteries format: "Active/Inactive, Name, Season. Description Condition"
                // (description already has trailing period from game localization)
                string status = item.IsActive ? "Active" : "Inactive";
                parts.Add($"{status}, {item.Name}, {item.Season}.");

                if (!string.IsNullOrEmpty(item.Description))
                    parts.Add(item.Description);

                if (item.IsConditional && !string.IsNullOrEmpty(item.ConditionText))
                    parts.Add(item.ConditionText);
            }

            string message = string.Join(" ", parts);
            Speech.Say(message);
            Debug.Log($"[ATSAccessibility] Mystery item {_currentItemIndex + 1}/{category.Items.Count}: {item.Name}");
        }

        /// <summary>
        /// Refresh the category list with current game data.
        /// </summary>
        private void RefreshCategories()
        {
            _categories.Clear();

            // Get all mysteries split by positive/negative
            var (positiveMysteries, negativeMysteries) = GetMysteriesByType();

            // Category 1: Positive Mysteries
            _categories.Add(new Category
            {
                Name = "Positive Mysteries",
                Items = positiveMysteries
            });

            // Category 2: Negative Mysteries
            _categories.Add(new Category
            {
                Name = "Negative Mysteries",
                Items = negativeMysteries
            });

            // Category 3: Modifiers
            _categories.Add(new Category
            {
                Name = "Modifiers",
                Items = GetActiveModifiers()
            });

            Debug.Log($"[ATSAccessibility] Mysteries panel refreshed: Positive={_categories[0].Items.Count}, Negative={_categories[1].Items.Count}, Modifiers={_categories[2].Items.Count}");
        }

        /// <summary>
        /// Get all mysteries split by positive/negative.
        /// </summary>
        private (List<MysteryItem> positive, List<MysteryItem> negative) GetMysteriesByType()
        {
            var positive = new List<MysteryItem>();
            var negative = new List<MysteryItem>();
            var effectsDict = GameReflection.GetSeasonalEffectsDictionary();

            if (effectsDict == null)
            {
                Debug.Log("[ATSAccessibility] SeasonalEffects dictionary is null");
                return (positive, negative);
            }

            EnsureSeasonalEffectStateFields();

            foreach (DictionaryEntry entry in effectsDict)
            {
                var state = entry.Value;
                if (state == null) continue;

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
        /// Get active modifiers (early + late effects from ConditionsState).
        /// </summary>
        private List<MysteryItem> GetActiveModifiers()
        {
            var items = new List<MysteryItem>();

            var earlyEffects = GameReflection.GetEarlyEffects();
            var lateEffects = GameReflection.GetLateEffects();

            EnsureModelFields();

            // Process early effects
            if (earlyEffects != null)
            {
                foreach (var effectName in earlyEffects)
                {
                    var item = CreateModifierItem(effectName);
                    if (item != null)
                        items.Add(item);
                }
            }

            // Process late effects
            if (lateEffects != null)
            {
                foreach (var effectName in lateEffects)
                {
                    var item = CreateModifierItem(effectName);
                    if (item != null)
                        items.Add(item);
                }
            }

            return items;
        }

        /// <summary>
        /// Cache reflection fields for SeasonalEffectState.
        /// </summary>
        private void EnsureSeasonalEffectStateFields()
        {
            if (_sesFieldsCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _sesFieldsCached = true;
                return;
            }

            try
            {
                var sesType = assembly.GetType("Eremite.Model.State.SeasonalEffectState");
                if (sesType != null)
                {
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
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] SeasonalEffectState field caching failed: {ex.Message}");
            }

            _sesFieldsCached = true;
        }

        /// <summary>
        /// Cache reflection fields for EffectModel (used by modifiers).
        /// Note: Seasonal effect models use runtime type reflection in CreateMysteryItem().
        /// </summary>
        private void EnsureModelFields()
        {
            if (_modelFieldsCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null)
            {
                _modelFieldsCached = true;
                return;
            }

            try
            {
                // EffectModel for modifiers
                var effectModelType = assembly.GetType("Eremite.Model.EffectModel");
                if (effectModelType != null)
                {
                    _effectDisplayNameProperty = effectModelType.GetProperty("DisplayName",
                        BindingFlags.Public | BindingFlags.Instance);
                    _effectDescriptionProperty = effectModelType.GetProperty("Description",
                        BindingFlags.Public | BindingFlags.Instance);
                    _effectIsPositiveProperty = effectModelType.GetProperty("isPositive",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                Debug.Log("[ATSAccessibility] Cached EffectModel fields");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] Model field caching failed: {ex.Message}");
            }

            _modelFieldsCached = true;
        }

        /// <summary>
        /// Get isActive from SeasonalEffectState.
        /// </summary>
        private bool GetIsActive(object state)
        {
            if (state == null || _sesIsActiveField == null) return false;

            try
            {
                return (bool)(_sesIsActiveField.GetValue(state) ?? false);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Create a MysteryItem from a SeasonalEffectState.
        /// </summary>
        private MysteryItem CreateMysteryItem(string key, object state)
        {
            if (state == null) return null;

            try
            {
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

                if (model == null)
                {
                    model = GameReflection.GetConditionalSeasonalEffectModel(modelName);
                    if (model != null)
                    {
                        isConditional = true;
                        conditionText = GetConditionText(model);
                    }
                }

                string displayName = modelName;
                string description = "";

                if (model != null)
                {
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

                return new MysteryItem
                {
                    Name = displayName,
                    Description = description,
                    Season = season,
                    IsPositive = isPositive,
                    IsActive = isActive,
                    IsConditional = isConditional,
                    ConditionText = conditionText
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CreateMysteryItem failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Ensure NeedCategoryCondition reflection fields are cached.
        /// Called lazily on first conditional mystery.
        /// </summary>
        private void EnsureConditionFields(object firstCondition)
        {
            if (_conditionFieldsCached || firstCondition == null) return;

            try
            {
                var condType = firstCondition.GetType();
                _conditionCategoryField = condType.GetField("category",
                    BindingFlags.Public | BindingFlags.Instance);
                _conditionAmountField = condType.GetField("amount",
                    BindingFlags.Public | BindingFlags.Instance);

                // Get category type for displayName field
                if (_conditionCategoryField != null)
                {
                    var category = _conditionCategoryField.GetValue(firstCondition);
                    if (category != null)
                    {
                        var catType = category.GetType();
                        _categoryDisplayNameField = catType.GetField("displayName",
                            BindingFlags.Public | BindingFlags.Instance);

                    }
                }

                Debug.Log("[ATSAccessibility] Cached NeedCategoryCondition fields");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] EnsureConditionFields failed: {ex.Message}");
            }

            _conditionFieldsCached = true;
        }

        /// <summary>
        /// Get the condition text for a conditional seasonal effect model.
        /// </summary>
        private string GetConditionText(object conditionalModel)
        {
            if (conditionalModel == null) return "";

            try
            {
                // Get conditions array from ConditionalSeasonalEffectModel (it's a field, not property)
                var conditionsField = conditionalModel.GetType().GetField("conditions",
                    BindingFlags.Public | BindingFlags.Instance);
                var conditions = conditionsField?.GetValue(conditionalModel) as Array;

                if (conditions == null || conditions.Length == 0)
                    return "";

                var parts = new List<string>();
                bool firstCondition = true;

                foreach (var condition in conditions)
                {
                    if (condition == null) continue;

                    // Cache condition fields on first iteration
                    if (firstCondition)
                    {
                        EnsureConditionFields(condition);
                        firstCondition = false;
                    }

                    var category = _conditionCategoryField?.GetValue(condition);
                    var amount = _conditionAmountField?.GetValue(condition);

                    if (category != null)
                    {
                        var displayName = _categoryDisplayNameField?.GetValue(category);
                        string text = GameReflection.GetLocaText(displayName) ?? "";

                        if (!string.IsNullOrEmpty(text))
                            parts.Add($"{text} x{amount}");
                    }
                }

                return parts.Count > 0 ? "requires " + string.Join(", ", parts) : "";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetConditionText failed: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// Create a MysteryItem from an effect name (for modifiers).
        /// </summary>
        private MysteryItem CreateModifierItem(string effectName)
        {
            if (string.IsNullOrEmpty(effectName)) return null;

            try
            {
                object model = GameReflection.GetEffectModel(effectName);

                string displayName = effectName;
                string description = "";

                if (model != null)
                {
                    EnsureModelFields();

                    var nameObj = _effectDisplayNameProperty?.GetValue(model);
                    if (nameObj != null)
                        displayName = nameObj.ToString();

                    var descObj = _effectDescriptionProperty?.GetValue(model);
                    if (descObj != null)
                        description = descObj.ToString();
                }

                return new MysteryItem
                {
                    Name = displayName,
                    Description = description,
                    Season = "",  // Modifiers don't have seasons
                    IsActive = true,  // Modifiers are always active
                    IsConditional = false,
                    ConditionText = ""
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] CreateModifierItem failed: {ex.Message}");
                return null;
            }
        }
    }
}
