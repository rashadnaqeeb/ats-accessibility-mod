using ATSAccessibility.Utils;
using ATSAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ATSAccessibility.Panels {
	/// <summary>
	/// Speech-only confirmation dialog handler.
	/// Blocks all input while active, confirms with Enter, cancels with Escape.
	/// </summary>
	public class ConfirmationDialog: IKeyHandler, IHelpProvider {
		private bool _isOpen;
		private Action _onConfirm;
		private string _itemName;

		// ========================================
		// IHELPPROVIDER
		// ========================================

		private static readonly List<HelpEntry> _helpEntries = new List<HelpEntry>();

		public HelpBehavior HelpBehavior => HelpBehavior.Terminator;
		public string HelpContextName => "Confirmation";
		public IReadOnlyList<HelpEntry> GetHelpEntries() => _helpEntries;
		public IReadOnlyList<string> GetPassthroughKeys() => null;

		public bool IsActive => _isOpen;

		/// <summary>
		/// Show the confirmation dialog.
		/// </summary>
		/// <param name="itemName">Name of the item being acted on</param>
		/// <param name="onConfirm">Action to execute when confirmed</param>
		/// <param name="refundGoods">Optional list of goods to be refunded</param>
		public void Show(string itemName, Action onConfirm, List<(string name, int amount)> refundGoods = null) {
			_isOpen = true;
			_itemName = itemName;
			_onConfirm = onConfirm;

			var message = new StringBuilder();
			message.Append(Strings.Get("dialog.confirm.destroy", itemName));

			// Add refund information if available
			if (refundGoods != null && refundGoods.Count > 0) {
				message.Append(' ');
				message.Append(Strings.Get("dialog.confirm.refund_prefix"));
				message.Append(' ');
				for (int i = 0; i < refundGoods.Count; i++) {
					var (name, amount) = refundGoods[i];
					if (i > 0) message.Append(", ");
					message.Append(Strings.Get("dialog.confirm.refund_item", amount, name));
				}
				message.Append(".");
			}

			message.Append(' ');
			message.Append(Strings.Get("dialog.confirm.instructions"));
			Speech.Say(message.ToString());
		}

		/// <summary>
		/// Show the confirmation dialog with a custom message.
		/// </summary>
		/// <param name="message">Full message to announce</param>
		/// <param name="onConfirm">Action to execute when confirmed</param>
		public void ShowMessage(string message, Action onConfirm) {
			_isOpen = true;
			_itemName = null;
			_onConfirm = onConfirm;
			Speech.Say(message);
		}

		public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			switch (keyCode) {
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					_onConfirm?.Invoke();
					Close();
					return true;

				case KeyCode.Escape:
					Speech.Say(Strings.Get("dialog.confirm.cancelled"));
					Close();
					return true;

				default:
					// Consume all keys while dialog is open
					return true;
			}
		}

		private void Close() {
			_isOpen = false;
			_onConfirm = null;
			_itemName = null;
			InputBlocker.BlockCancelOnce = true;
		}
	}
}
