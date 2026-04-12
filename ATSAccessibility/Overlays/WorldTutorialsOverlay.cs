using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the WorldTutorialsHUD.
	/// Provides keyboard navigation for the 4 tutorial missions on the world map.
	/// Opened via F1 key from WorldMapKeyHandler.
	/// </summary>
	public class WorldTutorialsOverlay: MenuBase {
		// Data
		private List<TutorialReflection.TutorialInfo> _tutorials = new List<TutorialReflection.TutorialInfo>();

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("overlay.world_tutorials.title");
		protected override string EmptyMessage => Strings.Get("overlay.world_tutorials.empty");

		protected override int GetItemCount() => _tutorials.Count;

		protected override string GetLabel(int index) {
			if (index < 0 || index >= _tutorials.Count) return null;

			var tutorial = _tutorials[index];
			var status = "";

			if (!tutorial.IsUnlocked) {
				status = Strings.Get("common.suffix_locked");
			} else if (tutorial.IsCompleted) {
				status = Strings.Get("overlay.world_tutorials.suffix.completed");
			}

			return Strings.Get("overlay.world_tutorials.row", tutorial.DisplayName, status);
		}

		protected override void RefreshData() {
			_tutorials = TutorialReflection.GetAllTutorials();
		}

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			if (index < 0 || index >= _tutorials.Count) return;

			var tutorial = _tutorials[index];

			if (!tutorial.IsUnlocked) {
				var reason = tutorial.LockedReason;
				if (!string.IsNullOrEmpty(reason)) {
					Speech.Say(Strings.Get("overlay.world_tutorials.locked.with_reason", reason));
				} else {
					Speech.Say(Strings.Get("common.locked"));
				}
				SoundManager.PlayFailed();
				return;
			}

			if (TutorialReflection.StartTutorial(tutorial.Config)) {
				SoundManager.PlayButtonClick();
				// Game will show confirmation popup, then start tutorial if confirmed
			} else {
				Speech.Say(Strings.Get("overlay.world_tutorials.start_failed"));
				SoundManager.PlayFailed();
			}
		}

		protected override int SearchItemCount => 0; // No search

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (keyCode == KeyCode.F1) {
				// Close the HUD and overlay (F1 toggles)
				TutorialReflection.ToggleWorldTutorialsHUD();
				Close();
				return true;
			}
			return null;
		}

		protected override EscapeAction OnEscape() {
			// Close the HUD and overlay
			TutorialReflection.ToggleWorldTutorialsHUD();
			return EscapeAction.Close;
		}

		protected override void OnClosed() {
			_tutorials.Clear();
		}
	}
}
