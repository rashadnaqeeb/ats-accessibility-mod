using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Navigator for Shrine buildings.
    /// Shrines extend Building (not ProductionBuilding) and have no workers.
    /// Provides Info and Effects sections with tiered effects that can be used.
    /// </summary>
    public class ShrineNavigator : BuildingSectionNavigator
    {
        // ========================================
        // SECTION TYPES
        // ========================================

        private enum SectionType
        {
            Info,
            Effects
        }

        // ========================================
        // CACHED DATA
        // ========================================

        private string[] _sectionNames;
        private SectionType[] _sectionTypes;
        private string _buildingName;
        private string _buildingDescription;
        private bool _isFinished;
        private bool _isSleeping;

        // Effect tier data
        private List<EffectTierInfo> _effectTiers = new List<EffectTierInfo>();

        // ========================================
        // EFFECT TIER INFO CLASS
        // ========================================

        private class EffectTierInfo
        {
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

        protected override string[] GetSections()
        {
            return _sectionNames;
        }

        protected override int GetItemCount(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Info:
                    return GetInfoItemCount();
                case SectionType.Effects:
                    return _effectTiers.Count > 0 ? _effectTiers.Count : 1;
                default:
                    return 0;
            }
        }

        protected override int GetSubItemCount(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return 0;

            // Effects tiers have sub-items (individual effects to use)
            if (_sectionTypes[sectionIndex] == SectionType.Effects && itemIndex < _effectTiers.Count)
            {
                var tier = _effectTiers[itemIndex];
                // Show effects if unlimited (MaxCharges == 0) or has charges left
                if (tier.MaxCharges <= 0 || tier.ChargesLeft > 0)
                {
                    return tier.DrawableEffectIndices.Count;
                }
            }

            return 0;
        }

        protected override void AnnounceSection(int sectionIndex)
        {
            string sectionName = _sectionNames[sectionIndex];
            Speech.Say(sectionName);
        }

        protected override void AnnounceItem(int sectionIndex, int itemIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
                return;

            switch (_sectionTypes[sectionIndex])
            {
                case SectionType.Info:
                    AnnounceInfoItem(itemIndex);
                    break;
                case SectionType.Effects:
                    AnnounceEffectTierItem(itemIndex);
                    break;
            }
        }

        protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Effects && itemIndex < _effectTiers.Count)
            {
                var tier = _effectTiers[itemIndex];
                if (subItemIndex < tier.DrawableEffectIndices.Count)
                {
                    int actualEffectIndex = tier.DrawableEffectIndices[subItemIndex];
                    string effectName = BuildingReflection.GetShrineTierEffectName(_building, tier.TierIndex, actualEffectIndex);
                    Speech.Say(effectName ?? "Unknown effect");
                }
            }
        }

        protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex)
        {
            if (_sectionTypes[sectionIndex] == SectionType.Effects && itemIndex < _effectTiers.Count)
            {
                var tier = _effectTiers[itemIndex];
                // Check if usable: unlimited (MaxCharges == 0) or has charges left
                if (tier.MaxCharges > 0 && tier.ChargesLeft <= 0)
                {
                    Speech.Say("No charges remaining");
                    return false;
                }

                if (subItemIndex >= tier.DrawableEffectIndices.Count)
                    return false;

                int actualEffectIndex = tier.DrawableEffectIndices[subItemIndex];

                if (BuildingReflection.UseShrineEffect(_building, tier.TierIndex, actualEffectIndex))
                {
                    string effectName = BuildingReflection.GetShrineTierEffectName(_building, tier.TierIndex, actualEffectIndex);
                    Speech.Say($"Used {effectName ?? "effect"}");
                    RefreshEffectData();  // Refresh to update charges and drawable effects
                    return true;
                }
                else
                {
                    Speech.Say("Failed to use effect");
                }
            }
            return false;
        }

        protected override void RefreshData()
        {
            _buildingName = BuildingReflection.GetBuildingName(_building) ?? "Shrine";
            _buildingDescription = BuildingReflection.GetBuildingDescription(_building);
            _isFinished = BuildingReflection.IsBuildingFinished(_building);
            _isSleeping = BuildingReflection.IsBuildingSleeping(_building);

            RefreshEffectData();
            BuildSections();

            int totalDrawable = 0;
            foreach (var tier in _effectTiers)
                totalDrawable += tier.DrawableEffectIndices.Count;
            Debug.Log($"[ATSAccessibility] ShrineNavigator: Refreshed data - {_effectTiers.Count} tiers, {totalDrawable} drawable effects");
        }

        protected override void ClearData()
        {
            _effectTiers.Clear();
            _sectionNames = null;
            _sectionTypes = null;
        }

        // ========================================
        // DATA REFRESH
        // ========================================

        private void RefreshEffectData()
        {
            _effectTiers.Clear();

            int tierCount = BuildingReflection.GetShrineEffectTierCount(_building);
            for (int i = 0; i < tierCount; i++)
            {
                var tier = new EffectTierInfo
                {
                    TierIndex = i,
                    Label = BuildingReflection.GetShrineTierLabel(_building, i),
                    ChargesLeft = BuildingReflection.GetShrineTierChargesLeft(_building, i),
                    MaxCharges = BuildingReflection.GetShrineTierMaxCharges(_building, i)
                };

                // Only include effects that can be drawn (visible to sighted players)
                int effectCount = BuildingReflection.GetShrineTierEffectCount(_building, i);
                for (int j = 0; j < effectCount; j++)
                {
                    if (BuildingReflection.CanShrineTierEffectBeDrawn(_building, i, j))
                    {
                        tier.DrawableEffectIndices.Add(j);
                    }
                }

                _effectTiers.Add(tier);
            }
        }

        private void BuildSections()
        {
            var sections = new List<string>();
            var types = new List<SectionType>();

            // Always have Info
            sections.Add("Info");
            types.Add(SectionType.Info);

            // Abilities section
            sections.Add("Abilities");
            types.Add(SectionType.Effects);

            _sectionNames = sections.ToArray();
            _sectionTypes = types.ToArray();
        }

        // ========================================
        // INFO SECTION
        // ========================================

        private int GetInfoItemCount()
        {
            int count = 1;  // Name
            if (!string.IsNullOrEmpty(_buildingDescription)) count++;  // Description
            count++;  // Status
            return count;
        }

        private void AnnounceInfoItem(int itemIndex)
        {
            int index = 0;

            // Name
            if (itemIndex == index)
            {
                Speech.Say($"Name: {_buildingName}");
                return;
            }
            index++;

            // Description (if present)
            if (!string.IsNullOrEmpty(_buildingDescription))
            {
                if (itemIndex == index)
                {
                    Speech.Say($"Description: {_buildingDescription}");
                    return;
                }
                index++;
            }

            // Status
            if (itemIndex == index)
            {
                if (!_isFinished)
                {
                    Speech.Say("Status: Under construction");
                }
                else if (_isSleeping)
                {
                    Speech.Say("Status: Paused");
                }
                else
                {
                    Speech.Say("Status: Active");
                }
            }
        }

        // ========================================
        // EFFECTS SECTION
        // ========================================

        private void AnnounceEffectTierItem(int itemIndex)
        {
            if (_effectTiers.Count == 0)
            {
                Speech.Say("No effects available");
                return;
            }

            if (itemIndex < _effectTiers.Count)
            {
                var tier = _effectTiers[itemIndex];
                string label = tier.Label ?? $"Tier {tier.TierIndex + 1}";

                // MaxCharges == 0 means unlimited uses
                if (tier.MaxCharges <= 0)
                {
                    Speech.Say($"{label}, unlimited");
                }
                else if (tier.ChargesLeft > 0)
                {
                    Speech.Say($"{label}, {tier.ChargesLeft} of {tier.MaxCharges} charges");
                }
                else
                {
                    Speech.Say($"{label}, no charges remaining");
                }
            }
        }
    }
}
