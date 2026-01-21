using System;
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
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] WorldCameraController.UpdateMovement patch failed: {ex.Message}"); }
        }
    }

    /// <summary>
    /// Patch CameraController.UpdateMovement to add target-following behavior for settlement map.
    /// Unlike WorldCameraController, the game's CameraController DOES clear the target field
    /// when keyboard input is detected. So we store our own target in a static field that
    /// the game can't clear, and implement following in the Postfix.
    /// </summary>
    [HarmonyPatch]
    public static class CameraControllerUpdateMovementPatch
    {
        private static FieldInfo _movementVelocityField;
        private static float _smoothTime = 0.3f;
        private static float _maxSpeed = 50f;

        // Our own target storage - the game can't clear this
        private static Transform _accessibilityTarget;
        private static Vector3 _velocity = Vector3.zero;

        /// <summary>
        /// Set the camera target for accessibility navigation.
        /// This stores the target in our own static field that the game can't clear.
        /// </summary>
        public static void SetTarget(Transform target)
        {
            _accessibilityTarget = target;
            _velocity = Vector3.zero; // Reset velocity when target changes
        }

        /// <summary>
        /// Clear the camera target (e.g., when exiting map navigation).
        /// </summary>
        public static void ClearTarget()
        {
            _accessibilityTarget = null;
            _velocity = Vector3.zero;
        }

        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Eremite.View.Cameras.CameraController");
            return AccessTools.Method(type, "UpdateMovement");
        }

        public static void Postfix(object __instance)
        {
            if (__instance == null || _accessibilityTarget == null) return;

            try
            {
                // Cache velocity field for smooth movement
                if (_movementVelocityField == null)
                {
                    var type = __instance.GetType();
                    _movementVelocityField = type.GetField("movementVelocity", BindingFlags.Public | BindingFlags.Instance);
                }

                var transform = (__instance as MonoBehaviour)?.transform;
                if (transform == null) return;

                // Get the game's velocity for smooth integration
                var gameVelocity = (Vector3)(_movementVelocityField?.GetValue(__instance) ?? Vector3.zero);

                // Use our own velocity tracking for smooth following
                // The settlement camera is 3D, so we follow X/Z and let the game handle Y
                var targetPos = new Vector3(
                    _accessibilityTarget.position.x,
                    transform.position.y,  // Keep current height
                    _accessibilityTarget.position.z
                );

                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    targetPos,
                    ref _velocity,
                    _smoothTime,
                    _maxSpeed,
                    Time.unscaledDeltaTime
                );
            }
            catch (Exception ex) { Debug.LogWarning($"[ATSAccessibility] CameraController.UpdateMovement patch failed: {ex.Message}"); }
        }
    }
}
