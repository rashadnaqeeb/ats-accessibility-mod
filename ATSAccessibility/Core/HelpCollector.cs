using System.Collections.Generic;

namespace ATSAccessibility.Core {
	/// <summary>
	/// Collects help entries from the active handler chain, respecting
	/// handler behaviors (Terminator, Filter, SelectivePassthrough).
	/// </summary>
	public static class HelpCollector {
		/// <summary>
		/// Walk the handler chain and collect help entries from active IHelpProviders.
		/// </summary>
		public static (string contextName, List<HelpEntry> entries) Collect(IReadOnlyList<IKeyHandler> handlers) {
			var result = new List<HelpEntry>();
			var shadowedKeys = new HashSet<string>();
			HashSet<string> allowedKeys = null;
			string contextName = null;

			for (int i = 0; i < handlers.Count; i++) {
				var handler = handlers[i];
				if (!handler.IsActive) continue;

				var provider = handler as IHelpProvider;
				if (provider == null) continue;

				if (contextName == null && provider.HelpContextName != null)
					contextName = provider.HelpContextName;

				var entries = provider.GetHelpEntries();
				if (entries != null) {
					for (int j = 0; j < entries.Count; j++) {
						var entry = entries[j];
						if (shadowedKeys.Contains(entry.KeyName)) continue;
						if (allowedKeys != null && !allowedKeys.Contains(entry.KeyName)) continue;
						result.Add(entry);
					}
				}

				switch (provider.HelpBehavior) {
					case HelpBehavior.Filter:
						if (entries != null) {
							for (int j = 0; j < entries.Count; j++)
								shadowedKeys.Add(entries[j].KeyName);
						}
						break;

					case HelpBehavior.Terminator:
						return (contextName ?? "Help", result);

					case HelpBehavior.SelectivePassthrough:
						allowedKeys = new HashSet<string>();
						var passthrough = provider.GetPassthroughKeys();
						if (passthrough != null) {
							for (int j = 0; j < passthrough.Count; j++) {
								if (!shadowedKeys.Contains(passthrough[j]))
									allowedKeys.Add(passthrough[j]);
							}
						}
						break;
				}
			}

			return (contextName ?? "Help", result);
		}
	}
}
