using ATSAccessibility.Core;
using ATSAccessibility.Utils;
using System.Collections.Generic;

namespace ATSAccessibility.Panels {
	/// <summary>
	/// Read-only overlay that displays context-sensitive help entries.
	/// Opened via F12, shows all key bindings available in the current context.
	/// </summary>
	public class HelpOverlay: MenuBase {
		private List<HelpEntry> _entries = new List<HelpEntry>();

		protected override string OverlayName => Strings.Get("panel.help.title");
		protected override string EmptyMessage => Strings.Get("panel.help.empty");

		protected override string GetOpenAnnouncement() {
			// Open with just the overlay name — no first-entry prefix or active-handler name.
			return OverlayName;
		}

		protected override int GetItemCount() => _entries.Count;

		protected override string GetLabel(int index) {
			if (index < 0 || index >= _entries.Count) return null;
			return Strings.Get("panel.help.entry", _entries[index].KeyName, _entries[index].Description);
		}

		protected override void RefreshData() {
			// Data is set externally via ShowHelp
		}

		protected override EnterAction OnEnter(int index) => EnterAction.None;

		protected override void OnClosed() {
			Speech.Say(Strings.Get("common.closed"));
		}

		protected override EscapeAction OnEscape() {
			// Block the Escape from reaching the game and closing underlying panels
			InputBlocker.BlockCancelOnce = true;
			return EscapeAction.Close;
		}

		/// <summary>
		/// Show the help overlay with the given entries. Uses OpenSilently to avoid
		/// firing OnAnyMenuOpened, which would cancel active modes like tree marking,
		/// build mode, and move mode.
		/// </summary>
		public void ShowHelp(List<HelpEntry> entries) {
			_entries = entries ?? new List<HelpEntry>();
			OpenSilently();
		}
	}
}
