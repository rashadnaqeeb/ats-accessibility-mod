# InputPatches.cs

Harmony patches to block game input while allowing the mod to handle navigation.

Two input pipelines are blocked:
1. StandaloneInputModule (UI navigation) - blocked via Input.GetAxis/GetAxisRaw
2. InputService (game actions) - blocked via WasTriggered/IsTriggering

Also contains two camera-following patches: one for the world map camera (WorldCameraController)
and one for the settlement camera (CameraController).

---

## class InputPatches (static) (line 16)

### class InputGetAxisPatch (static, nested) (line 22)
Patch for Input.GetAxis — blocks "Horizontal" and "Vertical" axes when InputBlocker.IsBlocking.

#### Methods
- public static bool Prefix(string axisName, ref float __result) (line 23)

### class InputGetAxisRawPatch (static, nested) (line 38)
Patch for Input.GetAxisRaw — same blocking as InputGetAxisPatch.

#### Methods
- public static bool Prefix(string axisName, ref float __result) (line 40)

### class InputServiceWasTriggeredPatch (static, nested) (line 55)
Patch for InputService.WasTriggered — blocks all non-whitelisted actions. Also handles BlockCancelOnce.

#### Methods
- public static bool Prefix(InputAction action, ref bool __result) (line 57)

### class InputServiceIsTriggeringPatch (static, nested) (line 77)
Patch for InputService.IsTriggering — blocks continuous non-whitelisted actions.

#### Methods
- public static bool Prefix(InputAction action, ref bool __result) (line 80)

---

## class WorldCameraControllerUpdateMovementPatch (static) (line 97)

Postfix patch on WorldCameraController.UpdateMovement. Adds smooth target-following when
a Transform target is set (via the game's existing but unused `target` field).
Uses SmoothDamp with _smoothTime=0.5f and _maxSpeed=40f.

### Fields
- private static FieldInfo _targetField (line 98)
- private static FieldInfo _movementVelocityField (line 99)
- private static float _smoothTime = 0.5f (line 100)
- private static float _maxSpeed = 40f (line 101)

### Methods
- static MethodBase TargetMethod() (line 103)
  - Resolves Eremite.View.Cameras.WorldCameraController.UpdateMovement at runtime.
- public static void Postfix(object __instance) (line 108)

---

## class CameraControllerUpdateMovementPatch (static) (line 146)

Postfix patch on CameraController.UpdateMovement for the settlement camera.
Unlike WorldCameraController, the game clears its `target` field on keyboard input, so this
patch stores its own target in a static field the game cannot clear.
Uses SmoothDamp with _smoothTime=0.3f and _maxSpeed=50f.

### Fields
- private static FieldInfo _movementVelocityField (line 147)
- private static float _smoothTime = 0.3f (line 148)
- private static float _maxSpeed = 50f (line 149)
- private static Transform _accessibilityTarget (line 152)
  - Our own target storage — the game can't clear this.
- private static Vector3 _velocity = Vector3.zero (line 153)

### Methods
- public static void SetTarget(Transform target) (line 159)
  - Set camera target for accessibility navigation. Resets velocity on change.
- public static void ClearTarget() (line 167)
  - Clear the camera target (e.g., when exiting map navigation).
- static MethodBase TargetMethod() (line 172)
  - Resolves Eremite.View.Cameras.CameraController.UpdateMovement at runtime.
- public static void Postfix(object __instance) (line 177)
  - Follows _accessibilityTarget using SmoothDamp on X/Z; preserves camera Y.
