using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Accessible overlay for the TraderPanel.
	/// Provides multi-level navigation for trader interaction:
	/// - Mode 1 (No Trader): Flat list with next trader info and force arrival
	/// - Mode 2 (Trader Present): Main menu with goods trading, perks, and assault
	/// </summary>
	public class TraderOverlay: MenuBase {
		// ========================================
		// NAVIGATION STATE
		// ========================================

		private enum Mode { NoTrader, TraderPresent }
		private enum SubLevel { MainMenu, GoodsTrade, Perks, AssaultConfirm, TradeConfirm }
		private enum Tab { Sell, Buy }

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
		private SubLevel _subLevel;
		private Tab _currentTab;
		private int _subIndex;
		private bool _inConfirmation;

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

		protected override string OverlayName => "Trader";
		protected override string EmptyMessage => "No trader information available";

		protected override int GetItemCount() {
			if (Level > 0) return 0;
			if (_mode == Mode.NoTrader) return _noTraderItems.Count;
			return _mainMenuItems.Count;
		}

		protected override string GetLabel(int index) {
			if (Level > 0) return null;
			if (_mode == Mode.NoTrader) {
				if (index < 0 || index >= _noTraderItems.Count) return null;
				return _noTraderItems[index].Label;
			} else {
				if (index < 0 || index >= _mainMenuItems.Count) return null;
				return _mainMenuItems[index].Label;
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
			if (_mode == Mode.NoTrader) {
				if (index < 0 || index >= _noTraderItems.Count) return;
				var item = _noTraderItems[index];
				if (item.OnActivate != null)
					item.OnActivate();
				else
					Speech.Say(item.Label);
			} else {
				if (index < 0 || index >= _mainMenuItems.Count) return;
				var item = _mainMenuItems[index];
				if (item.OnActivate != null)
					item.OnActivate();
				else
					Speech.Say(item.Label);
			}
		}

		protected override EscapeAction OnEscape() {
			// Pass to game to close panel
			return EscapeAction.PassThrough;
		}

		protected override void AnnounceCurrentItem() {
			if (Level > 0) {
				AnnounceSubLevelItem();
				return;
			}

			int count = GetItemCount();
			if (count == 0) return;

			string label = GetLabel(CurrentIndex);
			if (!string.IsNullOrEmpty(label))
				Speech.Say(label);
		}

		protected override string GetOpenAnnouncement() {
			if (_mode == Mode.NoTrader) {
				if (_noTraderItems.Count > 0)
					return $"Trader. {_noTraderItems[0].Label}";
				return "Trader. No trader information available";
			} else {
				if (_mainMenuItems.Count > 0)
					return $"Trader. {_mainMenuItems[0].Label}";
				return "Trader";
			}
		}

		protected override void OnClosed() {
			_inConfirmation = false;
			ClearData();
		}

		// ========================================
		// SEARCH OVERRIDES (Level > 0)
		// ========================================

		protected override int SearchItemCount {
			get {
				if (Level == 0) return base.SearchItemCount;

				switch (_subLevel) {
					case SubLevel.GoodsTrade:
						return (_currentTab == Tab.Sell ? _sellGoods : _buyGoods).Count;
					case SubLevel.Perks:
						return _perks.Count;
					default:
						return 0;
				}
			}
		}

		protected override int SearchCurrentIndex {
			get {
				if (Level == 0) return base.SearchCurrentIndex;
				return _subIndex;
			}
		}

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

			switch (_subLevel) {
				case SubLevel.GoodsTrade:
					var goodsList = _currentTab == Tab.Sell ? _sellGoods : _buyGoods;
					return (index >= 0 && index < goodsList.Count) ? goodsList[index].DisplayName : null;
				case SubLevel.Perks:
					return (index >= 0 && index < _perks.Count) ? _perks[index].DisplayName : null;
				default:
					return null;
			}
		}

		protected override void SearchMoveTo(int index) {
			if (Level == 0) {
				base.SearchMoveTo(index);
				return;
			}

			_subIndex = index;
			AnnounceSubLevelItem();
		}

		// ========================================
		// SPECIAL KEY HANDLING (Level > 0)
		// ========================================

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (_inConfirmation)
				return ProcessConfirmationKey(keyCode);

			if (Level > 0) {
				if (_search.HandleKey(keyCode, modifiers, this))
					return true;

				if (_subLevel == SubLevel.GoodsTrade && modifiers.Alt) {
					if (keyCode == KeyCode.B) { AnnounceBalance(); return true; }
					if (keyCode == KeyCode.A) { TryAcceptTrade(); return true; }
				}

				return ProcessSubLevelKey(keyCode, modifiers);
			}

			if (_mode == Mode.TraderPresent && modifiers.Alt) {
				if (keyCode == KeyCode.B) { AnnounceBalance(); return true; }
				if (keyCode == KeyCode.A) { TryAcceptTrade(); return true; }
			}

			return null;
		}

		private bool ProcessSubLevelKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			switch (_subLevel) {
				case SubLevel.GoodsTrade:
					return ProcessGoodsTradeKey(keyCode, modifiers);
				case SubLevel.Perks:
					return ProcessPerksKey(keyCode, modifiers);
				default:
					return true;
			}
		}

		// ========================================
		// SUB-LEVEL ANNOUNCEMENTS
		// ========================================

		private void AnnounceSubLevelItem() {
			switch (_subLevel) {
				case SubLevel.GoodsTrade:
					var goodsList = _currentTab == Tab.Sell ? _sellGoods : _buyGoods;
					if (_subIndex >= 0 && _subIndex < goodsList.Count)
						Speech.Say(BuildGoodLabel(goodsList[_subIndex]));
					break;
				case SubLevel.Perks:
					if (_subIndex >= 0 && _subIndex < _perks.Count)
						Speech.Say(BuildPerkLabel(_perks[_subIndex]));
					break;
			}
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
						Label = "Trading is blocked",
						SearchName = null,
						OnActivate = null
					});
				} else {
					_noTraderItems.Add(new NavItem {
						Label = "No trader on the way. Traders may be too scared to visit",
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
				arrivalInfo = "Trading blocked";
			} else if (isStorm) {
				float stormEnds = TradeReflection.GetTimeTillSeasonChange();
				if (progress >= 1f) {
					arrivalInfo = $"waiting for storm to end, {FormattingUtils.FormatTime(stormEnds)} remaining";
				} else {
					arrivalInfo = $"travel paused during storm, {Mathf.RoundToInt(progress * 100)}% traveled, storm ends in {FormattingUtils.FormatTime(stormEnds)}";
				}
			} else if (timeToArrival > 0) {
				arrivalInfo = $"arriving in {FormattingUtils.FormatTime(timeToArrival)}";
			} else if (progress < 1f) {
				arrivalInfo = $"{Mathf.RoundToInt(progress * 100)}% traveled";
			} else {
				arrivalInfo = "arriving soon";
			}

			string headerLabel = !string.IsNullOrEmpty(traderLabel)
				? $"Next trader: {traderName}, {traderLabel}, {arrivalInfo}"
				: $"Next trader: {traderName}, {arrivalInfo}";

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
						Label = $"Force arrival, costs {cost:F1} Impatience",
						SearchName = "Force",
						OnActivate = ActivateForceArrival
					});
				} else {
					string reason = TradeReflection.GetForceArrivalUnavailableReason() ?? "unavailable";
					_noTraderItems.Add(new NavItem {
						Label = $"Force arrival: {reason}",
						SearchName = null,
						OnActivate = null
					});
				}
			}
		}

		private void ActivateForceArrival() {
			if (!TradeReflection.CanForceArrival()) {
				Speech.Say("Cannot force arrival");
				SoundManager.PlayFailed();
				return;
			}

			if (TradeReflection.ForceTraderArrival()) {
				SoundManager.PlayButtonClick();
				Speech.Say("Trader arrival forced");
				RefreshNoTraderData();
				CurrentIndex = 0;
				if (_noTraderItems.Count > 0)
					Speech.Say(_noTraderItems[0].Label);
			} else {
				Speech.Say("Failed to force arrival");
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

			string traderName = TradeReflection.GetTraderName() ?? "Unknown";
			string traderLabel = TradeReflection.GetTraderLabel() ?? "";
			float timeLeft = TradeReflection.GetStayingTimeLeft();
			string dialogue = TradeReflection.GetTraderDialogue() ?? "";

			string infoLabel = !string.IsNullOrEmpty(traderLabel)
				? $"{traderName}, {traderLabel}, {FormattingUtils.FormatTime(timeLeft)} remaining"
				: $"{traderName}, {FormattingUtils.FormatTime(timeLeft)} remaining";

			if (!string.IsNullOrEmpty(dialogue))
				infoLabel += $". {dialogue}";

			_mainMenuItems.Add(new NavItem {
				Label = infoLabel,
				SearchName = null,
				OnActivate = null
			});

			_mainMenuItems.Add(new NavItem {
				Label = "Goods Trade",
				SearchName = "Goods",
				OnActivate = () => EnterGoodsTrade()
			});

			int unsoldPerks = 0;
			foreach (var p in _perks)
				if (!p.Sold) unsoldPerks++;

			_mainMenuItems.Add(new NavItem {
				Label = unsoldPerks > 0 ? $"Perks, {unsoldPerks} available" : "Perks, none available",
				SearchName = "Perks",
				OnActivate = () => EnterPerks()
			});

			if (TradeReflection.CanAssaultTrader()) {
				_mainMenuItems.Add(new NavItem {
					Label = "Assault Trader",
					SearchName = "Assault",
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
			SetLevel(1);
			_subLevel = SubLevel.GoodsTrade;
			_currentTab = Tab.Sell;
			_subIndex = 0;
			_search.Clear();

			SoundManager.PlayButtonClick();
			AnnounceSellTab();
		}

		private void AnnounceSellTab() {
			if (_sellGoods.Count > 0) {
				Speech.Say($"Sell tab. {BuildGoodLabel(_sellGoods[0])}");
			} else {
				Speech.Say("Sell tab. No goods to sell");
			}
		}

		private void AnnounceBuyTab() {
			if (_buyGoods.Count > 0) {
				Speech.Say($"Buy tab. {BuildGoodLabel(_buyGoods[0])}");
			} else {
				Speech.Say("Buy tab. No goods available");
			}
		}

		private string BuildGoodLabel(TradeGoodItem good) {
			if (good.OfferedAmount > 0) {
				float totalValue = good.IsSell
					? TradeReflection.GetGoodSellValue(good.Name, good.OfferedAmount)
					: TradeReflection.GetGoodBuyValue(good.Name, good.OfferedAmount);

				if (good.IsSell)
					return $"{good.DisplayName}, {good.MaxAmount} stored, {good.OfferedAmount} offered, {totalValue:F2} Amber";
				else
					return $"{good.DisplayName}, {good.MaxAmount} available, {good.OfferedAmount} requested, {totalValue:F2} Amber";
			} else {
				if (good.IsSell)
					return $"{good.DisplayName}, {good.MaxAmount} stored, {good.UnitValue:F2} each";
				else
					return $"{good.DisplayName}, {good.MaxAmount} available, {good.UnitValue:F2} each";
			}
		}

		private bool ProcessGoodsTradeKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			switch (keyCode) {
				case KeyCode.LeftArrow:
					if (_currentTab != Tab.Sell) {
						_currentTab = Tab.Sell;
						_subIndex = 0;
						_search.Clear();
						AnnounceSellTab();
					}
					return true;

				case KeyCode.RightArrow:
					if (_currentTab != Tab.Buy) {
						_currentTab = Tab.Buy;
						_subIndex = 0;
						_search.Clear();
						AnnounceBuyTab();
					}
					return true;

				case KeyCode.UpArrow:
					NavigateGoods(-1);
					return true;

				case KeyCode.DownArrow:
					NavigateGoods(1);
					return true;

				case KeyCode.Home: {
						var homeList = _currentTab == Tab.Sell ? _sellGoods : _buyGoods;
						if (homeList.Count > 0) {
							_subIndex = 0;
							Speech.Say(BuildGoodLabel(homeList[_subIndex]));
						}
					}
					return true;

				case KeyCode.End: {
						var endList = _currentTab == Tab.Sell ? _sellGoods : _buyGoods;
						if (endList.Count > 0) {
							_subIndex = endList.Count - 1;
							Speech.Say(BuildGoodLabel(endList[_subIndex]));
						}
					}
					return true;

				case KeyCode.Plus:
				case KeyCode.KeypadPlus:
				case KeyCode.Equals:
					AdjustQuantity(modifiers.Shift ? 10 : 1);
					return true;

				case KeyCode.Minus:
				case KeyCode.KeypadMinus:
					AdjustQuantity(modifiers.Shift ? -10 : -1);
					return true;

				case KeyCode.Escape:
					SetLevel(0);
					CurrentIndex = 1; // Goods Trade item
					_search.Clear();
					Speech.Say("Main menu. Goods Trade");
					InputBlocker.BlockCancelOnce = true;
					return true;

				default:
					return true;
			}
		}

		private void NavigateGoods(int direction) {
			var currentList = _currentTab == Tab.Sell ? _sellGoods : _buyGoods;
			if (currentList.Count == 0) return;

			_subIndex = NavigationUtils.WrapIndex(_subIndex, direction, currentList.Count);
			Speech.Say(BuildGoodLabel(currentList[_subIndex]));
		}

		private void AdjustQuantity(int delta) {
			var currentList = _currentTab == Tab.Sell ? _sellGoods : _buyGoods;
			if (_subIndex < 0 || _subIndex >= currentList.Count) return;

			var good = currentList[_subIndex];
			int oldAmount = good.OfferedAmount;
			good.OfferedAmount = Mathf.Clamp(good.OfferedAmount + delta, 0, good.MaxAmount);

			if (good.OfferedAmount != oldAmount) {
				SoundManager.PlayButtonClick();
			}

			float balance = CalculateBalance();
			Speech.Say($"{good.OfferedAmount}, balance {balance:F2}");
		}

		private float CalculateBalance() {
			CalculateTradeTotals(out float sellTotal, out float buyTotal);
			return sellTotal - buyTotal;
		}

		private void AnnounceBalance() {
			CalculateTradeTotals(out float sellTotal, out float buyTotal);
			float balance = sellTotal - buyTotal;
			string fairness = balance >= 0 ? "fair" : "unfair";

			Speech.Say($"Selling {sellTotal:F2}, Buying {buyTotal:F2}, Balance {balance:F2}, {fairness}");
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
				Speech.Say("Not buying anything");
				SoundManager.PlayFailed();
				return;
			}

			float sellTotal = 0f;
			var sellList = new List<string>();
			foreach (var g in _sellGoods) {
				if (g.OfferedAmount > 0) {
					sellTotal += TradeReflection.GetGoodSellValue(g.Name, g.OfferedAmount);
					sellList.Add($"{g.OfferedAmount} {g.DisplayName}");
				}
			}

			float buyTotal = 0f;
			var buyList = new List<string>();
			foreach (var g in _buyGoods) {
				if (g.OfferedAmount > 0) {
					buyTotal += TradeReflection.GetGoodBuyValue(g.Name, g.OfferedAmount);
					buyList.Add($"{g.OfferedAmount} {g.DisplayName}");
				}
			}

			float balance = sellTotal - buyTotal;

			if (balance < 0) {
				Speech.Say($"Trade unfair, need {Mathf.Abs(balance):F2} more");
				SoundManager.PlayFailed();
				return;
			}

			string sellText = sellList.Count > 0 ? string.Join(", ", sellList) : "nothing";
			string buyText = buyList.Count > 0 ? string.Join(", ", buyList) : "nothing";

			_inConfirmation = true;
			_subLevel = SubLevel.TradeConfirm;
			Speech.Say($"Selling: {sellText}. Buying: {buyText}. Balance: {balance:F2}. Enter to confirm, Escape to cancel");
		}

		// ========================================
		// PERKS SUB-LEVEL
		// ========================================

		private void EnterPerks() {
			SetLevel(1);
			_subLevel = SubLevel.Perks;
			_subIndex = 0;
			_search.Clear();

			SoundManager.PlayButtonClick();

			if (_perks.Count > 0) {
				Speech.Say($"Perks. {BuildPerkLabel(_perks[0])}");
			} else {
				Speech.Say("Perks. No perks available");
			}
		}

		private string BuildPerkLabel(PerkItem perk) {
			string nameAndDesc = !string.IsNullOrEmpty(perk.Description)
				? $"{perk.DisplayName}. {perk.Description}"
				: perk.DisplayName;

			if (perk.Sold) {
				return $"{nameAndDesc}, sold";
			}

			if (perk.Discounted) {
				int discountPercent = Mathf.RoundToInt((1f - perk.DiscountRatio) * 100);
				return $"{nameAndDesc}, {perk.Price:F0} Amber, {discountPercent}% off";
			}

			return $"{nameAndDesc}, {perk.Price:F0} Amber";
		}

		private bool ProcessPerksKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			switch (keyCode) {
				case KeyCode.UpArrow:
					NavigatePerks(-1);
					return true;

				case KeyCode.DownArrow:
					NavigatePerks(1);
					return true;

				case KeyCode.Home:
					if (_perks.Count > 0) {
						_subIndex = 0;
						Speech.Say(BuildPerkLabel(_perks[_subIndex]));
					}
					return true;

				case KeyCode.End:
					if (_perks.Count > 0) {
						_subIndex = _perks.Count - 1;
						Speech.Say(BuildPerkLabel(_perks[_subIndex]));
					}
					return true;

				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					BuyCurrentPerk();
					return true;

				case KeyCode.Escape:
					SetLevel(0);
					CurrentIndex = 2; // Perks item
					RefreshMainMenu();
					_search.Clear();
					Speech.Say($"Main menu. {_mainMenuItems[CurrentIndex].Label}");
					InputBlocker.BlockCancelOnce = true;
					return true;

				default:
					return true;
			}
		}

		private void NavigatePerks(int direction) {
			if (_perks.Count == 0) return;
			_subIndex = NavigationUtils.WrapIndex(_subIndex, direction, _perks.Count);
			Speech.Say(BuildPerkLabel(_perks[_subIndex]));
		}

		private void BuyCurrentPerk() {
			if (_subIndex < 0 || _subIndex >= _perks.Count) return;

			var perk = _perks[_subIndex];
			if (perk.Sold) {
				Speech.Say("Already sold");
				SoundManager.PlayFailed();
				return;
			}

			int amber = TradeReflection.GetAmberInStorage();
			if (amber < perk.Price) {
				Speech.Say($"Not enough Amber, have {amber}, need {perk.Price:F0}");
				SoundManager.PlayFailed();
				return;
			}

			if (TradeReflection.BuyPerk(perk.EffectState)) {
				perk.Sold = true;
				SoundManager.PlayTraderTransactionCompleted();
				var traderSound = TradeReflection.GetTraderTransactionSound();
				if (traderSound != null)
					SoundManager.PlaySoundEffect(traderSound);
				Speech.Say($"Purchased {perk.DisplayName}");
				RefreshPerks();
				RefreshSellGoods();
				RefreshBuyGoods();
				RefreshMainMenu();
			} else {
				Speech.Say("Purchase failed");
				SoundManager.PlayFailed();
			}
		}

		// ========================================
		// ASSAULT CONFIRM SUB-LEVEL
		// ========================================

		private void EnterAssaultConfirm() {
			SetLevel(1);
			_subLevel = SubLevel.AssaultConfirm;
			_inConfirmation = true;
			SoundManager.PlayButtonClick();
			Speech.Say("Assault trader? May lose villagers and reputation. Enter to confirm, Escape to cancel");
		}

		// ========================================
		// CONFIRMATION HANDLING
		// ========================================

		private bool ProcessConfirmationKey(KeyCode keyCode) {
			if (_subLevel == SubLevel.AssaultConfirm) {
				switch (keyCode) {
					case KeyCode.Return:
					case KeyCode.KeypadEnter:
						ExecuteAssault();
						return true;

					case KeyCode.Escape:
						_inConfirmation = false;
						SetLevel(0);
						CurrentIndex = Math.Min(_mainMenuItems.Count - 1, 3);
						Speech.Say($"Cancelled. Main menu. {_mainMenuItems[CurrentIndex].Label}");
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
					_inConfirmation = false;
					SetLevel(1);
					_subLevel = SubLevel.GoodsTrade;
					Speech.Say("Cancelled");
					InputBlocker.BlockCancelOnce = true;
					return true;

				default:
					return true;
			}
		}

		private void ExecuteAssault() {
			var result = TradeReflection.AssaultTrader();
			_inConfirmation = false;

			if (result.Success) {
				SoundManager.PlayButtonClick();
				Speech.Say($"Assault successful. Stole {result.GoodsStolen} goods, {result.PerksStolen} perks. Lost {result.VillagersLost} villagers");
			} else {
				Speech.Say("Assault failed");
				SoundManager.PlayFailed();
				SetLevel(0);
				CurrentIndex = 0;
			}
		}

		private void ExecuteTrade() {
			_inConfirmation = false;

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
				Speech.Say("Trade complete");
			} else {
				SoundManager.PlayFailed();
				Speech.Say("Trade failed");
			}

			foreach (var g in _sellGoods)
				g.OfferedAmount = 0;
			foreach (var g in _buyGoods)
				g.OfferedAmount = 0;

			SetLevel(1);
			_subLevel = SubLevel.GoodsTrade;
			_subIndex = 0;

			RefreshSellGoods();
			RefreshBuyGoods();

			if (_currentTab == Tab.Sell)
				AnnounceSellTab();
			else
				AnnounceBuyTab();
		}
	}
}
