using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Navigators {
	/// <summary>
	/// Navigator for Relic buildings (glade events).
	/// Provides phase-based navigation with name/description header at top:
	/// - Phase A (not started): [Name/Desc], Decisions, Choose Requirements, Effects, Preview Rewards, Start Investigation
	/// - Phase B (in progress): [Name/Desc], Status (info only), Workers, Requirements, Effects, Rewards, Cancel Investigation
	/// - Phase C (finished): [Name/Desc], Status (info only), Workers, Storage
	/// </summary>
	public class RelicNavigator: BuildingSectionNavigator {
		// ========================================
		// SECTION TYPES
		// ========================================

		private enum SectionType {
			Info,
			Decisions,
			Requirements,
			Effects,
			Rewards,
			Status,
			Workers,
			Storage,
			Upgrades,
			Cancel
		}

		// ========================================
		// CACHED DATA
		// ========================================

		private string[] _sectionNames;
		private SectionType[] _sectionTypes;
		private string _buildingName;
		private string _buildingDescription;
		private string _threatLevel;

		// Phase flags
		private bool _investigationStarted;
		private bool _investigationFinished;
		private float _progress;  // 0-1
		private float _timeLeft;

		// Decision data
		private int _decisionCount;
		private bool _hasMultipleDecisions;
		private int _selectedDecisionIndex;

		// Requirements for current decision
		private struct GoodsSetData {
			public int alternativeCount;
			public string[] goodNames;
			public string[] goodDisplayNames;
			public int[] goodAmounts;
			public int pickedIndex;
		}
		private GoodsSetData[] _goodsSets;
		private int _goodsSetCount;

		// Effects for current decision
		private RelicReflection.RelicEffectInfo[] _workingEffects;
		private RelicReflection.RelicEffectInfo[] _activeEffects;
		private RelicReflection.RelicEffectInfo[] _dynamicEffects;  // Current tier effects
		private RelicReflection.RelicEffectInfo[] _nextTierEffects;  // Next tier effects (preview)
		private bool _areEffectsPermanent;

		// Dynamic effects (escalating over time)
		private bool _hasDynamicEffects;
		private int _currentEffectTier;
		private int _totalEffectTiers;
		private float _timeToNextTier;
		private bool _isLastTierReached;

		// Rewards for current decision
		private RelicReflection.RelicRewardInfo[] _rewards;
		private bool _hasRewards;

		// Status/action data
		private bool _canStart;
		private string _startBlockingReason;
		private bool _canCancel;
		private bool _hasAnyWorkplace;

		// Storage data (Phase C rewards)
		private List<(string goodName, string displayName, int amount)> _storageItems = new List<(string, string, int)>();
		private int _storageTotalSum;

		// ========================================
		// BASE CLASS IMPLEMENTATION
		// ========================================

		protected override string NavigatorName => "RelicNavigator";

		protected override string GetOpenAnnouncement() {
			string header = _buildingName ?? Strings.Get("common.relic");
			header += Strings.Get("nav.relic.threat_level_suffix", _threatLevel);
			if (!string.IsNullOrEmpty(_buildingDescription))
				header += ". " + _buildingDescription;
			return header;
		}

		public RelicNavigator() {
			_workersSection.GetWorkerIdsFunc = RelicReflection.GetRelicWorkerIds;
		}

		protected override string[] GetSections() {
			return _sectionNames;
		}

		protected override int GetItemCount(int sectionIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return 0;

			switch (_sectionTypes[sectionIndex]) {
				case SectionType.Decisions:
					return _decisionCount;
				case SectionType.Requirements:
					return _goodsSetCount;
				case SectionType.Effects:
					return GetEffectsItemCount();
				case SectionType.Rewards:
					return _rewards?.Length ?? 0;
				case SectionType.Status:
					return GetStatusItemCount();
				case SectionType.Workers:
					return _workersSection.GetItemCount();
				case SectionType.Storage:
					return _storageItems.Count;
				case SectionType.Upgrades:
					return _upgradesSection.GetItemCount();
				default:
					return 0;
			}
		}

		protected override int GetSubItemCount(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return 0;

			if (_sectionTypes[sectionIndex] == SectionType.Requirements) {
				// Sub-items only if goods set has alternatives (more than 1 good)
				if (itemIndex >= 0 && itemIndex < _goodsSetCount && _goodsSets[itemIndex].alternativeCount > 1)
					return _goodsSets[itemIndex].alternativeCount;
				return 0;
			}

			if (_sectionTypes[sectionIndex] == SectionType.Workers) {
				return _workersSection.GetSubItemCount(itemIndex);
			}

			if (_sectionTypes[sectionIndex] == SectionType.Upgrades) {
				return _upgradesSection.GetSubItemCount(itemIndex);
			}

			return 0;
		}

		protected override void AnnounceSection(int sectionIndex) {
			if (_sectionTypes == null || sectionIndex < 0 || sectionIndex >= _sectionTypes.Length) return;

			var sectionType = _sectionTypes[sectionIndex];

			if (sectionType == SectionType.Status) {
				RefreshLiveData();
				if (_investigationFinished) {
					// Phase C: Resolved status
					if (_storageTotalSum > 0)
						Speech.Say(Strings.Get("nav.relic.status_resolved_with_pickup", _storageTotalSum));
					else
						Speech.Say(Strings.Get("nav.relic.status_resolved"));
				} else if (_investigationStarted) {
					// Phase B: In progress status with full details
					int percentage = Mathf.RoundToInt(_progress * 100f);
					string timeStr = FormatTimeLeft();
					Speech.Say(Strings.Get("nav.relic.status_in_progress", percentage, timeStr));
				} else {
					// Phase A: Start investigation
					if (_canStart)
						Speech.Say(Strings.Get("common.start_investigation"));
					else
						Speech.Say(_startBlockingReason ?? Strings.Get("nav.relic.cannot_start"));
				}
				return;
			}

			if (sectionType == SectionType.Effects) {
				string effectsAnnouncement = _sectionNames[sectionIndex];
				if (_areEffectsPermanent)
					effectsAnnouncement += Strings.Get("nav.relic.permanent_suffix");
				Speech.Say(effectsAnnouncement);
				return;
			}

			string sectionName = _sectionNames[sectionIndex];
			Speech.Say(sectionName);
		}

		private string FormatDynamicEffectTime(float seconds) {
			int secs = Mathf.RoundToInt(seconds);
			if (secs <= 0)
				return Strings.Get("nav.relic.time.moments");
			if (secs < 60)
				return Strings.Get("nav.relic.time.seconds", secs);
			int minutes = secs / 60;
			int remainingSecs = secs % 60;
			if (remainingSecs > 0)
				return Strings.Get("nav.relic.time.minutes_seconds", minutes, remainingSecs);
			return Strings.Get("nav.relic.time.minutes", minutes);
		}

		private string FormatTimeLeft() => FormattingUtils.FormatTimeRemaining(_timeLeft);

		protected override void AnnounceItem(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return;

			switch (_sectionTypes[sectionIndex]) {
				case SectionType.Decisions:
					AnnounceDecisionItem(itemIndex);
					break;
				case SectionType.Requirements:
					AnnounceRequirementItem(itemIndex);
					break;
				case SectionType.Effects:
					AnnounceEffectItem(itemIndex);
					break;
				case SectionType.Rewards:
					AnnounceRewardItem(itemIndex);
					break;
				case SectionType.Workers:
					_workersSection.AnnounceItem(itemIndex);
					break;
				case SectionType.Storage:
					AnnounceStorageItem(itemIndex);
					break;
				case SectionType.Upgrades:
					_upgradesSection.AnnounceItem(itemIndex);
					break;
			}
		}

		protected override void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex) {
			if (_sectionTypes[sectionIndex] == SectionType.Requirements) {
				AnnounceRequirementSubItem(itemIndex, subItemIndex);
			} else if (_sectionTypes[sectionIndex] == SectionType.Workers) {
				_workersSection.AnnounceSubItem(itemIndex, subItemIndex);
			} else if (_sectionTypes[sectionIndex] == SectionType.Upgrades) {
				_upgradesSection.AnnounceSubItem(itemIndex, subItemIndex);
			}
		}

		protected override bool PerformItemAction(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return false;

			switch (_sectionTypes[sectionIndex]) {
				case SectionType.Decisions:
					return PerformDecisionAction(itemIndex);
				case SectionType.Effects:
					return PerformEffectAction(itemIndex);
				case SectionType.Rewards:
					return PerformRewardAction(itemIndex);
				default:
					return false;
			}
		}

		protected override string GetNoSubItemsMessage(int sectionIndex, int itemIndex) {
			if (_sectionTypes[sectionIndex] == SectionType.Workers)
				return Strings.Get("common.no_free_workers");
			return null;
		}

		protected override bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex) {
			if (_sectionTypes[sectionIndex] == SectionType.Requirements) {
				return PerformRequirementSubItemAction(itemIndex, subItemIndex);
			}
			if (_sectionTypes[sectionIndex] == SectionType.Workers) {
				return PerformWorkerSubItemAction(itemIndex, subItemIndex);
			}
			if (_sectionTypes[sectionIndex] == SectionType.Upgrades) {
				return _upgradesSection.PerformSubItemAction(itemIndex, subItemIndex);
			}
			return false;
		}

		protected override string GetItemName(int sectionIndex, int itemIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length)
				return null;

			switch (_sectionTypes[sectionIndex]) {
				case SectionType.Decisions:
					return RelicReflection.GetRelicDecisionLabel(_building, itemIndex);
				case SectionType.Requirements:
					if (itemIndex >= 0 && itemIndex < _goodsSetCount) {
						int picked = _goodsSets[itemIndex].pickedIndex;
						if (picked >= 0 && picked < _goodsSets[itemIndex].goodDisplayNames.Length)
							return _goodsSets[itemIndex].goodDisplayNames[picked];
					}
					return null;
				case SectionType.Storage:
					if (itemIndex >= 0 && itemIndex < _storageItems.Count)
						return _storageItems[itemIndex].displayName;
					return null;
				default:
					return null;
			}
		}

		protected override void RefreshData() {
			_buildingName = BuildingReflection.GetBuildingName(_building) ?? Strings.Get("common.relic");
			_buildingDescription = BuildingReflection.GetBuildingDescription(_building);
			_threatLevel = RelicReflection.GetRelicDangerLevel(_building) ?? Strings.Get("common.none");

			// Phase flags
			_investigationStarted = RelicReflection.IsRelicInvestigationStarted(_building);
			_investigationFinished = RelicReflection.IsRelicInvestigationFinished(_building);
			_progress = RelicReflection.GetRelicProgress(_building);
			_timeLeft = RelicReflection.GetRelicTimeLeft(_building);

			// Decision metadata
			_hasMultipleDecisions = RelicReflection.RelicHasMultipleDecisions(_building);
			_decisionCount = RelicReflection.GetRelicDecisionCount(_building);
			_selectedDecisionIndex = RelicReflection.GetRelicDecisionIndex(_building);

			// Model properties
			_hasAnyWorkplace = RelicReflection.RelicHasAnyWorkplace(_building);
			_areEffectsPermanent = RelicReflection.RelicAreEffectsPermanent(_building);

			// Dynamic effects (escalating over time)
			_hasDynamicEffects = RelicReflection.RelicHasDynamicEffects(_building);
			_currentEffectTier = RelicReflection.GetRelicCurrentEffectTier(_building);
			_totalEffectTiers = RelicReflection.GetRelicEffectTierCount(_building);
			_timeToNextTier = RelicReflection.GetRelicTimeToNextEffectTier(_building);
			_isLastTierReached = RelicReflection.RelicIsLastEffectTierReached(_building);

			// Decision-dependent data
			RefreshDecisionDetails();

			// Storage data (Phase C)
			RefreshStorageData();

			// Status/actions
			RefreshStatusData();

			// Build sections
			BuildSections();
		}

		protected override void ClearData() {
			_sectionNames = null;
			_sectionTypes = null;
			_buildingName = null;
			_buildingDescription = null;
			_threatLevel = null;
			ClearWorkersSection();
			_goodsSets = null;
			_goodsSetCount = 0;
			_workingEffects = null;
			_activeEffects = null;
			_dynamicEffects = null;
			_nextTierEffects = null;
			_hasDynamicEffects = false;
			_currentEffectTier = 0;
			_totalEffectTiers = 0;
			_timeToNextTier = 0f;
			_isLastTierReached = false;
			_rewards = null;
			_hasRewards = false;
			_storageItems.Clear();
			_storageTotalSum = 0;
			ClearUpgradesSection();
		}

		// ========================================
		// SECTION BUILDING
		// ========================================

		private void BuildSections() {
			var sectionNames = new List<string>();
			var sectionTypes = new List<SectionType>();

			if (_investigationFinished) {
				// Phase C: Info, Status, Storage
				sectionNames.Add(Strings.Get("common.status"));
				sectionTypes.Add(SectionType.Status);

				if (TryInitializeWorkersSection()) {
					sectionNames.Add(Strings.Get("common.workers"));
					sectionTypes.Add(SectionType.Workers);
				}

				if (_storageItems.Count > 0) {
					sectionNames.Add(Strings.Get("common.storage"));
					sectionTypes.Add(SectionType.Storage);
				}
			} else if (_investigationStarted) {
				// Phase B: Info, Status, Workers, Requirements, Effects, Rewards, Cancel
				sectionNames.Add(Strings.Get("common.status"));
				sectionTypes.Add(SectionType.Status);

				if (TryInitializeWorkersSection()) {
					sectionNames.Add(Strings.Get("common.workers"));
					sectionTypes.Add(SectionType.Workers);
				}

				if (_goodsSetCount > 0) {
					sectionNames.Add(Strings.Get("nav.relic.section.requirements"));
					sectionTypes.Add(SectionType.Requirements);
				}

				if (GetEffectsItemCount() > 0) {
					sectionNames.Add(Strings.Get("common.effects"));
					sectionTypes.Add(SectionType.Effects);
				}

				if (_hasRewards) {
					sectionNames.Add(Strings.Get("common.rewards"));
					sectionTypes.Add(SectionType.Rewards);
				}

				if (_canCancel) {
					sectionNames.Add(Strings.Get("nav.relic.section.cancel_investigation"));
					sectionTypes.Add(SectionType.Cancel);
				}
			} else {
				// Phase A: Info, Decisions, Choose Requirements, Effects, Workers, Rewards, Start Investigation
				if (_hasMultipleDecisions) {
					sectionNames.Add(Strings.Get("nav.relic.section.decisions"));
					sectionTypes.Add(SectionType.Decisions);
				}

				if (_goodsSetCount > 0) {
					sectionNames.Add(Strings.Get("nav.relic.section.choose_requirements"));
					sectionTypes.Add(SectionType.Requirements);
				}

				if (GetEffectsItemCount() > 0) {
					sectionNames.Add(Strings.Get("common.effects"));
					sectionTypes.Add(SectionType.Effects);
				}

				// Workers section - required for relics with workplaces
				if (TryInitializeWorkersSection()) {
					sectionNames.Add(Strings.Get("common.workers"));
					sectionTypes.Add(SectionType.Workers);
				}

				if (_hasRewards) {
					sectionNames.Add(Strings.Get("nav.relic.section.preview_rewards"));
					sectionTypes.Add(SectionType.Rewards);
				}

				sectionNames.Add(Strings.Get("common.start_investigation"));
				sectionTypes.Add(SectionType.Status);
			}

			// Add Upgrades section if available (all phases)
			if (TryInitializeUpgradesSection()) {
				sectionNames.Add(Strings.Get("common.upgrades"));
				sectionTypes.Add(SectionType.Upgrades);
			}

			_sectionNames = sectionNames.ToArray();
			_sectionTypes = sectionTypes.ToArray();
		}

		// ========================================
		// DECISION-DEPENDENT DATA REFRESH
		// ========================================

		private void RefreshDecisionDetails() {
			int safeDecision = RelicReflection.GetRelicSafeDecisionIndex(_building);

			// Requirements
			RefreshGoodsSets(safeDecision);

			// Effects
			// Working effects shown in Phase A (preview) and Phase B (active), not in Phase C
			_workingEffects = !_investigationFinished
				? RelicReflection.GetRelicWorkingEffects(_building)
				: null;

			// Active effects only used when NOT using dynamic effects (mutually exclusive)
			_activeEffects = !_hasDynamicEffects
				? RelicReflection.GetRelicActiveEffects(_building)
				: null;

			_dynamicEffects = RelicReflection.GetRelicCurrentDynamicEffects(_building);
			_nextTierEffects = RelicReflection.GetRelicNextDynamicEffects(_building);

			// Rewards
			RefreshRewards(safeDecision);
		}

		private void RefreshGoodsSets(int decisionIndex) {
			_goodsSetCount = RelicReflection.GetRelicGoodsSetCount(_building, decisionIndex);
			if (_goodsSetCount == 0) {
				_goodsSets = null;
				return;
			}

			_goodsSets = new GoodsSetData[_goodsSetCount];
			for (int i = 0; i < _goodsSetCount; i++) {
				int altCount = RelicReflection.GetRelicGoodsAlternativeCount(_building, decisionIndex, i);
				int pickedIndex = RelicReflection.GetRelicPickedGoodIndex(_building, decisionIndex, i);

				var names = new string[altCount];
				var displayNames = new string[altCount];
				var amounts = new int[altCount];

				for (int j = 0; j < altCount; j++) {
					names[j] = RelicReflection.GetRelicGoodName(_building, decisionIndex, i, j);
					displayNames[j] = RelicReflection.GetRelicGoodDisplayName(_building, decisionIndex, i, j) ?? Strings.Get("common.unknown");
					amounts[j] = RelicReflection.GetRelicGoodAmount(_building, decisionIndex, i, j);
				}

				_goodsSets[i] = new GoodsSetData {
					alternativeCount = altCount,
					goodNames = names,
					goodDisplayNames = displayNames,
					goodAmounts = amounts,
					pickedIndex = pickedIndex
				};
			}
		}

		private void RefreshRewards(int decisionIndex) {
			bool hasDynamic = RelicReflection.RelicHasDynamicRewards(_building);
			bool hasDecisionRewards = RelicReflection.RelicHasDecisionRewards(_building);

			if (hasDynamic || hasDecisionRewards) {
				_rewards = RelicReflection.GetRelicDecisionRewards(_building, decisionIndex);
				_hasRewards = _rewards != null && _rewards.Length > 0;
			} else {
				_rewards = null;
				_hasRewards = false;
			}
		}

		private void RefreshStatusData() {
			if (!_investigationStarted && !_investigationFinished) {
				_canStart = RelicReflection.RelicCanStart(_building, out _startBlockingReason);
			}
			_canCancel = RelicReflection.RelicCanCancel(_building);
		}

		/// <summary>
		/// Refresh time-sensitive data (progress, timers, dynamic effects) for live updates
		/// while the game is unpaused. Called before announcing Status and Effects sections.
		/// </summary>
		private void RefreshLiveData() {
			_progress = RelicReflection.GetRelicProgress(_building);
			_timeLeft = RelicReflection.GetRelicTimeLeft(_building);

			if (_hasDynamicEffects) {
				_currentEffectTier = RelicReflection.GetRelicCurrentEffectTier(_building);
				_timeToNextTier = RelicReflection.GetRelicTimeToNextEffectTier(_building);
				_isLastTierReached = RelicReflection.RelicIsLastEffectTierReached(_building);
				_dynamicEffects = RelicReflection.GetRelicCurrentDynamicEffects(_building);
				_nextTierEffects = RelicReflection.GetRelicNextDynamicEffects(_building);
			}
		}

		// ========================================
		// DECISIONS SECTION
		// ========================================

		private void AnnounceDecisionItem(int itemIndex) {
			if (itemIndex < 0 || itemIndex >= _decisionCount) return;

			string label = RelicReflection.GetRelicDecisionLabel(_building, itemIndex) ?? Strings.Get("nav.relic.decision_default", itemIndex + 1);
			float workTime = RelicReflection.GetRelicDecisionWorkingTime(_building, itemIndex);

			string announcement = label;
			if (workTime > 0f)
				announcement += Strings.Get("nav.relic.decision_seconds_suffix", Mathf.RoundToInt(workTime));

			if (itemIndex == _selectedDecisionIndex)
				announcement += Strings.Get("common.suffix_selected");

			// Append requirements summary for this decision
			string reqSummary = GetDecisionRequirementsSummary(itemIndex);
			if (!string.IsNullOrEmpty(reqSummary))
				announcement += Strings.Get("nav.relic.requires_suffix", reqSummary);

			// Append effects (cached, only accurate for selected decision)
			if (itemIndex == _selectedDecisionIndex) {
				string effectsSummary = GetEffectsSummary();
				if (!string.IsNullOrEmpty(effectsSummary))
					announcement += Strings.Get("nav.relic.effects_suffix", effectsSummary);
			}

			// Append rewards for this decision
			string rewardsSummary = GetDecisionRewardsSummary(itemIndex);
			if (!string.IsNullOrEmpty(rewardsSummary))
				announcement += Strings.Get("nav.relic.rewards_suffix", rewardsSummary);

			Speech.Say(announcement);
		}

		private string GetDecisionRequirementsSummary(int decisionIndex) {
			int setCount = RelicReflection.GetRelicGoodsSetCount(_building, decisionIndex);
			if (setCount == 0) return null;

			var parts = new List<string>();
			for (int i = 0; i < setCount; i++) {
				int altCount = RelicReflection.GetRelicGoodsAlternativeCount(_building, decisionIndex, i);
				if (altCount == 0) continue;

				if (altCount == 1) {
					string name = RelicReflection.GetRelicGoodDisplayName(_building, decisionIndex, i, 0) ?? Strings.Get("common.unknown");
					int amount = RelicReflection.GetRelicGoodAmount(_building, decisionIndex, i, 0);
					parts.Add(Strings.Get("nav.relic.req_item", name, amount));
				} else {
					var alts = new List<string>();
					for (int j = 0; j < altCount; j++) {
						string name = RelicReflection.GetRelicGoodDisplayName(_building, decisionIndex, i, j) ?? Strings.Get("common.unknown");
						int amount = RelicReflection.GetRelicGoodAmount(_building, decisionIndex, i, j);
						alts.Add(Strings.Get("nav.relic.req_item", name, amount));
					}
					parts.Add(string.Join(Strings.Get("nav.relic.req_or_separator"), alts));
				}
			}

			return parts.Count > 0 ? string.Join(", ", parts) : null;
		}

		private string GetEffectsSummary() {
			var parts = new List<string>();
			if (_workingEffects != null) {
				foreach (var e in _workingEffects)
					parts.Add((e.IsPositive ? "+" : "-") + e.Name);
			}
			if (_activeEffects != null) {
				foreach (var e in _activeEffects)
					parts.Add((e.IsPositive ? "+" : "-") + e.Name);
			}
			return parts.Count > 0 ? string.Join(", ", parts) : null;
		}

		private string GetDecisionRewardsSummary(int decisionIndex) {
			var rewards = RelicReflection.GetRelicDecisionRewards(_building, decisionIndex);
			if (rewards == null || rewards.Length == 0) return null;

			var parts = new List<string>();
			foreach (var r in rewards)
				parts.Add(r.Name);
			return parts.Count > 0 ? string.Join(", ", parts) : null;
		}

		private bool PerformDecisionAction(int itemIndex) {
			if (itemIndex < 0 || itemIndex >= _decisionCount) return false;

			if (RelicReflection.SetRelicDecisionIndex(_building, itemIndex)) {
				_selectedDecisionIndex = itemIndex;
				string label = RelicReflection.GetRelicDecisionLabel(_building, itemIndex) ?? Strings.Get("nav.relic.decision_default", itemIndex + 1);
				Speech.Say(Strings.Get("nav.relic.selected", label));
				SoundManager.PlayButtonClick();

				// Refresh decision-dependent data and rebuild sections
				RefreshDecisionDetails();
				RefreshStatusData();
				BuildSections();
				return true;
			}

			Speech.Say(Strings.Get("nav.relic.cannot_select_decision"));
			return false;
		}

		// ========================================
		// REQUIREMENTS SECTION
		// ========================================

		private void AnnounceRequirementItem(int itemIndex) {
			if (itemIndex < 0 || itemIndex >= _goodsSetCount || _goodsSets == null) return;

			var set = _goodsSets[itemIndex];
			if (set.alternativeCount == 0) return;
			int pickedIndex = set.pickedIndex;
			if (pickedIndex < 0 || pickedIndex >= set.alternativeCount) pickedIndex = 0;

			string displayName = set.goodDisplayNames[pickedIndex];
			int amount = set.goodAmounts[pickedIndex];

			if (_investigationStarted) {
				// Phase B: show delivery progress
				string goodName = set.goodNames[pickedIndex];
				int delivered = RelicReflection.GetRelicDeliveredAmount(_building, goodName);
				string announcement = Strings.Get("nav.relic.req_delivered", displayName, delivered, amount);
				Speech.Say(announcement);
			} else {
				// Phase A: show requirement with stored amount
				string goodName = set.goodNames[pickedIndex];
				int inStorage = BuildingReflection.GetStoredGoodAmount(goodName);
				string announcement = Strings.Get("nav.relic.req_with_storage", displayName, amount, inStorage);
				if (set.alternativeCount > 1)
					announcement += Strings.Get("nav.relic.other_options_suffix", set.alternativeCount - 1);
				Speech.Say(announcement);
			}
		}

		private void AnnounceRequirementSubItem(int itemIndex, int subItemIndex) {
			if (itemIndex < 0 || itemIndex >= _goodsSetCount || _goodsSets == null) return;

			var set = _goodsSets[itemIndex];
			if (subItemIndex < 0 || subItemIndex >= set.alternativeCount) return;

			string displayName = set.goodDisplayNames[subItemIndex];
			int amount = set.goodAmounts[subItemIndex];
			int inStorage = BuildingReflection.GetStoredGoodAmount(set.goodNames[subItemIndex]);
			string announcement = Strings.Get("nav.relic.req_with_storage", displayName, amount, inStorage);

			if (subItemIndex == set.pickedIndex)
				announcement += Strings.Get("common.suffix_selected");

			Speech.Say(announcement);
		}

		private bool PerformRequirementSubItemAction(int itemIndex, int subItemIndex) {
			if (itemIndex < 0 || itemIndex >= _goodsSetCount || _goodsSets == null) return false;
			if (_investigationStarted) return false;  // Can't change during investigation

			var set = _goodsSets[itemIndex];
			if (subItemIndex < 0 || subItemIndex >= set.alternativeCount) return false;

			int safeDecision = RelicReflection.GetRelicSafeDecisionIndex(_building);
			if (RelicReflection.SetRelicPickedGoodIndex(_building, safeDecision, itemIndex, subItemIndex)) {
				_goodsSets[itemIndex].pickedIndex = subItemIndex;
				string displayName = set.goodDisplayNames[subItemIndex];
				Speech.Say(Strings.Get("nav.relic.picked", displayName));
				SoundManager.PlayButtonClick();

				// Refresh status (availability may have changed)
				RefreshStatusData();
				_navigationLevel = 1;
				return true;
			}

			Speech.Say(Strings.Get("common.cannot_pick_good"));
			return false;
		}

		// ========================================
		// EFFECTS SECTION
		// ========================================

		private int GetEffectsItemCount() {
			int count = 0;
			if (_workingEffects != null) count += _workingEffects.Length;
			if (_activeEffects != null) count += _activeEffects.Length;
			if (_dynamicEffects != null) count += _dynamicEffects.Length;
			if (_nextTierEffects != null) count += _nextTierEffects.Length;
			return count;
		}

		private void AnnounceEffectItem(int itemIndex) {
			RefreshLiveData();
			var effect = GetEffectAtIndex(itemIndex);
			if (effect == null) return;

			string announcement = effect.Value.Name;
			announcement += effect.Value.IsPositive ? Strings.Get("nav.relic.effect.positive_suffix") : Strings.Get("nav.relic.effect.negative_suffix");

			// Indicate effect type
			if (IsWorkingEffect(itemIndex)) {
				announcement += Strings.Get("nav.relic.effect.during_investigation_suffix");
			} else if (IsNextTierEffect(itemIndex)) {
				if (_timeToNextTier > 0f)
					announcement += Strings.Get("nav.relic.effect.in_time_suffix", FormatDynamicEffectTime(_timeToNextTier));
				else
					announcement += Strings.Get("nav.relic.effect.pending_suffix");
			}

			// Include description
			if (!string.IsNullOrEmpty(effect.Value.Description))
				announcement += ". " + effect.Value.Description;

			Speech.Say(announcement);
		}

		private bool PerformEffectAction(int itemIndex) {
			var effect = GetEffectAtIndex(itemIndex);
			if (effect == null) return false;

			if (!string.IsNullOrEmpty(effect.Value.Description)) {
				Speech.Say(effect.Value.Description);
				return true;
			}
			return false;
		}

		private RelicReflection.RelicEffectInfo? GetEffectAtIndex(int itemIndex) {
			int workingCount = _workingEffects?.Length ?? 0;
			int activeCount = _activeEffects?.Length ?? 0;
			int dynamicCount = _dynamicEffects?.Length ?? 0;
			int nextTierCount = _nextTierEffects?.Length ?? 0;

			if (itemIndex < 0) return null;

			if (itemIndex < workingCount)
				return _workingEffects[itemIndex];

			int activeIndex = itemIndex - workingCount;
			if (activeIndex < activeCount)
				return _activeEffects[activeIndex];

			int dynamicIndex = itemIndex - workingCount - activeCount;
			if (dynamicIndex < dynamicCount)
				return _dynamicEffects[dynamicIndex];

			int nextTierIndex = itemIndex - workingCount - activeCount - dynamicCount;
			if (nextTierIndex < nextTierCount)
				return _nextTierEffects[nextTierIndex];

			return null;
		}

		private bool IsWorkingEffect(int itemIndex) {
			int workingCount = _workingEffects?.Length ?? 0;
			return itemIndex >= 0 && itemIndex < workingCount;
		}

		private bool IsNextTierEffect(int itemIndex) {
			int workingCount = _workingEffects?.Length ?? 0;
			int activeCount = _activeEffects?.Length ?? 0;
			int dynamicCount = _dynamicEffects?.Length ?? 0;
			return itemIndex >= workingCount + activeCount + dynamicCount;
		}

		// ========================================
		// REWARDS SECTION
		// ========================================

		private void AnnounceRewardItem(int itemIndex) {
			if (_rewards == null || itemIndex < 0 || itemIndex >= _rewards.Length) return;

			string announcement = _rewards[itemIndex].Name;
			if (!string.IsNullOrEmpty(_rewards[itemIndex].Description))
				announcement += Strings.Get("nav.relic.reward_description_suffix", _rewards[itemIndex].Description);
			Speech.Say(announcement);
		}

		private bool PerformRewardAction(int itemIndex) {
			return false;  // Description shown inline in announcement
		}

		// ========================================
		// STATUS SECTION
		// ========================================

		protected override bool PerformSectionAction(int sectionIndex) {
			if (sectionIndex < 0 || sectionIndex >= _sectionTypes.Length) return false;

			var sectionType = _sectionTypes[sectionIndex];

			// Handle Cancel section (Phase B)
			if (sectionType == SectionType.Cancel && _investigationStarted && _canCancel) {
				return PerformCancelAction();
			}

			// Handle Start Investigation (Phase A Status section)
			if (sectionType != SectionType.Status) return false;
			if (_investigationStarted || _investigationFinished) return false;

			// Phase A: Start investigation from section level
			if (!_canStart) {
				Speech.Say(_startBlockingReason ?? Strings.Get("nav.relic.cannot_start"));
				SoundManager.PlayFailed();
				return true;
			}

			if (RelicReflection.RelicStartInvestigation(_building)) {
				Speech.Say(Strings.Get("nav.relic.investigation_started"));
				SoundManager.PlayButtonClick();
				var startSound = RelicReflection.GetRelicInvestigationStartSoundModel(_building);
				SoundManager.PlaySoundEffect(startSound);
				if (RelicReflection.RelicHasWorkingEffects(_building))
					SoundManager.PlayRelicStartWithWorkingEffects();

				_investigationStarted = true;
				_workersSection.RefreshWorkerIds();
				RefreshDecisionDetails();
				RefreshStatusData();
				BuildSections();
				_currentSectionIndex = 0;
				_navigationLevel = 0;
				return true;
			} else {
				Speech.Say(Strings.Get("nav.relic.failed_to_start"));
				SoundManager.PlayFailed();
				return true;
			}
		}

		private bool PerformCancelAction() {
			if (RelicReflection.RelicCancelInvestigation(_building)) {
				Speech.Say(Strings.Get("nav.relic.investigation_cancelled"));
				SoundManager.PlayButtonClick();
				if (RelicReflection.RelicHasWorkingEffects(_building))
					SoundManager.PlayRelicStopWithWorkingEffects();

				// Refresh to transition back to Phase A
				_investigationStarted = false;
				_progress = 0f;
				_workersSection.RefreshWorkerIds();
				RefreshDecisionDetails();
				RefreshStatusData();
				BuildSections();
				_currentSectionIndex = 0;
				_navigationLevel = 0;
				return true;
			} else {
				Speech.Say(Strings.Get("nav.relic.failed_to_cancel"));
				SoundManager.PlayFailed();
				return false;
			}
		}

		private int GetStatusItemCount() {
			// Status is now section-level only (no items to drill into)
			return 0;
		}

		// ========================================
		// STORAGE SECTION (Phase C)
		// ========================================

		private void RefreshStorageData() {
			_storageItems.Clear();
			_storageTotalSum = 0;

			if (!_investigationFinished) return;

			_storageItems = RelicReflection.GetRelicRewardStorageItems(_building);
			_storageTotalSum = RelicReflection.GetRelicRewardStorageFullSum(_building);
		}

		private void AnnounceStorageItem(int itemIndex) {
			// Refresh storage data to get current amounts
			RefreshStorageData();

			if (itemIndex < 0 || itemIndex >= _storageItems.Count) return;

			var (_, displayName, amount) = _storageItems[itemIndex];
			Speech.Say(Strings.Get("nav.common.storage_item", displayName, amount));
		}

	}
}
