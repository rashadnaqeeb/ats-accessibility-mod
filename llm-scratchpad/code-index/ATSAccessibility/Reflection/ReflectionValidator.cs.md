# ReflectionValidator.cs
Validates reflection caches by inspecting static fields for null values. Uses reflection-on-reflection to automatically detect cached metadata fields and report missing ones.

## class ReflectionValidator (line 7)

### Fields
- public static HashSet<Type> `ReflectionTypes` (line 12)
  Registry of all reflection classes. Used by LogCacheStatus to enumerate types.

### Methods
- public static int `ValidateFields(Type type, string name)` (line 26)
  Inspects all private static fields in `type` for nulls. Logs warnings for each null field. Returns count of null fields found.
- public static int `TriggerAndValidate(Type type, string name)` (line 60)
  Triggers cache initialization by calling an EnsureCached/EnsureTypesCached method on `type` via reflection, then calls `ValidateFields`. Called by every reflection class's `LogCacheStatus()`.
