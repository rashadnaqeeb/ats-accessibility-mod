using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using UnityEngine;

namespace ATSAccessibility.Panels {
	/// <summary>
	/// Menu Hub for quick access to game popups.
	/// Opened with F2 from the settlement map.
	/// Isolated in a single file for easy removal if needed.
	/// </summary>
	public class MenuHub: MenuBase {
		private static readonly string[] _menuLabelKeys = {
			"common.recipes",
			"common.orders",
			"common.trade_routes",
			"common.payments",
			"panel.menu_hub.menu.consumption_control",
			"common.trends",
			"common.trader"
		};

		// Flag to suppress "Closed" speech when closing to open a popup
		private bool _closingForPopup;

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("panel.menu_hub.title");
		protected override string EmptyMessage => "";

		protected override int GetItemCount() => _menuLabelKeys.Length;

		protected override string GetLabel(int index) {
			if (index < 0 || index >= _menuLabelKeys.Length) return null;

			string label = Strings.Get(_menuLabelKeys[index]);
			string lockSuffix = "";
			if (index == 2 && !ConstructionReflection.AreTradeRoutesUnlocked())
				lockSuffix = Strings.Get("common.suffix_locked");
			else if (index == 4 && !ConstructionReflection.IsConsumptionControlUnlocked())
				lockSuffix = Strings.Get("common.suffix_locked");

			return Strings.Get("panel.menu_hub.item", label, lockSuffix);
		}

		protected override string GetSearchName(int index) {
			if (index < 0 || index >= _menuLabelKeys.Length) return null;
			return Strings.Get(_menuLabelKeys[index]);
		}

		protected override void RefreshData() { } // Static list, nothing to refresh

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			if (index < 0 || index >= _menuLabelKeys.Length) return;
			OpenSelectedMenu(index);
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			// F-key panel switching - close self and pass through to open target panel
			if (keyCode == KeyCode.F1 || keyCode == KeyCode.F3) {
				Close();
				return false; // Let SettlementKeyHandler open the target panel
			}
			if (keyCode == KeyCode.F2) {
				SoundManager.PlayButtonClick();
				Close();
				return true;
			}
			return null;
		}

		protected override EscapeAction OnEscape() {
			SoundManager.PlayButtonClick();
			return EscapeAction.Close;
		}

		protected override void OnOpened() {
			SoundManager.PlayPopupShow();
		}

		protected override void OnClosed() {
			if (!_closingForPopup && !IsClosingSilently) {
				InputBlocker.BlockCancelOnce = true;
				Speech.Say(Strings.Get("common.closed"));
			}
			_closingForPopup = false;
		}

		// ========================================
		// PUBLIC METHODS
		// ========================================

		/// <summary>
		/// Toggle the menu hub. If already open, closes it.
		/// Callers should use this instead of Open() for toggle behavior.
		/// </summary>
		public void Toggle() {
			if (IsOpen) {
				SoundManager.PlayButtonClick();
				Close();
				return;
			}

			Open();
		}

		// ========================================
		// POPUP OPENING
		// ========================================

		private void OpenSelectedMenu(int index) {
			string menuName = Strings.Get(_menuLabelKeys[index]);
			Debug.Log($"[ATSAccessibility] Opening {menuName} from Menu Hub");

			bool success = false;

			switch (index) {
				case 0: // Recipes
					success = GameReflection.OpenRecipesPopup();
					if (success) SoundManager.PlayMenuRecipes();
					break;
				case 1: // Orders
					success = GameReflection.OpenOrdersPopup();
					if (success) SoundManager.PlayMenuOrders();
					break;
				case 2: // Trade Routes
					if (!ConstructionReflection.AreTradeRoutesUnlocked()) {
						Speech.Say(Strings.Get("panel.menu_hub.trade_routes_locked"));
						SoundManager.PlayFailed();
						return;
					}
					success = GameReflection.OpenTradeRoutesPopup();
					if (success) SoundManager.PlayMenuTradeRoutes();
					break;
				case 3: // Payments
					success = GameReflection.OpenPaymentsPopup();
					if (success) SoundManager.PlayMenuRecipes();
					break;
				case 4: // Consumption Control
					if (!ConstructionReflection.IsConsumptionControlUnlocked()) {
						Speech.Say(Strings.Get("panel.menu_hub.consumption_locked"));
						SoundManager.PlayFailed();
						return;
					}
					success = GameReflection.OpenConsumptionPopup();
					if (success) SoundManager.PlayConsumptionPopupShow();
					break;
				case 5: // Trends
					success = GameReflection.OpenTrendsPopup();
					if (success) SoundManager.PlayMenuTrends();
					break;
				case 6: // Trader
					success = GameReflection.OpenTraderPanel();
					if (!success) {
						Speech.Say(Strings.Get("panel.menu_hub.trader_unavailable"));
						SoundManager.PlayFailed();
						Debug.Log("[ATSAccessibility] Trader panel unavailable - no Trading Post");
						return;
					}
					break;
			}

			if (success) {
				_closingForPopup = true;
				Close();
				Debug.Log($"[ATSAccessibility] Successfully opened {menuName}");
			} else {
				Speech.Say(Strings.Get("panel.menu_hub.unavailable", menuName));
				Debug.Log($"[ATSAccessibility] Failed to open {menuName}");
			}
		}
	}
}
