// =============================================================================
// HARMONY PATCHES REFERENCE - Working examples from ATS Accessibility Mod
// =============================================================================

using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace Reference
{
    /// <summary>
    /// Pattern for patching game methods via reflection (no direct references)
    /// </summary>
    public static class HarmonyPatchPatterns
    {
        // =====================================================================
        // POPUP PATCHES - Detect popup show/hide
        // =====================================================================

        private static Type _popupsServiceType = null;
        private static Type _popupBaseType = null;

        public static void ApplyPopupPatches(Harmony harmony)
        {
            // Find types using direct GetType (faster, safer than GetTypes())
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "Assembly-CSharp") continue;

                // Direct lookup - doesn't trigger all type initializers
                _popupsServiceType = assembly.GetType("Eremite.Services.PopupsService");
                _popupBaseType = assembly.GetType("Eremite.View.Popups.Popup");
                break;
            }

            if (_popupsServiceType != null)
            {
                // Patch PopupsService.PopupShown and PopupClosed
                PatchMethod(harmony, _popupsServiceType, "PopupShown", nameof(OnPopupShown_Postfix));
                PatchMethod(harmony, _popupsServiceType, "PopupClosed", nameof(OnPopupClosed_Postfix));
            }

            if (_popupBaseType != null)
            {
                // Backup: patch base Popup class methods
                PatchMethod(harmony, _popupBaseType, "AnimateShow", nameof(OnPopupAnimateShow_Postfix));
                PatchMethod(harmony, _popupBaseType, "Hide", nameof(OnPopupHide_Postfix));
            }
        }

        private static void PatchMethod(Harmony harmony, Type targetType, string methodName, string patchMethodName)
        {
            try
            {
                var method = targetType.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (method == null) return;

                var patchMethod = typeof(HarmonyPatchPatterns).GetMethod(patchMethodName,
                    BindingFlags.NonPublic | BindingFlags.Static);

                harmony.Patch(method, postfix: new HarmonyMethod(patchMethod));
            }
            catch (Exception ex)
            {
                Debug.LogError($"Patch failed for {methodName}: {ex.Message}");
            }
        }

        // Postfix handlers - __0 is first parameter, __instance is 'this'
        private static void OnPopupShown_Postfix(object __0)
        {
            // Guard: skip during scene loading
            if (Time.timeScale == 0 || !Application.isPlaying) return;

            if (__0 is MonoBehaviour mono)
            {
                string popupName = mono.GetType().Name;
                Debug.Log($"Popup opened: {popupName}");
                // Notify your event manager here
            }
        }

        private static void OnPopupClosed_Postfix(object __0)
        {
            if (Time.timeScale == 0 || !Application.isPlaying) return;

            if (__0 is MonoBehaviour mono)
            {
                string popupName = mono.GetType().Name;
                Debug.Log($"Popup closed: {popupName}");
            }
        }

        private static void OnPopupAnimateShow_Postfix(object __instance)
        {
            if (Time.timeScale == 0 || !Application.isPlaying) return;

            if (__instance is MonoBehaviour mono)
            {
                string popupName = mono.GetType().Name;
                Debug.Log($"Popup animate show: {popupName}");
            }
        }

        private static void OnPopupHide_Postfix(object __instance)
        {
            if (Time.timeScale == 0 || !Application.isPlaying) return;

            if (__instance is MonoBehaviour mono)
            {
                string popupName = mono.GetType().Name;
                Debug.Log($"Popup hide: {popupName}");
            }
        }

        // =====================================================================
        // DIALOGUE PATCHES - Detect tutorial tooltips and decision popups
        // =====================================================================

        private static Type _tutorialTooltipType = null;
        private static Type _decisionPopupType = null;

        public static void ApplyDialoguePatches(Harmony harmony)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "Assembly-CSharp") continue;

                _tutorialTooltipType = assembly.GetType("Eremite.Tutorial.Views.TutorialTooltip");
                _decisionPopupType = assembly.GetType("Eremite.View.DecisionPopup");
                break;
            }

            if (_tutorialTooltipType != null)
            {
                PatchMethod(harmony, _tutorialTooltipType, "Show", nameof(TutorialTooltip_Show_Postfix));
                PatchMethod(harmony, _tutorialTooltipType, "Hide", nameof(TutorialTooltip_Hide_Postfix));
            }

            if (_decisionPopupType != null)
            {
                PatchMethod(harmony, _decisionPopupType, "Show", nameof(DecisionPopup_Show_Postfix));
                PatchMethod(harmony, _decisionPopupType, "Confirm", nameof(DecisionPopup_Close_Postfix));
                PatchMethod(harmony, _decisionPopupType, "Cancel", nameof(DecisionPopup_Close_Postfix));
            }
        }

        private static void TutorialTooltip_Show_Postfix(object __instance)
        {
            if (__instance is MonoBehaviour mono)
            {
                var (title, body) = ExtractDialogueText(mono);
                Debug.Log($"Tutorial shown: {title}");
            }
        }

        private static void TutorialTooltip_Hide_Postfix()
        {
            if (Time.timeScale == 0 || !Application.isPlaying) return;
            Debug.Log("Tutorial hidden");
        }

        private static void DecisionPopup_Show_Postfix(object __instance)
        {
            if (__instance is MonoBehaviour mono)
            {
                var (title, body) = ExtractDialogueText(mono);
                Debug.Log($"Decision popup: {title}");
            }
        }

        private static void DecisionPopup_Close_Postfix()
        {
            if (Time.timeScale == 0 || !Application.isPlaying) return;
            Debug.Log("Decision popup closed");
        }

        /// <summary>
        /// Extract title and body text from dialogue by searching TMP_Text components
        /// </summary>
        private static (string title, string body) ExtractDialogueText(MonoBehaviour mb)
        {
            string title = "";
            string body = "";

            var texts = mb.GetComponentsInChildren<TMPro.TMP_Text>();
            foreach (var text in texts)
            {
                if (text == null || !text.gameObject.activeInHierarchy) continue;

                string content = text.text?.Trim() ?? "";
                if (string.IsNullOrEmpty(content)) continue;

                string objName = text.gameObject.name.ToLower();

                // Categorize by object name patterns
                if (objName.Contains("name") || objName.Contains("title") || objName.Contains("header"))
                {
                    title = content;
                }
                else if (objName.Contains("label") || objName.Contains("body") ||
                         objName.Contains("description") || objName.Contains("text"))
                {
                    if (string.IsNullOrEmpty(body) || content.Length > body.Length)
                        body = content;
                }
            }

            return (title, body);
        }

        // =====================================================================
        // TAB PATCHES - Detect tab switches
        // =====================================================================

        private static Type _tabsPanelType = null;

        public static void ApplyTabPatches(Harmony harmony)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != "Assembly-CSharp") continue;

                _tabsPanelType = assembly.GetType("Eremite.View.UI.TabsPanel");
                break;
            }

            if (_tabsPanelType != null)
            {
                PatchMethod(harmony, _tabsPanelType, "OnButtonClicked", nameof(TabsPanel_OnButtonClicked_Postfix));
            }
        }

        private static void TabsPanel_OnButtonClicked_Postfix(object __instance, object __0)
        {
            // __0 is the TabsButton that was clicked
            // Get the content GameObject from the button
            var buttonType = __0.GetType();
            var contentField = buttonType.GetField("content",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (contentField != null)
            {
                var content = contentField.GetValue(__0) as GameObject;
                Debug.Log($"Tab changed to: {content?.name}");
            }
        }
    }
}
