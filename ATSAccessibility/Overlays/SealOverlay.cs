using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the Seal building panel in Sealed Forest biome.
	/// Two-level navigation: sections -> offerings detail.
	/// Level 0 = 5 sections (Effects, Progress, Dialogue, Offerings, Reward).
	/// Level 1 = offerings list (Enter/Space delivers).
	/// </summary>
	public class SealOverlay: MenuBase {
		// ========================================
		// TYPES
		// ========================================

		private enum Section { Effects, Progress, Dialogue, Offerings, Reward }
		private static readonly Section[] _allSections = (Section[])Enum.GetValues(typeof(Section));

		// ========================================
		// STATE
		// ========================================

		private object _seal;
		private bool _sealUnavailable;

		// Cached data
		private object _currentStage;       // SealKitState
		private object _currentStageModel;  // SealKitModel
		private Array _offerings;           // SealPartModel[]
		private Array _offeringOrders;      // OrderState[]

		// Cached effect property info
		private static PropertyInfo _effectDisplayNameProperty = null;
		private static PropertyInfo _effectDescriptionProperty = null;
		private static bool _effectPropsCached = false;

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("common.seal");
		protected override string EmptyMessage => "";

		protected override int GetItemCount() {
			if (_sealUnavailable) return 0;

			if (Level == 0)
				return _allSections.Length;
			else
				return _offerings?.Length ?? 0;
		}

		protected override string GetLabel(int index) {
			if (Level == 0) {
				if (index < 0 || index >= _allSections.Length) return null;
				return _allSections[index].ToString();
			} else {
				if (_offerings == null || index < 0 || index >= _offerings.Length) return null;
				return SealReflection.GetOfferingDisplayName(_offerings.GetValue(index));
			}
		}

		protected override void RefreshData() {
			_seal = SealReflection.GetFirstSeal();
			if (_seal == null || SealReflection.IsSealCompleted(_seal)) {
				_sealUnavailable = true;
				return;
			}

			_sealUnavailable = false;
			_currentStage = SealReflection.GetFirstUncompletedStage(_seal);
			_currentStageModel = SealReflection.GetStageModel(_seal, _currentStage);
			_offerings = SealReflection.GetStageOfferings(_currentStageModel);
			_offeringOrders = SealReflection.GetStageOrders(_currentStage);
		}

		protected override EnterAction OnEnter(int index) {
			if (Level == 0) {
				if (index >= 0 && index < _allSections.Length && _allSections[index] == Section.Offerings)
					return EnterAction.DrillDown;
				return EnterAction.Action;
			}
			// Level 1: deliver on Enter
			return EnterAction.Action;
		}

		protected override void OnAction(int index) {
			if (Level == 0) {
				// Re-announce section detail
				AnnounceCurrentItem();
			} else {
				TryDeliver();
			}
		}

		protected override void OnSpace(int index) {
			if (Level == 1)
				TryDeliver();
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			return null;
		}

		protected override void OnDrillDown(int index) {
			// Offerings data already loaded in RefreshData
		}

		/// <summary>
		/// Custom announcement: sections have unique announce methods, offerings use AnnounceOffering.
		/// </summary>
		protected override void AnnounceCurrentItem() {
			if (Level == 0)
				AnnounceSection();
			else
				AnnounceOffering(CurrentIndex);
		}

		protected override string GetOpenAnnouncement() {
			if (_sealUnavailable) {
				if (_seal != null && SealReflection.IsSealCompleted(_seal))
					return Strings.Get("overlay.seal.completed");
				return Strings.Get("overlay.seal.not_found");
			}
			// OnOpened will handle the section announcement
			return null;
		}

		protected override void OnOpened() {
			if (!_sealUnavailable)
				AnnounceSection();
		}

		protected override void OnClosed() {
			_seal = null;
			_currentStage = null;
			_currentStageModel = null;
			_offerings = null;
			_offeringOrders = null;
		}

		// ========================================
		// SEARCH (offerings only)
		// ========================================

		protected override int SearchItemCount => Level == 1 ? (_offerings?.Length ?? 0) : 0;

		protected override string GetSearchName(int index) {
			if (Level == 1) {
				if (_offerings == null || index < 0 || index >= _offerings.Length) return null;
				return SealReflection.GetOfferingDisplayName(_offerings.GetValue(index));
			}
			return null;
		}

		// ========================================
		// SECTION ANNOUNCEMENTS
		// ========================================

		private void AnnounceSection() {
			int index = CurrentIndex;
			if (index < 0 || index >= _allSections.Length) return;

			switch (_allSections[index]) {
				case Section.Effects:
					AnnounceEffects();
					break;
				case Section.Progress:
					AnnounceProgress();
					break;
				case Section.Dialogue:
					AnnounceDialogue();
					break;
				case Section.Offerings:
					Speech.Say(Strings.Get("overlay.seal.section.offerings", _offerings?.Length ?? 0));
					break;
				case Section.Reward:
					AnnounceReward();
					break;
			}
		}

		private void AnnounceEffects() {
			var state = SealReflection.GetSealGameState();
			if (state == null) {
				Speech.Say(Strings.Get("overlay.seal.plague.unreadable"));
				return;
			}

			if (SealReflection.IsEffectActive(state)) {
				string effectName = SealReflection.GetCurrentEffect(state);
				var effectModel = GameReflection.GetEffectModel(effectName);
				string displayName = GetEffectDisplayName(effectModel) ?? effectName;
				string description = GetEffectDescription(effectModel);

				if (!string.IsNullOrEmpty(description))
					Speech.Say(Strings.Get("overlay.seal.plague.current.with_desc", displayName, description));
				else
					Speech.Say(Strings.Get("overlay.seal.plague.current", displayName));
			} else {
				string effectName = SealReflection.GetNextEffect(state);
				var effectModel = GameReflection.GetEffectModel(effectName);
				string displayName = GetEffectDisplayName(effectModel) ?? effectName;
				string description = GetEffectDescription(effectModel);

				float seconds = SealReflection.GetSecondsUntilStorm();
				string timeText = FormattingUtils.FormatTime(seconds);

				if (!string.IsNullOrEmpty(description))
					Speech.Say(Strings.Get("overlay.seal.plague.next.with_desc", displayName, description, timeText));
				else
					Speech.Say(Strings.Get("overlay.seal.plague.next", displayName, timeText));
			}
		}

		private void AnnounceProgress() {
			var (current, total, completedNames) = SealReflection.GetProgress(_seal);

			// Handle completion case
			if (current > total) {
				string completedStr = string.Join(", ", completedNames);
				Speech.Say(Strings.Get("overlay.seal.progress.all", total, completedStr));
				return;
			}

			string completedText = completedNames.Count > 0
				? Strings.Get("overlay.seal.progress.completed", string.Join(", ", completedNames))
				: Strings.Get("overlay.seal.progress.none");
			Speech.Say(Strings.Get("overlay.seal.progress.stage", current, total, completedText));
		}

		private void AnnounceDialogue() {
			string dialogue = SealReflection.GetStageDialogue(_currentStageModel);
			if (!string.IsNullOrEmpty(dialogue))
				Speech.Say(dialogue);
			else
				Speech.Say(Strings.Get("overlay.seal.dialogue.none"));
		}

		private void AnnounceOffering(int index) {
			if (_offerings == null || index < 0 || index >= _offerings.Length) {
				Speech.Say(Strings.Get("overlay.seal.offering.unavailable"));
				return;
			}

			var offering = _offerings.GetValue(index);
			var order = (_offeringOrders != null && index < _offeringOrders.Length)
				? _offeringOrders.GetValue(index)
				: null;

			string name = SealReflection.GetOfferingDisplayName(offering) ?? Strings.Get("overlay.seal.offering.unknown");
			string description = SealReflection.GetOfferingDescription(offering);
			string objectives = GetObjectivesText(offering, order);
			bool canDeliver = CanDeliverOffering(order, offering);
			string status = Strings.Get(canDeliver ? "overlay.seal.offering.deliverable" : "overlay.seal.offering.in_progress");

			var parts = new System.Collections.Generic.List<string> { name };
			if (!string.IsNullOrEmpty(objectives))
				parts.Add(objectives);
			if (!string.IsNullOrEmpty(description))
				parts.Add(description.TrimEnd('.'));
			parts.Add(status);
			Speech.Say(string.Join(". ", parts));
		}

		private void AnnounceReward() {
			var reward = SealReflection.GetStageReward(_currentStageModel);
			if (reward == null) {
				Speech.Say(Strings.Get("overlay.seal.reward.none"));
				return;
			}

			string name = GetEffectDisplayName(reward);
			string description = GetEffectDescription(reward);

			if (!string.IsNullOrEmpty(description))
				Speech.Say(Strings.Get("overlay.seal.reward.with_desc", name, description));
			else if (!string.IsNullOrEmpty(name))
				Speech.Say(Strings.Get("overlay.seal.reward.simple", name));
			else
				Speech.Say(Strings.Get("overlay.seal.reward.available"));
		}

		// ========================================
		// ACTIONS
		// ========================================

		private void TryDeliver() {
			if (_offerings == null || CurrentIndex < 0 || CurrentIndex >= _offerings.Length) {
				Speech.Say(Strings.Get("common.cannot_deliver"));
				SoundManager.PlayFailed();
				return;
			}

			var offering = _offerings.GetValue(CurrentIndex);
			var order = (_offeringOrders != null && CurrentIndex < _offeringOrders.Length)
				? _offeringOrders.GetValue(CurrentIndex)
				: null;

			if (!CanDeliverOffering(order, offering)) {
				Speech.Say(Strings.Get("overlay.seal.deliver.not_ready"));
				SoundManager.PlayFailed();
				return;
			}

			// Complete the offering
			if (SealReflection.CompleteOffering(_currentStage, _currentStageModel, CurrentIndex)) {
				string name = SealReflection.GetOfferingDisplayName(offering) ?? Strings.Get("overlay.seal.deliver.fallback");
				SoundManager.PlaySealOrderDeliver();

				// Refresh data for next stage
				_currentStage = SealReflection.GetFirstUncompletedStage(_seal);
				_currentStageModel = SealReflection.GetStageModel(_seal, _currentStage);
				_offerings = SealReflection.GetStageOfferings(_currentStageModel);
				_offeringOrders = SealReflection.GetStageOrders(_currentStage);

				// Check if seal is now complete and close overlay
				if (SealReflection.IsSealCompleted(_seal)) {
					Speech.Say(Strings.Get("overlay.seal.deliver.done_completed", name));
					Close();
					return;
				}

				Speech.Say(Strings.Get("overlay.seal.deliver.done", name));

				// Reset to section view
				SetLevel(0);
				CurrentIndex = 0;
			} else {
				Speech.Say(Strings.Get("overlay.seal.deliver.failed"));
				SoundManager.PlayFailed();
			}
		}

		// ========================================
		// HELPERS
		// ========================================

		private bool CanDeliverOffering(object orderState, object offering) {
			if (orderState == null || offering == null) return false;

			// Get the OrderModel from the offering
			var orderModel = SealReflection.GetOfferingOrder(offering);
			if (orderModel == null) return false;

			return OrdersReflection.CanComplete(orderState, orderModel);
		}

		private string GetObjectivesText(object offering, object orderState) {
			if (offering == null || orderState == null) return null;

			var orderModel = SealReflection.GetOfferingOrder(offering);
			if (orderModel == null) return null;

			var objectives = OrdersReflection.GetObjectiveTexts(orderModel, orderState);
			if (objectives == null || objectives.Count == 0) return null;

			return string.Join(", ", objectives);
		}

		private static void EnsureEffectPropertyCached() {
			if (_effectPropsCached) return;
			_effectPropsCached = true;

			try {
				var effectModelType = GameReflection.GameAssembly?.GetType("Eremite.Model.EffectModel");
				if (effectModelType != null) {
					_effectDisplayNameProperty = effectModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
					_effectDescriptionProperty = effectModelType.GetProperty("Description", GameReflection.PublicInstance);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] SealOverlay: Failed to cache effect properties: {ex.Message}");
			}
		}

		private static string GetEffectDisplayName(object effectModel) {
			if (effectModel == null) return null;
			EnsureEffectPropertyCached();
			if (_effectDisplayNameProperty == null) return null;

			try { return _effectDisplayNameProperty.GetValue(effectModel)?.ToString(); } catch { return null; }
		}

		private static string GetEffectDescription(object effectModel) {
			if (effectModel == null) return null;
			EnsureEffectPropertyCached();
			if (_effectDescriptionProperty == null) return null;

			try {
				string desc = _effectDescriptionProperty.GetValue(effectModel)?.ToString();
				// Strip rich text tags
				if (!string.IsNullOrEmpty(desc))
					desc = OrdersReflection.StripRichText(desc).Trim();
				return desc;
			} catch { return null; }
		}
	}
}
