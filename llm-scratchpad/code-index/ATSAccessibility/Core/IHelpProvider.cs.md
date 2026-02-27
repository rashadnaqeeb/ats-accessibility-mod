# IHelpProvider.cs

Defines the help system types: HelpEntry (key+description pair), HelpBehavior enum, and IHelpProvider
interface. Handlers opt in to the F12 help screen by implementing IHelpProvider.

## struct HelpEntry (line 7)

A key binding + description pair for the help screen.

### Fields
- public string KeyName (line 8)
- public string Description (line 9)

### Methods
- public HelpEntry(string keyName, string description) (line 11)

---

## enum HelpBehavior (line 20)

How a handler interacts with the key handler chain for help collection.

- Terminator (line 22) - Consumes all keys; collection stops here.
- Filter (line 24) - Consumes only declared keys; collection continues past this handler.
- SelectivePassthrough (line 26) - Consumes by default but passes specific keys through; collection continues but only for those passed-through keys.

---

## interface IHelpProvider (line 33)

Interface for handlers that provide help entries for the F12 help screen.
Opt-in: handlers that don't implement this are skipped during collection.

### Properties
- HelpBehavior HelpBehavior { get; } (line 35)
  - How this handler interacts with the chain.
- string HelpContextName { get; } (line 50)
  - Label for the help screen. Null = don't set context name.

### Methods
- IReadOnlyList<HelpEntry> GetHelpEntries() (line 38)
  - This handler's key bindings.
- IReadOnlyList<string> GetPassthroughKeys() (line 44)
  - For SelectivePassthrough: key names that pass through. Return null for other behaviors.
