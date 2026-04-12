using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Handlers {
	/// <summary>
	/// High-priority handler for settlement info hotkeys (Alt+S, Alt+V, Alt+O).
	/// Registered above menus/overlays so these work even inside popups
	/// without interfering with typeahead search.
	/// </summary>
	public class SettlementInfoHandler: IKeyHandler, IHelpProvider {
		// ========================================
		// IHELPPROVIDER
		// ========================================

		private static readonly List<HelpEntry> _helpEntries = new List<HelpEntry> {
			HelpEntry.Loca("Alt+S", "handler.settlement_info.help.settlement_summary"),
			HelpEntry.Loca("Alt+V", "handler.settlement_info.help.species_resolve"),
			HelpEntry.Loca("Alt+O", "handler.settlement_info.help.tracked_orders"),
		};

		public HelpBehavior HelpBehavior => HelpBehavior.Filter;
		public string HelpContextName => null;
		public IReadOnlyList<HelpEntry> GetHelpEntries() => _helpEntries;
		public IReadOnlyList<string> GetPassthroughKeys() => null;

		public bool IsActive => GameReflection.GetIsGameActive();

		public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (!modifiers.Alt) return false;

			switch (keyCode) {
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

		public static void AnnounceTrackedOrders() {
			var orders = OrdersReflection.GetOrders();
			if (orders == null || orders.Count == 0) {
				Speech.Say(Strings.Get("handler.settleinfo.no_orders"));
				return;
			}

			var parts = new List<string>();
			foreach (var orderState in orders) {
				if (orderState == null) continue;
				if (!OrdersReflection.IsTracked(orderState)) continue;
				if (!OrdersReflection.IsStarted(orderState)) continue;
				if (!OrdersReflection.IsPicked(orderState)) continue;
				if (OrdersReflection.IsCompleted(orderState)) continue;
				if (OrdersReflection.IsFailed(orderState)) continue;

				var model = OrdersReflection.GetOrderModel(orderState);
				if (model == null) continue;

				string name = OrdersReflection.GetOrderDisplayName(model) ?? Strings.Get("common.unknown");
				var objectives = OrdersReflection.GetObjectiveTexts(model, orderState);
				string objText = objectives.Count > 0 ? string.Join(", ", objectives) : "";

				if (!string.IsNullOrEmpty(objText))
					parts.Add(Strings.Get("handler.settleinfo.order_with_objectives", name, objText));
				else
					parts.Add(name);
			}

			if (parts.Count == 0) {
				Speech.Say(Strings.Get("handler.settleinfo.no_tracked_orders"));
				return;
			}

			Speech.Say(string.Join(". ", parts));
		}
	}
}
