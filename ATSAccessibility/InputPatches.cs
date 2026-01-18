using System.Reflection;
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

                // Block Cancel once when flag is set (used by StatsPanel close)
                if (action != null && action.name == "Cancel" && InputBlocker.BlockCancelOnce)
                {
                    Debug.Log("[ATSAccessibility] DEBUG: Blocking Cancel action once");
                    InputBlocker.BlockCancelOnce = false;  // Reset after blocking
                    __result = false;
                    return false;
                }

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

    /// <summary>
    /// Patch WorldCameraController.UpdateMovement to add target-following behavior.
    /// The game's world map camera has a target field but doesn't use it.
    /// This patch adds smooth following when target is set (for accessibility navigation).
    /// </summary>
    [HarmonyPatch]
    public static class WorldCameraControllerUpdateMovementPatch
    {
        private static FieldInfo _targetField;
        private static FieldInfo _movementVelocityField;
        private static float _smoothTime = 0.5f;
        private static float _maxSpeed = 40f;

        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Eremite.View.Cameras.WorldCameraController");
            return AccessTools.Method(type, "UpdateMovement");
        }

        public static void Postfix(object __instance)
        {
            if (__instance == null) return;

            try
            {
                // Cache fields
                if (_targetField == null)
                {
                    var type = __instance.GetType();
                    _targetField = type.GetField("target", BindingFlags.Public | BindingFlags.Instance);
                    _movementVelocityField = type.GetField("movementVelocity", BindingFlags.Public | BindingFlags.Instance);
                }

                var target = _targetField?.GetValue(__instance) as Transform;
                if (target == null) return;

                var transform = (__instance as MonoBehaviour)?.transform;
                if (transform == null) return;

                // Get current velocity
                var velocity = (Vector3)(_movementVelocityField?.GetValue(__instance) ?? Vector3.zero);

                // Smooth move toward target (preserve Z)
                var targetPos = new Vector3(target.position.x, target.position.y, transform.position.z);
                transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref velocity,
                    _smoothTime, _maxSpeed, Time.unscaledDeltaTime);

                // Store velocity back
                _movementVelocityField?.SetValue(__instance, velocity);
            }
            catch { }
        }
    }
}
