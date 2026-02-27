using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Navigators {
	/// <summary>
	/// Base class for building navigators with section-based navigation.
	/// Extends MenuBase to provide 4-level navigation mapped to building concepts:
	/// - Level 0: Sections (Info, Workers, Recipes, Storage, etc.)
	/// - Level 1: Items within section (individual recipes, workers, goods)
	/// - Level 2: Sub-items (recipe settings, worker details) - optional
	/// - Level 3: Sub-sub-items (ingredient options) - optional
	///
	/// All existing subclasses (ProductionNavigator, HouseNavigator, etc.) work
	/// without changes through compatibility properties that map to MenuBase's
	/// level-based index array.
	/// </summary>
	public abstract class BuildingSectionNavigator: MenuBase, IBuildingNavigator {
		// ========================================
		// BUILDING STATE
		// ========================================

		protected object _building;
		protected bool _isSleeping;
		protected bool _canSleep;

		// Shared section handlers
		protected readonly BuildingUpgradesSection _upgradesSection = new BuildingUpgradesSection();
		protected readonly BuildingWorkerSection _workersSection = new BuildingWorkerSection();

		// ========================================
		// COMPATIBILITY PROPERTIES
		// ========================================
		// Subclasses read/write these directly. They map to MenuBase's _indices array.

		protected int _navigationLevel {
			get => Level;
			set => SetLevel(value);
		}

		protected int _currentSectionIndex {
			get => _indices[0];
			set => _indices[0] = value;
		}

		protected int _currentItemIndex {
			get => _indices[1];
			set => _indices[1] = value;
		}

		protected int _currentSubItemIndex {
			get => _indices[2];
			set => _indices[2] = value;
		}

		protected int _currentSubSubItemIndex {
			get => _indices[3];
			set => _indices[3] = value;
		}

		// ========================================
		// IBUILDINGNAVIGATOR IMPLEMENTATION
		// ========================================

		void IBuildingNavigator.Open(object building) {
			_building = building;
			base.Open();
		}

		void IBuildingNavigator.Close() {
			base.Close();
		}

		bool IBuildingNavigator.ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (_building == null) return false;
			return base.ProcessKey(keyCode, modifiers);
		}

		// ========================================
		// MENUBASE ABSTRACT IMPLEMENTATIONS
		// ========================================

		protected override string OverlayName => NavigatorName;

		protected override string EmptyMessage => "No sections";

		protected sealed override int GetItemCount() {
			switch (Level) {
				case 0:
					var sections = GetSections();
					return sections != null ? sections.Length : 0;
				case 1:
					return GetItemCount(_indices[0]);
				case 2:
					return GetSubItemCount(_indices[0], _indices[1]);
				case 3:
					return GetSubSubItemCount(_indices[0], _indices[1], _indices[2]);
				default:
					return 0;
			}
		}

		protected sealed override string GetLabel(int index) {
			switch (Level) {
				case 0: return GetSectionName(index);
				case 1: return GetItemName(_indices[0], index);
				case 2: return GetSubItemName(_indices[0], _indices[1], index);
				case 3: return GetSubSubItemName(_indices[0], _indices[1], _indices[2], index);
				default: return null;
			}
		}

		protected sealed override EnterAction OnEnter(int index) {
			switch (Level) {
				case 0:
					return GetItemCount(_indices[0]) > 0 ? EnterAction.DrillDown : EnterAction.Action;
				case 1:
					return GetSubItemCount(_indices[0], _indices[1]) > 0 ? EnterAction.DrillDown : EnterAction.Action;
				case 2:
					return GetSubSubItemCount(_indices[0], _indices[1], _indices[2]) > 0 ? EnterAction.DrillDown : EnterAction.Action;
				case 3:
					return EnterAction.Action;
				default:
					return EnterAction.None;
			}
		}

		// ========================================
		// MENUBASE VIRTUAL OVERRIDES
		// ========================================

		protected override void AnnounceCurrentItem() {
			switch (Level) {
				case 0:
					AnnounceSection(_indices[0]);
					break;
				case 1:
					AnnounceItem(_indices[0], _indices[1]);
					break;
				case 2:
					AnnounceSubItem(_indices[0], _indices[1], _indices[2]);
					break;
				case 3:
					AnnounceSubSubItem(_indices[0], _indices[1], _indices[2], _indices[3]);
					break;
			}
		}

		protected override void OnAction(int index) {
			switch (Level) {
				case 0:
					if (!PerformSectionAction(_indices[0]))
						Speech.Say("No items in this section");
					break;
				case 1:
					if (!PerformItemAction(_indices[0], index)) {
						string msg = GetNoSubItemsMessage(_indices[0], index);
						if (msg != null) {
							Speech.Say(msg);
							SoundManager.PlayFailed();
						}
					}
					break;
				case 2:
					PerformSubItemAction(_indices[0], _indices[1], index);
					break;
				case 3:
					PerformSubSubItemAction(_indices[0], _indices[1], _indices[2], index);
					break;
			}
		}

		protected override void OnSpace(int index) {
			switch (Level) {
				case 0:
					ToggleBuildingSleep();
					break;
				case 1:
					PerformItemAction(_indices[0], index);
					break;
				case 2:
					PerformSubItemAction(_indices[0], _indices[1], index);
					break;
				case 3:
					PerformSubSubItemAction(_indices[0], _indices[1], _indices[2], index);
					break;
			}
		}

		protected override void OnAdjust(int index, int dir, KeyboardManager.KeyModifiers modifiers) {
			if (Level >= 1)
				AdjustItemValue(_indices[0], _indices[1], dir, modifiers);
			else
				AdjustSectionValue(_indices[0], dir, modifiers);
		}

		protected override bool? HandleSpecialKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			// Alt+Space for pause toggle
			if (modifiers.Alt && keyCode == KeyCode.Space) {
				GameReflection.TogglePause();
				Speech.Say(GameReflection.IsPaused() ? "Paused" : "Unpaused");
				return true;
			}
			return null;
		}

		// ========================================
		// IHELPPROVIDER OVERRIDE
		// ========================================

		private static readonly List<HelpEntry> _buildingHelpEntries = new List<HelpEntry>(MenuBaseHelpEntries) {
			new HelpEntry("Alt+Space", "Pause/unpause"),
		};

		public override IReadOnlyList<HelpEntry> GetHelpEntries() => _buildingHelpEntries;

		protected override string GetOpenAnnouncement() {
			string buildingName = BuildingReflection.GetBuildingName(_building) ?? "Building";
			string status = GetBuildingStatus();

			string announcement = buildingName;
			if (!string.IsNullOrEmpty(status))
				announcement += ", " + status;

			return announcement;
		}

		protected override void OnOpened() {
			Debug.Log($"[ATSAccessibility] {NavigatorName}: Opened panel for {(BuildingReflection.GetBuildingName(_building) ?? "Building")}");

			// Queue first section after the building name (interrupt: false)
			var sections = GetSections();
			if (sections != null && sections.Length > 0) {
				string sectionName = GetSectionName(0);
				if (!string.IsNullOrEmpty(sectionName))
					Speech.Say(sectionName, false);
			}
		}

		protected override void OnClosed() {
			ClearData();
			_building = null;
		}

		protected override EscapeAction OnEscape() {
			if (Level > 0)
				return EscapeAction.GoBack;

			// Pass to game to close building panel
			Debug.Log($"[ATSAccessibility] {NavigatorName}: Letting game close panel via Escape");
			return EscapeAction.PassThrough;
		}

		// ========================================
		// BSN-SPECIFIC ABSTRACTS (subclasses implement)
		// ========================================

		protected abstract string NavigatorName { get; }
		protected abstract string[] GetSections();
		protected abstract int GetItemCount(int sectionIndex);
		protected abstract void AnnounceSection(int sectionIndex);
		protected abstract void AnnounceItem(int sectionIndex, int itemIndex);
		// RefreshData() inherited from MenuBase as abstract — subclasses implement directly
		protected abstract void ClearData();

		// ========================================
		// BSN-SPECIFIC VIRTUALS (subclasses may override)
		// ========================================

		protected virtual int GetSubItemCount(int sectionIndex, int itemIndex) => 0;
		protected virtual int GetSubSubItemCount(int sectionIndex, int itemIndex, int subItemIndex) => 0;

		protected virtual void AnnounceSubItem(int sectionIndex, int itemIndex, int subItemIndex) { }
		protected virtual void AnnounceSubSubItem(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex) { }

		protected virtual bool ToggleBuildingSleep() {
			if (!_canSleep) {
				Speech.Say("Cannot pause this building");
				return false;
			}

			bool wasSleeping = _isSleeping;
			if (BuildingReflection.ToggleBuildingSleep(_building)) {
				_isSleeping = !wasSleeping;
				if (!wasSleeping) {
					_workersSection.RefreshWorkerIds();
				}
				Speech.Say(_isSleeping ? "Paused" : "Active");
				return true;
			} else {
				SoundManager.PlayFailed();
				Speech.Say("Cannot change building state");
				return false;
			}
		}
		protected virtual bool PerformSectionAction(int sectionIndex) => false;
		protected virtual bool PerformItemAction(int sectionIndex, int itemIndex) => false;
		protected virtual bool PerformSubItemAction(int sectionIndex, int itemIndex, int subItemIndex) => false;

		/// <summary>
		/// Handle worker sub-item action (assign/unassign) and reset navigation level on success.
		/// </summary>
		protected bool PerformWorkerSubItemAction(int itemIndex, int subItemIndex) {
			if (_workersSection.PerformSubItemAction(itemIndex, subItemIndex)) {
				_navigationLevel = 1;
				return true;
			}
			return false;
		}
		protected virtual bool PerformSubSubItemAction(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex) => false;

		protected virtual string GetNoSubItemsMessage(int sectionIndex, int itemIndex) => null;

		protected virtual void AdjustItemValue(int sectionIndex, int itemIndex, int delta, KeyboardManager.KeyModifiers modifiers) { }
		protected virtual void AdjustSectionValue(int sectionIndex, int delta, KeyboardManager.KeyModifiers modifiers) { }

		protected virtual string GetSectionName(int sectionIndex) {
			var sections = GetSections();
			if (sections != null && sectionIndex >= 0 && sectionIndex < sections.Length)
				return sections[sectionIndex];
			return null;
		}

		protected virtual string GetItemName(int sectionIndex, int itemIndex) => null;
		protected virtual string GetSubItemName(int sectionIndex, int itemIndex, int subItemIndex) => null;
		protected virtual string GetSubSubItemName(int sectionIndex, int itemIndex, int subItemIndex, int subSubItemIndex) => null;

		// ========================================
		// BUILDING HELPERS
		// ========================================

		private string GetBuildingStatus() {
			if (!BuildingReflection.IsBuildingFinished(_building))
				return "under construction";
			if (BuildingReflection.IsBuildingSleeping(_building))
				return "paused";
			return null;
		}

		// ========================================
		// UPGRADES SECTION HELPERS
		// ========================================

		protected bool TryInitializeUpgradesSection() {
			if (_building == null) return false;
			if (!BuildingReflection.HasUpgradesAvailable(_building)) return false;

			_upgradesSection.Initialize(_building);
			return _upgradesSection.HasUpgrades();
		}

		protected void ClearUpgradesSection() {
			_upgradesSection.Clear();
		}

		// ========================================
		// WORKERS SECTION HELPERS
		// ========================================

		protected bool TryInitializeWorkersSection() {
			if (_building == null) return false;
			if (!BuildingReflection.ShouldAllowWorkerManagement(_building)) return false;

			_workersSection.Initialize(_building);
			return _workersSection.HasWorkers();
		}

		protected void ClearWorkersSection() {
			_workersSection.Clear();
		}
	}
}
