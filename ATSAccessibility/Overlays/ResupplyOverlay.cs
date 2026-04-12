using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Overlay for CycleEffectsPickPopup (Royal Resupply on World Map after winning
	/// a settlement near negative modifiers). Player picks 1 of 3 rewards.
	/// </summary>
	public class ResupplyOverlay: MenuBase {
		// Popup type detection
		private static System.Type _cycleEffectsPickPopupType;

		// Data
		private object _popup;
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
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("overlay.resupply.title");
		protected override string EmptyMessage => Strings.Get("overlay.resupply.empty");

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index >= 0 && index < _items.Count)
				return _items[index];
			return null;
		}

		protected override void RefreshData() {
			_items.Clear();
			_slots.Clear();

			if (_popup == null) return;

			var slotsObj = _slotsField?.GetValue(_popup);
			if (!(slotsObj is System.Collections.IList slotsList)) return;

			foreach (var slot in slotsList) {
				if (slot == null) continue;

				var mb = slot as MonoBehaviour;
				if (mb != null && !mb.gameObject.activeSelf) continue;

				var model = _slotModelField?.GetValue(slot);
				if (model == null) continue;

				string displayName = _modelDisplayNameProperty?.GetValue(model) as string;
				string description = _modelDescriptionProperty?.GetValue(model) as string;

				if (string.IsNullOrEmpty(displayName)) continue;

				if (!string.IsNullOrEmpty(description))
					_items.Add(Strings.Get("overlay.resupply.item_with_desc", displayName, description));
				else
					_items.Add(displayName);

				_slots.Add(slot);
			}
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			if (index < 0 || index >= _slots.Count) {
				Speech.Say(Strings.Get("common.cannot_select"));
				SoundManager.PlayFailed();
				return;
			}

			var slot = _slots[index];
			if (slot == null) {
				Speech.Say(Strings.Get("common.cannot_select"));
				SoundManager.PlayFailed();
				return;
			}

			EnsureTypes();
			if (_slotOnClickMethod == null) {
				Speech.Say(Strings.Get("common.cannot_select"));
				SoundManager.PlayFailed();
				return;
			}

			try {
				_slotOnClickMethod.Invoke(slot, null);
				SoundManager.PlayButtonClick();
			} catch (System.Exception ex) {
				Debug.LogError($"[ATSAccessibility] ResupplyOverlay: Failed to pick: {ex.Message}");
				Speech.Say(Strings.Get("common.cannot_select"));
				SoundManager.PlayFailed();
			}
		}

		protected override int SearchItemCount => 0; // No search

		// Escape passes to game to close popup naturally
		protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

		protected override void StorePopup(object popup) {
			_popup = popup;
			EnsureTypes();
		}

		protected override void OnClosed() {
			_popup = null;
			_items.Clear();
			_slots.Clear();
		}

		// ========================================
		// DETECTION
		// ========================================

		public static bool IsCycleEffectsPickPopup(object popup) {
			if (popup == null) return false;
			if (_cycleEffectsPickPopupType == null)
				_cycleEffectsPickPopupType = GameReflection.GameAssembly?.GetType("Eremite.View.HUD.CycleEffectsPickPopup");
			return _cycleEffectsPickPopupType != null && _cycleEffectsPickPopupType.IsInstanceOfType(popup);
		}

		// ========================================
		// REFLECTION CACHING
		// ========================================

		private static void EnsureTypes() {
			if (_typesCached) return;

			var assembly = GameReflection.GameAssembly;
			if (assembly == null) return;

			try {
				var popupType = assembly.GetType("Eremite.View.HUD.CycleEffectsPickPopup");
				if (popupType != null) {
					_slotsField = popupType.GetField("slots", GameReflection.NonPublicInstance);
				}

				var slotType = assembly.GetType("Eremite.View.HUD.CycleEffectsPickSlot");
				if (slotType != null) {
					_slotModelField = slotType.GetField("model", GameReflection.NonPublicInstance);
					_slotOnClickMethod = slotType.GetMethod("OnClick",
						BindingFlags.NonPublic | BindingFlags.Instance);
				}

				var modelType = assembly.GetType("Eremite.Model.Meta.CycleEffectModel");
				if (modelType != null) {
					_modelDisplayNameProperty = modelType.GetProperty("DisplayName", GameReflection.PublicInstance);
					_modelDescriptionProperty = modelType.GetProperty("Description", GameReflection.PublicInstance);
				}
			} catch (System.Exception ex) {
				Debug.LogError($"[ATSAccessibility] ResupplyOverlay: Type caching failed: {ex.Message}");
			}

			_typesCached = true;
		}
	}
}
