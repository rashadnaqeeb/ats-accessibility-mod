using BepInEx;
using HarmonyLib;
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace ATSAccessibility
{
    [BepInPlugin("com.accessibility.ats", "ATS Accessibility", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        private void Awake()
        {
            try
            {
                // CRITICAL: Set DLL directory FIRST before any other code
                // This allows Tolk.dll to be found when P/Invoke calls are made
                string modFolder = Path.GetDirectoryName(Info.Location);
                bool result = SetDllDirectory(modFolder);
                Logger.LogInfo($"SetDllDirectory({modFolder}): {result}");

                if (!result)
                {
                    int error = Marshal.GetLastWin32Error();
                    Logger.LogWarning($"SetDllDirectory failed with error code: {error}");
                }

                // Apply Harmony patches to block game input
                var harmony = new Harmony("com.ats.accessibility");
                harmony.PatchAll();
                Logger.LogInfo("Harmony patches applied");

                // Create persistent GameObject that survives scene transitions
                var go = new GameObject("ATSAccessibilityCore");
                go.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(go);
                go.AddComponent<AccessibilityCore>();

                Logger.LogInfo("ATS Accessibility mod initialized");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize: {ex}");
            }
        }
    }
}
