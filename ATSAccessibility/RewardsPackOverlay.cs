using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Overlay for RewardsPackPopup (shown after port expedition rewards are granted).
	/// Provides flat list navigation through goods and effects received.
	/// </summary>
	public class RewardsPackOverlay: MenuBase, IKeyHandler {
		// Data
		private object _popup;
		private List<string> _items = new List<string>();

		// Cached reflection (popup-specific fields only; shared slot types in PopupReflection)
		private static bool _typesCached;
		private static FieldInfo _goodsSlotsField;
		private static FieldInfo _effectsSlotsField;
		private static FieldInfo _headerField;
		private static FieldInfo _descField;

		// ========================================
		// IKeyHandler Implementation
		// ========================================

		public bool IsActive => IsOpen;

		bool IKeyHandler.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) =>
			ProcessKey(keyCode, modifiers);

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "Rewards";
		protected override string EmptyMessage => "No items";

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index >= 0 && index < _items.Count)
				return _items[index];
			return null;
		}

		protected override void RefreshData() {
			_items.Clear();

			if (_popup == null) return;

			// Read goods
			var goodsSlots = _goodsSlotsField?.GetValue(_popup);
			if (goodsSlots is System.Collections.IList goodsList) {
				foreach (var slot in goodsList) {
					if (slot == null) continue;

					var mb = slot as MonoBehaviour;
					if (mb != null && !mb.gameObject.activeSelf) continue;

					var good = PopupReflection.GetGoodFromSlot(slot);
					if (good == null) continue;

					string name = PopupReflection.GetGoodName(good);
					if (string.IsNullOrEmpty(name)) continue;

					int amount = PopupReflection.GetGoodAmount(good);
					string displayName = PopupReflection.GetGoodDisplayName(name);
					if (amount > 1)
						_items.Add($"{displayName}, {amount}");
					else
						_items.Add(displayName);
				}
			}

			// Read effects
			var effectsSlots = _effectsSlotsField?.GetValue(_popup);
			if (effectsSlots is System.Collections.IList effectsList) {
				foreach (var slot in effectsList) {
					if (slot == null) continue;

					var mb = slot as MonoBehaviour;
					if (mb != null && !mb.gameObject.activeSelf) continue;

					var model = PopupReflection.GetEffectModel(slot);
					if (model == null) continue;

					string displayName = PopupReflection.GetEffectDisplayName(model);
					string description = PopupReflection.GetEffectDescription(model);

					if (!string.IsNullOrEmpty(displayName)) {
						if (!string.IsNullOrEmpty(description))
							_items.Add($"{displayName}: {description}");
						else
							_items.Add(displayName);
					}
				}
			}

			Debug.Log($"[ATSAccessibility] RewardsPackOverlay: {_items.Count} reward items");
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			Dismiss();
		}

		protected override int SearchItemCount => 0; // No search for rewards

		protected override EscapeAction OnEscape() {
			Dismiss();
			Speech.Say("Closed");
			InputBlocker.BlockCancelOnce = true;
			return EscapeAction.Close;
		}

		protected override void StorePopup(object popup) {
			_popup = popup;
			EnsureTypes();
		}

		protected override string GetOpenAnnouncement() {
			string flavorText = GetPopupText(_descField);
			string announcement = !string.IsNullOrEmpty(flavorText) ? flavorText : OverlayName;

			if (_items.Count > 0)
				return $"{announcement}. {_items[0]}";
			return $"{announcement}. {EmptyMessage}";
		}

		protected override void OnClosed() {
			_popup = null;
			_items.Clear();
		}

		// ========================================
		// DETECTION
		// ========================================

		public static bool IsRewardsPackPopup(object popup) {
			if (popup == null) return false;
			return popup.GetType().Name == "RewardsPackPopup";
		}

		// ========================================
		// ACTIONS
		// ========================================

		private void Dismiss() {
			if (_popup == null) return;

			if (PopupReflection.HidePopup(_popup)) {
				SoundManager.PlayButtonClick();
			}
		}

		// ========================================
		// HELPERS
		// ========================================

		private string GetPopupText(FieldInfo textField) {
			if (_popup == null || textField == null) return null;
			return PopupReflection.GetTmpText(textField.GetValue(_popup));
		}

		// ========================================
		// REFLECTION CACHING
		// ========================================

		private static void EnsureTypes() {
			if (_typesCached) return;
			_typesCached = true;

			ReflectionHelper.InitCache("RewardsPackOverlay", assembly => {
				var popupType = assembly.GetType("Eremite.View.HUD.Rewards.RewardsPackPopup");
				if (popupType != null) {
					_goodsSlotsField = popupType.GetField("goodsSlots", GameReflection.NonPublicInstance);
					_effectsSlotsField = popupType.GetField("effectsSlots", GameReflection.NonPublicInstance);
					_headerField = popupType.GetField("header", GameReflection.NonPublicInstance);
					_descField = popupType.GetField("desc", GameReflection.NonPublicInstance);
				}
			});
		}
	}
}
