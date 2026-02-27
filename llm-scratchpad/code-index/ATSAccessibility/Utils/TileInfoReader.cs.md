# TileInfoReader.cs
Reads detailed tile information (like tooltips) for the I key feature.
Provides building, natural resource, and deposit info via reflection.
Reflection caching is handled by TileInfoReflection.

## class TileInfoReader (line 14)

### Methods
- private static string GetChargesInfo(object state, FieldInfo chargesLeftField, object maxSource, FieldInfo maxChargesField) (line 24)
  Returns "X of Y charges" or null if maxCharges is 0. For NaturalResource, chargesLeft from state and max from model. For Deposit, both from state.
- private static string GetSourceBuildingNames(object dictionary, object key, bool useContainsKey) (line 35)
  Shared logic for CampsMatrix (string key, no ContainsKey needed) and HutsMatrix (object key, needs ContainsKey) lookups. Returns comma-joined display names.
- public static void ReadCurrentTile(int cursorX, int cursorY) (line 91)
  Entry point for I key. Dispatches to type-specific handlers based on GetObjectOn result. Checks inheritance chain for Building (Storage/ProductionBuilding/Building). Unrevealed glades say "Unrevealed glade"; Field type says "No object".
- private static string AngleToCompassDirection(float angle) (line 153)
  8-point compass, 45-degree segments centered on each direction. North = 0/360 degrees.
- private static string GetGuidepostDirection(object building) (line 172)
  Returns "Pointing N degrees" if building is a SealGuidepostView in a sealed biome. Calculates bearing from guidepost world position to seal center using Atan2.
- private static string GetBuildingInfo(object building) (line 226)
  Returns guidepost info + construction priority (if non-zero) + building model description. Name is not included (already announced by caller).
- private static string GetNaturalResourceInfo(object resource) (line 274)
  Returns charges, description, main product ("Produces X"), extra products ("Extra: X Y%"), and harvesting camps ("Harvested by: X").
- private static string GetResourceDepositInfo(object deposit) (line 346)
  Returns charges, priority (if non-default), description, main product, extra products, and gathering huts.
- private static string GetOreInfo(object ore) (line 424)
  Sums mainCharges + extraCharges arrays from state. Returns total charges remaining + description + product from displayProduct field.
- private static string GetSpringInfo(object spring) (line 502)
  Returns chargesLeft/maxCharges from state fields + description.
- private static string GetLakeInfo(object lake) (line 550)
  Returns charges, stored fish waiting for pickup (via GoodsContainer.Sum()), priority (if non-default), description, and main product.
- private static string GetGenericObjectInfo(object obj) (line 625)
  Tries Model.displayName, falls back to type name.
- private static string GetMainProductInfo(object model) (line 655)
  Path: model.production.good.displayName. Uses TileInfoReflection cached fields where available.
- private static string GetExtraProductsInfo(object model) (line 693)
  Path: model.extraProduction[] array of GoodRefChance with DisplayName property and chance field. Returns "Product1 N%, Product2 M%".
- private static string GetCampsForResource(object resourceModel, PropertyInfo refGoodNameProp) (line 734)
  Path: ResourcesService.CampsMatrix[Model.RefGoodName]. Uses string key (no ContainsKey).
- private static string GetHutsForDeposit(object depositModel) (line 765)
  Path: DepositsService.HutsMatrix[depositModel]. Uses object key (needs ContainsKey).
- private static bool InheritsFrom(Type type, string ancestorName) (line 795)
  Walks type.BaseType chain checking Name.
- private static string FormatNodePriority(int priority) (line 804)
  Special-cases -5 "(lowest)", 5 "(highest)", 0 "(default)".
