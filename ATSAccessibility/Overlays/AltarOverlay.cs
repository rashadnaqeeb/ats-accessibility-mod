using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the Forsaken Altar panel.
	/// Provides multi-level navigation: Main Menu -> Resources/Cornerstones -> Currencies/Races.
	/// </summary>
	public class AltarOverlay: MenuBase {
		// ========================================
		// MENU LEVELS
		// ========================================

		private enum MenuLevel {
			Main,           // Resources, Cornerstones, Skip
			Resources,      // Currencies, Villagers
			Currencies,     // Individual currency toggles
			Races,          // Individual race toggles
			Cornerstones    // Cornerstone options
		}

		private enum MainItem { Resources, Cornerstones, Skip }

		private enum ResourceItem { Currencies, Villagers }

		// ========================================
		// STATE
		// ========================================

		private bool _isActive;
		private MenuLevel _menuLevel;

		private List<AltarReflection.CurrencyInfo> _currencies;
		private List<AltarReflection.RaceInfo> _races;
		private List<AltarReflection.EffectInfo> _cornerstones;

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("overlay.altar.title");
		protected override string EmptyMessage => "";

		protected override int GetItemCount() {
			switch (_menuLevel) {
				case MenuLevel.Main: return 3;
				case MenuLevel.Resources: return 2;
				case MenuLevel.Currencies: return _currencies?.Count ?? 0;
				case MenuLevel.Races: return _races?.Count ?? 0;
				case MenuLevel.Cornerstones: return _cornerstones?.Count ?? 0;
				default: return 0;
			}
		}

		protected override string GetLabel(int index) {
			switch (_menuLevel) {
				case MenuLevel.Main:
					switch ((MainItem)index) {
						case MainItem.Resources: return Strings.Get("common.resources");
						case MainItem.Cornerstones: return Strings.Get("common.cornerstones");
						case MainItem.Skip: return Strings.Get("overlay.altar.main.skip");
						default: return null;
					}

				case MenuLevel.Resources:
					switch ((ResourceItem)index) {
						case ResourceItem.Currencies:
							int totalValue = AltarReflection.GetTotalMetaValue();
							return Strings.Get("overlay.altar.resources.currencies", totalValue);
						case ResourceItem.Villagers:
							int totalVillagers = AltarReflection.GetTotalVillagers();
							bool villagersEnabled = AltarReflection.AreVillagersAllowed();
							return Strings.Get("overlay.altar.resources.villagers", totalVillagers, Strings.Get(villagersEnabled ? "common.enabled_lower" : "common.disabled_lower"));
						default: return null;
					}

				case MenuLevel.Currencies:
					if (_currencies != null && index >= 0 && index < _currencies.Count) {
						var currency = _currencies[index];
						string state = Strings.Get(currency.Enabled ? "common.enabled_lower" : "common.disabled_lower");
						return Strings.Get("overlay.altar.currency.item", currency.DisplayName, currency.Amount, state);
					}
					return null;

				case MenuLevel.Races:
					if (_races != null && index >= 0 && index < _races.Count) {
						var race = _races[index];
						string state = Strings.Get(race.Enabled ? "common.enabled_lower" : "common.disabled_lower");
						return Strings.Get("overlay.altar.race.item", race.DisplayName, race.Count, state);
					}
					return null;

				case MenuLevel.Cornerstones:
					if (_cornerstones != null && index >= 0 && index < _cornerstones.Count) {
						var cornerstone = _cornerstones[index];
						string priceStr = Strings.Get("overlay.altar.cornerstone.price", cornerstone.MetaPrice);
						if (AltarReflection.AreVillagersAllowed() && cornerstone.VillagersPrice > 0)
							priceStr += Strings.Get("overlay.altar.cornerstone.price_villagers", cornerstone.VillagersPrice);
						string affordStr = Strings.Get(cornerstone.CanAfford ? "overlay.altar.cornerstone.can_afford" : "overlay.altar.cornerstone.cannot_afford");
						string upgradeStr = cornerstone.IsUpgrade ? Strings.Get("overlay.altar.cornerstone.upgrade_suffix") : "";
						return Strings.Get("overlay.altar.cornerstone.item", cornerstone.DisplayName, priceStr, affordStr, upgradeStr);
					}
					return null;

				default: return null;
			}
		}

		protected override void RefreshData() {
			_isActive = AltarReflection.IsAltarActive();
			if (_isActive) {
				_currencies = AltarReflection.GetCurrencies();
				_races = AltarReflection.GetRaces();
				_cornerstones = AltarReflection.GetCurrentPick();
			}
		}

		protected override EnterAction OnEnter(int index) {
			switch (_menuLevel) {
				case MenuLevel.Main:
					if ((MainItem)index == MainItem.Skip)
						return EnterAction.Action;
					return EnterAction.DrillDown;

				case MenuLevel.Resources:
					return EnterAction.DrillDown;

				case MenuLevel.Currencies:
				case MenuLevel.Races:
				case MenuLevel.Cornerstones:
					return EnterAction.Action;

				default:
					return EnterAction.None;
			}
		}

		protected override void OnDrillDown(int index) {
			switch (_menuLevel) {
				case MenuLevel.Main:
					if ((MainItem)index == MainItem.Resources)
						_menuLevel = MenuLevel.Resources;
					else if ((MainItem)index == MainItem.Cornerstones)
						_menuLevel = MenuLevel.Cornerstones;
					break;

				case MenuLevel.Resources:
					if ((ResourceItem)index == ResourceItem.Currencies) {
						_menuLevel = MenuLevel.Currencies;
						_currencies = AltarReflection.GetCurrencies();
					} else if ((ResourceItem)index == ResourceItem.Villagers) {
						_menuLevel = MenuLevel.Races;
						_races = AltarReflection.GetRaces();
					}
					break;
			}
		}

		protected override void OnGoBack() {
			switch (_menuLevel) {
				case MenuLevel.Resources:
				case MenuLevel.Cornerstones:
					_menuLevel = MenuLevel.Main;
					break;
				case MenuLevel.Currencies:
				case MenuLevel.Races:
					_menuLevel = MenuLevel.Resources;
					break;
			}
		}

		protected override void OnAction(int index) {
			switch (_menuLevel) {
				case MenuLevel.Currencies:
					ToggleCurrency();
					break;
				case MenuLevel.Races:
					ToggleRace();
					break;
				case MenuLevel.Cornerstones:
					PurchaseCornerstone();
					break;
				case MenuLevel.Main:
					if ((MainItem)index == MainItem.Skip)
						ExecuteSkip();
					break;
			}
		}

		protected override void OnSpace(int index) {
			switch (_menuLevel) {
				case MenuLevel.Currencies:
					ToggleCurrency();
					break;
				case MenuLevel.Races:
					ToggleRace();
					break;
				case MenuLevel.Resources:
					if ((ResourceItem)index == ResourceItem.Villagers)
						ToggleVillagersMaster();
					break;
			}
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (!_isActive) {
				if (keyCode == KeyCode.Escape)
					return false; // Pass to game to close popup
				return true;
			}
			return null;
		}

		protected override EscapeAction OnEscape() {
			return Level > 0 ? EscapeAction.GoBack : EscapeAction.PassThrough;
		}

		protected override string GetOpenAnnouncement() {
			if (!_isActive) {
				var nextCharge = AltarReflection.GetNextChargeThreshold();
				string message = Strings.Get("overlay.altar.inactive.header");
				if (nextCharge.HasValue)
					message += Strings.Get("overlay.altar.inactive.next", nextCharge.Value);
				else
					message += Strings.Get("overlay.altar.inactive.none");
				return message;
			}

			return Strings.Get("overlay.altar.open");
		}

		protected override void OnClosed() {
			// _menuLevel is this overlay's own state on top of MenuBase's Level/_indices
			// (which Open/Close reset). Without this, closing while drilled into e.g.
			// Cornerstones leaves every future open stuck reading that submenu as the
			// root, with no way back to Resources or Skip.
			_menuLevel = MenuLevel.Main;
			_currencies?.Clear();
			_races?.Clear();
			_cornerstones?.Clear();
		}

		// ========================================
		// SEARCH
		// ========================================

		protected override int SearchItemCount {
			get {
				switch (_menuLevel) {
					case MenuLevel.Currencies: return _currencies?.Count ?? 0;
					case MenuLevel.Races: return _races?.Count ?? 0;
					case MenuLevel.Cornerstones: return _cornerstones?.Count ?? 0;
					default: return 0;
				}
			}
		}

		protected override string GetSearchName(int index) {
			switch (_menuLevel) {
				case MenuLevel.Currencies:
					if (_currencies != null && index >= 0 && index < _currencies.Count)
						return _currencies[index].DisplayName;
					break;
				case MenuLevel.Races:
					if (_races != null && index >= 0 && index < _races.Count)
						return _races[index].DisplayName;
					break;
				case MenuLevel.Cornerstones:
					if (_cornerstones != null && index >= 0 && index < _cornerstones.Count)
						return _cornerstones[index].DisplayName;
					break;
			}
			return null;
		}

		// ========================================
		// TOGGLE ACTIONS
		// ========================================

		private void ToggleCurrency() {
			if (_currencies == null || CurrentIndex < 0 || CurrentIndex >= _currencies.Count) return;

			if (AltarReflection.ToggleCurrency(CurrentIndex)) {
				SoundManager.PlayButtonClick();
				_currencies = AltarReflection.GetCurrencies();
				AnnounceCurrentItem();
			} else {
				Speech.Say(Strings.Get("overlay.altar.toggle.failed"));
				SoundManager.PlayFailed();
			}
		}

		private void ToggleRace() {
			if (_races == null || CurrentIndex < 0 || CurrentIndex >= _races.Count) return;

			if (AltarReflection.ToggleRace(CurrentIndex)) {
				SoundManager.PlayButtonClick();
				_races = AltarReflection.GetRaces();
				AnnounceCurrentItem();
			} else {
				Speech.Say(Strings.Get("overlay.altar.toggle.failed"));
				SoundManager.PlayFailed();
			}
		}

		private void ToggleVillagersMaster() {
			if (AltarReflection.ToggleVillagersAllowed()) {
				SoundManager.PlayButtonClick();
				AnnounceCurrentItem();
			} else {
				Speech.Say(Strings.Get("overlay.altar.toggle.failed"));
				SoundManager.PlayFailed();
			}
		}

		// ========================================
		// PURCHASE / SKIP
		// ========================================

		private void PurchaseCornerstone() {
			if (_cornerstones == null || CurrentIndex < 0 || CurrentIndex >= _cornerstones.Count) {
				Speech.Say(Strings.Get("overlay.altar.cornerstone.none_selected"));
				return;
			}

			var cornerstone = _cornerstones[CurrentIndex];

			if (!cornerstone.CanAfford) {
				Speech.Say(Strings.Get("common.cannot_afford"));
				SoundManager.PlayFailed();
				return;
			}

			if (AltarReflection.PickEffect(cornerstone.Model)) {
				Speech.Say(Strings.Get("overlay.altar.cornerstone.purchased", cornerstone.DisplayName));
				SoundManager.PlayButtonClick();

				if (AltarReflection.HasActivePick()) {
					RefreshData();
					SetLevel(1);
					_menuLevel = MenuLevel.Cornerstones;
					CurrentIndex = 0;
					AnnounceCurrentItem();
				}
			} else {
				Speech.Say(Strings.Get("common.purchase_failed"));
				SoundManager.PlayFailed();
			}
		}

		private void ExecuteSkip() {
			if (AltarReflection.Skip()) {
				Speech.Say(Strings.Get("common.skipped"));
				SoundManager.PlayDecline();

				if (AltarReflection.HasActivePick()) {
					RefreshData();
					SetLevel(0);
					_menuLevel = MenuLevel.Main;
					CurrentIndex = 0;
					AnnounceCurrentItem();
				}
			} else {
				Speech.Say(Strings.Get("overlay.altar.skip.failed"));
				SoundManager.PlayFailed();
			}
		}
	}
}
