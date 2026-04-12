using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Handlers {
	/// <summary>
	/// High-priority handler for world map info hotkeys (Alt+L, Alt+R, Alt+S, Alt+T).
	/// Registered above menus/overlays so these work even inside popups
	/// without interfering with typeahead search.
	/// </summary>
	public class WorldMapInfoHandler: IKeyHandler, IHelpProvider {
		// ========================================
		// IHELPPROVIDER
		// ========================================

		private static readonly List<HelpEntry> _helpEntries = new List<HelpEntry> {
			HelpEntry.Loca("Alt+L", "handler.worldmap_info.help.level_info"),
			HelpEntry.Loca("Alt+R", "handler.worldmap_info.help.meta_resources"),
			HelpEntry.Loca("Alt+S", "handler.worldmap_info.help.seal_info"),
			HelpEntry.Loca("Alt+T", "handler.worldmap_info.help.cycle_info"),
		};

		public HelpBehavior HelpBehavior => HelpBehavior.Filter;
		public string HelpContextName => null;
		public IReadOnlyList<HelpEntry> GetHelpEntries() => _helpEntries;
		public IReadOnlyList<string> GetPassthroughKeys() => null;

		public bool IsActive => WorldMapReflection.IsWorldMapActive();

		public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (!modifiers.Alt) return false;

			switch (keyCode) {
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
				default:
					return false;
			}
		}
	}
}
