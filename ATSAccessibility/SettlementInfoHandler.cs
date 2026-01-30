using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// High-priority handler for settlement info hotkeys (Alt+S, Alt+V, Alt+O).
    /// Registered above menus/overlays so these work even inside popups
    /// without interfering with typeahead search.
    /// </summary>
    public class SettlementInfoHandler : IKeyHandler
    {
        public bool IsActive => GameReflection.GetIsGameActive();

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!modifiers.Alt) return false;

            switch (keyCode)
            {
                case KeyCode.S:
                    StatsReader.AnnounceQuickSummary();
                    return true;
                case KeyCode.V:
                    StatsReader.AnnounceNextSpeciesResolve();
                    return true;
                case KeyCode.O:
                    AnnounceTrackedOrders();
                    return true;
                default:
                    return false;
            }
        }

        public static void AnnounceTrackedOrders()
        {
            var orders = OrdersReflection.GetOrders();
            if (orders == null || orders.Count == 0)
            {
                Speech.Say("No orders");
                return;
            }

            var parts = new List<string>();
            foreach (var orderState in orders)
            {
                if (orderState == null) continue;
                if (!OrdersReflection.IsTracked(orderState)) continue;
                if (!OrdersReflection.IsStarted(orderState)) continue;
                if (!OrdersReflection.IsPicked(orderState)) continue;
                if (OrdersReflection.IsCompleted(orderState)) continue;
                if (OrdersReflection.IsFailed(orderState)) continue;

                var model = OrdersReflection.GetOrderModel(orderState);
                if (model == null) continue;

                string name = OrdersReflection.GetOrderDisplayName(model) ?? "Unknown";
                var objectives = OrdersReflection.GetObjectiveTexts(model, orderState);
                string objText = objectives.Count > 0 ? string.Join(", ", objectives) : "";

                if (!string.IsNullOrEmpty(objText))
                    parts.Add($"{name}: {objText}");
                else
                    parts.Add(name);
            }

            if (parts.Count == 0)
            {
                Speech.Say("No tracked orders");
                return;
            }

            Speech.Say(string.Join(". ", parts));
        }
    }
}
