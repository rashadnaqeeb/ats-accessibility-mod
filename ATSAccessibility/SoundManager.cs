using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Centralized sound playback for accessibility mod.
	/// Triggers game sounds when mod actions bypass normal game flow.
	/// </summary>
	public static class SoundManager {
		// Infrastructure reflection metadata
		private static PropertyInfo _soundsManagerProperty;
		private static PropertyInfo _soundsProperty;
		private static MethodInfo _playSoundEffectMethod;
		private static MethodInfo _playButtonSoundMethod;
		private static MethodInfo _playFailedSoundMethod;
		private static bool _cached;

		// Sound properties from SoundReferences, keyed by game property name
		private static readonly Dictionary<string, PropertyInfo> _sounds = new Dictionary<string, PropertyInfo>();

		// All SoundReferences property names to cache. To add a new sound:
		// 1. Add the property name here
		// 2. Add a one-liner public method below
		private static readonly string[] SoundPropertyNames = {
			"BuildingDestroyed", "BuildingPlaced", "BuildingRotated",
			"BuildingMoveStarted", "BuildingMoveFinished",
			"BuildingSleep", "BuildingWakeUp",
			"BuildingRecipeOn", "BuildingRecipeOff",
			"BuildingPanelShow", "BuildingPanelHide",
			"BuildingFireButtonStart",
			"RainpunkUnlock", "RainpunkStopButton",
			"PopupShow", "HomePopupHide", "ConsumptionPopupShow", "TraderPanelOpened",
			"SeasonRewardsPopupSlot", "CapitalUpgradeBought",
			"RelicStartWithWorkingEffects", "RelicStopWithWorkingEffects",
			"PortStartClick", "PortCancelClick", "PortRewardsClick",
			"TraderTransactionCompleted", "TraderAssault",
			"ResourceRemoved",
			"PortNetsRetrived",
			"SealOrderDeliver",
		};

		private static void EnsureCached() {
			if (_cached) return;
			_cached = true;

			ReflectionHelper.InitCache("SoundManager", assembly => {
				var mainControllerType = assembly.GetType("Eremite.Controller.MainController");
				if (mainControllerType != null) {
					_soundsManagerProperty = mainControllerType.GetProperty("SoundsManager", GameReflection.PublicInstance);
					_soundsProperty = mainControllerType.GetProperty("Sounds", GameReflection.PublicInstance);
				}

				var soundsManagerType = assembly.GetType("Eremite.Sound.ISoundsManager");
				if (soundsManagerType != null) {
					_playSoundEffectMethod = soundsManagerType.GetMethod("PlaySoundEffect", GameReflection.PublicInstance);
					_playButtonSoundMethod = soundsManagerType.GetMethod("PlayButtonSound", GameReflection.PublicInstance);
					_playFailedSoundMethod = soundsManagerType.GetMethod("PlayFailedSound", GameReflection.PublicInstance);
				}

				var soundReferencesType = assembly.GetType("Eremite.Model.Sound.SoundReferences");
				if (soundReferencesType != null) {
					foreach (var name in SoundPropertyNames) {
						var prop = soundReferencesType.GetProperty(name, GameReflection.PublicInstance);
						if (prop != null)
							_sounds[name] = prop;
					}
				}
			});
		}

		private static object GetSoundsManager() {
			EnsureCached();
			var mainController = GameReflection.GetMainControllerInstance();
			return ReflectionHelper.GetProp(_soundsManagerProperty, mainController);
		}

		private static void PlaySound(string soundName) {
			EnsureCached();
			if (_sounds.TryGetValue(soundName, out var prop))
				PlaySoundRef(prop);
		}

		private static void PlaySoundRef(PropertyInfo soundProperty) {
			var mainController = GameReflection.GetMainControllerInstance();
			var soundsManager = ReflectionHelper.GetProp(_soundsManagerProperty, mainController);
			var sounds = ReflectionHelper.GetProp(_soundsProperty, mainController);
			var sound = ReflectionHelper.GetProp(soundProperty, sounds);
			ReflectionHelper.Invoke(_playSoundEffectMethod, soundsManager, sound);
		}

		// ========================================
		// UI SOUNDS
		// ========================================

		public static void PlayButtonClick() {
			ReflectionHelper.InvokeVoid(_playButtonSoundMethod, GetSoundsManager());
		}

		public static void PlayFailed() {
			ReflectionHelper.InvokeVoid(_playFailedSoundMethod, GetSoundsManager());
		}

		// ========================================
		// BUILDING SOUNDS
		// ========================================

		public static void PlayBuildingDestroyed() => PlaySound("BuildingDestroyed");
		public static void PlayBuildingPlaced() => PlaySound("BuildingPlaced");
		public static void PlayBuildingRotated() => PlaySound("BuildingRotated");
		public static void PlayBuildingMoveStarted() => PlaySound("BuildingMoveStarted");
		public static void PlayBuildingMoveFinished() => PlaySound("BuildingMoveFinished");
		public static void PlayBuildingSleep() => PlaySound("BuildingSleep");
		public static void PlayBuildingWakeUp() => PlaySound("BuildingWakeUp");
		public static void PlayRecipeOn() => PlaySound("BuildingRecipeOn");
		public static void PlayRecipeOff() => PlaySound("BuildingRecipeOff");
		/// <summary>Play the fire button start sound (used for sacrifice enable).</summary>
		public static void PlayBuildingFireButtonStart() => PlaySound("BuildingFireButtonStart");
		public static void PlayBuildingPanelShow() => PlaySound("BuildingPanelShow");
		public static void PlayBuildingPanelHide() => PlaySound("BuildingPanelHide");

		// ========================================
		// RAINPUNK SOUNDS
		// ========================================

		public static void PlayRainpunkUnlock() => PlaySound("RainpunkUnlock");
		public static void PlayRainpunkStop() => PlaySound("RainpunkStopButton");

		// ========================================
		// POPUP SOUNDS
		// ========================================

		public static void PlayHomePopupHide() => PlaySound("HomePopupHide");
		public static void PlayPopupShow() => PlaySound("PopupShow");
		public static void PlayConsumptionPopupShow() => PlaySound("ConsumptionPopupShow");
		public static void PlayTraderPanelOpened() => PlaySound("TraderPanelOpened");
		public static void PlaySeasonRewardsSlot() => PlaySound("SeasonRewardsPopupSlot");
		public static void PlayCapitalUpgradeBought() => PlaySound("CapitalUpgradeBought");

		// ========================================
		// RELIC & PORT SOUNDS
		// ========================================

		public static void PlayRelicStartWithWorkingEffects() => PlaySound("RelicStartWithWorkingEffects");
		public static void PlayRelicStopWithWorkingEffects() => PlaySound("RelicStopWithWorkingEffects");
		public static void PlayPortStartClick() => PlaySound("PortStartClick");
		public static void PlayPortCancelClick() => PlaySound("PortCancelClick");
		public static void PlayPortRewardsClick() => PlaySound("PortRewardsClick");

		// ========================================
		// TRADE SOUNDS
		// ========================================

		public static void PlayTraderTransactionCompleted() => PlaySound("TraderTransactionCompleted");
		public static void PlayTraderAssault() => PlaySound("TraderAssault");

		// ========================================
		// OTHER SOUNDS
		// ========================================

		public static void PlaySealOrderDeliver() => PlaySound("SealOrderDeliver");
		public static void PlayResourceRemoved() => PlaySound("ResourceRemoved");
		public static void PlayPortNetsRetrieved() => PlaySound("PortNetsRetrived");

		public static void PlaySoundEffect(object soundModel) {
			if (soundModel == null) return;
			ReflectionHelper.Invoke(_playSoundEffectMethod, GetSoundsManager(), soundModel);
		}

		// ========================================
		// CLIP-NAME SOUNDS
		// ========================================

		public static void PlayNewcomersBannerAccept() => PlaySoundByClipName("newcomers banner accept");
		public static void PlayReroll() => PlaySoundByClipName("reroll");
		public static void PlayDecline() => PlaySoundByClipName("decline");
		public static void PlayMenuRecipes() => PlaySoundByClipName("menu_recipes");
		public static void PlayMenuOrders() => PlaySoundByClipName("menu_orders");
		public static void PlayMenuTrends() => PlaySoundByClipName("trends_window");
		public static void PlayMenuTradeRoutes() => PlaySoundByClipName("menu_trade_routes");

		// ========================================
		// CLIP-NAME INFRASTRUCTURE
		// ========================================

		private static Type _audioClipType;
		private static Type _audioSourceType;
		private static readonly Dictionary<string, object> _clipCache = new Dictionary<string, object>();

		// Volume access: MainController.AppServices.ClientPrefsService.EffectsVolume.Value
		private static PropertyInfo _appServicesProperty;
		private static PropertyInfo _clientPrefsServiceProperty;
		private static PropertyInfo _effectsVolumeProperty;
		private static PropertyInfo _reactiveValueProperty;
		private static bool _volumeCached;

		// Our own AudioSource to avoid conflicts with game's buttonAudioSource
		private static object _modAudioSource;
		private static MethodInfo _playOneShotWithVolumeMethod;

		private static object GetModAudioSource() {
			if (_modAudioSource == null) {
				if (_audioSourceType == null) {
					_audioSourceType = Type.GetType("UnityEngine.AudioSource, UnityEngine.AudioModule");
					if (_audioSourceType == null) {
						foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
							_audioSourceType = asm.GetType("UnityEngine.AudioSource");
							if (_audioSourceType != null) break;
						}
					}
				}
				if (_audioSourceType == null) return null;

				var go = GameObject.Find("ATSAccessibilityCore");
				if (go == null) {
					go = new GameObject("ATSAccessibilityCore_Audio");
					go.hideFlags = HideFlags.HideAndDontSave;
					UnityEngine.Object.DontDestroyOnLoad(go);
				}

				_modAudioSource = go.GetComponent(_audioSourceType);
				if (_modAudioSource == null)
					_modAudioSource = go.AddComponent(_audioSourceType);
			}
			return _modAudioSource;
		}

		private static void EnsureVolumeCached() {
			if (_volumeCached) return;
			_volumeCached = true;

			ReflectionHelper.InitCache("SoundManager.Volume", assembly => {
				var mainControllerType = assembly.GetType("Eremite.Controller.MainController");
				_appServicesProperty = mainControllerType?.GetProperty("AppServices", GameReflection.PublicInstance);

				var servicesType = assembly.GetType("Eremite.Services.IServices");
				_clientPrefsServiceProperty = servicesType?.GetProperty("ClientPrefsService", GameReflection.PublicInstance);

				var clientPrefsType = assembly.GetType("Eremite.Services.IClientPrefsService");
				_effectsVolumeProperty = clientPrefsType?.GetProperty("EffectsVolume", GameReflection.PublicInstance);

				if (_effectsVolumeProperty != null)
					_reactiveValueProperty = _effectsVolumeProperty.PropertyType.GetProperty("Value", GameReflection.PublicInstance);
			});
		}

		private static float GetEffectsVolume() {
			EnsureVolumeCached();
			var mainController = GameReflection.GetMainControllerInstance();
			var appServices = ReflectionHelper.GetProp(_appServicesProperty, mainController);
			var clientPrefs = ReflectionHelper.GetProp(_clientPrefsServiceProperty, appServices);
			var effectsVolume = ReflectionHelper.GetProp(_effectsVolumeProperty, clientPrefs);
			var value = ReflectionHelper.GetProp(_reactiveValueProperty, effectsVolume);
			return value is float f ? f : 1f;
		}

		/// <summary>
		/// Play a sound by finding its AudioClip by name.
		/// Uses our own AudioSource to avoid conflicts with game's sound system.
		/// </summary>
		private static void PlaySoundByClipName(string clipName) {
			try {
				if (_audioClipType == null) {
					_audioClipType = Type.GetType("UnityEngine.AudioClip, UnityEngine.AudioModule");
					if (_audioClipType == null) {
						foreach (var asm in AppDomain.CurrentDomain.GetAssemblies()) {
							_audioClipType = asm.GetType("UnityEngine.AudioClip");
							if (_audioClipType != null) break;
						}
					}
				}

				if (_audioClipType == null) {
					Debug.LogWarning("[ATSAccessibility] AudioClip type not found");
					return;
				}

				// Check cache first, then search loaded clips
				if (!_clipCache.TryGetValue(clipName, out object targetClip)) {
					var clips = Resources.FindObjectsOfTypeAll(_audioClipType);
					foreach (var clip in clips) {
						if (clip != null && clip.name == clipName) {
							targetClip = clip;
							break;
						}
					}

					// Cache result (including null) to avoid repeated searches
					_clipCache[clipName] = targetClip;

					if (targetClip == null) {
						Debug.LogWarning($"[ATSAccessibility] Sound clip not found: {clipName}");
						return;
					}
				} else if (targetClip == null) {
					// Cached as not found
					return;
				}

				// Play on our own AudioSource via PlayOneShot with game's effects volume
				var source = GetModAudioSource();
				if (source == null) return;

				if (_playOneShotWithVolumeMethod == null)
					_playOneShotWithVolumeMethod = _audioSourceType.GetMethod("PlayOneShot",
						new[] { _audioClipType, typeof(float) });

				float volume = GetEffectsVolume();
				_playOneShotWithVolumeMethod?.Invoke(source, new object[] { targetClip, volume });
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] PlaySoundByClipName({clipName}) failed: {ex.Message}");
			}
		}
	}
}
