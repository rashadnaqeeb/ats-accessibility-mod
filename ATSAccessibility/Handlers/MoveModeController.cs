using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Handlers {
	/// <summary>
	/// Controls building move mode: relocating existing buildings.
	/// Works with MapNavigator for cursor position.
	/// </summary>
	public class MoveModeController: IKeyHandler, IHelpProvider {
		private bool _isActive = false;
		private object _movingBuilding = null;
		private string _buildingName = null;
		private Vector2Int _originalPosition;
		private int _originalRotation;
		private int _currentRotation;
		private bool _pricePaid = false;
		private bool _awaitingPlaceConfirm = false;

		// Reference to map navigator for cursor position
		private readonly MapNavigator _mapNavigator;

		// ========================================
		// IHELPPROVIDER
		// ========================================

		private static readonly List<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("R", "Rotate clockwise"),
			new HelpEntry("Shift+R", "Rotate counter-clockwise"),
			new HelpEntry("Space", "Place building"),
			new HelpEntry("Enter", "Place building"),
			new HelpEntry("Escape", "Cancel move"),
			new HelpEntry("D", "Range preview"),
			new HelpEntry("E", "Entrance preview"),
		};

		public HelpBehavior HelpBehavior => HelpBehavior.Filter;
		public string HelpContextName => "Move Mode";
		public IReadOnlyList<HelpEntry> GetHelpEntries() => _helpEntries;
		public IReadOnlyList<string> GetPassthroughKeys() => null;

		/// <summary>
		/// Whether move mode is currently active.
		/// </summary>
		public bool IsActive => _isActive;

		public MoveModeController(MapNavigator mapNavigator) {
			_mapNavigator = mapNavigator;
			MenuBase.OnAnyMenuOpened += () => { if (_isActive) ExitMoveMode(true); };
		}

		/// <summary>
		/// Enter move mode for a building at the specified position.
		/// </summary>
		public void EnterMoveMode(object building) {
			if (building == null) {
				Speech.Say("No building here");
				return;
			}

			// Check if building can be moved
			if (!ConstructionReflection.CanMovePlacedBuilding(building)) {
				Speech.Say("Unmovable");
				Debug.Log($"[ATSAccessibility] Building cannot be moved");
				return;
			}

			// Check if player can afford the move
			if (!ConstructionReflection.CanAffordMove(building)) {
				var costInfo = ConstructionReflection.GetMovingCostInfo(building);
				string costDesc = costInfo.HasValue ? $"{costInfo.Value.amount} {costInfo.Value.displayName}" : "resources";
				Speech.Say($"Cannot afford to move, requires {costDesc}");
				SoundManager.PlayFailed();
				return;
			}

			// Pay moving cost (resources taken pre-move, refunded on cancel)
			_pricePaid = ConstructionReflection.HasMovingCost(building) && ConstructionReflection.PayForMoving(building);

			// Store original state for potential cancellation
			_originalPosition = ConstructionReflection.GetBuildingGridPosition(building);
			_originalRotation = ConstructionReflection.GetBuildingRotation(building);
			_currentRotation = _originalRotation;

			_movingBuilding = building;
			_buildingName = GameReflection.GetDisplayName(building) ?? "Building";

			// Lift building from grid (removes from grid but keeps the object)
			ConstructionReflection.LiftBuilding(building);

			_isActive = true;
			SoundManager.PlayBuildingMoveStarted();

			// Get building size for extension announcement
			var buildingModel = ConstructionReflection.GetBuildingModel(building);
			Vector2Int size = buildingModel != null
				? ConstructionReflection.GetBuildingSize(buildingModel)
				: new Vector2Int(1, 1);

			bool isRotated = (_currentRotation % 2) == 1;
			int extendEast = (isRotated ? size.y : size.x) - 1;
			int extendNorth = (isRotated ? size.x : size.y) - 1;
			string extension = NavigationUtils.GetExtensionAnnouncement(extendEast, extendNorth);

			string costNote = "";
			if (_pricePaid) {
				var costInfo = ConstructionReflection.GetMovingCostInfo(building);
				if (costInfo.HasValue)
					costNote = $"cost: {costInfo.Value.amount} {costInfo.Value.displayName}, ";
			}

			Speech.Say($"Move mode: {costNote}{extension}");
			Debug.Log($"[ATSAccessibility] Entered move mode for: {_buildingName} at ({_originalPosition.x}, {_originalPosition.y})");
		}

		/// <summary>
		/// Silently clear all state without sounds, speech, or grid operations.
		/// Called on scene unload when game objects may be destroyed.
		/// </summary>
		public void Reset() {
			_isActive = false;
			_movingBuilding = null;
			_buildingName = null;
			_pricePaid = false;
			_awaitingPlaceConfirm = false;
		}

		private void TryConfirmPlacement() {
			if (_awaitingPlaceConfirm) {
				_awaitingPlaceConfirm = false;
				ExitMoveMode(false); // Confirmed placement
			} else if (_pricePaid) {
				int x = _mapNavigator.CursorX;
				int y = _mapNavigator.CursorY;
				Vector2Int newPos = new Vector2Int(x, y);
				ConstructionReflection.SetBuildingPosition(_movingBuilding, newPos);
				if (!ConstructionReflection.CanPlaceBuilding(_movingBuilding)) {
					ConstructionReflection.SetBuildingPosition(_movingBuilding, _originalPosition);
					Speech.Say("Cannot place here");
				} else {
					ConstructionReflection.SetBuildingPosition(_movingBuilding, _originalPosition);
					_awaitingPlaceConfirm = true;
					Speech.Say("Space to confirm move");
				}
			} else {
				ExitMoveMode(false); // Free move, no confirm needed
			}
		}

		/// <summary>
		/// Exit move mode, either placing at new position or cancelling.
		/// </summary>
		/// <param name="cancel">True to restore original position, false to place at cursor.</param>
		public void ExitMoveMode(bool cancel) {
			if (!_isActive || _movingBuilding == null) return;

			if (cancel) {
				// Restore original position and rotation
				ConstructionReflection.SetBuildingPosition(_movingBuilding, _originalPosition);
				if (_currentRotation != _originalRotation) {
					ConstructionReflection.RotateBuilding(_movingBuilding, _originalRotation);
				}
				ConstructionReflection.PlaceBuildingOnGrid(_movingBuilding);

				// Refund moving cost if it was paid
				if (_pricePaid) {
					ConstructionReflection.RefundMoving(_movingBuilding);
					_pricePaid = false;
				}

				InputBlocker.BlockCancelOnce = true;
				Speech.Say("Move cancelled");
				Debug.Log($"[ATSAccessibility] Move cancelled, restored to ({_originalPosition.x}, {_originalPosition.y})");
			} else {
				// Try to place at current cursor position
				int x = _mapNavigator.CursorX;
				int y = _mapNavigator.CursorY;
				Vector2Int newPos = new Vector2Int(x, y);

				// Set position to cursor
				ConstructionReflection.SetBuildingPosition(_movingBuilding, newPos);

				// Check if placement is valid
				if (!ConstructionReflection.CanPlaceBuilding(_movingBuilding)) {
					// Restore position (building stays lifted for another attempt)
					ConstructionReflection.SetBuildingPosition(_movingBuilding, _originalPosition);
					Speech.Say("Cannot place here");
					Debug.Log($"[ATSAccessibility] Cannot place {_buildingName} at ({x}, {y})");
					return; // Don't exit move mode, let user try another position
				}

				// Place building on grid at new position
				ConstructionReflection.PlaceBuildingOnGrid(_movingBuilding);

				SoundManager.PlayBuildingMoveFinished();
				Speech.Say($"{_buildingName} moved");
				Debug.Log($"[ATSAccessibility] Moved {_buildingName} from ({_originalPosition.x}, {_originalPosition.y}) to ({x}, {y})");
			}

			// Clean up state
			_isActive = false;
			_movingBuilding = null;
			_buildingName = null;
			_pricePaid = false;
			_awaitingPlaceConfirm = false;
		}

		/// <summary>
		/// Process a key event for move mode (IKeyHandler).
		/// Returns true if the key was handled.
		/// </summary>
		public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (!_isActive) return false;

			switch (keyCode) {
				case KeyCode.R:
					_awaitingPlaceConfirm = false;
					RotateBuilding(!modifiers.Shift);
					return true;

				case KeyCode.Space:
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					TryConfirmPlacement();
					return true;

				case KeyCode.Escape:
					_awaitingPlaceConfirm = false;
					ExitMoveMode(true); // Cancel
					return true;

				// Pass to MapNavigator for cursor movement
				case KeyCode.UpArrow:
				case KeyCode.DownArrow:
				case KeyCode.LeftArrow:
				case KeyCode.RightArrow:
					_awaitingPlaceConfirm = false;
					return false;

				// Pass to MapScanner for building/resource scanning
				case KeyCode.PageUp:
				case KeyCode.PageDown:
				case KeyCode.Home:
				case KeyCode.End:
					_awaitingPlaceConfirm = false;
					return false;

				// Pass to MapNavigator for position/tile info
				case KeyCode.K:
				case KeyCode.I:
					_awaitingPlaceConfirm = false;
					return false;

				// Building range info for move preview
				case KeyCode.D: {
						_awaitingPlaceConfirm = false;
						var buildingModel = ConstructionReflection.GetBuildingModel(_movingBuilding);
						if (buildingModel != null) {
							int cursorX = _mapNavigator.CursorX;
							int cursorY = _mapNavigator.CursorY;
							Vector2Int cursorPos = new Vector2Int(cursorX, cursorY);

							// Temporarily set position to cursor to test placement
							ConstructionReflection.SetBuildingPosition(_movingBuilding, cursorPos);
							bool canPlace = ConstructionReflection.CanPlaceBuilding(_movingBuilding);
							ConstructionReflection.SetBuildingPosition(_movingBuilding, _originalPosition);

							string preview = RangeInfoHelper.GetBuildingRangePreview(
								buildingModel, cursorX, cursorY, _currentRotation, canPlace);
							Speech.Say(preview);
						}
						return true;
					}

				// Entrance preview for building being moved
				case KeyCode.E: {
						_awaitingPlaceConfirm = false;
						var buildingModel = ConstructionReflection.GetBuildingModel(_movingBuilding);
						if (buildingModel != null) {
							int cursorX = _mapNavigator.CursorX;
							int cursorY = _mapNavigator.CursorY;
							Vector2Int cursorPos = new Vector2Int(cursorX, cursorY);

							// Temporarily set position to cursor for entrance calculation
							ConstructionReflection.SetBuildingPosition(_movingBuilding, cursorPos);
							string preview = EntranceInfoHelper.GetEntrancePreview(
								_movingBuilding, cursorX, cursorY, buildingModel, _currentRotation);
							ConstructionReflection.SetBuildingPosition(_movingBuilding, _originalPosition);

							Speech.Say(preview);
						}
						return true;
					}

				// Consume M and Tab to prevent activating build mode while in move mode
				case KeyCode.M:
				case KeyCode.Tab:
					return true;

				default:
					// Pass unhandled keys through to other handlers
					return false;
			}
		}

		/// <summary>
		/// Rotate the building and announce the new direction.
		/// </summary>
		private void RotateBuilding(bool clockwise) {
			if (_movingBuilding == null) return;

			// Check if building can be rotated
			var buildingModel = ConstructionReflection.GetBuildingModel(_movingBuilding);
			if (buildingModel != null && !ConstructionReflection.CanRotateBuildingModel(buildingModel)) {
				Speech.Say("Cannot rotate");
				return;
			}

			// Increment rotation
			_currentRotation = (_currentRotation + (clockwise ? 3 : 1)) % 4;
			ConstructionReflection.RotateBuilding(_movingBuilding, _currentRotation);
			SoundManager.PlayBuildingRotated();

			string direction = NavigationUtils.GetCardinalDirection(_currentRotation);

			// Get building size and adjust for rotation
			Vector2Int baseSize = buildingModel != null
				? ConstructionReflection.GetBuildingSize(buildingModel)
				: new Vector2Int(1, 1);
			bool isRotated = (_currentRotation % 2) == 1;
			int extendEast = (isRotated ? baseSize.y : baseSize.x) - 1;
			int extendNorth = (isRotated ? baseSize.x : baseSize.y) - 1;

			string extension = NavigationUtils.GetExtensionAnnouncement(extendEast, extendNorth);

			Speech.Say($"{direction}, {extension}");
			Debug.Log($"[ATSAccessibility] Building rotated to {_currentRotation} ({direction})");
		}

	}
}
