using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Reads meta-level game statistics for world map announcements.
	/// Provides level, meta resources, seal info, and cycle data.
	/// Delegates reflection access to WorldMapReflection.
	/// </summary>
	public static class WorldMapStatsReader {

		/// <summary>
		/// Get player level info.
		/// Returns (level, currentXP, targetXP).
		/// </summary>
		public static (int level, int currentXP, int targetXP) GetLevelInfo() {
			return WorldMapReflection.GetLevelInfo();
		}

		/// <summary>
		/// Get meta resources (Food, Machinery, Artifacts, etc.) with their amounts.
		/// Returns list of (displayName, amount) tuples.
		/// </summary>
		public static List<(string name, int amount)> GetMetaResources() {
			var result = new List<(string name, int amount)>();

			try {
				var currencies = WorldMapReflection.GetMetaCurrencies();
				if (currencies == null) return result;

				foreach (DictionaryEntry entry in currencies) {
					var currencyName = entry.Key as string;
					var amount = (int)entry.Value;

					if (string.IsNullOrEmpty(currencyName) || amount <= 0) continue;

					var displayName = WorldMapReflection.GetMetaCurrencyDisplayName(currencyName);
					result.Add((displayName, amount));
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetMetaResources failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get info about the highest reforged seal.
		/// Returns (sealName, rewardsMultiplier, bonusYears, currentFragments).
		/// Returns empty values if no seals reforged.
		/// </summary>
		public static (string sealName, float rewardsMult, int bonusYears, int fragments) GetHighestSealInfo() {
			try {
				var worldServices = WorldMapReflection.GetWorldServices();
				if (worldServices == null) return (null, 0f, 0, 0);

				// Get WorldSealsService
				var sealsServiceProp = WorldMapReflection.WorldSealsServiceProperty;
				var sealsService = sealsServiceProp?.GetValue(worldServices);
				if (sealsService == null) return (null, 0f, 0, 0);

				// Check if any seal was completed
				var wasCompletedMethod = WorldMapReflection.SealsWasAnyCompleted;
				var wasCompleted = wasCompletedMethod?.Invoke(sealsService, null);
				if (wasCompleted == null || !(bool)wasCompleted)
					return (null, 0f, 0, 0);

				// Get highest won seal
				var getHighestMethod = WorldMapReflection.SealsGetHighestWon;
				var highestSeal = getHighestMethod?.Invoke(sealsService, null);
				if (highestSeal == null) return (null, 0f, 0, 0);

				// Get seal displayName
				var displayNameField = highestSeal.GetType().GetField("displayName",
					GameReflection.PublicInstance);
				var displayName = displayNameField?.GetValue(highestSeal);
				var sealName = GameReflection.GetLocaText(displayName) ?? "";

				// Get rewardsMultiplier
				var rewardsMulField = highestSeal.GetType().GetField("rewardsMultiplier",
					GameReflection.PublicInstance);
				var rewardsMult = (float)(rewardsMulField?.GetValue(highestSeal) ?? 0f);

				// Get bonusYearsPerCycle
				var bonusYearsField = highestSeal.GetType().GetField("bonusYearsPerCycle",
					GameReflection.PublicInstance);
				var bonusYears = (int)(bonusYearsField?.GetValue(highestSeal) ?? 0);

				// Get current seal fragments from CycleState
				int fragments = 0;
				var worldStateService = WorldMapReflection.GetWorldStateService();
				var cycleProp = WorldMapReflection.CycleProperty;
				if (worldStateService != null && cycleProp != null) {
					var cycleState = cycleProp.GetValue(worldStateService);
					if (cycleState != null) {
						var fragField = cycleState.GetType().GetField("sealFragments",
							GameReflection.PublicInstance);
						fragments = (int)(fragField?.GetValue(cycleState) ?? 0);
					}
				}

				return (sealName, rewardsMult, bonusYears, fragments);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetHighestSealInfo failed: {ex.Message}");
				return (null, 0f, 0, 0);
			}
		}

		/// <summary>
		/// Get cycle/storm information.
		/// Returns (year, yearsInCycle, gamesWon, gamesPlayed, sealFragments).
		/// </summary>
		public static (int year, int yearsInCycle, int gamesWon, int gamesPlayed, int sealFragments) GetCycleInfo() {
			return WorldMapReflection.GetCycleInfo();
		}

		// ========================================
		// ANNOUNCEMENT METHODS (called by KeyboardManager)
		// ========================================

		/// <summary>
		/// Announce player level and XP to next level.
		/// </summary>
		public static void AnnounceLevel() {
			var (level, currentXP, targetXP) = GetLevelInfo();
			int remaining = targetXP - currentXP;
			Speech.Say($"Level {level}, {remaining} experience to next level");
		}

		/// <summary>
		/// Announce meta resources with counts.
		/// </summary>
		public static void AnnounceMetaResources() {
			var resources = GetMetaResources();
			if (resources.Count == 0) {
				Speech.Say("No meta resources");
				return;
			}

			var parts = new List<string>();
			foreach (var (name, amount) in resources) {
				parts.Add($"{amount} {name}");
			}
			Speech.Say(string.Join(", ", parts));
		}

		/// <summary>
		/// Announce highest seal info or fragment count if no seals reforged.
		/// </summary>
		public static void AnnounceSealInfo() {
			var (name, mult, years, frags) = GetHighestSealInfo();
			if (string.IsNullOrEmpty(name)) {
				// No seal reforged yet, but still show fragment count from cycle state
				var (_, _, _, _, fragments) = GetCycleInfo();
				Speech.Say($"No seals reforged, {fragments} fragments");
				return;
			}

			int rewardsPercent = (int)(mult * 100);
			Speech.Say($"{name}, {rewardsPercent} percent rewards, {years} bonus years, {frags} fragments");
		}

		/// <summary>
		/// Announce cycle/storm info.
		/// Seal fragments are reported by S key, not repeated here.
		/// </summary>
		public static void AnnounceCycleInfo() {
			var (year, yearsInCycle, won, played, _) = GetCycleInfo();
			int yearsLeft = yearsInCycle - year;

			string cycleStatus;
			if (yearsLeft <= 0)
				cycleStatus = "Blightstorm approaching, press E to end cycle";
			else
				cycleStatus = $"{yearsLeft} years left in cycle";

			Speech.Say($"Year {year}, {cycleStatus}, {won} of {played} games won");
		}

		/// <summary>
		/// Check if the Blightstorm is approaching (cycle can be finished).
		/// </summary>
		public static bool IsBlightstormApproaching() {
			var (year, yearsInCycle, _, _, _) = GetCycleInfo();
			return year > yearsInCycle - 1;
		}

		/// <summary>
		/// Open the cycle end popup to trigger the Blightstorm.
		/// </summary>
		public static bool OpenCycleEndPopup() {
			if (!IsBlightstormApproaching()) {
				Speech.Say("Cannot end cycle yet");
				return false;
			}

			var wbb = WorldMapReflection.GetWorldBlackboardService();
			if (wbb == null) return false;

			if (GameReflection.InvokeSubjectOnNext(wbb, "CycleEndPopupRequested", null)) {
				return true;
			}

			Speech.Say("Failed to open cycle end");
			return false;
		}
	}
}
