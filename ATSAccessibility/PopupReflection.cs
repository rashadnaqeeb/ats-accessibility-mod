using System.Reflection;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Provides reflection-based access to shared popup slot types (GoodSlot, EffectSlot,
	/// Good, EffectModel) and Popup.Hide. Used by AssaultResultOverlay, RewardsPackOverlay,
	/// and CycleEndOverlay.
	///
	/// CRITICAL RULES:
	/// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
	/// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
	/// </summary>
	public static class PopupReflection {
		// GoodSlot.good field
		private static FieldInfo _goodSlotGoodField;

		// EffectSlot.model field
		private static FieldInfo _effectSlotModelField;

		// Good fields
		private static FieldInfo _goodNameField;
		private static FieldInfo _goodAmountField;

		// EffectModel properties
		private static PropertyInfo _effectDisplayNameProperty;
		private static PropertyInfo _effectDescriptionProperty;

		// Popup.Hide() method
		private static MethodInfo _popupHideMethod;

		private static bool _typesCached;

		private static void EnsureTypes() {
			if (_typesCached) return;
			_typesCached = true;

			ReflectionHelper.InitCache("PopupReflection", assembly => {
				var goodSlotType = assembly.GetType("Eremite.View.HUD.GoodSlot");
				if (goodSlotType != null) {
					_goodSlotGoodField = goodSlotType.GetField("good", GameReflection.NonPublicInstance);
				}

				var effectSlotType = assembly.GetType("Eremite.View.HUD.EffectSlot");
				if (effectSlotType != null) {
					_effectSlotModelField = effectSlotType.GetField("model",
						BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
					if (_effectSlotModelField == null) {
						_effectSlotModelField = effectSlotType.GetField("model", GameReflection.NonPublicInstance);
					}
				}

				var effectModelType = assembly.GetType("Eremite.Model.EffectModel");
				if (effectModelType != null) {
					_effectDisplayNameProperty = effectModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
					_effectDescriptionProperty = effectModelType.GetProperty("Description", GameReflection.PublicInstance);
				}

				var goodType = assembly.GetType("Eremite.Model.Good");
				if (goodType != null) {
					_goodNameField = goodType.GetField("name", GameReflection.PublicInstance);
					_goodAmountField = goodType.GetField("amount", GameReflection.PublicInstance);
				}

				var popupType = assembly.GetType("Eremite.View.Popups.Popup");
				if (popupType != null) {
					_popupHideMethod = popupType.GetMethod("Hide", GameReflection.PublicInstance);
				}
			});
		}

		/// <summary>
		/// Get the Good object from a GoodSlot.
		/// </summary>
		public static object GetGoodFromSlot(object slot) {
			EnsureTypes();
			return ReflectionHelper.GetField(_goodSlotGoodField, slot);
		}

		/// <summary>
		/// Get the internal name string from a Good struct.
		/// </summary>
		public static string GetGoodName(object good) {
			EnsureTypes();
			return ReflectionHelper.GetString(_goodNameField, good);
		}

		/// <summary>
		/// Get the amount from a Good struct.
		/// </summary>
		public static int GetGoodAmount(object good) {
			EnsureTypes();
			return ReflectionHelper.GetInt(_goodAmountField, good);
		}

		/// <summary>
		/// Get the EffectModel from an EffectSlot.
		/// </summary>
		public static object GetEffectModel(object slot) {
			EnsureTypes();
			return ReflectionHelper.GetField(_effectSlotModelField, slot);
		}

		/// <summary>
		/// Get the display name from an EffectModel.
		/// </summary>
		public static string GetEffectDisplayName(object model) {
			EnsureTypes();
			return ReflectionHelper.GetPropString(_effectDisplayNameProperty, model);
		}

		/// <summary>
		/// Get the description from an EffectModel.
		/// </summary>
		public static string GetEffectDescription(object model) {
			EnsureTypes();
			return ReflectionHelper.GetPropString(_effectDescriptionProperty, model);
		}

		/// <summary>
		/// Hide a popup using Popup.Hide().
		/// </summary>
		public static bool HidePopup(object popup) {
			EnsureTypes();
			if (_popupHideMethod == null) return false;
			return ReflectionHelper.InvokeVoid(_popupHideMethod, popup);
		}

		/// <summary>
		/// Get the text value from a TMP_Text component field on a popup.
		/// </summary>
		public static string GetTmpText(object tmpTextComponent) {
			if (tmpTextComponent == null) return null;

			try {
				var textProp = tmpTextComponent.GetType().GetProperty("text", GameReflection.PublicInstance);
				return textProp?.GetValue(tmpTextComponent) as string;
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Resolve a Good's internal name to its localized display name via Settings.GetGood().
		/// </summary>
		public static string GetGoodDisplayName(string goodName) {
			if (string.IsNullOrEmpty(goodName)) return goodName;

			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return goodName;

				var getGoodMethod = settings.GetType().GetMethod("GetGood", new[] { typeof(string) });
				var goodModel = getGoodMethod?.Invoke(settings, new object[] { goodName });
				if (goodModel == null) return goodName;

				var displayNameField = goodModel.GetType().GetField("displayName", GameReflection.PublicInstance);
				var displayNameObj = displayNameField?.GetValue(goodModel);
				return GameReflection.GetLocaText(displayNameObj) ?? goodName;
			} catch {
				return goodName;
			}
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(PopupReflection), "PopupReflection");
		}
	}
}
