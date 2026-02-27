# HelpCollector.cs

Collects help entries from the active handler chain, respecting handler behaviors
(Terminator, Filter, SelectivePassthrough).

## class HelpCollector (static) (line 8)

### Methods
- public static (string contextName, List<HelpEntry> entries) Collect(IReadOnlyList<IKeyHandler> handlers) (line 12)
  - Walks the handler chain collecting IHelpProvider entries from active handlers. Applies shadowing for Filter behavior, stops at Terminator, and narrows allowed keys for SelectivePassthrough. Returns the first non-null HelpContextName found along with all collected entries.
