using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Reflection helpers for accessing ProfilesPopup and ProfilesService.
    /// Provides save/profile management functionality.
    /// </summary>
    public static class ProfilesReflection
    {
        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        // ProfilesPopup type for detection
        private static Type _profilesPopupType;

        // MB.ProfilesService access
        private static PropertyInfo _mbProfilesServiceProperty;

        // IProfilesService interface methods/properties
        private static PropertyInfo _pssManifestProperty;      // List<ProfileData> Manifest
        private static PropertyInfo _pssCurrentProperty;       // ProfileData Current
        private static MethodInfo _pssIsDefaultMethod;         // bool IsDefault(ProfileData)
        private static MethodInfo _pssIsIronmanUnlockedMethod; // bool IsIronmanUnlocked()
        private static MethodInfo _pssGetProfileDisplayNameMethod; // string GetProfileDisplayName(ProfileData)
        private static MethodInfo _pssCreateNewProfileMethod;  // void CreateNewProfile(string, bool)
        private static MethodInfo _pssRenameProfileMethod;     // void RenameProfile(ProfileData, string)
        private static MethodInfo _pssChangeProfileMethod;     // async ChangeProfile(ProfileData)
        private static MethodInfo _pssClearProfileMethod;      // async ClearProfile(ProfileData)
        private static MethodInfo _pssRemoveProfileMethod;     // async RemoveProfile(ProfileData)
        private static MethodInfo _pssCanResetIronmanSeedMethod; // bool CanResetIronmanSeed(ProfileData)

        // ProfileData type fields
        private static Type _profileDataType;
        private static FieldInfo _pdNameField;
        private static FieldInfo _pdIsIronmanField;
        private static FieldInfo _pdIsIronmanActiveField;
        private static FieldInfo _pdIronmanResultField;
        private static FieldInfo _pdCreationTimeField;

        private static bool _typesCached = false;

        // ========================================
        // INITIALIZATION
        // ========================================

        private static void EnsureTypesCached()
        {
            if (_typesCached) return;
            _typesCached = true;

            try
            {
                var assembly = GameReflection.GameAssembly;
                if (assembly == null)
                {
                    Debug.LogWarning("[ATSAccessibility] ProfilesReflection: Game assembly not available");
                    return;
                }

                CachePopupType(assembly);
                CacheMbTypes(assembly);
                CacheProfilesServiceTypes(assembly);
                CacheProfileDataTypes(assembly);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ProfilesReflection: Failed to cache types: {ex.Message}");
            }
        }

        private static void CachePopupType(Assembly assembly)
        {
            _profilesPopupType = assembly.GetType("Eremite.Voting.ProfilesPopup");
            if (_profilesPopupType != null)
            {
                Debug.Log("[ATSAccessibility] ProfilesReflection: Cached ProfilesPopup type");
            }
        }

        private static void CacheMbTypes(Assembly assembly)
        {
            var mbType = assembly.GetType("Eremite.MB");
            if (mbType != null)
            {
                // ProfilesService is a protected static property
                _mbProfilesServiceProperty = mbType.GetProperty("ProfilesService",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (_mbProfilesServiceProperty != null)
                {
                    Debug.Log("[ATSAccessibility] ProfilesReflection: Cached MB.ProfilesService property");
                }
            }
        }

        private static void CacheProfilesServiceTypes(Assembly assembly)
        {
            var serviceType = assembly.GetType("Eremite.Services.IProfilesService");
            if (serviceType != null)
            {
                _pssManifestProperty = serviceType.GetProperty("Manifest", GameReflection.PublicInstance);
                _pssCurrentProperty = serviceType.GetProperty("Current", GameReflection.PublicInstance);
                _pssIsDefaultMethod = serviceType.GetMethod("IsDefault", GameReflection.PublicInstance);
                _pssIsIronmanUnlockedMethod = serviceType.GetMethod("IsIronmanUnlocked", GameReflection.PublicInstance);
                _pssGetProfileDisplayNameMethod = serviceType.GetMethod("GetProfileDisplayName",
                    new[] { assembly.GetType("Eremite.Services.ProfileData") });
                _pssCreateNewProfileMethod = serviceType.GetMethod("CreateNewProfile",
                    new[] { typeof(string), typeof(bool) });
                _pssRenameProfileMethod = serviceType.GetMethod("RenameProfile",
                    new[] { assembly.GetType("Eremite.Services.ProfileData"), typeof(string) });
                _pssChangeProfileMethod = serviceType.GetMethod("ChangeProfile",
                    new[] { assembly.GetType("Eremite.Services.ProfileData") });
                _pssClearProfileMethod = serviceType.GetMethod("ClearProfile",
                    new[] { assembly.GetType("Eremite.Services.ProfileData") });
                _pssRemoveProfileMethod = serviceType.GetMethod("RemoveProfile",
                    new[] { assembly.GetType("Eremite.Services.ProfileData") });
                _pssCanResetIronmanSeedMethod = serviceType.GetMethod("CanResetIronmanSeed",
                    new[] { assembly.GetType("Eremite.Services.ProfileData") });

                Debug.Log("[ATSAccessibility] ProfilesReflection: Cached ProfilesService methods");
            }
        }

        private static void CacheProfileDataTypes(Assembly assembly)
        {
            _profileDataType = assembly.GetType("Eremite.Services.ProfileData");
            if (_profileDataType != null)
            {
                _pdNameField = _profileDataType.GetField("name", GameReflection.PublicInstance);
                _pdIsIronmanField = _profileDataType.GetField("isIronman", GameReflection.PublicInstance);
                _pdIsIronmanActiveField = _profileDataType.GetField("isIronmanActive", GameReflection.PublicInstance);
                _pdIronmanResultField = _profileDataType.GetField("ironmanResult", GameReflection.PublicInstance);
                _pdCreationTimeField = _profileDataType.GetField("creationTime", GameReflection.PublicInstance);

                Debug.Log("[ATSAccessibility] ProfilesReflection: Cached ProfileData fields");
            }
        }

        // ========================================
        // SERVICE ACCESS
        // ========================================

        private static object GetProfilesService()
        {
            EnsureTypesCached();
            if (_mbProfilesServiceProperty == null) return null;

            try
            {
                return _mbProfilesServiceProperty.GetValue(null);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ProfilesReflection: GetProfilesService failed: {ex.Message}");
                return null;
            }
        }

        // ========================================
        // POPUP DETECTION
        // ========================================

        /// <summary>
        /// Check if a popup is the ProfilesPopup.
        /// </summary>
        public static bool IsProfilesPopup(object popup)
        {
            if (popup == null) return false;
            EnsureTypesCached();
            if (_profilesPopupType == null) return false;
            return _profilesPopupType.IsInstanceOfType(popup);
        }

        // ========================================
        // PROFILE DATA ACCESS
        // ========================================

        /// <summary>
        /// Get all profiles in the manifest.
        /// </summary>
        public static List<object> GetAllProfiles()
        {
            EnsureTypesCached();
            var result = new List<object>();

            var service = GetProfilesService();
            if (service == null || _pssManifestProperty == null) return result;

            try
            {
                var manifest = _pssManifestProperty.GetValue(service) as IList;
                if (manifest == null) return result;

                foreach (var profile in manifest)
                {
                    if (profile != null)
                        result.Add(profile);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ProfilesReflection: GetAllProfiles failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get profiles filtered by ironman status.
        /// </summary>
        public static List<object> GetProfiles(bool ironman)
        {
            EnsureTypesCached();
            var result = new List<object>();
            var all = GetAllProfiles();

            foreach (var profile in all)
            {
                if (IsIronman(profile) == ironman)
                    result.Add(profile);
            }

            return result;
        }

        /// <summary>
        /// Get the current active profile.
        /// </summary>
        public static object GetCurrentProfile()
        {
            EnsureTypesCached();
            var service = GetProfilesService();
            if (service == null || _pssCurrentProperty == null) return null;

            try
            {
                return _pssCurrentProperty.GetValue(service);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Check if a profile is ironman (Queen's Hand).
        /// </summary>
        public static bool IsIronman(object profile)
        {
            if (profile == null) return false;
            EnsureTypesCached();
            if (_pdIsIronmanField == null) return false;

            try
            {
                return _pdIsIronmanField.GetValue(profile) as bool? ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if an ironman profile is active (in progress, not yet won/lost).
        /// Non-ironman profiles always return true.
        /// </summary>
        public static bool IsIronmanActive(object profile)
        {
            if (profile == null) return false;
            if (!IsIronman(profile)) return true;  // Non-ironman always "active"
            EnsureTypesCached();
            if (_pdIsIronmanActiveField == null) return false;

            try
            {
                return _pdIsIronmanActiveField.GetValue(profile) as bool? ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a completed ironman profile was won (true) or lost (false).
        /// Only meaningful if IsIronmanActive returns false.
        /// </summary>
        public static bool GetIronmanResult(object profile)
        {
            if (profile == null) return false;
            EnsureTypesCached();
            if (_pdIronmanResultField == null) return false;

            try
            {
                return _pdIronmanResultField.GetValue(profile) as bool? ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the status text for an ironman profile.
        /// Returns "In Progress", "Won", or "Lost".
        /// Returns null for non-ironman profiles.
        /// </summary>
        public static string GetIronmanStatus(object profile)
        {
            if (!IsIronman(profile)) return null;

            if (IsIronmanActive(profile))
            {
                return "In Progress";
            }

            return GetIronmanResult(profile) ? "Won" : "Lost";
        }

        /// <summary>
        /// Check if a profile can be switched to (picked).
        /// Regular profiles are always pickable.
        /// Ironman profiles are only pickable if active (in progress).
        /// </summary>
        public static bool IsPickable(object profile)
        {
            if (profile == null) return false;
            if (!IsIronman(profile)) return true;
            return IsIronmanActive(profile);
        }

        /// <summary>
        /// Check if an ironman profile can reset its seed (get a new world seed on reset).
        /// Requires winning a certain number of games.
        /// </summary>
        public static bool CanResetIronmanSeed(object profile)
        {
            if (profile == null) return false;
            if (!IsIronman(profile)) return true;  // Non-ironman always "can reset"
            EnsureTypesCached();

            var service = GetProfilesService();
            if (service == null || _pssCanResetIronmanSeedMethod == null) return false;

            try
            {
                return _pssCanResetIronmanSeedMethod.Invoke(service, new[] { profile }) as bool? ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if a profile is the default (first profile, can't be deleted).
        /// </summary>
        public static bool IsDefault(object profile)
        {
            if (profile == null) return false;
            EnsureTypesCached();

            var service = GetProfilesService();
            if (service == null || _pssIsDefaultMethod == null) return false;

            try
            {
                return _pssIsDefaultMethod.Invoke(service, new[] { profile }) as bool? ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Check if the current profile is this profile.
        /// </summary>
        public static bool IsCurrent(object profile)
        {
            if (profile == null) return false;
            var current = GetCurrentProfile();
            return current == profile;
        }

        /// <summary>
        /// Check if Queen's Hand (Ironman) mode is unlocked.
        /// </summary>
        public static bool IsIronmanUnlocked()
        {
            EnsureTypesCached();
            var service = GetProfilesService();
            if (service == null || _pssIsIronmanUnlockedMethod == null) return false;

            try
            {
                return _pssIsIronmanUnlockedMethod.Invoke(service, null) as bool? ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Get the display name for a profile.
        /// </summary>
        public static string GetProfileDisplayName(object profile)
        {
            if (profile == null) return "Unknown";
            EnsureTypesCached();

            var service = GetProfilesService();
            if (service == null || _pssGetProfileDisplayNameMethod == null)
            {
                // Fallback to reading name field directly
                return GetProfileName(profile);
            }

            try
            {
                return _pssGetProfileDisplayNameMethod.Invoke(service, new[] { profile }) as string ?? "Unknown";
            }
            catch
            {
                return GetProfileName(profile);
            }
        }

        /// <summary>
        /// Get the raw name field from a profile.
        /// </summary>
        public static string GetProfileName(object profile)
        {
            if (profile == null || _pdNameField == null) return "Unknown";

            try
            {
                var name = _pdNameField.GetValue(profile) as string;
                return string.IsNullOrEmpty(name) ? "Unnamed" : name;
            }
            catch
            {
                return "Unknown";
            }
        }

        /// <summary>
        /// Get the max number of profiles for a mode.
        /// </summary>
        public static int GetMaxProfiles(bool ironman)
        {
            return ironman ? 3 : 5;
        }

        // ========================================
        // PROFILE ACTIONS
        // ========================================

        /// <summary>
        /// Create a new profile.
        /// </summary>
        public static bool CreateNewProfile(bool ironman)
        {
            EnsureTypesCached();
            var service = GetProfilesService();
            if (service == null || _pssCreateNewProfileMethod == null) return false;

            try
            {
                // Count existing profiles of this type
                var profiles = GetProfiles(ironman);
                int count = profiles.Count;
                string prefix = ironman ? "QH#" : "#";
                string name = $"{prefix}{count + 1}";

                _pssCreateNewProfileMethod.Invoke(service, new object[] { name, ironman });
                Debug.Log($"[ATSAccessibility] ProfilesReflection: Created new profile: {name}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ProfilesReflection: CreateNewProfile failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Rename a profile.
        /// </summary>
        public static bool RenameProfile(object profile, string newName)
        {
            if (profile == null || string.IsNullOrEmpty(newName)) return false;
            EnsureTypesCached();

            var service = GetProfilesService();
            if (service == null || _pssRenameProfileMethod == null) return false;

            try
            {
                _pssRenameProfileMethod.Invoke(service, new object[] { profile, newName });
                Debug.Log($"[ATSAccessibility] ProfilesReflection: Renamed profile to: {newName}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ProfilesReflection: RenameProfile failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Switch to a different profile.
        /// </summary>
        public static bool ChangeProfile(object profile)
        {
            if (profile == null) return false;
            EnsureTypesCached();

            var service = GetProfilesService();
            if (service == null || _pssChangeProfileMethod == null) return false;

            try
            {
                // This is async but we just invoke it and return
                _pssChangeProfileMethod.Invoke(service, new[] { profile });
                Debug.Log("[ATSAccessibility] ProfilesReflection: ChangeProfile invoked");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ProfilesReflection: ChangeProfile failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clear/reset a profile's progress.
        /// </summary>
        public static bool ClearProfile(object profile)
        {
            if (profile == null) return false;
            EnsureTypesCached();

            var service = GetProfilesService();
            if (service == null || _pssClearProfileMethod == null) return false;

            try
            {
                // This is async but we just invoke it and return
                _pssClearProfileMethod.Invoke(service, new[] { profile });
                Debug.Log("[ATSAccessibility] ProfilesReflection: ClearProfile invoked");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ProfilesReflection: ClearProfile failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Delete a profile. Cannot delete the default profile.
        /// </summary>
        public static bool RemoveProfile(object profile)
        {
            if (profile == null) return false;
            if (IsDefault(profile)) return false;
            EnsureTypesCached();

            var service = GetProfilesService();
            if (service == null || _pssRemoveProfileMethod == null) return false;

            try
            {
                // This is async but we just invoke it and return
                _pssRemoveProfileMethod.Invoke(service, new[] { profile });
                Debug.Log("[ATSAccessibility] ProfilesReflection: RemoveProfile invoked");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] ProfilesReflection: RemoveProfile failed: {ex.Message}");
                return false;
            }
        }
    }
}
