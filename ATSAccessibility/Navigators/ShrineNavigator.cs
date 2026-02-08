using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Navigators {
	/// <summary>
	/// Navigator for Shrine buildings.
	/// Shrines extend Building (not ProductionBuilding) and have no workers.
	/// Provides Abilities section with tiered effects that can be used.
	/// </summary>
	public class ShrineNavigator: BuildingSectionNavigator {
		// ========================================
		// SECTION TYPES
		// ========================================

		private enum SectionType {
			Effects
		}

		// ========================================
		// CACHED DATA
		// ========================================

		private string[] _sectionNames;
		private SectionType[] _sectionTypes;

		// Effect tier data
		private List<EffectTierInfo> _effectTiers = new List<EffectTierInfo>();

		// Confirmation state
		private bool _awaitingConfirm = false;
		private int _confirmTierIndex;
		private int _confirmEffectIndex;

		// ========================================
		// EFFECT TIER INFO CLASS
		// ========================================

		private class EffectTierInfo {
			public int TierIndex;
			public string Label;
			public int ChargesLeft;
			public int MaxCharges;
			// List of effect indices that can be drawn (visible to sighted players)
			public List<int> DrawableEffectIndices = new List<int>();
		}

		// ========================================
		// BASE CLASS IMPLEMENTATION
		// ========================================

		protected override string NavigatorName => "ShrineNavigator";

		protected override string[] GetSections() {
			return _sectionNames;
		}

		protected override int GetItemCount(int sectionIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return 0;

			switch (_sectionTypes[sectionIndex]) {
				case SectionType.Effects:
					return _effectTiers.Count > 0 ? _effectTiers.Count : 1;
				default:
					return 0;
			}
		}

		protected override int GetSubItemCount(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return 0;

			// Effects tiers have sub-items (individual effects to use)
			if (_sectionTypes[sectionIndex] == SectionType.Effects && itemIndex < _effectTiers.Count) {
				var tier = _effectTiers[itemIndex];
				// Show effects if unlimited (MaxCharges == 0) or has charges left
				if (tier.MaxCharges <= 0 || tier.ChargesLeft > 0) {
					return tier.DrawableEffectIndices.Count;
				}
			}

			return 0;
		}

		protected override void AnnounceSection(int sectionIndex) {
			string sectionName = _sectionNames[sectionIndex];
			Speech.Say(sectionName);
		}

		protected override void AnnounceItem(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return;

			switch (_sectionTypes[sectionIndex]) {
				case SectionType.Effects:
					AnnounceEffectTierItem(itemIndex);
					break;
			}
		}

		protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex) {
			if (_sectionTypes[sectionIndex] == SectionType.Effects && itemIndex < _effectTiers.Count) {
				var tier = _effectTiers[itemIndex];
				if (subItemIndex < tier.DrawableEffectIndices.Count) {
					int actualEffectIndex = tier.DrawableEffectIndices[subItemIndex];
					string effectName = BuildingReflection.GetShrineTierEffectName(_building, tier.TierIndex, actualEffectIndex);
					string description = BuildingReflection.GetShrineTierEffectDescription(_building, tier.TierIndex, actualEffectIndex);

					if (!string.IsNullOrEmpty(description))
						Speech.Say($"{effectName ?? "Unknown effect"}: {description}");
					else
						Speech.Say(effectName ?? "Unknown effect");
				}
			}
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (_awaitingConfirm) {
				if (keyCode == KeyCode.Return || keyCode == KeyCode.KeypadEnter || keyCode == KeyCode.Space) {
					// Confirm — use the effect
					_awaitingConfirm = false;
					DoUseEffect(_confirmTierIndex, _confirmEffectIndex);
				} else {
					// Any other key cancels
					_awaitingConfirm = false;
					Speech.Say("Cancelled");
				}
				return true;
			}

			return base.HandleSpecialKey(keyCode, modifiers);
		}

		protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex) {
			if (_sectionTypes[sectionIndex] == SectionType.Effects && itemIndex < _effectTiers.Count) {
				var tier = _effectTiers[itemIndex];
				// Check if usable: unlimited (MaxCharges == 0) or has charges left
				if (tier.MaxCharges > 0 && tier.ChargesLeft <= 0) {
					Speech.Say("No charges remaining");
					return false;
				}

				if (subItemIndex >= tier.DrawableEffectIndices.Count)
					return false;

				int actualEffectIndex = tier.DrawableEffectIndices[subItemIndex];

				// Play charging loop sound (or fallback) and await confirmation
				var loopSound = BuildingReflection.GetShrineChargingLoopSound(_building);
				if (loopSound != null)
					SoundManager.PlaySoundEffect(loopSound);
				else
					SoundManager.PlaySeasonRewardsSlot();

				_awaitingConfirm = true;
				_confirmTierIndex = tier.TierIndex;
				_confirmEffectIndex = actualEffectIndex;

				Speech.Say("Enter to confirm");
				return true;
			}
			return false;
		}

		protected override void RefreshData() {
			RefreshEffectData();
			BuildSections();

			int totalDrawable = 0;
			foreach (var tier in _effectTiers)
				totalDrawable += tier.DrawableEffectIndices.Count;
			Debug.Log($"[ATSAccessibility] ShrineNavigator: Refreshed data - {_effectTiers.Count} tiers, {totalDrawable} drawable effects");
		}

		protected override void ClearData() {
			_effectTiers.Clear();
			_sectionNames = null;
			_sectionTypes = null;
			_awaitingConfirm = false;
		}

		// ========================================
		// DATA REFRESH
		// ========================================

		private void RefreshEffectData() {
			_effectTiers.Clear();

			int tierCount = BuildingReflection.GetShrineEffectTierCount(_building);
			for (int i = 0; i < tierCount; i++) {
				var tier = new EffectTierInfo {
					TierIndex = i,
					Label = BuildingReflection.GetShrineTierLabel(_building, i),
					ChargesLeft = BuildingReflection.GetShrineTierChargesLeft(_building, i),
					MaxCharges = BuildingReflection.GetShrineTierMaxCharges(_building, i)
				};

				// Only include effects that can be drawn (visible to sighted players)
				int effectCount = BuildingReflection.GetShrineTierEffectCount(_building, i);
				for (int j = 0; j < effectCount; j++) {
					if (BuildingReflection.CanShrineTierEffectBeDrawn(_building, i, j)) {
						tier.DrawableEffectIndices.Add(j);
					}
				}

				_effectTiers.Add(tier);
			}
		}

		private void BuildSections() {
			// Abilities is the only section
			_sectionNames = new[] { "Abilities" };
			_sectionTypes = new[] { SectionType.Effects };
		}

		// ========================================
		// EFFECTS SECTION
		// ========================================

		private void AnnounceEffectTierItem(int itemIndex) {
			if (_effectTiers.Count == 0) {
				Speech.Say("No effects available");
				return;
			}

			if (itemIndex < _effectTiers.Count) {
				var tier = _effectTiers[itemIndex];
				string label = tier.Label ?? $"Tier {tier.TierIndex + 1}";

				// MaxCharges == 0 means unlimited uses
				if (tier.MaxCharges <= 0) {
					Speech.Say($"{label}, unlimited");
				} else if (tier.ChargesLeft > 0) {
					Speech.Say($"{label}, {tier.ChargesLeft} of {tier.MaxCharges} charges");
				} else {
					Speech.Say($"{label}, no charges remaining");
				}
			}
		}

		private void DoUseEffect(int tierIndex, int effectIndex) {
			if (BuildingReflection.UseShrineEffect(_building, tierIndex, effectIndex)) {
				// Play final sound on success (or fallback)
				var finalSound = BuildingReflection.GetShrineFinalSound(_building);
				if (finalSound != null)
					SoundManager.PlaySoundEffect(finalSound);
				else
					SoundManager.PlayButtonClick();

				string effectName = BuildingReflection.GetShrineTierEffectName(_building, tierIndex, effectIndex);
				Speech.Say($"Used {effectName ?? "effect"}");
				RefreshEffectData();  // Refresh to update charges and drawable effects
			} else {
				SoundManager.PlayFailed();
				Speech.Say("Failed to use effect");
			}
		}
	}
}
