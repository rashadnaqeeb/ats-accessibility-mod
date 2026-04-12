using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the TradeRoutesPopup.
	/// Level 0: Main menu (active routes summary, towns, toggles).
	/// Level 1: Active Routes or Town Offers (determined by _branch).
	/// </summary>
	public class TradeRoutesOverlay: MenuBase {
		private enum Branch { MainMenu, ActiveRoutes, TownOffers }
		private enum MainMenuItemType { ActiveRoutes, Town, AutoCollect, OnlyAvailable }

		private class MainMenuItem {
			public MainMenuItemType Type;
			public string Label;
			public string SearchName;
			public int TownIndex;
		}

		private Branch _branch;
		private int _currentTownIndex;

		private List<MainMenuItem> _mainMenuItems = new List<MainMenuItem>();
		private List<TradeRoutesReflection.RouteInfo> _routes = new List<TradeRoutesReflection.RouteInfo>();
		private List<TradeRoutesReflection.TownInfo> _towns = new List<TradeRoutesReflection.TownInfo>();
		private List<TradeRoutesReflection.OfferInfo> _offers = new List<TradeRoutesReflection.OfferInfo>();

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("common.trade_routes");
		protected override string EmptyMessage => Strings.Get("overlay.trade_routes.empty");

		protected override int GetItemCount() {
			switch (_branch) {
				case Branch.MainMenu:
					return _mainMenuItems.Count;
				case Branch.ActiveRoutes:
					return _routes.Count;
				case Branch.TownOffers:
					return _offers.Count + 1;
				default:
					return 0;
			}
		}

		protected override string GetLabel(int index) {
			switch (_branch) {
				case Branch.MainMenu:
					if (index >= 0 && index < _mainMenuItems.Count)
						return _mainMenuItems[index].Label;
					break;
				case Branch.ActiveRoutes:
					if (index >= 0 && index < _routes.Count)
						return BuildRouteLabel(_routes[index]);
					break;
				case Branch.TownOffers:
					if (index == 0) {
						if (_currentTownIndex >= 0 && _currentTownIndex < _towns.Count)
							return BuildExtendOffersLabel(_towns[_currentTownIndex]);
						return null;
					}
					int offerIdx = index - 1;
					if (offerIdx >= 0 && offerIdx < _offers.Count)
						return BuildOfferLabel(_offers[offerIdx]);
					break;
			}
			return null;
		}

		protected override void RefreshData() {
			RefreshAllData();
			RefreshMainMenu();
		}

		protected override EnterAction OnEnter(int index) {
			return EnterAction.Action;
		}

		protected override void OnAction(int index) {
			switch (_branch) {
				case Branch.MainMenu:
					ActivateMainMenuItem(index);
					break;
				case Branch.ActiveRoutes:
					CollectCurrentRoute();
					break;
				case Branch.TownOffers:
					ActivateTownOffersItem();
					break;
			}
		}

		protected override bool CanDrillDown(int index) {
			if (_branch != Branch.MainMenu) return false;
			if (index < 0 || index >= _mainMenuItems.Count) return false;
			var type = _mainMenuItems[index].Type;
			return type == MainMenuItemType.ActiveRoutes || type == MainMenuItemType.Town;
		}

		protected override void OnAdjust(int index, int dir, KeyboardManager.KeyModifiers modifiers) {
			if (_branch == Branch.TownOffers)
				AdjustAmount(dir);
		}

		protected override void OnGoBack() {
			_branch = Branch.MainMenu;
			RefreshAllData();
			RefreshMainMenu();
		}

		protected override EscapeAction OnEscape() {
			if (Level > 0) return EscapeAction.GoBack;
			// Pass to game to close popup
			return EscapeAction.PassThrough;
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (_branch == Branch.MainMenu && keyCode == KeyCode.RightArrow) {
				if (CurrentIndex >= 0 && CurrentIndex < _mainMenuItems.Count) {
					var type = _mainMenuItems[CurrentIndex].Type;
					if (type == MainMenuItemType.ActiveRoutes) {
						EnterActiveRoutes();
						return true;
					} else if (type == MainMenuItemType.Town) {
						EnterTownOffers(_mainMenuItems[CurrentIndex]);
						return true;
					}
				}
				return true;
			}

			return null;
		}

		protected override string GetOpenAnnouncement() {
			if (_mainMenuItems.Count == 0) return EmptyMessage;
			return Strings.Get("overlay.trade_routes.open", _mainMenuItems[0].Label);
		}

		protected override void AnnounceCurrentItem() {
			string label = GetLabel(CurrentIndex);
			if (!string.IsNullOrEmpty(label))
				Speech.Say(label);
		}

		protected override void OnClosed() {
			ClearData();
		}

		// ========================================
		// SEARCH OVERRIDES
		// ========================================

		protected override int SearchItemCount {
			get {
				switch (_branch) {
					case Branch.MainMenu: return _mainMenuItems.Count;
					case Branch.TownOffers: return _offers.Count + 1;
					default: return 0;
				}
			}
		}

		protected override string GetSearchName(int index) {
			switch (_branch) {
				case Branch.MainMenu:
					if (index >= 0 && index < _mainMenuItems.Count)
						return _mainMenuItems[index].SearchName;
					break;
				case Branch.TownOffers:
					if (index == 0) return null;
					int offerIdx = index - 1;
					if (offerIdx >= 0 && offerIdx < _offers.Count)
						return _offers[offerIdx].GoodName;
					break;
			}
			return null;
		}

		// ========================================
		// DATA REFRESH
		// ========================================

		private void RefreshAllData() {
			_routes = TradeRoutesReflection.GetActiveRoutes();
			_towns = TradeRoutesReflection.GetTradeTowns();
		}

		private void RefreshMainMenu() {
			_mainMenuItems.Clear();

			int activeCount = _routes.Count;
			int maxRoutes = TradeRoutesReflection.GetMaxRoutes();
			int readyCount = 0;
			foreach (var r in _routes)
				if (r.CanCollect) readyCount++;

			string activeLabel = readyCount > 0
				? Strings.Get("overlay.trade_routes.active.ready", activeCount, maxRoutes, readyCount)
				: Strings.Get("overlay.trade_routes.active", activeCount, maxRoutes);

			_mainMenuItems.Add(new MainMenuItem {
				Type = MainMenuItemType.ActiveRoutes,
				Label = activeLabel,
				SearchName = Strings.Get("common.active")
			});

			for (int i = 0; i < _towns.Count; i++) {
				var town = _towns[i];
				_mainMenuItems.Add(new MainMenuItem {
					Type = MainMenuItemType.Town,
					Label = BuildTownLabel(town),
					SearchName = town.Name,
					TownIndex = i
				});
			}

			bool autoCollect = TradeRoutesReflection.IsAutoCollectEnabled();
			_mainMenuItems.Add(new MainMenuItem {
				Type = MainMenuItemType.AutoCollect,
				Label = Strings.Get(autoCollect ? "overlay.trade_routes.auto.on" : "overlay.trade_routes.auto.off"),
				SearchName = Strings.Get("overlay.trade_routes.search.auto")
			});

			bool onlyAvailable = TradeRoutesReflection.IsOnlyAvailableEnabled();
			_mainMenuItems.Add(new MainMenuItem {
				Type = MainMenuItemType.OnlyAvailable,
				Label = Strings.Get(onlyAvailable ? "overlay.trade_routes.available.on" : "overlay.trade_routes.available.off"),
				SearchName = Strings.Get("overlay.trade_routes.search.show")
			});
		}

		private void RefreshActiveRoutes() {
			_routes = TradeRoutesReflection.GetActiveRoutes();
		}

		private void RefreshTownOffers(int townIndex) {
			if (townIndex < 0 || townIndex >= _towns.Count) {
				_offers.Clear();
				return;
			}

			var town = _towns[townIndex];
			_offers = TradeRoutesReflection.GetTownOffers(town.State);
		}

		private void ClearData() {
			_mainMenuItems.Clear();
			_routes.Clear();
			_towns.Clear();
			_offers.Clear();
		}

		// ========================================
		// MAIN MENU ACTIVATION
		// ========================================

		private void ActivateMainMenuItem(int index) {
			if (index < 0 || index >= _mainMenuItems.Count) return;

			var item = _mainMenuItems[index];
			switch (item.Type) {
				case MainMenuItemType.ActiveRoutes:
					EnterActiveRoutes();
					break;
				case MainMenuItemType.Town:
					EnterTownOffers(item);
					break;
				case MainMenuItemType.AutoCollect:
					ToggleAutoCollect();
					break;
				case MainMenuItemType.OnlyAvailable:
					ToggleOnlyAvailable();
					break;
			}
		}

		private void ToggleAutoCollect() {
			bool current = TradeRoutesReflection.IsAutoCollectEnabled();
			bool newState = !current;
			TradeRoutesReflection.SetAutoCollect(newState);
			SoundManager.PlayButtonClick();

			var item = _mainMenuItems[CurrentIndex];
			item.Label = Strings.Get(newState ? "overlay.trade_routes.auto.on" : "overlay.trade_routes.auto.off");

			if (newState) {
				int collected = TradeRoutesReflection.AutoCollectAllReady();
				if (collected > 0) {
					RefreshAllData();
					RefreshMainMenu();
					Speech.Say(Strings.Get("overlay.trade_routes.auto.collected", item.Label, collected));
					return;
				}
			}

			Speech.Say(item.Label);
		}

		private void ToggleOnlyAvailable() {
			bool current = TradeRoutesReflection.IsOnlyAvailableEnabled();
			TradeRoutesReflection.SetOnlyAvailable(!current);
			SoundManager.PlayButtonClick();

			var item = _mainMenuItems[CurrentIndex];
			item.Label = Strings.Get(!current ? "overlay.trade_routes.available.on" : "overlay.trade_routes.available.off");
			Speech.Say(item.Label);
		}

		// ========================================
		// ACTIVE ROUTES (Level 1, Branch.ActiveRoutes)
		// ========================================

		private void EnterActiveRoutes() {
			RefreshActiveRoutes();

			if (_routes.Count == 0) {
				Speech.Say(Strings.Get("overlay.trade_routes.no_active_routes"));
				SoundManager.PlayFailed();
				return;
			}

			_branch = Branch.ActiveRoutes;
			SetLevel(1);
			_indices[1] = 0;
			_search.Clear();

			Speech.Say(Strings.Get("overlay.trade_routes.active_routes_header", BuildRouteLabel(_routes[0])));
		}

		private string BuildRouteLabel(TradeRoutesReflection.RouteInfo route) {
			if (route.CanCollect) {
				return Strings.Get("overlay.trade_routes.route.ready", route.GoodName, route.GoodAmount, route.TownName, route.PriceAmount, route.PriceName);
			} else {
				int percent = Mathf.RoundToInt(route.Progress * 100);
				string time = FormattingUtils.FormatTime(route.TimeRemaining);
				return Strings.Get("overlay.trade_routes.route.traveling", route.GoodName, route.GoodAmount, route.TownName, percent, time);
			}
		}

		private void CollectCurrentRoute() {
			if (CurrentIndex < 0 || CurrentIndex >= _routes.Count) return;

			var route = _routes[CurrentIndex];
			if (!route.CanCollect) {
				int percent = Mathf.RoundToInt(route.Progress * 100);
				Speech.Say(Strings.Get("overlay.trade_routes.not_ready", percent));
				SoundManager.PlayFailed();
				return;
			}

			if (TradeRoutesReflection.Collect(route.State)) {
				SoundManager.PlayButtonClick();
				Speech.Say(Strings.Get("overlay.trade_routes.collected", route.PriceAmount, route.PriceName));

				RefreshActiveRoutes();

				if (_routes.Count == 0) {
					ReturnToMainMenu(true);
				} else {
					CurrentIndex = Mathf.Min(CurrentIndex, _routes.Count - 1);
					Speech.Say(BuildRouteLabel(_routes[CurrentIndex]));
				}
			} else {
				Speech.Say(Strings.Get("overlay.trade_routes.collect_failed"));
				SoundManager.PlayFailed();
			}
		}

		// ========================================
		// TOWN OFFERS (Level 1, Branch.TownOffers)
		// ========================================

		private void EnterTownOffers(MainMenuItem item) {
			if (item.TownIndex < 0 || item.TownIndex >= _towns.Count) {
				Speech.Say(Strings.Get("overlay.trade_routes.town.not_found"));
				SoundManager.PlayFailed();
				return;
			}

			_currentTownIndex = item.TownIndex;
			RefreshTownOffers(_currentTownIndex);

			_branch = Branch.TownOffers;
			SetLevel(1);
			_indices[1] = 0;
			_search.Clear();

			var town = _towns[_currentTownIndex];
			AnnounceTownHeader(town);
		}

		private void AnnounceTownHeader(TradeRoutesReflection.TownInfo town) {
			string standingInfo = town.IsMaxStanding
				? Strings.Get("overlay.trade_routes.town.header_max", town.Name, town.StandingLabel)
				: Strings.Get("overlay.trade_routes.town.header", town.Name, town.StandingLabel, town.CurrentStandingValue, town.ValueForLevelUp);

			Speech.Say(Strings.Get("overlay.trade_routes.town.announce", standingInfo, BuildExtendOffersLabel(town)));
		}

		private string BuildTownLabel(TradeRoutesReflection.TownInfo town) {
			var parts = new List<string>();
			parts.Add(town.Name);

			if (!string.IsNullOrEmpty(town.Faction))
				parts.Add(town.Faction);

			parts.Add(Strings.Get("overlay.trade_routes.town.distance", town.Distance));
			parts.Add(Strings.Get("overlay.trade_routes.town.standing", town.StandingLevel));
			parts.Add(town.StandingLabel);

			if (town.IsMaxStanding)
				parts.Add(Strings.Get("overlay.trade_routes.town.max"));
			else
				parts.Add(Strings.Get("overlay.trade_routes.town.progress", town.CurrentStandingValue, town.ValueForLevelUp));

			return string.Join(", ", parts);
		}

		private string BuildExtendOffersLabel(TradeRoutesReflection.TownInfo town) {
			if (town.CanExtend)
				return Strings.Get("overlay.trade_routes.extend.available", town.ExtendCost);
			else if (town.ReachedMaxOffers)
				return Strings.Get("overlay.trade_routes.extend.max");
			else
				return Strings.Get("overlay.trade_routes.extend.not_enough", town.ExtendCost);
		}

		private string BuildOfferLabel(TradeRoutesReflection.OfferInfo offer) {
			string baseLabel = Strings.Get("overlay.trade_routes.offer.base", offer.GoodName, offer.GoodAmount * offer.Multiplier);

			if (offer.Multiplier > 1)
				baseLabel += Strings.Get("overlay.trade_routes.offer.multiplier", offer.Multiplier);

			baseLabel += Strings.Get("overlay.trade_routes.offer.sells", offer.PriceAmount, offer.PriceName);
			baseLabel += Strings.Get("overlay.trade_routes.offer.time", FormattingUtils.FormatTime(offer.TravelTime));
			baseLabel += Strings.Get("overlay.trade_routes.offer.requires", offer.FuelAmount, offer.FuelName);

			if (offer.Accepted) {
				baseLabel += Strings.Get("overlay.trade_routes.offer.accepted");
			} else if (!string.IsNullOrEmpty(offer.BlockedReason)) {
				string reason = offer.BlockedReason;
				if (reason == "not enough fuel")
					reason = Strings.Get("overlay.trade_routes.offer.not_enough_fuel", offer.FuelName);
				baseLabel += Strings.Get("overlay.trade_routes.offer.blocked", reason);
			} else {
				baseLabel += Strings.Get("overlay.trade_routes.offer.available");
			}

			if (!offer.Accepted && offer.MaxMultiplier > 1)
				baseLabel += Strings.Get("overlay.trade_routes.offer.adjust_hint");

			return baseLabel;
		}

		private void ActivateTownOffersItem() {
			if (CurrentIndex == 0)
				ExtendOffers();
			else
				AcceptCurrentOffer();
		}

		private void AdjustAmount(int delta) {
			if (CurrentIndex == 0) {
				Speech.Say(Strings.Get("overlay.trade_routes.offer.navigate_first"));
				return;
			}

			int offerIndex = CurrentIndex - 1;
			if (offerIndex < 0 || offerIndex >= _offers.Count) return;

			var offer = _offers[offerIndex];
			if (offer.Accepted) {
				Speech.Say(Strings.Get("overlay.trade_routes.offer.already_accepted"));
				SoundManager.PlayFailed();
				return;
			}

			int current = TradeRoutesReflection.GetOfferAmount(offer.State);
			int newAmount = Mathf.Clamp(current + delta, 1, offer.MaxMultiplier);

			if (newAmount != current) {
				TradeRoutesReflection.SetOfferAmount(offer.State, newAmount);
				SoundManager.PlayButtonClick();

				RefreshTownOffers(_currentTownIndex);
				if (offerIndex < _offers.Count)
					Speech.Say(BuildOfferLabel(_offers[offerIndex]));
			} else {
				Speech.Say(Strings.Get(newAmount == 1 ? "overlay.trade_routes.offer.min" : "overlay.trade_routes.offer.max"));
			}
		}

		private void AcceptCurrentOffer() {
			int offerIndex = CurrentIndex - 1;
			if (offerIndex < 0 || offerIndex >= _offers.Count) return;

			var offer = _offers[offerIndex];

			if (offer.Accepted) {
				Speech.Say(Strings.Get("overlay.trade_routes.offer.already_accepted"));
				SoundManager.PlayFailed();
				return;
			}

			if (!offer.CanAccept) {
				Speech.Say(offer.BlockedReason ?? Strings.Get("overlay.trade_routes.offer.cannot"));
				SoundManager.PlayFailed();
				return;
			}

			if (TradeRoutesReflection.AcceptOffer(offer.State)) {
				SoundManager.PlayButtonClick();
				Speech.Say(Strings.Get("overlay.trade_routes.offer.accepted_msg", offer.GoodAmount * offer.Multiplier, offer.GoodName, offer.TownName));

				RefreshAllData();
				RefreshTownOffers(_currentTownIndex);

				if (_offers.Count == 0) {
					ReturnToMainMenu(false);
				} else {
					CurrentIndex = Mathf.Min(CurrentIndex, _offers.Count);
					AnnounceCurrentItem();
				}
			} else {
				Speech.Say(Strings.Get("overlay.trade_routes.offer.failed"));
				SoundManager.PlayFailed();
			}
		}

		private void ExtendOffers() {
			if (_currentTownIndex < 0 || _currentTownIndex >= _towns.Count) return;

			var town = _towns[_currentTownIndex];
			if (!town.CanExtend) {
				Speech.Say(Strings.Get("overlay.trade_routes.extend.cannot"));
				SoundManager.PlayFailed();
				return;
			}

			if (TradeRoutesReflection.ExtendOffer(town.State)) {
				SoundManager.PlayButtonClick();

				_towns = TradeRoutesReflection.GetTradeTowns();
				RefreshTownOffers(_currentTownIndex);

				if (_offers.Count > 0) {
					CurrentIndex = _offers.Count;
					Speech.Say(Strings.Get("overlay.trade_routes.extend.done_with", BuildOfferLabel(_offers[_offers.Count - 1])));
				} else {
					Speech.Say(Strings.Get("overlay.trade_routes.extend.done"));
				}
			} else {
				Speech.Say(Strings.Get("overlay.trade_routes.extend.failed"));
				SoundManager.PlayFailed();
			}
		}

		// ========================================
		// NAVIGATION HELPERS
		// ========================================

		private void ReturnToMainMenu(bool dataChanged) {
			_branch = Branch.MainMenu;
			_search.Clear();

			if (dataChanged) {
				RefreshAllData();
			}

			RefreshMainMenu();
			SetLevel(0);
			_indices[0] = 0;
			InputBlocker.BlockCancelOnce = true;
			Speech.Say(Strings.Get("overlay.trade_routes.return", _mainMenuItems[0].Label));
		}
	}
}
