using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Overlay for TraderAssaultResultPopup (shown after assaulting a trader).
    /// Provides flat list navigation through stolen goods, perks, consequences, and villagers lost.
    /// </summary>
    public class AssaultResultOverlay : IKeyHandler
    {
        // State
        private bool _isOpen;
        private object _popup;
        private int _currentIndex;
        private List<string> _items = new List<string>();

        // Cached reflection
        private static bool _typesCached;
        private static FieldInfo _descField;
        private static FieldInfo _villagersKilledField;
        private static FieldInfo _gainedGoodsSlotsField;
        private static FieldInfo _gainedRewardsSlotsField;
        private static FieldInfo _effectsRewardSlotsField;
        private static FieldInfo _goodSlotGoodField;
        private static FieldInfo _effectSlotModelField;
        private static FieldInfo _goodNameField;
        private static FieldInfo _goodAmountField;
        private static PropertyInfo _effectDisplayNameProperty;
        private static PropertyInfo _effectDescriptionProperty;
        private static MethodInfo _popupHideMethod;

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    Navigate(-1);
                    return true;

                case KeyCode.DownArrow:
                    Navigate(1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    Dismiss();
                    return true;

                case KeyCode.Escape:
                    Dismiss();
                    Speech.Say("Closed");
                    InputBlocker.BlockCancelOnce = true;
                    return true;

                default:
                    // Consume all other keys while active
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        public void Open(object popup)
        {
            if (_isOpen) return;

            _isOpen = true;
            _popup = popup;
            _currentIndex = 0;

            EnsureTypes();
            RefreshData();

            if (_items.Count > 0)
            {
                Speech.Say($"Assault result. {_items[0]}");
            }
            else
            {
                Speech.Say("Assault result");
            }

            Debug.Log($"[ATSAccessibility] AssaultResultOverlay opened, {_items.Count} items");
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _popup = null;
            _items.Clear();

            Debug.Log("[ATSAccessibility] AssaultResultOverlay closed");
        }

        // ========================================
        // DETECTION
        // ========================================

        public static bool IsAssaultResultPopup(object popup)
        {
            if (popup == null) return false;
            return popup.GetType().Name == "TraderAssaultResultPopup";
        }

        // ========================================
        // NAVIGATION
        // ========================================

        private void Navigate(int direction)
        {
            if (_items.Count == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _items.Count);
            Speech.Say(_items[_currentIndex]);
        }

        private void Dismiss()
        {
            if (_popup == null) return;

            EnsureTypes();
            if (_popupHideMethod != null)
            {
                try
                {
                    _popupHideMethod.Invoke(_popup, null);
                    SoundManager.PlayButtonClick();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ATSAccessibility] AssaultResultOverlay: Failed to hide popup: {ex.Message}");
                }
            }
        }

        // ========================================
        // DATA
        // ========================================

        private void RefreshData()
        {
            _items.Clear();

            if (_popup == null) return;

            // 1. Description text
            string desc = GetTextFieldValue(_descField);
            if (!string.IsNullOrEmpty(desc))
            {
                _items.Add(desc);
            }

            // 2. Villagers killed
            string villagersKilled = GetTextFieldValue(_villagersKilledField);
            if (!string.IsNullOrEmpty(villagersKilled))
            {
                _items.Add($"Villagers lost: {villagersKilled}");
            }

            // 3. Stolen goods
            ReadGoodsSlots(_gainedGoodsSlotsField, "Stolen");

            // 4. Stolen effects/perks
            ReadEffectsSlots(_gainedRewardsSlotsField, "Stolen");

            // 5. Consequences (negative effects)
            ReadEffectsSlots(_effectsRewardSlotsField, "Consequence");

            Debug.Log($"[ATSAccessibility] AssaultResultOverlay: {_items.Count} items");
        }

        private void ReadGoodsSlots(FieldInfo slotsField, string prefix)
        {
            if (slotsField == null || _popup == null) return;

            var slots = slotsField.GetValue(_popup);
            if (slots is System.Collections.IList goodsList)
            {
                foreach (var slot in goodsList)
                {
                    if (slot == null) continue;

                    // Check if slot is active
                    var mb = slot as MonoBehaviour;
                    if (mb != null && !mb.gameObject.activeSelf) continue;

                    var good = _goodSlotGoodField?.GetValue(slot);
                    if (good == null) continue;

                    string name = _goodNameField?.GetValue(good) as string;
                    int amount = 0;
                    var amountObj = _goodAmountField?.GetValue(good);
                    if (amountObj is int a) amount = a;

                    if (string.IsNullOrEmpty(name)) continue;

                    string displayName = GetGoodDisplayName(name);
                    if (amount > 1)
                        _items.Add($"{prefix}: {displayName}, {amount}");
                    else
                        _items.Add($"{prefix}: {displayName}");
                }
            }
        }

        private void ReadEffectsSlots(FieldInfo slotsField, string prefix)
        {
            if (slotsField == null || _popup == null) return;

            var slots = slotsField.GetValue(_popup);
            if (slots is System.Collections.IList effectsList)
            {
                foreach (var slot in effectsList)
                {
                    if (slot == null) continue;

                    var mb = slot as MonoBehaviour;
                    if (mb != null && !mb.gameObject.activeSelf) continue;

                    var model = _effectSlotModelField?.GetValue(slot);
                    if (model == null) continue;

                    string displayName = _effectDisplayNameProperty?.GetValue(model) as string;
                    string description = _effectDescriptionProperty?.GetValue(model) as string;

                    if (!string.IsNullOrEmpty(displayName))
                    {
                        if (!string.IsNullOrEmpty(description))
                            _items.Add($"{prefix}: {displayName}. {description}");
                        else
                            _items.Add($"{prefix}: {displayName}");
                    }
                }
            }
        }

        private string GetTextFieldValue(FieldInfo textField)
        {
            if (_popup == null || textField == null) return null;

            try
            {
                var tmpText = textField.GetValue(_popup);
                if (tmpText == null) return null;

                var textProp = tmpText.GetType().GetProperty("text", GameReflection.PublicInstance);
                return textProp?.GetValue(tmpText) as string;
            }
            catch
            {
                return null;
            }
        }

        private string GetGoodDisplayName(string goodName)
        {
            if (string.IsNullOrEmpty(goodName)) return goodName;

            try
            {
                var settings = GameReflection.GetSettings();
                if (settings == null) return goodName;

                var getGoodMethod = settings.GetType().GetMethod("GetGood", new[] { typeof(string) });
                var goodModel = getGoodMethod?.Invoke(settings, new object[] { goodName });
                if (goodModel == null) return goodName;

                var displayNameField = goodModel.GetType().GetField("displayName", GameReflection.PublicInstance);
                var displayNameObj = displayNameField?.GetValue(goodModel);
                return GameReflection.GetLocaText(displayNameObj) ?? goodName;
            }
            catch
            {
                return goodName;
            }
        }

        // ========================================
        // REFLECTION CACHING
        // ========================================

        private static void EnsureTypes()
        {
            if (_typesCached) return;

            var assembly = GameReflection.GameAssembly;
            if (assembly == null) return;

            try
            {
                var popupType = assembly.GetType("Eremite.Buildings.UI.Trade.TraderAssaultResultPopup");
                if (popupType != null)
                {
                    _descField = popupType.GetField("desc", GameReflection.NonPublicInstance);
                    _villagersKilledField = popupType.GetField("villagersKileldCounter", GameReflection.NonPublicInstance);
                    _gainedGoodsSlotsField = popupType.GetField("gainedGoodsSlots", GameReflection.NonPublicInstance);
                    _gainedRewardsSlotsField = popupType.GetField("gainedRewardsSlots", GameReflection.NonPublicInstance);
                    _effectsRewardSlotsField = popupType.GetField("effectsRewardSlots", GameReflection.NonPublicInstance);
                }

                var goodSlotType = assembly.GetType("Eremite.View.HUD.GoodSlot");
                if (goodSlotType != null)
                {
                    _goodSlotGoodField = goodSlotType.GetField("good", GameReflection.NonPublicInstance);
                }

                var effectSlotType = assembly.GetType("Eremite.View.HUD.EffectSlot");
                if (effectSlotType != null)
                {
                    _effectSlotModelField = effectSlotType.GetField("model",
                        BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                    if (_effectSlotModelField == null)
                    {
                        _effectSlotModelField = effectSlotType.GetField("model", GameReflection.NonPublicInstance);
                    }
                }

                var effectModelType = assembly.GetType("Eremite.Model.EffectModel");
                if (effectModelType != null)
                {
                    _effectDisplayNameProperty = effectModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
                    _effectDescriptionProperty = effectModelType.GetProperty("Description", GameReflection.PublicInstance);
                }

                var goodType = assembly.GetType("Eremite.Model.Good");
                if (goodType != null)
                {
                    _goodNameField = goodType.GetField("name", GameReflection.PublicInstance);
                    _goodAmountField = goodType.GetField("amount", GameReflection.PublicInstance);
                }

                var basePopupType = assembly.GetType("Eremite.View.Popups.Popup");
                if (basePopupType != null)
                {
                    _popupHideMethod = basePopupType.GetMethod("Hide", GameReflection.PublicInstance);
                }

                Debug.Log("[ATSAccessibility] AssaultResultOverlay: Types cached successfully");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] AssaultResultOverlay: Type caching failed: {ex.Message}");
            }

            _typesCached = true;
        }
    }
}
