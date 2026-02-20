using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using UnityEngine;

namespace ATSAccessibility.Handlers {
	/// <summary>
	/// High-priority handler for world map info hotkeys (Alt+L, Alt+R, Alt+S, Alt+T).
	/// Registered above menus/overlays so these work even inside popups
	/// without interfering with typeahead search.
	/// </summary>
	public class WorldMapInfoHandler: IKeyHandler {
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
