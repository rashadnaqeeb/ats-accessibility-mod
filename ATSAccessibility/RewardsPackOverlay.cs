using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Overlay for RewardsPackPopup (shown after port expedition rewards are granted).
    /// Provides flat list navigation through goods and effects received.
    /// </summary>
    public class RewardsPackOverlay : IKeyHandler
    {
        // State
        private bool _isOpen;
        private object _popup;
        private int _currentIndex;
        private List<string> _items = new List<string>();

        // Cached reflection
        private static bool _typesCached;
        private static FieldInfo _goodsSlotsField;
        private static FieldInfo _effectsSlotsField;
        private static FieldInfo _goodSlotGoodField;
        private static FieldInfo _effectSlotModelField;
        private static FieldInfo _goodNameField;
        private static FieldInfo _goodAmountField;
        private static PropertyInfo _effectDisplayNameProperty;
        private static PropertyInfo _effectDescriptionProperty;
        private static MethodInfo _popupHideMethod;
        private static FieldInfo _headerField;
        private static FieldInfo _descField;

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
                    InputBlocker.BlockCancelOnce = true;
                    Speech.Say("Closed");
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

            string flavorText = GetPopupText(_descField);
            string announcement = !string.IsNullOrEmpty(flavorText) ? flavorText : "Rewards";

            if (_items.Count > 0)
            {
                Speech.Say($"{announcement}. {_items[0]}");
            }
            else
            {
                Speech.Say($"{announcement}. No items");
            }

            Debug.Log($"[ATSAccessibility] RewardsPackOverlay opened, {_items.Count} items");
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _popup = null;
            _items.Clear();

            Debug.Log("[ATSAccessibility] RewardsPackOverlay closed");
        }

        // ========================================
        // DETECTION
        // ========================================

        public static bool IsRewardsPackPopup(object popup)
        {
            if (popup == null) return false;
            return popup.GetType().Name == "RewardsPackPopup";
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
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[ATSAccessibility] RewardsPackOverlay: Failed to hide popup: {ex.Message}");
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

            // Read goods
            var goodsSlots = _goodsSlotsField?.GetValue(_popup);
            if (goodsSlots is System.Collections.IList goodsList)
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
                        _items.Add($"{displayName}, {amount}");
                    else
                        _items.Add(displayName);
                }
            }

            // Read effects
            var effectsSlots = _effectsSlotsField?.GetValue(_popup);
            if (effectsSlots is System.Collections.IList effectsList)
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
                            _items.Add($"{displayName}: {description}");
                        else
                            _items.Add(displayName);
                    }
                }
            }

            Debug.Log($"[ATSAccessibility] RewardsPackOverlay: {_items.Count} reward items");
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

        private string GetPopupText(FieldInfo textField)
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
                var popupType = assembly.GetType("Eremite.View.HUD.Rewards.RewardsPackPopup");
                if (popupType != null)
                {
                    _goodsSlotsField = popupType.GetField("goodsSlots", GameReflection.NonPublicInstance);
                    _effectsSlotsField = popupType.GetField("effectsSlots", GameReflection.NonPublicInstance);
                    _headerField = popupType.GetField("header", GameReflection.NonPublicInstance);
                    _descField = popupType.GetField("desc", GameReflection.NonPublicInstance);
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
                        // model is 'protected' in EffectSlot, try getting it directly
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
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] RewardsPackOverlay: Type caching failed: {ex.Message}");
            }

            _typesCached = true;
        }
    }
}
