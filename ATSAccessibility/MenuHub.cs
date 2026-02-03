using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Menu Hub for quick access to game popups.
	/// Opened with F2 from the settlement map.
	/// Isolated in a single file for easy removal if needed.
	/// </summary>
	public class MenuHub: MenuBase, IKeyHandler {
		private static readonly string[] _menuLabels = {
			"Recipes",
			"Orders",
			"Trade Routes",
			"Payments",
			"Consumption Control",
			"Trends",
			"Trader"
		};

		// Flag to suppress "Closed" speech when closing to open a popup
		private bool _closingForPopup;

		// ========================================
		// IKeyHandler Implementation
		// ========================================

		public bool IsActive => IsOpen;

		bool IKeyHandler.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) =>
			ProcessKey(keyCode, modifiers);

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => "Menu Hub";
		protected override string EmptyMessage => "";

		protected override int GetItemCount() => _menuLabels.Length;

		protected override string GetLabel(int index) {
			if (index < 0 || index >= _menuLabels.Length) return null;

			string label = _menuLabels[index];
			string lockSuffix = "";
			if (index == 2 && !GameReflection.AreTradeRoutesUnlocked())
				lockSuffix = ", locked";
			else if (index == 4 && !GameReflection.IsConsumptionControlUnlocked())
				lockSuffix = ", locked";

			return $"{label}{lockSuffix}";
		}

		protected override string GetSearchName(int index) {
			if (index < 0 || index >= _menuLabels.Length) return null;
			return _menuLabels[index];
		}

		protected override void RefreshData() { } // Static list, nothing to refresh

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			if (index < 0 || index >= _menuLabels.Length) return;
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
			if (!_closingForPopup) {
				InputBlocker.BlockCancelOnce = true;
				Speech.Say("Closed");
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
			string menuName = _menuLabels[index];
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
					if (!GameReflection.AreTradeRoutesUnlocked()) {
						Speech.Say("Trade Routes locked. Unlock via meta progression");
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
					if (!GameReflection.IsConsumptionControlUnlocked()) {
						Speech.Say("Consumption Control locked. Unlock via meta progression");
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
						Speech.Say("Trader unavailable. Build a Trading Post first");
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
				Speech.Say($"{menuName} unavailable");
				Debug.Log($"[ATSAccessibility] Failed to open {menuName}");
			}
		}
	}
}
