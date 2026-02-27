# WorldMapStatsReader.cs
Reads meta-level game statistics for world map announcements.
Provides level, meta resources, seal info, and cycle data.
Delegates reflection access to WorldMapReflection.

## class WorldMapStatsReader (line 13)

### Methods
- public static (int level, int currentXP, int targetXP) GetLevelInfo() (line 19)
  Delegates directly to WorldMapReflection.GetLevelInfo().
- public static List<(string name, int amount)> GetMetaResources() (line 27)
  Iterates WorldMapReflection.GetMetaCurrencies() dictionary, skips zero-amount entries, resolves display names.
- public static (string sealName, float rewardsMult, int bonusYears, int fragments) GetHighestSealInfo() (line 55)
  Returns empty values if no seals have been reforged. Reads displayName (LocaText), rewardsMultiplier, bonusYearsPerCycle from highest won seal, and sealFragments from cycle state.
- public static (int year, int yearsInCycle, int gamesWon, int gamesPlayed, int sealFragments) GetCycleInfo() (line 116)
  Delegates to WorldMapReflection.GetCycleInfo().
- public static void AnnounceLevel() (line 127)
  Announces "Level N, M experience to next level".
- public static void AnnounceMetaResources() (line 135)
  Announces "N Name, M Name2, ..." or "No meta resources".
- public static void AnnounceSealInfo() (line 153)
  Announces highest seal info or fragment count if no seals reforged.
- public static void AnnounceCycleInfo() (line 170)
  Announces "Year N, M years left in cycle, W of P games won" or "Blightstorm approaching, press E to end cycle" when year >= yearsInCycle.
- public static bool IsBlightstormApproaching() (line 186)
  Returns true when year > yearsInCycle - 1.
- public static bool OpenCycleEndPopup() (line 194)
  Invokes CycleEndPopupRequested on WorldBlackboardService via InvokeSubjectOnNext. Returns false if blightstorm not yet approaching.
