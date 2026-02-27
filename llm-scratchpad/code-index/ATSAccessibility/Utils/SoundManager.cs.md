# SoundManager.cs
Centralized sound playback for accessibility mod.
Triggers game sounds when mod actions bypass normal game flow.

## class SoundManager (line 12)

### Fields
- private static PropertyInfo _soundsManagerProperty (line 14)
- private static PropertyInfo _soundsProperty (line 15)
- private static MethodInfo _playSoundEffectMethod (line 16)
- private static MethodInfo _playButtonSoundMethod (line 17)
- private static MethodInfo _playFailedSoundMethod (line 18)
- private static bool _cached (line 19)
- private static readonly Dictionary<string, PropertyInfo> _sounds (line 22)
  Keyed by SoundReferences property name; populated from SoundPropertyNames array.
- private static readonly string[] SoundPropertyNames (line 27)
  All SoundReferences property names to cache. Add new sound name here + one public method below.
- private static Type _audioClipType (line 188)
- private static Type _audioSourceType (line 189)
- private static readonly Dictionary<string, object> _clipCache (line 190)
- private static PropertyInfo _appServicesProperty (line 193)
- private static PropertyInfo _clientPrefsServiceProperty (line 194)
- private static PropertyInfo _effectsVolumeProperty (line 195)
- private static PropertyInfo _reactiveValueProperty (line 196)
- private static bool _volumeCached (line 197)
- private static object _modAudioSource (line 200)
  Our own AudioSource GameObject to avoid conflicts with game's buttonAudioSource.
- private static MethodInfo _playOneShotWithVolumeMethod (line 201)

### Methods
- private static void EnsureCached() (line 45)
- private static object GetSoundsManager() (line 74)
- private static void PlaySound(string soundName) (line 80)
- private static void PlaySoundRef(PropertyInfo soundProperty) (line 86)
- public static void PlayButtonClick() (line 98)
- public static void PlayFailed() (line 102)
- public static void PlayBuildingDestroyed() (line 110)
- public static void PlayBuildingPlaced() (line 111)
- public static void PlayBuildingRotated() (line 112)
- public static void PlayBuildingMoveStarted() (line 113)
- public static void PlayBuildingMoveFinished() (line 114)
- public static void PlayBuildingSleep() (line 115)
- public static void PlayBuildingWakeUp() (line 116)
- public static void PlayRecipeOn() (line 117)
- public static void PlayRecipeOff() (line 118)
- public static void PlayBuildingFireButtonStart() (line 120)
  Play the fire button start sound (used for sacrifice enable).
- public static void PlayBuildingPanelShow() (line 121)
- public static void PlayBuildingPanelHide() (line 122)
- public static void PlayRainpunkUnlock() (line 128)
- public static void PlayRainpunkStop() (line 129)
- public static void PlayHomePopupHide() (line 135)
- public static void PlayPopupShow() (line 136)
- public static void PlayConsumptionPopupShow() (line 137)
- public static void PlayTraderPanelOpened() (line 138)
- public static void PlaySeasonRewardsSlot() (line 139)
- public static void PlayCapitalUpgradeBought() (line 140)
- public static void PlayRelicStartWithWorkingEffects() (line 146)
- public static void PlayRelicStopWithWorkingEffects() (line 147)
- public static void PlayPortStartClick() (line 148)
- public static void PlayPortCancelClick() (line 149)
- public static void PlayPortRewardsClick() (line 150)
- public static void PlayTraderTransactionCompleted() (line 156)
- public static void PlayTraderAssault() (line 157)
- public static void PlaySealOrderDeliver() (line 163)
- public static void PlayResourceRemoved() (line 164)
- public static void PlayPortNetsRetrieved() (line 165)
- public static void PlaySoundEffect(object soundModel) (line 167)
  Play an arbitrary sound model object directly.
- public static void PlayNewcomersBannerAccept() (line 176)
- public static void PlayReroll() (line 177)
- public static void PlayDecline() (line 178)
- public static void PlayMenuRecipes() (line 179)
- public static void PlayMenuOrders() (line 180)
- public static void PlayMenuTrends() (line 181)
- public static void PlayMenuTradeRoutes() (line 182)
- private static object GetModAudioSource() (line 203)
  Creates or reuses an AudioSource on the ATSAccessibilityCore GameObject.
- private static void EnsureVolumeCached() (line 230)
- private static float GetEffectsVolume() (line 249)
  Reads volume via MainController.AppServices.ClientPrefsService.EffectsVolume.Value chain.
- private static void PlaySoundByClipName(string clipName) (line 263)
  Finds AudioClip by name via Resources.FindObjectsOfTypeAll, caches result, then plays via PlayOneShot with game's effects volume on the mod's own AudioSource.
