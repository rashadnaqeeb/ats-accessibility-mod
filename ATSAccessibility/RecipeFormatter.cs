using System;
using System.Collections.Generic;
using System.Linq;

namespace ATSAccessibility
{
    /// <summary>
    /// Shared formatting for recipe ingredients and production time.
    /// Used by both RecipesOverlay and EncyclopediaNavigator.
    /// </summary>
    public static class RecipeFormatter
    {
        /// <summary>
        /// Format recipe ingredients in readable format.
        /// Same amounts: "3 x Herbs, Insects, Resin."
        /// Different amounts: "One of Stone x 4, Clay x 4, Salt x 3."
        /// </summary>
        public static string FormatIngredients(Array goodsSets,
            Func<object, Array> getGoodsSetGoods,
            Func<object, string> getGoodRefDisplayName,
            Func<object, int> getGoodRefAmount)
        {
            if (goodsSets == null || goodsSets.Length == 0) return "nothing.";

            var parts = new List<string>();
            foreach (var goodsSet in goodsSets)
            {
                var goods = getGoodsSetGoods(goodsSet);
                if (goods == null || goods.Length == 0) continue;

                // Collect names and amounts
                var items = new List<(string name, int amount)>();
                foreach (var goodRef in goods)
                {
                    var name = getGoodRefDisplayName(goodRef);
                    var amount = getGoodRefAmount(goodRef);
                    if (!string.IsNullOrEmpty(name))
                        items.Add((name, amount));
                }

                if (items.Count == 0) continue;

                if (items.Count == 1)
                {
                    // Single item, no alternatives
                    parts.Add($"{items[0].name} x {items[0].amount}.");
                }
                else
                {
                    // Multiple alternatives - check if all amounts are the same
                    bool sameAmounts = items.All(i => i.amount == items[0].amount);

                    if (sameAmounts)
                    {
                        // Same amounts: "3 x Herbs, Insects, Resin."
                        var names = string.Join(", ", items.Select(i => i.name));
                        parts.Add($"{items[0].amount} x {names}.");
                    }
                    else
                    {
                        // Different amounts: "One of Stone x 4, Clay x 4, Salt x 3."
                        var itemStrs = items.Select(i => $"{i.name} x {i.amount}");
                        parts.Add($"One of {string.Join(", ", itemStrs)}.");
                    }
                }
            }

            return parts.Count > 0 ? string.Join(" ", parts) : "nothing.";
        }

        /// <summary>
        /// Format production time for recipes.
        /// </summary>
        public static string FormatTime(float totalSeconds)
        {
            int secs = (int)totalSeconds;
            return $"Takes {secs} sec.";
        }
    }
}
