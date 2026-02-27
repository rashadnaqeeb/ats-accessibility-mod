using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Reflection helpers for accessing RewardPickPopup (cornerstone selection) and
	/// CornerstonesLimitPickPopup (choose-one-to-remove) data and interaction.
	/// </summary>
	public static class CornerstoneReflection {
		// ========================================
		// DATA STRUCTURES
		// ========================================

		public class CornerstoneOption {
			public object Model;         // EffectModel
			public string DisplayName;   // Includes Mythic suffix
			public string Description;
			public string Rarity;        // "Common", "Rare", etc.
			public bool IsEthereal;
		}

		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		// IGameServices.CornerstonesService
		private static PropertyInfo _gsCornerstonesServiceProperty;

		// ICornerstonesService methods
		private static MethodInfo _csGetCurrentPickMethod;
		private static MethodInfo _csGetRerollsLeftMethod;
		private static MethodInfo _csCanExtendMethod;
		private static MethodInfo _csCanAffordExtendMethod;
		private static MethodInfo _csExtendMethod;
		private static MethodInfo _csGetDeclinePayoffMethod;
		private static MethodInfo _csRemoveFromActiveMethod;

		// RewardPickState fields
		private static FieldInfo _rpsOptionsField;
		private static FieldInfo _rpsViewConfigurationField;

		// EffectModel properties/fields
		private static PropertyInfo _emDisplayNameProperty;
		private static PropertyInfo _emDescriptionProperty;
		private static FieldInfo _emRarityField;
		private static FieldInfo _emIsEtherealField;
		private static MethodInfo _emRemoveMethod;

		// NPC Dialogue
		private static MethodInfo _settingsGetCornerstonesViewConfigMethod;
		private static FieldInfo _cvcNpcNameField;
		private static FieldInfo _cvcNpcDialogueField;

		// Extend cost (BiomeService → CurrentBiome → seasons → seasonRewardsExtendPrice)
		private static PropertyInfo _gsBiomeServiceProperty;
		private static PropertyInfo _bsCurrentBiomeProperty;
		private static FieldInfo _bmSeasonsField;
		private static FieldInfo _scExtendPriceField;

		// Good struct fields
		private static FieldInfo _goodNameField;
		private static FieldInfo _goodAmountField;

		// Popup methods
		private static MethodInfo _rpOnRewardPickedMethod;
		private static MethodInfo _rpRerollMethod;
		private static MethodInfo _rpSkipMethod;
		private static FieldInfo _rpDefaultConfigurationField;
		private static MethodInfo _clpFinishTaskMethod;
		private static MethodInfo _popupHideMethod;

		// Popup type detection
		private static Type _rewardPickPopupType;
		private static Type _cornerstonesLimitPickPopupType;

		private static bool _typesCached = false;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureTypesCached() {
			if (_typesCached) return;
			_typesCached = true;

			ReflectionHelper.InitCache("CornerstoneReflection", assembly => {
				CacheServiceTypes(assembly);
				CacheRewardPickStateTypes(assembly);
				CacheEffectModelTypes(assembly);
				CacheNpcDialogueTypes(assembly);
				CacheBiomeTypes(assembly);
				CacheGoodTypes(assembly);
				CachePopupTypes(assembly);
			});
		}

		private static void CacheServiceTypes(Assembly assembly) {
			var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
			if (gameServicesType != null) {
				_gsCornerstonesServiceProperty = gameServicesType.GetProperty("CornerstonesService",
					GameReflection.PublicInstance);
				_gsBiomeServiceProperty = gameServicesType.GetProperty("BiomeService",
					GameReflection.PublicInstance);
			}

			var csType = assembly.GetType("Eremite.Services.ICornerstonesService");
			if (csType != null) {
				_csGetCurrentPickMethod = csType.GetMethod("GetCurrentPick", GameReflection.PublicInstance);
				_csGetRerollsLeftMethod = csType.GetMethod("GetRerollsLeft", GameReflection.PublicInstance);
				_csCanExtendMethod = csType.GetMethod("CanExtend", GameReflection.PublicInstance);
				_csCanAffordExtendMethod = csType.GetMethod("CanAffordExtend", GameReflection.PublicInstance);
				_csExtendMethod = csType.GetMethod("Extend", GameReflection.PublicInstance);
				_csGetDeclinePayoffMethod = csType.GetMethod("GetDeclinePayoff", GameReflection.PublicInstance);

				// RemoveFromActive takes an EffectModel parameter
				var effectModelType = assembly.GetType("Eremite.Model.EffectModel");
				if (effectModelType != null) {
					_csRemoveFromActiveMethod = csType.GetMethod("RemoveFromActive",
						new[] { effectModelType });
				}
			}
		}

		private static void CacheRewardPickStateTypes(Assembly assembly) {
			var rpsType = assembly.GetType("Eremite.Model.RewardPickState");
			if (rpsType != null) {
				_rpsOptionsField = rpsType.GetField("options", GameReflection.PublicInstance);
				_rpsViewConfigurationField = rpsType.GetField("viewConfiguration", GameReflection.PublicInstance);
			}
		}

		private static void CacheEffectModelTypes(Assembly assembly) {
			var emType = assembly.GetType("Eremite.Model.EffectModel");
			if (emType != null) {
				_emDisplayNameProperty = emType.GetProperty("DisplayName", GameReflection.PublicInstance);
				_emDescriptionProperty = emType.GetProperty("Description", GameReflection.PublicInstance);
				_emRarityField = emType.GetField("rarity", GameReflection.PublicInstance);
				_emIsEtherealField = emType.GetField("isEthereal", GameReflection.PublicInstance);
				_emRemoveMethod = emType.GetMethod("Remove", GameReflection.PublicInstance);
			}
		}

		private static void CacheNpcDialogueTypes(Assembly assembly) {
			var settingsType = assembly.GetType("Eremite.Model.Settings");
			if (settingsType != null) {
				_settingsGetCornerstonesViewConfigMethod = settingsType.GetMethod(
					"GetCornerstonesViewConfiguration", new[] { typeof(string) });
			}

			var cvcType = assembly.GetType("Eremite.Model.ViewsConfigurations.CornerstonesViewConfiguration");
			if (cvcType != null) {
				_cvcNpcNameField = cvcType.GetField("npcName", GameReflection.PublicInstance);
				_cvcNpcDialogueField = cvcType.GetField("npcDialogue", GameReflection.PublicInstance);
			}
		}

		private static void CacheBiomeTypes(Assembly assembly) {
			var biomeServiceType = assembly.GetType("Eremite.Services.IBiomeService");
			if (biomeServiceType != null) {
				_bsCurrentBiomeProperty = biomeServiceType.GetProperty("CurrentBiome",
					GameReflection.PublicInstance);
			}

			var biomeModelType = assembly.GetType("Eremite.WorldMap.BiomeModel");
			if (biomeModelType != null) {
				_bmSeasonsField = biomeModelType.GetField("seasons", GameReflection.PublicInstance);
			}

			var seasonsType = assembly.GetType("Eremite.Model.Configs.SeasonsConfig");
			if (seasonsType != null) {
				_scExtendPriceField = seasonsType.GetField("seasonRewardsExtendPrice",
					GameReflection.PublicInstance);
			}
		}

		private static void CacheGoodTypes(Assembly assembly) {
			var goodType = assembly.GetType("Eremite.Model.Good");
			if (goodType != null) {
				_goodNameField = goodType.GetField("name", GameReflection.PublicInstance);
				_goodAmountField = goodType.GetField("amount", GameReflection.PublicInstance);
			}
		}

		private static void CachePopupTypes(Assembly assembly) {
			_rewardPickPopupType = assembly.GetType("Eremite.View.HUD.RewardPickPopup");
			_cornerstonesLimitPickPopupType = assembly.GetType("Eremite.View.Popups.CornerstonesLimitPick.CornerstonesLimitPickPopup");

			var rpType = assembly.GetType("Eremite.View.HUD.RewardPickPopup");
			if (rpType != null) {
				_rpOnRewardPickedMethod = rpType.GetMethod("OnRewardPicked",
					GameReflection.NonPublicInstance);
				_rpRerollMethod = rpType.GetMethod("Reroll",
					GameReflection.NonPublicInstance);
				_rpSkipMethod = rpType.GetMethod("Skip",
					GameReflection.NonPublicInstance);
				_rpDefaultConfigurationField = rpType.GetField("defaultConfiguration",
					GameReflection.NonPublicInstance);
			}

			var clpType = assembly.GetType("Eremite.View.Popups.CornerstonesLimitPick.CornerstonesLimitPickPopup");
			if (clpType != null) {
				_clpFinishTaskMethod = clpType.GetMethod("FinishTask",
					GameReflection.NonPublicInstance);
			}

			// Popup.Hide() is on the base Popup class
			var popupType = assembly.GetType("Eremite.View.Popups.Popup");
			if (popupType != null) {
				_popupHideMethod = popupType.GetMethod("Hide", GameReflection.PublicInstance);
			}
		}

		// ========================================
		// SERVICE ACCESS
		// ========================================

		private static object GetCornerstonesService() {
			EnsureTypesCached();
			return GameReflection.GetService(_gsCornerstonesServiceProperty);
		}

		private static object GetBiomeService() {
			EnsureTypesCached();
			return GameReflection.GetService(_gsBiomeServiceProperty);
		}

		private static object GetCurrentPick() {
			var service = GetCornerstonesService();
			return ReflectionHelper.Invoke(_csGetCurrentPickMethod, service);
		}

		// ========================================
		// POPUP DETECTION
		// ========================================

		public static bool IsRewardPickPopup(object popup) {
			if (popup == null) return false;
			EnsureTypesCached();
			return _rewardPickPopupType != null && _rewardPickPopupType.IsInstanceOfType(popup);
		}

		public static bool IsCornerstonesLimitPickPopup(object popup) {
			if (popup == null) return false;
			EnsureTypesCached();
			return _cornerstonesLimitPickPopupType != null && _cornerstonesLimitPickPopupType.IsInstanceOfType(popup);
		}

		// ========================================
		// EFFECT MODEL HELPERS
		// ========================================

		private static string GetEffectDisplayName(object effectModel) {
			return ReflectionHelper.GetPropString(_emDisplayNameProperty, effectModel);
		}

		private static string GetEffectDescription(object effectModel) {
			return ReflectionHelper.GetPropString(_emDescriptionProperty, effectModel);
		}

		private static string GetEffectRarity(object effectModel) {
			var rarity = ReflectionHelper.GetField(_emRarityField, effectModel);
			return rarity?.ToString() ?? "Unknown";
		}

		private static bool GetEffectIsEthereal(object effectModel) {
			return ReflectionHelper.GetBool(_emIsEtherealField, effectModel);
		}

		private static CornerstoneOption BuildOption(object effectModel) {
			if (effectModel == null) return null;

			return new CornerstoneOption {
				Model = effectModel,
				DisplayName = GetEffectDisplayName(effectModel) ?? "Unknown",
				Description = GetEffectDescription(effectModel) ?? "",
				Rarity = GetEffectRarity(effectModel),
				IsEthereal = GetEffectIsEthereal(effectModel)
			};
		}

		// ========================================
		// CURRENT OPTIONS
		// ========================================

		/// <summary>
		/// Get the current cornerstone options from the active pick state.
		/// </summary>
		public static List<CornerstoneOption> GetCurrentOptions() {
			EnsureTypesCached();

			var result = new List<CornerstoneOption>();
			var pickState = GetCurrentPick();
			if (pickState == null || _rpsOptionsField == null) return result;

			try {
				var options = ReflectionHelper.GetField(_rpsOptionsField, pickState) as List<string>;
				if (options == null || options.Count == 0) return result;

				foreach (var effectName in options) {
					if (string.IsNullOrEmpty(effectName)) continue;

					var effectModel = GameReflection.GetEffectModel(effectName);
					if (effectModel == null) continue;

					var option = BuildOption(effectModel);
					if (option != null)
						result.Add(option);
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CornerstoneReflection: GetCurrentOptions failed: {ex.Message}");
			}

			return result;
		}

		// ========================================
		// NPC DIALOGUE
		// ========================================

		/// <summary>
		/// Get the NPC name and dialogue text for the current pick.
		/// Falls back to the popup's defaultConfiguration if viewConfiguration is empty.
		/// </summary>
		public static (string npcName, string dialogue) GetNpcDialogue(object popup) {
			EnsureTypesCached();

			try {
				var pickState = GetCurrentPick();
				object viewConfig = null;

				if (pickState != null) {
					var configName = ReflectionHelper.GetString(_rpsViewConfigurationField, pickState);
					if (!string.IsNullOrEmpty(configName)) {
						var settings = GameReflection.GetSettings();
						viewConfig = ReflectionHelper.Invoke(
							_settingsGetCornerstonesViewConfigMethod, settings, configName);
					}
				}

				// Fallback to popup's defaultConfiguration
				if (viewConfig == null && popup != null) {
					viewConfig = ReflectionHelper.GetField(_rpDefaultConfigurationField, popup);
				}

				if (viewConfig == null) return ("", "");

				string npcName = ReflectionHelper.GetLocaString(_cvcNpcNameField, viewConfig) ?? "";
				string dialogue = ReflectionHelper.GetLocaString(_cvcNpcDialogueField, viewConfig) ?? "";

				return (npcName, dialogue);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CornerstoneReflection: GetNpcDialogue failed: {ex.Message}");
				return ("", "");
			}
		}

		// ========================================
		// PICKING
		// ========================================

		/// <summary>
		/// Pick a cornerstone by invoking the popup's OnRewardPicked method.
		/// This triggers the async Pick flow (including limit check if needed).
		/// </summary>
		public static bool PickCornerstone(object popup, object effectModel) {
			if (popup == null || effectModel == null) return false;
			EnsureTypesCached();
			return ReflectionHelper.InvokeVoid(_rpOnRewardPickedMethod, popup, effectModel);
		}

		// ========================================
		// SKIP / DECLINE
		// ========================================

		/// <summary>
		/// Skip the current cornerstone pick (decline).
		/// </summary>
		public static bool Skip(object popup) {
			if (popup == null) return false;
			EnsureTypesCached();
			return ReflectionHelper.InvokeVoid(_rpSkipMethod, popup);
		}

		/// <summary>
		/// Get the decline payoff (good name and amount received for skipping).
		/// </summary>
		public static (int amount, string goodDisplayName) GetDeclinePayoff() {
			EnsureTypesCached();
			var service = GetCornerstonesService();
			var good = ReflectionHelper.Invoke(_csGetDeclinePayoffMethod, service);
			if (good == null) return (0, "Unknown");

			var name = ReflectionHelper.GetString(_goodNameField, good) ?? "";
			var amount = ReflectionHelper.GetInt(_goodAmountField, good);
			var displayName = GameReflection.GetGoodDisplayName(name);

			return (amount, displayName);
		}

		// ========================================
		// REROLL
		// ========================================

		/// <summary>
		/// Get the number of rerolls remaining.
		/// </summary>
		public static int GetRerollsLeft() {
			EnsureTypesCached();
			var service = GetCornerstonesService();
			return ReflectionHelper.InvokeInt(_csGetRerollsLeftMethod, service);
		}

		/// <summary>
		/// Reroll the current options via the popup's Reroll method.
		/// This keeps the popup UI in sync (updates slots and reroll button).
		/// </summary>
		public static bool Reroll(object popup) {
			if (popup == null) return false;
			EnsureTypesCached();
			return ReflectionHelper.InvokeVoid(_rpRerollMethod, popup);
		}

		// ========================================
		// EXTEND
		// ========================================

		/// <summary>
		/// Check if extending is available.
		/// </summary>
		public static bool CanExtend() {
			EnsureTypesCached();
			var service = GetCornerstonesService();
			return ReflectionHelper.InvokeBool(_csCanExtendMethod, service);
		}

		/// <summary>
		/// Check if the player can afford to extend.
		/// </summary>
		public static bool CanAffordExtend() {
			EnsureTypesCached();
			var service = GetCornerstonesService();
			return ReflectionHelper.InvokeBool(_csCanAffordExtendMethod, service);
		}

		/// <summary>
		/// Extend the current options (add one more cornerstone choice).
		/// </summary>
		public static bool Extend() {
			EnsureTypesCached();
			var service = GetCornerstonesService();
			return ReflectionHelper.InvokeVoid(_csExtendMethod, service);
		}

		/// <summary>
		/// Get the extend cost as (amount, good display name).
		/// Reads from BiomeService.CurrentBiome.seasons.seasonRewardsExtendPrice.
		/// </summary>
		public static (int amount, string goodDisplayName) GetExtendCost() {
			EnsureTypesCached();

			var biomeService = GetBiomeService();
			var biome = ReflectionHelper.GetProp(_bsCurrentBiomeProperty, biomeService);
			var seasons = ReflectionHelper.GetField(_bmSeasonsField, biome);
			var extendPrice = ReflectionHelper.GetField(_scExtendPriceField, seasons);
			if (extendPrice == null) return (0, "Unknown");

			var amount = ReflectionHelper.GetInt(GameReflection.GoodRefAmountField, extendPrice);
			var goodModel = ReflectionHelper.GetField(GameReflection.GoodRefGoodField, extendPrice);
			var displayName = goodModel != null
				? (GameReflection.GetDisplayName(goodModel) ?? "Unknown")
				: "Unknown";

			return (amount, displayName);
		}

		// ========================================
		// LIMIT POPUP - ACTIVE CORNERSTONES
		// ========================================

		/// <summary>
		/// Get all active cornerstones as CornerstoneOption objects.
		/// </summary>
		public static List<CornerstoneOption> GetActiveCornerstones() {
			EnsureTypesCached();

			var result = new List<CornerstoneOption>();
			var names = GameReflection.GetActiveCornerstones();
			if (names == null) return result;

			foreach (var name in names) {
				if (string.IsNullOrEmpty(name)) continue;

				var effectModel = GameReflection.GetEffectModel(name);
				if (effectModel == null) continue;

				var option = BuildOption(effectModel);
				if (option != null)
					result.Add(option);
			}

			return result;
		}

		/// <summary>
		/// Remove a cornerstone and confirm the limit popup (resolves the async task as true).
		/// </summary>
		public static bool RemoveAndConfirm(object limitPopup, object effectModel) {
			if (limitPopup == null || effectModel == null) return false;
			EnsureTypesCached();

			try {
				// service.RemoveFromActive(effectModel)
				var service = GetCornerstonesService();
				ReflectionHelper.InvokeVoid(_csRemoveFromActiveMethod, service, effectModel);

				// effectModel.Remove() - has optional params, pass defaults
				if (_emRemoveMethod != null) {
					var removeParams = _emRemoveMethod.GetParameters();
					var removeArgs = new object[removeParams.Length];
					for (int i = 0; i < removeParams.Length; i++)
						removeArgs[i] = removeParams[i].DefaultValue;
					_emRemoveMethod.Invoke(effectModel, removeArgs);
				}

				// popup.FinishTask(true)
				ReflectionHelper.InvokeVoid(_clpFinishTaskMethod, limitPopup, true);

				// popup.Hide()
				ReflectionHelper.InvokeVoid(_popupHideMethod, limitPopup);

				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] CornerstoneReflection: RemoveAndConfirm failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Cancel the limit popup (resolves the async task as false).
		/// </summary>
		public static bool CancelLimitPopup(object limitPopup) {
			if (limitPopup == null) return false;
			EnsureTypesCached();

			// popup.FinishTask(false)
			ReflectionHelper.InvokeVoid(_clpFinishTaskMethod, limitPopup, false);

			// popup.Hide()
			ReflectionHelper.InvokeVoid(_popupHideMethod, limitPopup);

			return true;
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(CornerstoneReflection), "CornerstoneReflection");
		}
	}
}
