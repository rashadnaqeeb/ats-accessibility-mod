using System;
using System.Collections.Generic;
using System.Linq;

namespace ATSAccessibility.Utils {
	/// <summary>
	/// Shared formatting for recipe ingredients and production time.
	/// Used by both RecipesOverlay and EncyclopediaNavigator.
	/// </summary>
	public static class RecipeFormatter {
		/// <summary>
		/// Format recipe ingredients in readable format.
		/// Same amounts: "3 x Herbs, Insects, Resin."
		/// Different amounts: "One of Stone x 4, Clay x 4, Salt x 3."
		/// </summary>
		public static string FormatIngredients(Array goodsSets,
			Func<object, Array> getGoodsSetGoods,
			Func<object, string> getGoodRefDisplayName,
			Func<object, int> getGoodRefAmount) {
			if (goodsSets == null || goodsSets.Length == 0) return Strings.Get("util.recipe_formatter.nothing");

			var parts = new List<string>();
			foreach (var goodsSet in goodsSets) {
				var goods = getGoodsSetGoods(goodsSet);
				if (goods == null || goods.Length == 0) continue;

				// Collect names and amounts
				var items = new List<(string name, int amount)>();
				foreach (var goodRef in goods) {
					var name = getGoodRefDisplayName(goodRef);
					var amount = getGoodRefAmount(goodRef);
					if (!string.IsNullOrEmpty(name))
						items.Add((name, amount));
				}

				if (items.Count == 0) continue;

				if (items.Count == 1) {
					// Single item, no alternatives
					parts.Add(Strings.Get("util.recipe_formatter.single", items[0].name, items[0].amount));
				} else {
					// Multiple alternatives - check if all amounts are the same
					bool sameAmounts = items.All(i => i.amount == items[0].amount);

					if (sameAmounts) {
						// Same amounts: "3 x Herbs, Insects, Resin."
						var names = string.Join(", ", items.Select(i => i.name));
						parts.Add(Strings.Get("util.recipe_formatter.same_amounts", items[0].amount, names));
					} else {
						// Different amounts: "One of Stone x 4, Clay x 4, Salt x 3."
						var itemStrs = items.Select(i => Strings.Get("util.recipe_formatter.diff_item", i.name, i.amount));
						parts.Add(Strings.Get("util.recipe_formatter.different_amounts", string.Join(", ", itemStrs)));
					}
				}
			}

			return parts.Count > 0 ? string.Join(" ", parts) : Strings.Get("util.recipe_formatter.nothing");
		}

		/// <summary>
		/// Format production time for recipes.
		/// </summary>
		public static string FormatTime(float totalSeconds) {
			int secs = (int)totalSeconds;
			return Strings.Get("util.recipe_formatter.time", secs);
		}
	}
}
