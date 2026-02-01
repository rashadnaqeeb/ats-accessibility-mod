using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Overlay for CycleEffectsPickPopup (Royal Resupply on World Map after winning
    /// a settlement near negative modifiers). Player picks 1 of 3 rewards.
    /// </summary>
    public class ResupplyOverlay : IKeyHandler
    {
        // State
        private bool _isOpen;
        private object _popup;
        private int _currentIndex;
        private List<string> _items = new List<string>();
        private List<object> _slots = new List<object>();

        // Cached reflection
        private static bool _typesCached;
        private static FieldInfo _slotsField;
        private static FieldInfo _slotModelField;
        private static PropertyInfo _modelDisplayNameProperty;
        private static PropertyInfo _modelDescriptionProperty;
        private static MethodInfo _slotOnClickMethod;

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

                case KeyCode.Home:
                    NavigateTo(0);
                    return true;

                case KeyCode.End:
                    NavigateTo(_items.Count - 1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Space:
                    Pick();
                    return true;

                case KeyCode.Escape:
                    // Pass to game to close popup naturally
                    return false;

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
                Speech.Say($"Royal Resupply. {_items[0]}");
            }
            else
            {
                Speech.Say("Royal Resupply. No options");
            }

            Debug.Log($"[ATSAccessibility] ResupplyOverlay opened, {_items.Count} options");
        }

        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _popup = null;
            _items.Clear();
            _slots.Clear();

            Debug.Log("[ATSAccessibility] ResupplyOverlay closed");
        }

        // ========================================
        // DETECTION
        // ========================================

        public static bool IsCycleEffectsPickPopup(object popup)
        {
            if (popup == null) return false;
            return popup.GetType().Name == "CycleEffectsPickPopup";
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

        private void NavigateTo(int index)
        {
            if (_items.Count == 0) return;
            _currentIndex = Mathf.Clamp(index, 0, _items.Count - 1);
            Speech.Say(_items[_currentIndex]);
        }

        // ========================================
        // PICK
        // ========================================

        private void Pick()
        {
            if (_currentIndex < 0 || _currentIndex >= _slots.Count)
            {
                Speech.Say("Cannot select");
                SoundManager.PlayFailed();
                return;
            }

            var slot = _slots[_currentIndex];
            if (slot == null)
            {
                Speech.Say("Cannot select");
                SoundManager.PlayFailed();
                return;
            }

            EnsureTypes();
            if (_slotOnClickMethod == null)
            {
                Speech.Say("Cannot select");
                SoundManager.PlayFailed();
                return;
            }

            try
            {
                _slotOnClickMethod.Invoke(slot, null);
                SoundManager.PlayButtonClick();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ResupplyOverlay: Failed to pick: {ex.Message}");
                Speech.Say("Cannot select");
                SoundManager.PlayFailed();
            }
        }

        // ========================================
        // DATA
        // ========================================

        private void RefreshData()
        {
            _items.Clear();
            _slots.Clear();

            if (_popup == null) return;

            var slotsObj = _slotsField?.GetValue(_popup);
            if (!(slotsObj is System.Collections.IList slotsList)) return;

            foreach (var slot in slotsList)
            {
                if (slot == null) continue;

                // Check if slot is active
                var mb = slot as MonoBehaviour;
                if (mb != null && !mb.gameObject.activeSelf) continue;

                var model = _slotModelField?.GetValue(slot);
                if (model == null) continue;

                string displayName = _modelDisplayNameProperty?.GetValue(model) as string;
                string description = _modelDescriptionProperty?.GetValue(model) as string;

                if (string.IsNullOrEmpty(displayName)) continue;

                if (!string.IsNullOrEmpty(description))
                    _items.Add($"{displayName}: {description}");
                else
                    _items.Add(displayName);

                _slots.Add(slot);
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
                var popupType = assembly.GetType("Eremite.View.HUD.CycleEffectsPickPopup");
                if (popupType != null)
                {
                    _slotsField = popupType.GetField("slots", GameReflection.NonPublicInstance);
                }

                var slotType = assembly.GetType("Eremite.View.HUD.CycleEffectsPickSlot");
                if (slotType != null)
                {
                    _slotModelField = slotType.GetField("model", GameReflection.NonPublicInstance);
                    _slotOnClickMethod = slotType.GetMethod("OnClick",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                }

                var modelType = assembly.GetType("Eremite.Model.Meta.CycleEffectModel");
                if (modelType != null)
                {
                    _modelDisplayNameProperty = modelType.GetProperty("DisplayName", GameReflection.PublicInstance);
                    _modelDescriptionProperty = modelType.GetProperty("Description", GameReflection.PublicInstance);
                }

            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ResupplyOverlay: Type caching failed: {ex.Message}");
            }

            _typesCached = true;
        }
    }
}
