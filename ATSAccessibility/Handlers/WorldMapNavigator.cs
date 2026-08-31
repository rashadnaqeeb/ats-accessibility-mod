using ATSAccessibility.Panels;
using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Handlers {
	/// <summary>
	/// Handles keyboard navigation on the world map hex grid.
	/// Uses arrow keys for navigation with zigzag pattern for up/down.
	/// </summary>
	public class WorldMapNavigator {
		// Hex directions in cubic coordinates
		// Order: NW (Q), NE (E), E (D), SE (C), SW (Z), W (A)
		private static readonly Vector3Int[] HexDirections = new Vector3Int[]
		{
			new Vector3Int(-1, 0, 1),   // 0: NW (Q)
            new Vector3Int(0, -1, 1),   // 1: NE (E)
            new Vector3Int(1, -1, 0),   // 2: E  (D)
            new Vector3Int(1, 0, -1),   // 3: SE (C)
            new Vector3Int(0, 1, -1),   // 4: SW (Z)
            new Vector3Int(-1, 1, 0)    // 5: W  (A)
        };

		private static string[] DirectionNames => new string[]
		{
			Strings.Get("common.northwest_lower"),
			Strings.Get("common.northeast_lower"),
			Strings.Get("common.east_lower"),
			Strings.Get("common.southeast_lower"),
			Strings.Get("common.southwest_lower"),
			Strings.Get("common.west_lower"),
		};

		// Current cursor position in cubic coordinates
		// (0, 0, 0) is the Smoldering City / capital
		private Vector3Int _cursorPos = Vector3Int.zero;

		// Cached tile info (updated on cursor move)
		private string _cachedBriefInfo;

		// Effects panel for M key
		private WorldMapEffectsPanel _effectsPanel = new WorldMapEffectsPanel();

		// Tile type for tooltip selection
		private enum TileType {
			Unexplored,
			Capital,
			City,
			Seal,
			Modifier,
			Event,
			PlayableField,
			OutOfReach
		}

		private TileType _cachedTileType;

		/// <summary>
		/// Current cursor position in cubic coordinates.
		/// </summary>
		public Vector3Int CursorPosition => _cursorPos;

		/// <summary>
		/// Move cursor in the specified direction and announce the new tile.
		/// </summary>
		/// <param name="directionIndex">Direction index 0-5 (NW, NE, E, SE, SW, W)</param>
		public void MoveCursor(int directionIndex) {
			if (directionIndex < 0 || directionIndex >= 6) return;

			var newPos = _cursorPos + HexDirections[directionIndex];

			// Check if in bounds
			if (!WorldMapReflection.WorldMapInBounds(newPos)) {
				Debug.Log($"[ATSAccessibility] WorldMapNavigator: edge of map at {newPos}");
				Speech.Say(Strings.Get("handler.worldmap.edge_of_map"));
				return;
			}

			_cursorPos = newPos;
			SyncCameraToTile();
			CacheTileInfo();
			AnnounceTile();
		}

		/// <summary>
		/// Move cursor using arrow key directions (fallback navigation).
		/// Up/Down zigzag based on z coordinate parity for predictable navigation.
		/// </summary>
		public void MoveArrow(int dx, int dy) {
			int directionIndex;

			if (dx > 0)  // Right → East
			{
				directionIndex = 2;
			} else if (dx < 0)  // Left → West
			  {
				directionIndex = 5;
			} else {
				// Use z coordinate for parity since NE/NW both change z but not necessarily x
				// This ensures zigzag alternates with each up/down press
				// Bitwise AND handles negative numbers correctly
				bool evenZ = (_cursorPos.z & 1) == 0;

				if (dy > 0)  // Up
				{
					directionIndex = evenZ ? 1 : 0;  // Even z→NE, Odd z→NW
				} else  // Down
				  {
					// Match the opposite of what Up does from the tile we came from
					// Even z: came here via NW from odd z, so go SE to return
					// Odd z: came here via NE from even z, so go SW to return
					directionIndex = evenZ ? 3 : 4;  // Even z→SE, Odd z→SW
				}
			}

			MoveCursor(directionIndex);
		}

		/// <summary>
		/// Set cursor to a specific position (used by scanner).
		/// Does not announce - caller handles announcement.
		/// </summary>
		public void SetCursorPosition(Vector3Int pos) {
			if (!WorldMapReflection.WorldMapInBounds(pos)) {
				Debug.Log($"[ATSAccessibility] WorldMapNavigator: SetCursorPosition out of bounds at {pos}");
				return;
			}
			_cursorPos = pos;
			SyncCameraToTile();
			CacheTileInfo();
		}

		/// <summary>
		/// Select the current tile (trigger embark/event).
		/// </summary>
		public void Interact() {
			WorldMapReflection.WorldMapTriggerFieldClick(_cursorPos);
		}

		/// <summary>
		/// Read detailed tooltip information about the current tile (I key).
		/// Content varies based on tile type.
		/// </summary>
		public void ReadTooltip() {
			string tooltip = BuildTooltip();
			Speech.Say(tooltip);
		}

		/// <summary>
		/// Read embark range and distance from embark starting point (D key).
		/// </summary>
		public void ReadEmbarkAndDistance() {
			var parts = new List<string>();

			// Embark status
			if (!WorldMapReflection.WorldMapIsRevealed(_cursorPos))
				parts.Add(Strings.Get("common.unexplored"));
			else if (WorldMapReflection.WorldMapCanBePicked(_cursorPos))
				parts.Add(Strings.Get("handler.worldmap.can_embark"));
			else if (!WorldMapReflection.WorldMapHasAnyPathTo(_cursorPos))
				parts.Add(Strings.Get("handler.worldmap.out_of_reach"));
			else
				parts.Add(GetUnpickableReason());

			// Get embark starting point (last town or capital)
			var (lastTownPos, lastTownName) = WorldMapReflection.GetLastTownInfo();

			// Embark range from that starting point
			var range = WorldMapReflection.GetEmbarkRange(lastTownPos);
			if (range >= 0)
				parts.Add(Strings.Get("handler.worldmap.range", range));

			// Distance and direction to embark point
			if (_cursorPos == lastTownPos) {
				var townLabel = !string.IsNullOrEmpty(lastTownName) ? lastTownName : Strings.Get("common.capital");
				parts.Add(Strings.Get("handler.worldmap.at_town", townLabel));
			} else {
				var distance = GetHexDistance(_cursorPos, lastTownPos);
				var direction = GetDirectionTo(_cursorPos, lastTownPos);
				var townLabel = !string.IsNullOrEmpty(lastTownName) ? lastTownName : Strings.Get("common.capital");
				parts.Add(Strings.Get("handler.worldmap.distance_to", townLabel, distance, direction));
			}

			Speech.Say(string.Join(", ", parts));
		}

		/// <summary>
		/// Reset cursor to capital.
		/// </summary>
		public void Reset() {
			_cursorPos = Vector3Int.zero;
			// Refresh the cached tile info too — otherwise I/M pressed before the
			// first cursor move read the previous visit's tile (or "Unexplored")
			// while actually sitting on the capital.
			CacheTileInfo();
			Debug.Log("[ATSAccessibility] WorldMapNavigator reset to capital");
		}

		/// <summary>
		/// Jump the cursor to the capital (Smoldering City) and announce it.
		/// </summary>
		public void JumpToCapital() {
			SetCursorPosition(Vector3Int.zero);
			AnnounceTile();
		}

		/// <summary>
		/// Open the effects panel for the current tile.
		/// Does not work on capital/city tiles.
		/// </summary>
		public void OpenEffectsPanel() {
			if (_cachedTileType == TileType.Capital || _cachedTileType == TileType.City) {
				Speech.Say(Strings.Get("handler.worldmap.no_effects_panel"));
				return;
			}
			_effectsPanel.Open(_cursorPos);
		}

		/// <summary>
		/// Process key events for the effects panel.
		/// Returns true if the key was handled.
		/// </summary>
		public bool ProcessPanelKeyEvent(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers = default) {
			return _effectsPanel.ProcessKeyEvent(keyCode, modifiers);
		}

		/// <summary>
		/// Build and cache tile info for current position.
		/// Called once per cursor move to avoid repeated reflection.
		/// </summary>
		private void CacheTileInfo() {
			bool isRevealed = WorldMapReflection.WorldMapIsRevealed(_cursorPos);

			// Check for special features visible in fog of war
			bool hasSeal = WorldMapReflection.WorldMapHasSeal(_cursorPos);
			bool hasModifier = !hasSeal && WorldMapReflection.WorldMapHasModifier(_cursorPos);
			bool hasEvent = !hasSeal && !hasModifier && WorldMapReflection.WorldMapHasEvent(_cursorPos);

			// Handle unexplored tiles with special visibility rules
			if (!isRevealed) {
				if (hasSeal) {
					// Seals visible through fog - show seal info
					_cachedTileType = TileType.Seal;
					var sealName = WorldMapReflection.WorldMapGetSealName(_cursorPos);
					var (_, _, minFragments, _, _, _) = WorldMapReflection.WorldMapGetSealInfo(_cursorPos);
					string sealLabel = !string.IsNullOrEmpty(sealName) ? Strings.Get("handler.worldmap.seal_with_name", sealName) : Strings.Get("common.seal");
					if (minFragments > 0)
						sealLabel = Strings.Get("handler.worldmap.seal_fragments", sealLabel, minFragments);
					_cachedBriefInfo = Strings.Get("handler.worldmap.unexplored_seal", sealLabel);
				} else if (hasModifier) {
					// Modifier visible as "?" - don't identify it
					_cachedTileType = TileType.Unexplored;
					_cachedBriefInfo = Strings.Get("handler.worldmap.unexplored_modifier");
				} else if (hasEvent) {
					// Event visible as "?" - don't identify it
					_cachedTileType = TileType.Unexplored;
					_cachedBriefInfo = Strings.Get("handler.worldmap.unexplored_event");
				} else {
					// Plain unexplored
					_cachedTileType = TileType.Unexplored;
					_cachedBriefInfo = Strings.Get("common.unexplored");
				}
				return;
			}

			// Get biome for revealed tiles
			var biome = WorldMapReflection.WorldMapGetBiomeName(_cursorPos) ?? Strings.Get("handler.worldmap.unknown_biome");

			// Check tile type once with short-circuit evaluation
			bool isCapital = WorldMapReflection.WorldMapIsCapital(_cursorPos);
			bool isCity = !isCapital && WorldMapReflection.WorldMapIsCity(_cursorPos);

			// Determine tile type and brief info
			string tileType = null;

			if (isCapital) {
				_cachedTileType = TileType.Capital;
				tileType = Strings.Get("common.capital");
			} else if (isCity) {
				_cachedTileType = TileType.City;
				var cityName = WorldMapReflection.WorldMapGetCityName(_cursorPos);
				tileType = !string.IsNullOrEmpty(cityName) ? cityName : Strings.Get("handler.worldmap.city_fallback");
			} else if (hasSeal) {
				_cachedTileType = TileType.Seal;
				var sealName = WorldMapReflection.WorldMapGetSealName(_cursorPos);
				var (_, _, minFragments, _, _, _) = WorldMapReflection.WorldMapGetSealInfo(_cursorPos);
				string sealLabel = !string.IsNullOrEmpty(sealName) ? Strings.Get("handler.worldmap.seal_with_name", sealName) : Strings.Get("common.seal");
				if (minFragments > 0)
					sealLabel = Strings.Get("handler.worldmap.seal_fragments", sealLabel, minFragments);
				tileType = sealLabel;
			} else if (hasModifier) {
				_cachedTileType = TileType.Modifier;
				var modifierName = WorldMapReflection.WorldMapGetModifierName(_cursorPos);
				tileType = !string.IsNullOrEmpty(modifierName) ? modifierName : Strings.Get("common.modifier");
			} else if (hasEvent) {
				_cachedTileType = TileType.Event;
				var eventName = WorldMapReflection.WorldMapGetEventName(_cursorPos);
				tileType = !string.IsNullOrEmpty(eventName) ? Strings.Get("handler.worldmap.event_with_name", eventName) : Strings.Get("common.event");
			} else if (!WorldMapReflection.WorldMapHasAnyPathTo(_cursorPos)) {
				_cachedTileType = TileType.OutOfReach;
				tileType = Strings.Get("handler.worldmap.out_of_reach");
			} else if (WorldMapReflection.WorldMapCanBePicked(_cursorPos)) {
				_cachedTileType = TileType.PlayableField;
			} else {
				_cachedTileType = TileType.OutOfReach;
				tileType = GetUnpickableReason();
			}

			// Brief info
			_cachedBriefInfo = string.IsNullOrEmpty(tileType) ? biome : Strings.Get("handler.worldmap.brief_with_type", biome, tileType);
		}

		/// <summary>
		/// Build tooltip content based on cached tile type.
		/// Seals show full info even if unexplored (visible through fog).
		/// Unexplored modifiers/events just return "Unexplored".
		/// </summary>
		private string BuildTooltip() {
			// Unexplored tiles with no special features (or unexplored modifiers/events)
			if (_cachedTileType == TileType.Unexplored)
				return Strings.Get("common.unexplored");

			// Capital/City tiles - show city tooltip
			if (_cachedTileType == TileType.Capital || _cachedTileType == TileType.City)
				return BuildCityTooltip();

			// Seal tiles - show full seal info even if unexplored (seals visible through fog)
			if (_cachedTileType == TileType.Seal)
				return BuildSealTooltip();

			// Modifier tiles - show modifier effect info
			if (_cachedTileType == TileType.Modifier)
				return BuildModifierTooltip();

			// Event tiles - show event info
			if (_cachedTileType == TileType.Event)
				return BuildEventTooltip();

			// Out of reach tiles - show limited info
			if (_cachedTileType == TileType.OutOfReach)
				return BuildOutOfReachTooltip();

			// Playable field tiles - show full field info
			return BuildPlayableFieldTooltip();
		}

		/// <summary>
		/// Build tooltip for capital/city tiles.
		/// </summary>
		private string BuildCityTooltip() {
			var parts = new List<string>();

			// City name
			var cityName = WorldMapReflection.WorldMapGetCityName(_cursorPos);
			if (!string.IsNullOrEmpty(cityName))
				parts.Add(cityName);
			else if (WorldMapReflection.WorldMapIsCapital(_cursorPos))
				parts.Add(Strings.Get("handler.worldmap.smoldering_city"));
			else
				parts.Add(Strings.Get("handler.worldmap.city_fallback"));

			// Biome
			var biome = WorldMapReflection.WorldMapGetBiomeName(_cursorPos);
			if (!string.IsNullOrEmpty(biome))
				parts.Add(biome);

			// Wanted goods (if trade routes enabled)
			var wantedGoods = WorldMapReflection.WorldMapGetWantedGoods(_cursorPos);
			if (wantedGoods != null && wantedGoods.Length > 0)
				parts.Add(Strings.Get("handler.worldmap.city_wants", string.Join(", ", wantedGoods)));

			return string.Join(", ", parts);
		}

		/// <summary>
		/// Build tooltip for seal tiles.
		/// </summary>
		private string BuildSealTooltip() {
			var (sealName, difficultyName, minFragments, rewardsPercent, bonusYears, isCompleted) = WorldMapReflection.WorldMapGetSealInfo(_cursorPos);

			var parts = new List<string>();

			// Seal name
			if (!string.IsNullOrEmpty(sealName))
				parts.Add(sealName);
			else
				parts.Add(Strings.Get("common.seal"));

			// Difficulty and requirements
			if (!string.IsNullOrEmpty(difficultyName))
				parts.Add(Strings.Get("handler.worldmap.seal_difficulty", difficultyName));

			if (minFragments > 0)
				parts.Add(Strings.Get("handler.worldmap.seal_requires", minFragments));

			// Rewards
			if (rewardsPercent > 0)
				parts.Add(Strings.Get("handler.worldmap.seal_bonus_percent", rewardsPercent));

			if (bonusYears > 0)
				parts.Add(Strings.Get("handler.worldmap.seal_bonus_years", bonusYears));

			// Completion status
			if (isCompleted)
				parts.Add(Strings.Get("handler.worldmap.seal_completed"));

			return string.Join(", ", parts);
		}

		/// <summary>
		/// Build tooltip for modifier tiles.
		/// </summary>
		private string BuildModifierTooltip() {
			var (effectName, labelName, description, isPositive) = WorldMapReflection.WorldMapGetModifierInfo(_cursorPos);

			var parts = new List<string>();

			// Effect name
			if (!string.IsNullOrEmpty(effectName))
				parts.Add(effectName);

			// Label (effect type)
			if (!string.IsNullOrEmpty(labelName))
				parts.Add(Strings.Get("handler.worldmap.modifier_label", labelName));

			// Description
			if (!string.IsNullOrEmpty(description))
				parts.Add(description);

			return string.Join(" ", parts);
		}

		/// <summary>
		/// Build tooltip for event tiles.
		/// </summary>
		private string BuildEventTooltip() {
			// Check if event is reachable
			if (!WorldMapReflection.WorldMapCanReachEvent(_cursorPos)) {
				return Strings.Get("handler.worldmap.event_unreachable");
			}

			var eventName = WorldMapReflection.WorldMapGetEventName(_cursorPos);
			return !string.IsNullOrEmpty(eventName) ? eventName : Strings.Get("common.event");
		}

		/// <summary>
		/// Build tooltip for playable field tiles.
		/// Biome is already announced in brief info, so not repeated here.
		/// </summary>
		private string BuildPlayableFieldTooltip() {
			var parts = new List<string>();

			// Min difficulty
			var difficulty = WorldMapReflection.WorldMapGetMinDifficultyName(_cursorPos);
			if (!string.IsNullOrEmpty(difficulty)) {
				int penalty = WorldMapReflection.WorldMapGetDifficultyPreparationPenalty(_cursorPos);
				if (penalty != 0)
					parts.Add(Strings.Get("handler.worldmap.difficulty_with_penalty", difficulty, penalty, penalty == 1 || penalty == -1 ? Strings.Get("handler.worldmap.prep_point") : Strings.Get("handler.worldmap.prep_points")));
				else
					parts.Add(Strings.Get("handler.worldmap.difficulty_only", difficulty));
			}

			// Field effects (biome + modifiers)
			var effects = WorldMapReflection.WorldMapGetFieldEffects(_cursorPos);
			if (effects != null && effects.Length > 0)
				parts.Add(Strings.Get("handler.worldmap.effects", string.Join(", ", effects)));

			// Seal fragments to win
			var fragments = WorldMapReflection.WorldMapGetSealFragmentsForWin(_cursorPos);
			if (fragments > 0)
				parts.Add(Strings.Get("handler.worldmap.fragments_to_win", fragments));

			// Meta currencies (rewards)
			var currencies = WorldMapReflection.WorldMapGetMetaCurrencies(_cursorPos);
			if (currencies != null && currencies.Length > 0)
				parts.Add(Strings.Get("handler.worldmap.rewards", string.Join(", ", currencies)));

			return string.Join(", ", parts);
		}

		/// <summary>
		/// Build tooltip for out of reach tiles.
		/// </summary>
		/// <summary>
		/// Determine why a reachable tile can't be embarked on.
		/// Checks all conditions since multiple can apply simultaneously.
		/// </summary>
		private string GetUnpickableReason() {
			var reasons = new List<string>();

			if (WorldMapReflection.HasPlayedFinalGame())
				reasons.Add(Strings.Get("handler.worldmap.reason_seal_attempted"));

			var (current, required) = WorldMapReflection.GetSealFragmentStatus(_cursorPos);
			if (required >= 0 && current < required)
				reasons.Add(Strings.Get("handler.worldmap.reason_need_fragments", required, current));

			if (WorldMapReflection.IsStormAboutToCome())
				reasons.Add(Strings.Get("handler.worldmap.reason_blightstorm"));

			return reasons.Count > 0 ? string.Join(", ", reasons) : Strings.Get("handler.worldmap.reason_unavailable");
		}

		private string BuildOutOfReachTooltip() {
			var biome = WorldMapReflection.WorldMapGetBiomeName(_cursorPos);
			var prefix = !string.IsNullOrEmpty(biome) ? Strings.Get("handler.worldmap.biome_prefix", biome) : "";

			// For tiles with no path, the brief info already says "Out of reach"
			if (!WorldMapReflection.WorldMapHasAnyPathTo(_cursorPos))
				return Strings.Get("handler.worldmap.out_of_reach_tooltip", prefix);

			// For tiles that have a path but can't be picked, show specific reasons
			return Strings.Get("handler.worldmap.out_of_reach_reason", prefix, GetUnpickableReason().ToLower());
		}

		/// <summary>
		/// Announce the current tile briefly.
		/// </summary>
		private void AnnounceTile() {
			Speech.Say(_cachedBriefInfo);
		}

		/// <summary>
		/// Move the camera to smoothly follow the cursor.
		/// Uses target-following (patched in WorldCameraController) for smooth movement.
		/// </summary>
		private void SyncCameraToTile() {
			WorldMapReflection.SetWorldCameraTarget(_cursorPos);
		}

		/// <summary>
		/// Calculate hex distance between two cubic coordinate positions.
		/// For hex grids, distance = max(|dx|, |dy|, |dz|)
		/// </summary>
		private int GetHexDistance(Vector3Int from, Vector3Int to) {
			return Mathf.Max(Mathf.Abs(from.x - to.x),
				Mathf.Max(Mathf.Abs(from.y - to.y), Mathf.Abs(from.z - to.z)));
		}

		/// <summary>
		/// Get the direction name from one position toward another.
		/// Returns the closest direction (north, south, or one of 6 hex directions).
		/// </summary>
		private string GetDirectionTo(Vector3Int from, Vector3Int to) {
			var toTarget = to - from;

			int x = toTarget.x;
			int y = toTarget.y;
			int z = toTarget.z;

			int absX = Mathf.Abs(x);
			int absY = Mathf.Abs(y);

			// Check if direction is close to pure north or south (within 2:1 ratio)
			// In hex cubic coords: north = x and y both negative, z positive
			//                      south = x and y both positive, z negative
			if (absX * 2 >= absY && absY * 2 >= absX) {
				if (z > 0 && x < 0 && y < 0)
					return Strings.Get("common.north_lower");
				if (z < 0 && x > 0 && y > 0)
					return Strings.Get("common.south_lower");
			}

			// Fall back to hex direction matching
			int bestIndex = 0;
			int bestDot = int.MinValue;

			for (int i = 0; i < HexDirections.Length; i++) {
				var dir = HexDirections[i];
				int dot = toTarget.x * dir.x + toTarget.y * dir.y + toTarget.z * dir.z;
				if (dot > bestDot) {
					bestDot = dot;
					bestIndex = i;
				}
			}

			return DirectionNames[bestIndex];
		}
	}
}
