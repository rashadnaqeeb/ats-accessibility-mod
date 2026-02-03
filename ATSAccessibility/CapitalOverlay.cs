using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the capital (Smoldering City) screen.
    /// Flat list navigation: Buy Upgrades, Deeds, Game History, Home (if unlocked).
    /// </summary>
    public class CapitalOverlay : MenuBase, IKeyHandler
    {
        private List<(string name, Action action)> _items = new List<(string, Action)>();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => IsOpen && !IsSuspended;

        bool IKeyHandler.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) =>
            ProcessKey(keyCode, modifiers);

        // ========================================
        // MENUBASE OVERRIDES
        // ========================================

        protected override string OverlayName => "Capital";
        protected override string EmptyMessage => "No items available";

        protected override int GetItemCount() => _items.Count;

        protected override string GetLabel(int index)
        {
            if (index < 0 || index >= _items.Count) return null;

            string name = _items[index].name;
            string lockSuffix = GetLockSuffix(name);
            return $"{name}{lockSuffix}";
        }

        protected override string GetSearchName(int index)
        {
            if (index < 0 || index >= _items.Count) return null;
            return _items[index].name;
        }

        protected override void RefreshData()
        {
            _items.Clear();

            _items.Add(("Buy Upgrades", () => CapitalReflection.OpenUpgrades()));
            _items.Add(("Deeds", () => CapitalReflection.OpenDeeds()));
            _items.Add(("Game History", () => CapitalReflection.OpenHistory()));
            _items.Add(("Daily Expedition", () => CapitalReflection.OpenDailyExpedition()));
            _items.Add(("Training Expedition", () => CapitalReflection.OpenTrainingExpedition()));

            if (CapitalReflection.IsHomeUnlocked())
            {
                _items.Add(("Home", () => CapitalReflection.OpenHome()));
            }
        }

        protected override EnterAction OnEnter(int index) => EnterAction.Action;

        protected override void OnAction(int index)
        {
            if (index < 0 || index >= _items.Count) return;

            var item = _items[index];

            // Check if item is locked before suspending
            if (IsItemLocked(item.name))
            {
                Speech.Say($"{item.name} locked. Unlock via meta progression");
                SoundManager.PlayFailed();
                return;
            }

            Debug.Log($"[ATSAccessibility] Capital overlay: activating {item.name}");
            SoundManager.PlayButtonClick();
            Suspend();
            item.action?.Invoke();
        }

        protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (keyCode == KeyCode.Escape)
            {
                Close();
                return false; // Pass to game to close capital screen
            }
            return null;
        }

        protected override string GetOpenAnnouncement()
        {
            if (_items.Count == 0) return EmptyMessage;
            return GetLabel(0); // No prefix, just the item
        }

        protected override void OnClosed()
        {
            _items.Clear();
        }

        // ========================================
        // HELPERS
        // ========================================

        private bool IsItemLocked(string name)
        {
            switch (name)
            {
                case "Deeds": return !CapitalReflection.IsDeedsUnlocked();
                case "Daily Expedition": return !CapitalReflection.IsDailyExpeditionUnlocked();
                case "Training Expedition": return !CapitalReflection.IsTrainingExpeditionUnlocked();
                default: return false;
            }
        }

        private string GetLockSuffix(string itemName)
        {
            switch (itemName)
            {
                case "Deeds":
                    return CapitalReflection.IsDeedsUnlocked() ? "" : ", locked";
                case "Daily Expedition":
                    return CapitalReflection.IsDailyExpeditionUnlocked() ? "" : ", locked";
                case "Training Expedition":
                    return CapitalReflection.IsTrainingExpeditionUnlocked() ? "" : ", locked";
                case "Home":
                    return NarrationReflection.HasAnyImportantTopics() ? ", new dialogue" : "";
                default:
                    return "";
            }
        }
    }
}
