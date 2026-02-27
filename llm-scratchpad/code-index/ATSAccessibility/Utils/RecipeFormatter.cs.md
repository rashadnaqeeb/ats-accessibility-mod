# RecipeFormatter.cs
Shared formatting for recipe ingredients and production time.
Used by both RecipesOverlay and EncyclopediaNavigator.

## class RecipeFormatter (line 10)

### Methods
- public static string FormatIngredients(Array goodsSets, Func<object, Array> getGoodsSetGoods, Func<object, string> getGoodRefDisplayName, Func<object, int> getGoodRefAmount) (line 16)
  Formats ingredient sets as readable strings. Same amounts: "3 x Herbs, Insects, Resin." Different amounts: "One of Stone x 4, Clay x 4, Salt x 3." Single item: "Herbs x 3." Takes delegate callbacks to decouple from specific reflection paths.
- public static string FormatTime(float totalSeconds) (line 63)
  Returns "Takes N sec." (truncates to integer seconds).
