# ProfilesReflection.cs
Reflection helpers for ProfilesPopup and ProfilesService. Provides save/profile management: listing profiles, querying ironman state, and CRUD operations.

## class ProfilesReflection (line 7)

### Fields
**Popup type cache**
- private static Type `_profilesPopupType` (line ~15)

**MB types cache**
- private static PropertyInfo `_mbMetaServicesProperty` (line ~20)
- private static PropertyInfo `_msProfilesServiceProperty` (line ~21)

**ProfilesService methods cache**
- private static MethodInfo `_getAllProfilesMethod` (line ~27)
- private static MethodInfo `_getIronmanProfilesMethod` (line ~28)
- private static MethodInfo `_getCurrentProfileMethod` (line ~29)
- private static MethodInfo `_createNewProfileMethod` (line ~30)
- private static MethodInfo `_renameProfileMethod` (line ~31)
- private static MethodInfo `_changeProfileMethod` (line ~32)
- private static MethodInfo `_clearProfileMethod` (line ~33)
- private static MethodInfo `_removeProfileMethod` (line ~34)
- private static MethodInfo `_getMaxProfilesMethod` (line ~35)
- private static MethodInfo `_isIronmanUnlockedMethod` (line ~36)

**ProfileData fields cache**
- private static FieldInfo `_profileIsIronmanField` (line ~42)
- private static FieldInfo `_profileIsIronmanActiveField` (line ~43)
- private static FieldInfo `_profileIronmanResultField` (line ~44)
- private static FieldInfo `_profileIsPickableField` (line ~45)
- private static FieldInfo `_profileCanResetSeedField` (line ~46)
- private static FieldInfo `_profileIsDefaultField` (line ~47)
- private static FieldInfo `_profileIsCurrentField` (line ~48)
- private static FieldInfo `_profileDisplayNameField` (line ~49)
- private static FieldInfo `_profileNameField` (line ~50)
- private static bool `_typesCached` (line ~53)

### Methods
- private static void `EnsureTypesCached()` (line 50)
- private static void `CachePopupType(Assembly)` (line ~60)
- private static void `CacheMbTypes(Assembly)` (line ~70)
- private static void `CacheProfilesServiceTypes(Assembly)` (line ~80)
- private static void `CacheProfileDataTypes(Assembly)` (line ~90)
- private static object `GetProfilesService()` (line 125)
- public static bool `IsProfilesPopup(object popup)` (line 138)
- public static List<object> `GetAllProfiles()` (line 152)
  Returns all profiles (both regular and ironman).
- public static List<object> `GetProfiles(bool ironman)` (line 177)
  Returns regular or ironman profiles depending on flag.
- public static object `GetCurrentProfile()` (line 193)
- public static bool `IsIronman(object profile)` (line 208)
- public static bool `IsIronmanActive(object profile)` (line 218)
  Returns whether the ironman run on this profile is currently active.
- public static int `GetIronmanResult(object profile)` (line 229)
  Returns the ironman result enum value (0=None, 1=Victory, 2=Defeat).
- public static string `GetIronmanStatus(object profile)` (line 240)
  Returns a human-readable string for the ironman result.
- public static bool `IsPickable(object profile)` (line 255)
- public static bool `CanResetIronmanSeed(object profile)` (line 265)
- public static bool `IsDefault(object profile)` (line 279)
- public static bool `IsCurrent(object profile)` (line 292)
- public static bool `IsIronmanUnlocked()` (line 301)
- public static string `GetProfileDisplayName(object profile)` (line 312)
  Returns the localized display name.
- public static string `GetProfileName(object profile)` (line 328)
  Returns the raw internal profile name.
- public static int `GetMaxProfiles(bool ironman)` (line 337)
- public static bool `CreateNewProfile(bool ironman)` (line 348)
- public static bool `RenameProfile(object profile, string newName)` (line 367)
- public static bool `ChangeProfile(object profile)` (line 383)
- public static bool `ClearProfile(object profile)` (line 397)
- public static bool `RemoveProfile(object profile)` (line 411)
- public static int `LogCacheStatus()` (line 423)
