using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the TraderPanel.
	/// Provides multi-level navigation for trader interaction:
	/// - Mode 1 (No Trader): Flat list with next trader info and force arrival
	/// - Mode 2 (Trader Present): Main menu with goods trading, perks, and assault
	///
	/// Level 0: Main menu (NoTrader items or TraderPresent items)
	/// Level 1: Branch content — GoodsTrade goods list (with tabs) or Perks list
	/// Confirmations are modal (not a level) — intercepted in HandleSpecialKey.
	/// </summary>
	public class TraderOverlay: MenuBase {
		// ========================================
		// NAVIGATION STATE
		// ========================================

		private enum Mode { NoTrader, TraderPresent }
		private enum Branch { GoodsTrade, Perks }
		private enum Tab { Sell, Buy }
		private enum ConfirmState { None, Trade, Assault }

		private class NavItem {
			public string Label;
			public string SearchName;
			public Action OnActivate;
		}

		private class TradeGoodItem {
			public string Name;
			public string DisplayName;
			public int MaxAmount;
			public int OfferedAmount;
			public float UnitValue;
			public bool IsSell;
		}

		private class PerkItem {
			public string Name;
			public string DisplayName;
			public string Description;
			public float Price;
			public bool Discounted;
			public float DiscountRatio;
			public bool Sold;
			public object EffectState;
		}

		private Mode _mode;
		private Branch _branch;
		private Tab _currentTab;
		private ConfirmState _confirmState;

		// Mode 1 (No Trader) data
		private List<NavItem> _noTraderItems = new List<NavItem>();

		// Mode 2 (Trader Present) data
		private List<NavItem> _mainMenuItems = new List<NavItem>();
		private List<TradeGoodItem> _sellGoods = new List<TradeGoodItem>();
		private List<TradeGoodItem> _buyGoods = new List<TradeGoodItem>();
		private List<PerkItem> _perks = new List<PerkItem>();

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("common.trader");
		protected override string EmptyMessage => Strings.Get("overlay.trader.empty");

		protected override int GetItemCount() {
			if (Level == 0) {
				if (_mode == Mode.NoTrader) return _noTraderItems.Count;
				return _mainMenuItems.Count;
			}

			switch (_branch) {
				case Branch.GoodsTrade:
					return (_currentTab == Tab.Sell ? _sellGoods : _buyGoods).Count;
				case Branch.Perks:
					return _perks.Count;
				default:
					return 0;
			}
		}

		protected override string GetLabel(int index) {
			if (Level == 0) {
				if (_mode == Mode.NoTrader) {
					if (index < 0 || index >= _noTraderItems.Count) return null;
					return _noTraderItems[index].Label;
				} else {
					if (index < 0 || index >= _mainMenuItems.Count) return null;
					return _mainMenuItems[index].Label;
				}
			}

			switch (_branch) {
				case Branch.GoodsTrade: {
						var goodsList = _currentTab == Tab.Sell ? _sellGoods : _buyGoods;
						if (index < 0 || index >= goodsList.Count) return null;
						return BuildGoodLabel(goodsList[index]);
					}
				case Branch.Perks:
					if (index < 0 || index >= _perks.Count) return null;
					return BuildPerkLabel(_perks[index]);
				default:
					return null;
			}
		}

		protected override void RefreshData() {
			if (TradeReflection.IsTraderPresent()) {
				_mode = Mode.TraderPresent;
				RefreshTraderData();
			} else {
				_mode = Mode.NoTrader;
				RefreshNoTraderData();
			}
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			if (Level == 0) {
				var items = _mode == Mode.NoTrader ? _noTraderItems : _mainMenuItems;
				if (index < 0 || index >= items.Count) return;
				var item = items[index];
				if (item.OnActivate != null)
					item.OnActivate();
				else
					Speech.Say(item.Label);
				return;
			}

			switch (_branch) {
				case Branch.Perks:
					BuyCurrentPerk();
					break;
			}
		}

		protected override EscapeAction OnEscape() {
			return Level > 0 ? EscapeAction.GoBack : EscapeAction.PassThrough;
		}

		private static readonly List<HelpEntry> _traderHelpEntries = new List<HelpEntry>(MenuBaseHelpEntries) {
			new HelpEntry("Left/Right", Strings.Get("overlay.trader.help.tab")),
			new HelpEntry("Alt+B", Strings.Get("overlay.trader.help.balance")),
			new HelpEntry("Alt+A", Strings.Get("overlay.trader.help.accept")),
		};
		public override IReadOnlyList<HelpEntry> GetHelpEntries() => _traderHelpEntries;

		protected override void OnGoBack() {
			if (_branch == Branch.Perks) {
				RefreshMainMenu();
			}
		}

		protected override void OnAdjust(int index, int dir, KeyboardManager.KeyModifiers modifiers) {
			if (Level == 1 && _branch == Branch.GoodsTrade) {
				AdjustQuantity(dir * (modifiers.Shift ? 10 : 1));
			}
		}

		protected override string GetOpenAnnouncement() {
			if (_mode == Mode.NoTrader) {
				if (_noTraderItems.Count > 0)
					return Strings.Get("overlay.trader.open.no_trader", _noTraderItems[0].Label);
				return Strings.Get("overlay.trader.open.no_info");
			} else {
				if (_mainMenuItems.Count > 0)
					return Strings.Get("overlay.trader.open.present", _mainMenuItems[0].Label);
				return Strings.Get("common.trader");
			}
		}

		protected override void OnClosed() {
			_confirmState = ConfirmState.None;
			ClearData();
		}

		// ========================================
		// SEARCH OVERRIDES
		// ========================================

		protected override string GetSearchName(int index) {
			if (Level == 0) {
				if (_mode == Mode.NoTrader) {
					if (index < 0 || index >= _noTraderItems.Count) return null;
					return _noTraderItems[index].SearchName;
				} else {
					if (index < 0 || index >= _mainMenuItems.Count) return null;
					return _mainMenuItems[index].SearchName;
				}
			}

			switch (_branch) {
				case Branch.GoodsTrade:
					var goodsList = _currentTab == Tab.Sell ? _sellGoods : _buyGoods;
					return (index >= 0 && index < goodsList.Count) ? goodsList[index].DisplayName : null;
				case Branch.Perks:
					return (index >= 0 && index < _perks.Count) ? _perks[index].DisplayName : null;
				default:
					return null;
			}
		}

		// ========================================
		// SPECIAL KEY HANDLING
		// ========================================

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			// Confirmation modal intercepts all keys
			if (_confirmState != ConfirmState.None)
				return ProcessConfirmationKey(keyCode);

			// Alt+B/A available when trader present (any level)
			if (_mode == Mode.TraderPresent && modifiers.Alt) {
				if (keyCode == KeyCode.B) { AnnounceBalance(); return true; }
				if (keyCode == KeyCode.A) { TryAcceptTrade(); return true; }
			}

			// Goods trade tab switching and +/- at level 1
			if (Level == 1 && _branch == Branch.GoodsTrade) {
				switch (keyCode) {
					case KeyCode.LeftArrow:
						if (_currentTab != Tab.Sell) {
							_currentTab = Tab.Sell;
							_indices[1] = 0;
							_search.Clear();
							AnnounceSellTab();
						}
						return true;

					case KeyCode.RightArrow:
						if (_currentTab != Tab.Buy) {
							_currentTab = Tab.Buy;
							_indices[1] = 0;
							_search.Clear();
							AnnounceBuyTab();
						}
						return true;

					case KeyCode.Plus:
						AdjustQuantity(modifiers.Shift ? 10 : 1);
						return true;
				}
			}

			return null;
		}

		// ========================================
		// MODE 1: NO TRADER
		// ========================================

		private void RefreshNoTraderData() {
			_noTraderItems.Clear();

			string traderName = TradeReflection.GetTraderName();
			string traderLabel = TradeReflection.GetTraderLabel() ?? "";

			if (string.IsNullOrEmpty(traderName)) {
				if (TradeReflection.IsTradingBlocked()) {
					_noTraderItems.Add(new NavItem {
						Label = Strings.Get("common.trading_is_blocked"),
						SearchName = null,
						OnActivate = null
					});
				} else {
					_noTraderItems.Add(new NavItem {
						Label = Strings.Get("overlay.trader.no_trader"),
						SearchName = null,
						OnActivate = null
					});
				}
				return;
			}

			float progress = TradeReflection.GetTravelProgress();
			float timeToArrival = TradeReflection.GetTimeToArrival();
			bool isStorm = TradeReflection.IsStormSeason();
			string arrivalInfo;

			if (TradeReflection.IsTradingBlocked()) {
				arrivalInfo = Strings.Get("overlay.trader.arrival.blocked");
			} else if (isStorm) {
				float stormEnds = TradeReflection.GetTimeTillSeasonChange();
				if (progress >= 1f) {
					arrivalInfo = Strings.Get("overlay.trader.arrival.waiting_storm", FormattingUtils.FormatTime(stormEnds));
				} else {
					arrivalInfo = Strings.Get("overlay.trader.arrival.travel_paused", Mathf.RoundToInt(progress * 100), FormattingUtils.FormatTime(stormEnds));
				}
			} else if (timeToArrival > 0) {
				arrivalInfo = Strings.Get("overlay.trader.arrival.arriving_in", FormattingUtils.FormatTime(timeToArrival));
			} else if (progress < 1f) {
				arrivalInfo = Strings.Get("overlay.trader.arrival.traveled", Mathf.RoundToInt(progress * 100));
			} else {
				arrivalInfo = Strings.Get("overlay.trader.arrival.arriving_soon");
			}

			string headerLabel = !string.IsNullOrEmpty(traderLabel)
				? Strings.Get("overlay.trader.next.with_label", traderName, traderLabel, arrivalInfo)
				: Strings.Get("overlay.trader.next", traderName, arrivalInfo);

			_noTraderItems.Add(new NavItem {
				Label = headerLabel,
				SearchName = null,
				OnActivate = null
			});

			string description = TradeReflection.GetTraderDescription();
			if (!string.IsNullOrEmpty(description)) {
				_noTraderItems.Add(new NavItem {
					Label = description,
					SearchName = null,
					OnActivate = null
				});
			}

			if (!TradeReflection.IsTradingBlocked()) {
				if (TradeReflection.CanForceArrival()) {
					float cost = TradeReflection.GetForceArrivalCost();
					_noTraderItems.Add(new NavItem {
						Label = Strings.Get("overlay.trader.force.available", cost),
						SearchName = Strings.Get("overlay.trader.force.search"),
						OnActivate = ActivateForceArrival
					});
				} else {
					string reason = TradeReflection.GetForceArrivalUnavailableReason() ?? Strings.Get("overlay.trader.force.unavailable");
					_noTraderItems.Add(new NavItem {
						Label = Strings.Get("overlay.trader.force.reason", reason),
						SearchName = null,
						OnActivate = null
					});
				}
			}
		}

		private void ActivateForceArrival() {
			if (!TradeReflection.CanForceArrival()) {
				Speech.Say(Strings.Get("overlay.trader.force.cannot"));
				SoundManager.PlayFailed();
				return;
			}

			if (TradeReflection.ForceTraderArrival()) {
				SoundManager.PlayButtonClick();
				Speech.Say(Strings.Get("overlay.trader.force.forced"));
				RefreshNoTraderData();
				CurrentIndex = 0;
				if (_noTraderItems.Count > 0)
					Speech.Say(_noTraderItems[0].Label);
			} else {
				Speech.Say(Strings.Get("overlay.trader.force.failed"));
				SoundManager.PlayFailed();
			}
		}

		// ========================================
		// MODE 2: TRADER PRESENT
		// ========================================

		private void RefreshTraderData() {
			RefreshSellGoods();
			RefreshBuyGoods();
			RefreshPerks();
			RefreshMainMenu();
		}

		private void RefreshMainMenu() {
			_mainMenuItems.Clear();

			string traderName = TradeReflection.GetTraderName() ?? Strings.Get("common.unknown");
			string traderLabel = TradeReflection.GetTraderLabel() ?? "";
			float timeLeft = TradeReflection.GetStayingTimeLeft();
			string dialogue = TradeReflection.GetTraderDialogue() ?? "";

			string infoLabel = !string.IsNullOrEmpty(traderLabel)
				? Strings.Get("overlay.trader.info.with_label", traderName, traderLabel, FormattingUtils.FormatTime(timeLeft))
				: Strings.Get("overlay.trader.info", traderName, FormattingUtils.FormatTime(timeLeft));

			if (!string.IsNullOrEmpty(dialogue))
				infoLabel += Strings.Get("overlay.trader.info.dialogue", dialogue);

			_mainMenuItems.Add(new NavItem {
				Label = infoLabel,
				SearchName = null,
				OnActivate = null
			});

			_mainMenuItems.Add(new NavItem {
				Label = Strings.Get("overlay.trader.goods_trade"),
				SearchName = Strings.Get("common.goods"),
				OnActivate = () => EnterGoodsTrade()
			});

			int unsoldPerks = 0;
			foreach (var p in _perks)
				if (!p.Sold) unsoldPerks++;

			_mainMenuItems.Add(new NavItem {
				Label = unsoldPerks > 0 ? Strings.Get("overlay.trader.perks", unsoldPerks) : Strings.Get("overlay.trader.perks.none"),
				SearchName = Strings.Get("common.perks"),
				OnActivate = () => EnterPerks()
			});

			if (TradeReflection.CanAssaultTrader()) {
				_mainMenuItems.Add(new NavItem {
					Label = Strings.Get("overlay.trader.assault"),
					SearchName = Strings.Get("overlay.trader.search.assault"),
					OnActivate = () => EnterAssaultConfirm()
				});
			}
		}

		private void RefreshSellGoods() {
			_sellGoods.Clear();
			var goods = TradeReflection.GetVillageGoods();
			foreach (var g in goods) {
				_sellGoods.Add(new TradeGoodItem {
					Name = g.Name,
					DisplayName = g.DisplayName,
					MaxAmount = g.StorageAmount,
					OfferedAmount = 0,
					UnitValue = g.UnitValue,
					IsSell = true
				});
			}
		}

		private void RefreshBuyGoods() {
			_buyGoods.Clear();
			var goods = TradeReflection.GetTraderGoods();
			foreach (var g in goods) {
				_buyGoods.Add(new TradeGoodItem {
					Name = g.Name,
					DisplayName = g.DisplayName,
					MaxAmount = g.StorageAmount,
					OfferedAmount = 0,
					UnitValue = g.UnitValue,
					IsSell = false
				});
			}
		}

		private void RefreshPerks() {
			_perks.Clear();
			var perks = TradeReflection.GetPerks();
			foreach (var p in perks) {
				_perks.Add(new PerkItem {
					Name = p.Name,
					DisplayName = p.DisplayName,
					Description = p.Description,
					Price = p.Price,
					Discounted = p.Discounted,
					DiscountRatio = p.DiscountRatio,
					Sold = p.Sold,
					EffectState = p.EffectState
				});
			}
		}

		private void ClearData() {
			_noTraderItems.Clear();
			_mainMenuItems.Clear();
			_sellGoods.Clear();
			_buyGoods.Clear();
			_perks.Clear();
		}

		// ========================================
		// GOODS TRADE SUB-LEVEL
		// ========================================

		private void EnterGoodsTrade() {
			_branch = Branch.GoodsTrade;
			_currentTab = Tab.Sell;
			SetLevel(1);
			_indices[1] = 0;
			_search.Clear();

			SoundManager.PlayButtonClick();
			AnnounceSellTab();
		}

		private void AnnounceSellTab() {
			if (_sellGoods.Count > 0) {
				Speech.Say(Strings.Get("overlay.trader.sell_tab", BuildGoodLabel(_sellGoods[0])));
			} else {
				Speech.Say(Strings.Get("overlay.trader.sell_tab.empty"));
			}
		}

		private void AnnounceBuyTab() {
			if (_buyGoods.Count > 0) {
				Speech.Say(Strings.Get("overlay.trader.buy_tab", BuildGoodLabel(_buyGoods[0])));
			} else {
				Speech.Say(Strings.Get("overlay.trader.buy_tab.empty"));
			}
		}

		private string BuildGoodLabel(TradeGoodItem good) {
			if (good.OfferedAmount > 0) {
				float totalValue = good.IsSell
					? TradeReflection.GetGoodSellValue(good.Name, good.OfferedAmount)
					: TradeReflection.GetGoodBuyValue(good.Name, good.OfferedAmount);

				if (good.IsSell)
					return Strings.Get("overlay.trader.good.sell.offered", good.DisplayName, good.MaxAmount, good.OfferedAmount, totalValue);
				else
					return Strings.Get("overlay.trader.good.buy.offered", good.DisplayName, good.MaxAmount, good.OfferedAmount, totalValue);
			} else {
				if (good.IsSell)
					return Strings.Get("overlay.trader.good.sell", good.DisplayName, good.MaxAmount, good.UnitValue);
				else
					return Strings.Get("overlay.trader.good.buy", good.DisplayName, good.MaxAmount, good.UnitValue);
			}
		}

		private void AdjustQuantity(int delta) {
			var currentList = _currentTab == Tab.Sell ? _sellGoods : _buyGoods;
			if (CurrentIndex < 0 || CurrentIndex >= currentList.Count) return;

			var good = currentList[CurrentIndex];
			int oldAmount = good.OfferedAmount;
			good.OfferedAmount = Mathf.Clamp(good.OfferedAmount + delta, 0, good.MaxAmount);

			if (good.OfferedAmount != oldAmount) {
				SoundManager.PlayButtonClick();
			}

			float balance = CalculateBalance();
			Speech.Say(Strings.Get("overlay.trader.adjust", good.OfferedAmount, balance));
		}

		private float CalculateBalance() {
			CalculateTradeTotals(out float sellTotal, out float buyTotal);
			return sellTotal - buyTotal;
		}

		private void AnnounceBalance() {
			CalculateTradeTotals(out float sellTotal, out float buyTotal);
			float balance = sellTotal - buyTotal;
			string fairness = Strings.Get(balance >= 0 ? "overlay.trader.balance.fair" : "overlay.trader.balance.unfair");

			Speech.Say(Strings.Get("overlay.trader.balance", sellTotal, buyTotal, balance, fairness));
		}

		private void CalculateTradeTotals(out float sellTotal, out float buyTotal) {
			sellTotal = 0f;
			foreach (var g in _sellGoods) {
				if (g.OfferedAmount > 0)
					sellTotal += TradeReflection.GetGoodSellValue(g.Name, g.OfferedAmount);
			}

			buyTotal = 0f;
			foreach (var g in _buyGoods) {
				if (g.OfferedAmount > 0)
					buyTotal += TradeReflection.GetGoodBuyValue(g.Name, g.OfferedAmount);
			}
		}

		private void TryAcceptTrade() {
			bool buyingAnything = false;
			foreach (var g in _buyGoods) {
				if (g.OfferedAmount > 0) {
					buyingAnything = true;
					break;
				}
			}

			if (!buyingAnything) {
				Speech.Say(Strings.Get("overlay.trader.accept.nothing"));
				SoundManager.PlayFailed();
				return;
			}

			float sellTotal = 0f;
			var sellList = new List<string>();
			foreach (var g in _sellGoods) {
				if (g.OfferedAmount > 0) {
					sellTotal += TradeReflection.GetGoodSellValue(g.Name, g.OfferedAmount);
					sellList.Add(Strings.Get("overlay.trader.accept.sell_item", g.OfferedAmount, g.DisplayName));
				}
			}

			float buyTotal = 0f;
			var buyList = new List<string>();
			foreach (var g in _buyGoods) {
				if (g.OfferedAmount > 0) {
					buyTotal += TradeReflection.GetGoodBuyValue(g.Name, g.OfferedAmount);
					buyList.Add(Strings.Get("overlay.trader.accept.sell_item", g.OfferedAmount, g.DisplayName));
				}
			}

			float balance = sellTotal - buyTotal;

			if (balance < 0) {
				Speech.Say(Strings.Get("overlay.trader.accept.unfair", Mathf.Abs(balance)));
				SoundManager.PlayFailed();
				return;
			}

			string sellText = sellList.Count > 0 ? string.Join(", ", sellList) : Strings.Get("overlay.trader.accept.side_nothing");
			string buyText = buyList.Count > 0 ? string.Join(", ", buyList) : Strings.Get("overlay.trader.accept.side_nothing");

			_confirmState = ConfirmState.Trade;
			Speech.Say(Strings.Get("overlay.trader.accept.confirm", sellText, buyText, balance));
		}

		// ========================================
		// PERKS SUB-LEVEL
		// ========================================

		private void EnterPerks() {
			_branch = Branch.Perks;
			SetLevel(1);
			_indices[1] = 0;
			_search.Clear();

			SoundManager.PlayButtonClick();

			if (_perks.Count > 0) {
				Speech.Say(Strings.Get("overlay.trader.perks.header", BuildPerkLabel(_perks[0])));
			} else {
				Speech.Say(Strings.Get("overlay.trader.perks.header.empty"));
			}
		}

		private string BuildPerkLabel(PerkItem perk) {
			string nameAndDesc = !string.IsNullOrEmpty(perk.Description)
				? Strings.Get("overlay.trader.perk.with_desc", perk.DisplayName, perk.Description)
				: perk.DisplayName;

			if (perk.Sold) {
				return Strings.Get("overlay.trader.perk.sold", nameAndDesc);
			}

			if (perk.Discounted) {
				int discountPercent = Mathf.RoundToInt((1f - perk.DiscountRatio) * 100);
				return Strings.Get("overlay.trader.perk.discounted", nameAndDesc, perk.Price, discountPercent);
			}

			return Strings.Get("overlay.trader.perk.full", nameAndDesc, perk.Price);
		}

		private void BuyCurrentPerk() {
			if (CurrentIndex < 0 || CurrentIndex >= _perks.Count) return;

			var perk = _perks[CurrentIndex];
			if (perk.Sold) {
				Speech.Say(Strings.Get("overlay.trader.perk.already_sold"));
				SoundManager.PlayFailed();
				return;
			}

			int amber = TradeReflection.GetAmberInStorage();
			if (amber < perk.Price) {
				Speech.Say(Strings.Get("overlay.trader.perk.not_enough", amber, perk.Price));
				SoundManager.PlayFailed();
				return;
			}

			if (TradeReflection.BuyPerk(perk.EffectState)) {
				perk.Sold = true;
				SoundManager.PlayTraderTransactionCompleted();
				var traderSound = TradeReflection.GetTraderTransactionSound();
				if (traderSound != null)
					SoundManager.PlaySoundEffect(traderSound);
				Speech.Say(Strings.Get("overlay.trader.perk.purchased", perk.DisplayName));
				RefreshPerks();
				RefreshSellGoods();
				RefreshBuyGoods();
				RefreshMainMenu();
			} else {
				Speech.Say(Strings.Get("common.purchase_failed"));
				SoundManager.PlayFailed();
			}
		}

		// ========================================
		// ASSAULT CONFIRM
		// ========================================

		private void EnterAssaultConfirm() {
			_confirmState = ConfirmState.Assault;
			SoundManager.PlayButtonClick();
			Speech.Say(Strings.Get("overlay.trader.assault.confirm"));
		}

		// ========================================
		// CONFIRMATION HANDLING
		// ========================================

		private bool ProcessConfirmationKey(KeyCode keyCode) {
			if (_confirmState == ConfirmState.Assault) {
				switch (keyCode) {
					case KeyCode.Return:
					case KeyCode.KeypadEnter:
						ExecuteAssault();
						return true;

					case KeyCode.Escape:
						_confirmState = ConfirmState.None;
						Speech.Say(Strings.Get("overlay.trader.assault.cancelled", _mainMenuItems[CurrentIndex].Label));
						InputBlocker.BlockCancelOnce = true;
						return true;

					default:
						return true;
				}
			}

			switch (keyCode) {
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					ExecuteTrade();
					return true;

				case KeyCode.Escape:
					_confirmState = ConfirmState.None;
					Speech.Say(Strings.Get("common.cancelled"));
					InputBlocker.BlockCancelOnce = true;
					return true;

				default:
					return true;
			}
		}

		private void ExecuteAssault() {
			var result = TradeReflection.AssaultTrader();
			_confirmState = ConfirmState.None;

			if (result.Success) {
				SoundManager.PlayButtonClick();
				Speech.Say(Strings.Get("overlay.trader.assault.success", result.GoodsStolen, result.PerksStolen, result.VillagersLost));
			} else {
				Speech.Say(Strings.Get("overlay.trader.assault.failed"));
				SoundManager.PlayFailed();
				CurrentIndex = 0;
			}
		}

		private void ExecuteTrade() {
			_confirmState = ConfirmState.None;

			var sellList = new List<KeyValuePair<string, int>>();
			foreach (var g in _sellGoods) {
				if (g.OfferedAmount > 0)
					sellList.Add(new KeyValuePair<string, int>(g.Name, g.OfferedAmount));
			}

			var buyList = new List<KeyValuePair<string, int>>();
			foreach (var g in _buyGoods) {
				if (g.OfferedAmount > 0)
					buyList.Add(new KeyValuePair<string, int>(g.Name, g.OfferedAmount));
			}

			if (TradeReflection.ExecuteTrade(sellList, buyList)) {
				SoundManager.PlayTraderTransactionCompleted();
				var traderSound = TradeReflection.GetTraderTransactionSound();
				if (traderSound != null)
					SoundManager.PlaySoundEffect(traderSound);
				Speech.Say(Strings.Get("overlay.trader.trade.done"));
			} else {
				SoundManager.PlayFailed();
				Speech.Say(Strings.Get("overlay.trader.trade.failed"));
			}

			foreach (var g in _sellGoods)
				g.OfferedAmount = 0;
			foreach (var g in _buyGoods)
				g.OfferedAmount = 0;

			_indices[1] = 0;

			RefreshSellGoods();
			RefreshBuyGoods();

			if (_currentTab == Tab.Sell)
				AnnounceSellTab();
			else
				AnnounceBuyTab();
		}
	}
}
