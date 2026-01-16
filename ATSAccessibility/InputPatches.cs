using Eremite.Services;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ATSAccessibility
{
    /// <summary>
    /// Harmony patches to block game input while allowing our mod to handle navigation.
    ///
    /// Two input pipelines are blocked:
    /// 1. StandaloneInputModule (UI navigation) - blocked via Input.GetAxis/GetAxisRaw
    /// 2. InputService (game actions) - blocked via WasTriggered/IsTriggering
    /// </summary>
    public static class InputPatches
    {
        /// <summary>
        /// Patch Input.GetAxis to block navigation axes (Horizontal/Vertical).
        /// This prevents StandaloneInputModule from processing arrow key navigation.
        /// </summary>
        [HarmonyPatch(typeof(Input), nameof(Input.GetAxis))]
        public static class InputGetAxisPatch
        {
            public static bool Prefix(string axisName, ref float __result)
            {
                if (!InputBlocker.IsBlocking) return true;

                // Block navigation axes
                if (axisName == "Horizontal" || axisName == "Vertical")
                {
                    __result = 0f;
                    return false; // Skip original
                }
                return true; // Let other axes through
            }
        }

        /// <summary>
        /// Patch Input.GetAxisRaw for completeness (some code paths use this instead).
        /// </summary>
        [HarmonyPatch(typeof(Input), nameof(Input.GetAxisRaw))]
        public static class InputGetAxisRawPatch
        {
            public static bool Prefix(string axisName, ref float __result)
            {
                if (!InputBlocker.IsBlocking) return true;

                if (axisName == "Horizontal" || axisName == "Vertical")
                {
                    __result = 0f;
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Patch InputService.WasTriggered to block game actions except whitelisted ones.
        /// This blocks camera movement, shortcuts, and other game hotkeys.
        /// </summary>
        [HarmonyPatch(typeof(InputService), nameof(InputService.WasTriggered))]
        public static class InputServiceWasTriggeredPatch
        {
            public static bool Prefix(InputAction action, ref bool __result)
            {
                if (!InputBlocker.IsBlocking) return true;

                // Allow whitelisted actions through
                if (InputBlocker.IsActionWhitelisted(action)) return true;

                __result = false;
                return false; // Skip original
            }
        }

        /// <summary>
        /// Patch InputService.IsTriggering to block continuous game actions.
        /// </summary>
        [HarmonyPatch(typeof(InputService), nameof(InputService.IsTriggering))]
        public static class InputServiceIsTriggeringPatch
        {
            public static bool Prefix(InputAction action, ref bool __result)
            {
                if (!InputBlocker.IsBlocking) return true;

                if (InputBlocker.IsActionWhitelisted(action)) return true;

                __result = false;
                return false;
            }
        }
    }
}
