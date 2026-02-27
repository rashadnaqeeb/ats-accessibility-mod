using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Overlay for TraderAssaultResultPopup (shown after assaulting a trader).
	/// Provides flat list navigation through stolen goods, perks, consequences, and villagers lost.
	/// </summary>
	public class AssaultResultOverlay: MenuBase {
		// Data
		private object _popup;
		private List<string> _items = new List<string>();

		// Popup type detection
		private static System.Type _assaultResultPopupType;

		// Cached reflection (popup-specific fields only; shared slot types in PopupReflection)
		private static bool _typesCached;
		private static FieldInfo _descField;
		private static FieldInfo _villagersKilledField;
		private static FieldInfo _gainedGoodsSlotsField;
		private static FieldInfo _gainedRewardsSlotsField;
		private static FieldInfo _effectsRewardSlotsField;

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "Assault result";
		protected override string EmptyMessage => "";

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index >= 0 && index < _items.Count)
				return _items[index];
			return null;
		}

		protected override void RefreshData() {
			_items.Clear();

			if (_popup == null) return;

			// 1. Description text
			string desc = GetTextFieldValue(_descField);
			if (!string.IsNullOrEmpty(desc)) {
				_items.Add(desc);
			}

			// 2. Villagers killed
			string villagersKilled = GetTextFieldValue(_villagersKilledField);
			if (!string.IsNullOrEmpty(villagersKilled)) {
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

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			Dismiss();
		}

		protected override int SearchItemCount => 0; // No search

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

		protected override void OnClosed() {
			_popup = null;
			_items.Clear();
		}

		// ========================================
		// DETECTION
		// ========================================

		public static bool IsAssaultResultPopup(object popup) {
			if (popup == null) return false;
			if (_assaultResultPopupType == null)
				_assaultResultPopupType = GameReflection.GameAssembly?.GetType("Eremite.Buildings.UI.Trade.TraderAssaultResultPopup");
			return _assaultResultPopupType != null && _assaultResultPopupType.IsInstanceOfType(popup);
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
		// DATA READING
		// ========================================

		private void ReadGoodsSlots(FieldInfo slotsField, string prefix) {
			if (slotsField == null || _popup == null) return;

			var slots = slotsField.GetValue(_popup);
			if (slots is System.Collections.IList goodsList) {
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
						_items.Add($"{prefix}: {displayName}, {amount}");
					else
						_items.Add($"{prefix}: {displayName}");
				}
			}
		}

		private void ReadEffectsSlots(FieldInfo slotsField, string prefix) {
			if (slotsField == null || _popup == null) return;

			var slots = slotsField.GetValue(_popup);
			if (slots is System.Collections.IList effectsList) {
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
							_items.Add($"{prefix}: {displayName}. {description}");
						else
							_items.Add($"{prefix}: {displayName}");
					}
				}
			}
		}

		// ========================================
		// HELPERS
		// ========================================

		private string GetTextFieldValue(FieldInfo textField) {
			if (_popup == null || textField == null) return null;
			return PopupReflection.GetTmpText(textField.GetValue(_popup));
		}

		// ========================================
		// REFLECTION CACHING
		// ========================================

		private static void EnsureTypes() {
			if (_typesCached) return;
			_typesCached = true;

			ReflectionHelper.InitCache("AssaultResultOverlay", assembly => {
				var popupType = assembly.GetType("Eremite.Buildings.UI.Trade.TraderAssaultResultPopup");
				if (popupType != null) {
					_descField = popupType.GetField("desc", GameReflection.NonPublicInstance);
					_villagersKilledField = popupType.GetField("villagersKileldCounter", GameReflection.NonPublicInstance);
					_gainedGoodsSlotsField = popupType.GetField("gainedGoodsSlots", GameReflection.NonPublicInstance);
					_gainedRewardsSlotsField = popupType.GetField("gainedRewardsSlots", GameReflection.NonPublicInstance);
					_effectsRewardSlotsField = popupType.GetField("effectsRewardSlots", GameReflection.NonPublicInstance);
				}
			});
		}
	}
}
