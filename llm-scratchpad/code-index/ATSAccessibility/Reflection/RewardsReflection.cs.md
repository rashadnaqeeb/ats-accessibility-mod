# RewardsReflection.cs
Reflection helpers for the F3 Rewards panel. Provides access to pending reward availability, methods to open blueprint/cornerstone/newcomer popups, and next availability dates/times.

## class RewardsReflection (line 7)

### Fields
**Service cache**
- private static PropertyInfo `_gsBlueprintsServiceProperty` (line ~15)
- private static PropertyInfo `_gsCornerstonesServiceProperty` (line ~16)
- private static PropertyInfo `_gsNewcomersServiceProperty` (line ~17)
- private static PropertyInfo `_gsPopupServiceProperty` (line ~18)
- private static PropertyInfo `_gsCalendarServiceProperty` (line ~19)

**Blueprints service methods**
- private static MethodInfo `_bsGetCurrentBlueprintsMethod` (line ~25)
- private static MethodInfo `_bsGetNextBlueprintThresholdMethod` (line ~26)

**Cornerstones service methods**
- private static MethodInfo `_csGetCurrentCornerstonesMethod` (line ~32)
- private static MethodInfo `_csGetNextCornerstoneDateMethod` (line ~33)

**Newcomers service methods**
- private static MethodInfo `_nsAreNewcomersWaitningMethod` (line ~39)
  Note: typo in game code — "Waitning" not "Waiting"
- private static MethodInfo `_nsGetTimeToNextNewcomersMethod` (line ~40)

**Popup open methods**
- private static MethodInfo `_psShowBlueprintsMethod` (line ~46)
- private static MethodInfo `_psShowCornerstonesMethod` (line ~47)
- private static MethodInfo `_psShowNewcomersMethod` (line ~48)

**Calendar service methods**
- private static MethodInfo `_calGetSeasonMethod` (line ~54)
- private static MethodInfo `_calGetYearMethod` (line ~55)
- private static bool `_cached` (line ~58)

**Unavailability type cache (separate)**
- private static Type `_unavailReasonType` (line ~65)
- private static bool `_unavailCached` (line ~66)

### Methods
- private static void `EnsureCached()` (line 45)
- public static bool `HasPendingBlueprints()` (line 88)
- public static bool `HasPendingCornerstones()` (line 118)
- public static bool `HasPendingNewcomers()` (line 141)
- public static bool `OpenBlueprintsPopup()` (line 167)
- public static bool `OpenCornerstonesPopup()` (line 201)
- public static bool `OpenNewcomersPopup()` (line 232)
- private static void `EnsureUnavailTypeCached()` (line 302)
  Lazily caches the unavailability reason type for checking why blueprints/cornerstones are blocked.
- public static (int reputation, int maxReputation) `GetNextBlueprintThreshold()` (line 377)
- public static (int season, int year) `GetNextCornerstoneDate()` (line 422)
- private static bool `IsDateAfter(int s1, int y1, int s2, int y2, int yearsPerSeason, int seasonsPerYear)` (line 493)
  Helper to compare two season/year pairs.
- public static float `GetTimeToNextNewcomers()` (line 502)
- public static string `FormatGameTime(float seconds)` (line 527)
  Formats a time in game seconds as a human-readable string (e.g., "2 seasons").
- public static int `LogCacheStatus()` (line 544)
