using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Speech-only confirmation dialog handler.
    /// Blocks all input while active, confirms with Enter, cancels with Escape.
    /// </summary>
    public class ConfirmationDialog : IKeyHandler
    {
        private bool _isOpen;
        private Action _onConfirm;
        private string _itemName;

        public bool IsActive => _isOpen;

        /// <summary>
        /// Show the confirmation dialog.
        /// </summary>
        /// <param name="itemName">Name of the item being acted on</param>
        /// <param name="onConfirm">Action to execute when confirmed</param>
        /// <param name="refundGoods">Optional list of goods to be refunded</param>
        public void Show(string itemName, Action onConfirm, List<(string name, int amount)> refundGoods = null)
        {
            _isOpen = true;
            _itemName = itemName;
            _onConfirm = onConfirm;

            var message = new StringBuilder();
            message.Append($"Destroy {itemName}?");

            // Add refund information if available
            if (refundGoods != null && refundGoods.Count > 0)
            {
                message.Append(" Refund: ");
                for (int i = 0; i < refundGoods.Count; i++)
                {
                    var (name, amount) = refundGoods[i];
                    if (i > 0) message.Append(", ");
                    message.Append($"{amount} {name}");
                }
                message.Append(".");
            }

            message.Append(" Enter to confirm, Escape to cancel");
            Speech.Say(message.ToString());
        }

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            switch (keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    _onConfirm?.Invoke();
                    Close();
                    return true;

                case KeyCode.Escape:
                    Speech.Say("Cancelled");
                    Close();
                    return true;

                default:
                    // Consume all keys while dialog is open
                    return true;
            }
        }

        private void Close()
        {
            _isOpen = false;
            _onConfirm = null;
            _itemName = null;
            InputBlocker.BlockCancelOnce = true;
        }
    }
}
