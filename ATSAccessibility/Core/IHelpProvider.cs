using System.Collections.Generic;

namespace ATSAccessibility.Core {
	/// <summary>
	/// A key binding + description pair for the help screen.
	/// </summary>
	public struct HelpEntry {
		public string KeyName;
		public string Description;

		public HelpEntry(string keyName, string description) {
			KeyName = keyName;
			Description = description;
		}
	}

	/// <summary>
	/// How a handler interacts with the key handler chain for help collection.
	/// </summary>
	public enum HelpBehavior {
		/// <summary>Consumes all keys; collection stops here.</summary>
		Terminator,
		/// <summary>Consumes only declared keys; collection continues past this handler.</summary>
		Filter,
		/// <summary>Consumes by default but passes specific keys through; collection continues but only for those passed-through keys.</summary>
		SelectivePassthrough
	}

	/// <summary>
	/// Interface for handlers that provide help entries for the F12 help screen.
	/// Opt-in: handlers that don't implement this are skipped during collection.
	/// </summary>
	public interface IHelpProvider {
		/// <summary>How this handler interacts with the chain.</summary>
		HelpBehavior HelpBehavior { get; }

		/// <summary>This handler's key bindings.</summary>
		IReadOnlyList<HelpEntry> GetHelpEntries();

		/// <summary>
		/// For SelectivePassthrough: key names that pass through
		/// (matched against lower handlers' HelpEntry.KeyName).
		/// Return null for other behaviors.
		/// </summary>
		IReadOnlyList<string> GetPassthroughKeys();

		/// <summary>
		/// Label for the help screen. Null = don't set context name
		/// (used by filters that shouldn't override the label).
		/// </summary>
		string HelpContextName { get; }
	}
}
