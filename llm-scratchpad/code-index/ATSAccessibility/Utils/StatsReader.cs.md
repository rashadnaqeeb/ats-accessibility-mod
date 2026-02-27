# StatsReader.cs
Provides access to game statistics (Reputation, Impatience, Hostility, Resolve)
via reflection and announces them via speech.

## class StatsReader (line 11)

### Fields
- private static readonly object[] _singleArgArray (line 13)
  Reusable single-element array for method invocations; avoids per-call allocations.
- private static int _currentSpeciesIndex (line 16)
- private static List<string> _cachedPresentRaces (line 17)
- private static readonly string[] SeasonNames (line 584)
  { "Drizzle", "Clearance", "Storm" }

### Methods
- public static void ResetSpeciesCycling() (line 20)
  Resets species cycling state between games.
- public static (float current, int target) GetReputationSummary() (line 32)
  Reads Reputation.Value (ReactiveProperty) and calls GetToWin() on reputation service.
- public static (float current, int max) GetImpatienceSummary() (line 60)
  Reads ReputationPenalty.Value and calls GetPenaltyToLoose().
- private static string FormatDecimal(float value) (line 89)
  Strips trailing zeros: 7.50 -> "7.5", 7.00 -> "7".
- public static (int points, int level, int pointsToNext) GetHostilitySummary() (line 96)
  Reads Points.Value, Level.Value, and calls GetPointsLeftToNextLevel() on hostility service.
- public static (float resolve, int threshold, int settling) GetResolveSummary(string race) (line 136)
  Calls GetResolveFor, GetMinResolveForRep, GetTargetResolveFor on resolve service.
- public static List<string> GetPresentRaces() (line 157)
  Returns races with at least one villager. Iterates VillagersService.Races dictionary via reflection.
- public static int GetRaceCount(string race) (line 196)
- public static (float baseResolve, string resilience) GetRaceBaseInfo(string raceName) (line 222)
  Gets initialResolve field and resilienceLabel (LocaText) from race model.
- public static List<string> GetReputationBreakdown() (line 260)
  Returns list of "+X.Y from Source" strings for ReputationChangeSource enum values (Other, Orders, Resolve, Relics).
- public static List<string> GetImpatienceBreakdown() (line 290)
  Returns rate per minute, modification from effects, and grace period seconds.
- public static List<string> GetHostilityBreakdown() (line 347)
  Returns "+/-N from Source" strings for known HostilitySource enum values (10-1001). Silently skips sources not configured for the current biome.
- public static List<string> GetResolveBreakdown(string race) (line 397)
  Returns base resolve, resilience label, and per-effect "Name: avg (+perVillager for count/total)" entries. Uses GetRoundedAverageResolveImpact with overload resolution via explicit Type[] array.
- public static void AnnounceQuickSummary() (line 504)
  Announces "Reputation X of Y, Impatience X of Y, Hostility level N, P/T" (S key).
- public static void AnnounceResolveSummary() (line 521)
  Announces resolve for all present races as "Race N of threshold, ..." (R key).
- public static void AnnounceNextSpeciesResolve() (line 546)
  Cycles through present races one at a time with population and resolve (V key). Advances index on each call.
- public static (int year, string season, float secondsRemaining) GetTimeSummary() (line 589)
- public static void AnnounceTimeSummary() (line 601)
  Announces "SeasonName, X minutes Y seconds remaining, Year N" (T key).
