using ATSAccessibility.Utils;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Reflection access for the main-menu DlcShopButton (Frogs / Bats icons that link
	/// to the platform DLC page). These buttons are icon-only, so the generic
	/// UIElementFinder falls back to the GameObject name ("Frogs" / "Bats"). This
	/// helper pulls the localized display name and description from the DLCConfig
	/// the button references, plus ownership state from IDLCsService.
	/// </summary>
	public static class DlcShopButtonReflection {
		private static bool _cached = false;

		// DlcShopButton
		private static Type _buttonType = null;
		private static FieldInfo _dlcField = null;       // DLCType
		private static FieldInfo _configField = null;    // DLCConfig (populated after Start)

		// DLCConfig
		private static FieldInfo _displayNameField = null;        // LocaText
		private static FieldInfo _shopDescField = null;           // LocaText

		// IMetaServices / IDLCsService
		private static PropertyInfo _dlcsServiceProperty = null;  // IMetaServices.DLCsService
		private static MethodInfo _hasDlcMethod = null;           // IDLCsService.HasDLC(DLCType)

		private static void EnsureCached() {
			if (_cached) return;
			_cached = true;

			ReflectionHelper.InitCache("DlcShopButtonReflection", assembly => {
				_buttonType = assembly.GetType("Eremite.View.UI.Platforms.DlcShopButton");
				if (_buttonType != null) {
					_dlcField = _buttonType.GetField("dlc", GameReflection.NonPublicInstance);
					_configField = _buttonType.GetField("config", GameReflection.NonPublicInstance);
				}

				var configType = assembly.GetType("Eremite.Model.Configs.DLCConfig");
				if (configType != null) {
					_displayNameField = configType.GetField("displayName", GameReflection.PublicInstance);
					_shopDescField = configType.GetField("shopButtonTooltipDesc", GameReflection.PublicInstance);
				}

				var metaServicesType = assembly.GetType("Eremite.Services.IMetaServices");
				if (metaServicesType != null) {
					_dlcsServiceProperty = metaServicesType.GetProperty("DLCsService", GameReflection.PublicInstance);
				}

				var dlcsServiceType = assembly.GetType("Eremite.Services.IDLCsService");
				if (dlcsServiceType != null) {
					_hasDlcMethod = dlcsServiceType.GetMethod("HasDLC");
				}
			});
		}

		/// <summary>
		/// Returns the localized "display name. description" string for a DlcShopButton,
		/// or null if the element is not a DlcShopButton or the config has not yet been
		/// populated (DlcShopButton.Start runs CallWhenControllerReady).
		/// Mirrors DlcShopButton.GetTooltipDesc: shopButtonTooltipDesc when unowned,
		/// "MenuUI_DlcShopButton_Tooltip_Desc_Active" when owned.
		/// </summary>
		public static string TryGetLabel(Selectable element) {
			var btn = GetButton(element);
			if (btn == null) return null;

			var config = ReflectionHelper.GetField(_configField, btn);
			if (config == null) return null;

			string name = GameReflection.GetLocaText(ReflectionHelper.GetField(_displayNameField, config));
			string desc = IsOwned(btn)
				? GameReflection.ResolveLocaKey("MenuUI_DlcShopButton_Tooltip_Desc_Active")
				: GameReflection.GetLocaText(ReflectionHelper.GetField(_shopDescField, config));

			if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(desc)) return null;
			if (string.IsNullOrEmpty(desc)) return name;
			if (string.IsNullOrEmpty(name)) return desc;
			return Strings.Get("overlay.ui.dlc_shop_button.label", name, desc);
		}

		private static Component GetButton(Selectable element) {
			if (element == null) return null;
			EnsureCached();
			if (_buttonType == null) return null;
			return element.GetComponent(_buttonType);
		}

		private static bool IsOwned(Component btn) {
			var dlcType = ReflectionHelper.GetField(_dlcField, btn);
			if (dlcType == null) return false;

			var dlcsService = ReflectionHelper.GetProp(_dlcsServiceProperty, GameReflection.GetMetaServices());
			if (dlcsService == null || _hasDlcMethod == null) return false;

			return ReflectionHelper.InvokeBool(_hasDlcMethod, dlcsService, dlcType);
		}
	}
}
