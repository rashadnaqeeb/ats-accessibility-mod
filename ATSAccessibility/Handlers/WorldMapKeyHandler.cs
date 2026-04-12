using ATSAccessibility.Overlays;
using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Handlers {
	/// <summary>
	/// Handles keyboard input for world map hex grid navigation.
	/// This is the fallback handler when no popups/menus are open on the world map.
	/// </summary>
	public class WorldMapKeyHandler: IKeyHandler, IHelpProvider {
		private readonly WorldMapNavigator _worldMapNavigator;
		private readonly WorldMapScanner _worldMapScanner;
		private WorldTutorialsOverlay _tutorialsOverlay;

		public WorldMapKeyHandler(WorldMapNavigator worldMapNavigator, WorldMapScanner worldMapScanner) {
			_worldMapNavigator = worldMapNavigator;
			_worldMapScanner = worldMapScanner;
		}

		/// <summary>
		/// Set the tutorials overlay reference for F1 key handling.
		/// </summary>
		public void SetTutorialsOverlay(WorldTutorialsOverlay overlay) {
			_tutorialsOverlay = overlay;
		}

		// ========================================
		// IHELPPROVIDER
		// ========================================

		private static readonly List<HelpEntry> _helpEntries = new List<HelpEntry> {
			new HelpEntry("I", Strings.Get("handler.worldmap_key.help.tooltip_info")),
			new HelpEntry("D", Strings.Get("handler.worldmap_key.help.embark_distance")),
			new HelpEntry("M", Strings.Get("handler.worldmap_key.help.effects_panel")),
			new HelpEntry("L", Strings.Get("handler.worldmap_key.help.level_info")),
			new HelpEntry("R", Strings.Get("handler.worldmap_key.help.meta_resources")),
			new HelpEntry("S", Strings.Get("handler.worldmap_key.help.seal_info")),
			new HelpEntry("T", Strings.Get("handler.worldmap_key.help.cycle_info")),
			new HelpEntry("E", Strings.Get("handler.worldmap_key.help.cycle_end")),
			new HelpEntry("F1", Strings.Get("handler.worldmap_key.help.tutorials")),
			new HelpEntry("PageUp/Down", Strings.Get("handler.worldmap_key.help.scanner_type")),
			new HelpEntry("Alt+PageUp/Down", Strings.Get("handler.worldmap_key.help.scanner_item")),
			new HelpEntry("Home", Strings.Get("handler.worldmap_key.help.scanner_jump")),
			new HelpEntry("Alt+Home", Strings.Get("handler.worldmap_key.help.scanner_automove")),
			new HelpEntry("End", Strings.Get("handler.worldmap_key.help.scanner_direction")),
		};

		public HelpBehavior HelpBehavior => HelpBehavior.Terminator;
		public string HelpContextName => "World Map";
		public IReadOnlyList<HelpEntry> GetHelpEntries() => _helpEntries;
		public IReadOnlyList<string> GetPassthroughKeys() => null;

		/// <summary>
		/// Active when the world map is displayed.
		/// </summary>
		public bool IsActive => WorldMapReflection.IsWorldMapActive();

		/// <summary>
		/// Process world map key events.
		/// </summary>
		public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (!IsActive || _worldMapNavigator == null) return false;

			// Check if effects panel is open first - it handles its own keys
			if (_worldMapNavigator.ProcessPanelKeyEvent(keyCode, modifiers))
				return true;

			switch (keyCode) {
				// Arrow key navigation (zigzag pattern for up/down)
				case KeyCode.RightArrow:
					_worldMapNavigator.MoveArrow(1, 0);
					return true;
				case KeyCode.LeftArrow:
					_worldMapNavigator.MoveArrow(-1, 0);
					return true;
				case KeyCode.UpArrow:
					_worldMapNavigator.MoveArrow(0, 1);
					return true;
				case KeyCode.DownArrow:
					_worldMapNavigator.MoveArrow(0, -1);
					return true;

				// Scanner controls
				case KeyCode.PageUp:
					if (modifiers.Alt)
						_worldMapScanner?.ChangeItem(-1);
					else
						_worldMapScanner?.ChangeType(-1);
					return true;
				case KeyCode.PageDown:
					if (modifiers.Alt)
						_worldMapScanner?.ChangeItem(1);
					else
						_worldMapScanner?.ChangeType(1);
					return true;
				case KeyCode.Home:
					if (modifiers.Alt) {
						Plugin.ScannerAutoMove.Value = !Plugin.ScannerAutoMove.Value;
						Speech.Say(Plugin.ScannerAutoMove.Value ? Strings.Get("handler.settlekey.automove_on") : Strings.Get("handler.settlekey.automove_off"));
					} else {
						_worldMapScanner?.JumpToItem();
					}
					return true;
				case KeyCode.End:
					_worldMapScanner?.AnnounceDirection();
					return true;

				// Select tile (embark)
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					_worldMapNavigator.Interact();
					return true;

				// Read full tooltip content
				case KeyCode.I:
					_worldMapNavigator.ReadTooltip();
					return true;

				// Read embark status and distance to capital
				case KeyCode.D:
					_worldMapNavigator.ReadEmbarkAndDistance();
					return true;

				// Open effects panel
				case KeyCode.M:
					_worldMapNavigator.OpenEffectsPanel();
					return true;

				// Meta stats announcements
				case KeyCode.L:
					WorldMapStatsReader.AnnounceLevel();
					return true;
				case KeyCode.R:
					WorldMapStatsReader.AnnounceMetaResources();
					return true;
				case KeyCode.S:
					WorldMapStatsReader.AnnounceSealInfo();
					return true;
				case KeyCode.T:
					WorldMapStatsReader.AnnounceCycleInfo();
					return true;
				case KeyCode.E:
					WorldMapStatsReader.OpenCycleEndPopup();
					return true;

				// Open tutorials HUD and overlay
				case KeyCode.F1:
					TutorialReflection.ToggleWorldTutorialsHUD();
					_tutorialsOverlay?.Open();
					return true;

				default:
					// Consume all keys - mod has full keyboard control on world map
					return true;
			}
		}
	}
}
