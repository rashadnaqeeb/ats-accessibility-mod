using ATSAccessibility.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to capital upgrade popup internals.
	///
	/// CRITICAL RULES:
	/// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
	/// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
	/// - All public methods return fresh values by querying through cached PropertyInfo
	/// </summary>
	public static class CapitalUpgradeReflection {
		public enum UpgradeStatus { Unlocked, Buyable, TooExpensive, LevelRequired, Locked }

		public struct StructureInfo {
			public string Name;
			public int TotalUpgrades;
			public int UnlockedCount;
			public object StructureObj;
		}

		public struct UpgradeInfo {
			public string Name;
			public UpgradeStatus Status;
			public int RequiredLevel;
			public string PriceText;
			public object UpgradeObj;
			public object StructureObj;
		}

		public struct RewardInfo {
			public string Name;
			public string Description;
		}

		// Popup type for detection
		private static Type _capitalUpgradePopupType = null;

		// Settings.capitalStructures access
		private static FieldInfo _settingsCapitalStructuresField = null;

		// CapitalStructureModel fields
		private static FieldInfo _structureDisplayNameField = null;
		private static FieldInfo _structureUpgradesField = null;

		// CapitalUpgradeModel fields
		private static FieldInfo _upgradeDisplayNameField = null;
		private static FieldInfo _upgradeRequiredLevelField = null;
		private static FieldInfo _upgradePriceField = null;
		private static FieldInfo _upgradeRewardsField = null;

		// MetaCurrencyRef fields
		private static FieldInfo _currencyRefCurrencyField = null;
		private static FieldInfo _currencyRefAmountField = null;

		// MetaCurrencyModel.DisplayName property
		private static PropertyInfo _currencyModelDisplayNameProperty = null;

		// MetaRewardModel properties
		private static PropertyInfo _rewardDisplayNameProperty = null;
		private static PropertyInfo _rewardDescriptionProperty = null;

		// ICapitalService methods
		private static PropertyInfo _wsCapitalServiceProperty = null;
		private static MethodInfo _csIsUnlockedMethod = null;
		private static MethodInfo _csCanBeBoughtMethod = null;
		private static MethodInfo _csIsReachedMethod = null;

		// ICapitalService.CanUnlockUpgradesFromLevel method
		private static MethodInfo _csCanUnlockUpgradesFromLevelMethod = null;

		// WorldBlackboardService.CapitalUpgradeRequested subject
		private static PropertyInfo _wbbCapitalUpgradeRequestedProperty = null;

		private static bool _typesCached = false;

		private static void EnsureTypes() {
			if (_typesCached) return;
			_typesCached = true;

			ReflectionHelper.InitCache("CapitalUpgradeReflection", assembly => {
				// Cache CapitalUpgradePopup type for popup detection
				_capitalUpgradePopupType = assembly.GetType("Eremite.WorldMap.UI.CapitalUpgradePopup");

				// Cache Settings.capitalStructures field
				var settingsType = assembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					_settingsCapitalStructuresField = settingsType.GetField("capitalStructures",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache CapitalStructureModel fields
				var structureModelType = assembly.GetType("Eremite.WorldMap.CapitalStructureModel");
				if (structureModelType != null) {
					_structureDisplayNameField = structureModelType.GetField("displayName",
						BindingFlags.Public | BindingFlags.Instance);
					_structureUpgradesField = structureModelType.GetField("upgrades",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache CapitalUpgradeModel fields
				var upgradeModelType = assembly.GetType("Eremite.WorldMap.CapitalUpgradeModel");
				if (upgradeModelType != null) {
					_upgradeDisplayNameField = upgradeModelType.GetField("displayName",
						BindingFlags.Public | BindingFlags.Instance);
					_upgradeRequiredLevelField = upgradeModelType.GetField("requiredLevel",
						BindingFlags.Public | BindingFlags.Instance);
					_upgradePriceField = upgradeModelType.GetField("price",
						BindingFlags.Public | BindingFlags.Instance);
					_upgradeRewardsField = upgradeModelType.GetField("rewards",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache MetaCurrencyRef fields
				var currencyRefType = assembly.GetType("Eremite.Model.MetaCurrencyRef");
				if (currencyRefType != null) {
					_currencyRefCurrencyField = currencyRefType.GetField("currency",
						BindingFlags.Public | BindingFlags.Instance);
					_currencyRefAmountField = currencyRefType.GetField("amount",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache MetaCurrencyModel.DisplayName
				var currencyModelType = assembly.GetType("Eremite.Model.MetaCurrencyModel");
				if (currencyModelType != null) {
					_currencyModelDisplayNameProperty = currencyModelType.GetProperty("DisplayName",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache MetaRewardModel properties
				var rewardModelType = assembly.GetType("Eremite.Model.Meta.MetaRewardModel");
				if (rewardModelType != null) {
					_rewardDisplayNameProperty = rewardModelType.GetProperty("DisplayName",
						BindingFlags.Public | BindingFlags.Instance);
					_rewardDescriptionProperty = rewardModelType.GetProperty("Description",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache IWorldServices.CapitalService property
				var worldServicesType = assembly.GetType("Eremite.Services.World.IWorldServices");
				if (worldServicesType != null) {
					_wsCapitalServiceProperty = worldServicesType.GetProperty("CapitalService",
						BindingFlags.Public | BindingFlags.Instance);
				}

				// Cache ICapitalService methods
				var capitalServiceType = assembly.GetType("Eremite.Services.World.ICapitalService");
				if (capitalServiceType != null && structureModelType != null && upgradeModelType != null) {
					_csIsUnlockedMethod = capitalServiceType.GetMethod("IsUnlocked",
						new Type[] { upgradeModelType });
					_csCanBeBoughtMethod = capitalServiceType.GetMethod("CanBeBought",
						new Type[] { structureModelType, upgradeModelType });
					_csIsReachedMethod = capitalServiceType.GetMethod("IsReached",
						new Type[] { structureModelType, upgradeModelType });
					_csCanUnlockUpgradesFromLevelMethod = capitalServiceType.GetMethod("CanUnlockUpgradesFromLevel",
						new Type[] { typeof(int) });
				}

				// Cache WorldBlackboardService.CapitalUpgradeRequested
				var wbbType = assembly.GetType("Eremite.Services.World.IWorldBlackboardService");
				if (wbbType != null) {
					_wbbCapitalUpgradeRequestedProperty = wbbType.GetProperty("CapitalUpgradeRequested",
						BindingFlags.Public | BindingFlags.Instance);
				}
			});
		}

		/// <summary>
		/// Check if a popup object is a CapitalUpgradePopup.
		/// </summary>
		public static bool IsCapitalUpgradePopup(object popup) {
			if (popup == null) return false;
			EnsureTypes();
			if (_capitalUpgradePopupType == null) return false;
			return _capitalUpgradePopupType.IsInstanceOfType(popup);
		}

		/// <summary>
		/// Get the CapitalService from WorldServices.
		/// </summary>
		private static object GetCapitalService() {
			var ws = WorldMapReflection.GetWorldServices();
			if (ws == null) return null;
			return ReflectionHelper.GetProp(_wsCapitalServiceProperty, ws);
		}

		/// <summary>
		/// Get all capital structures with their info.
		/// </summary>
		public static List<StructureInfo> GetStructures() {
			EnsureTypes();
			var result = new List<StructureInfo>();

			try {
				var settings = GameReflection.GetSettings();
				if (settings == null || _settingsCapitalStructuresField == null) return result;

				var structures = _settingsCapitalStructuresField.GetValue(settings) as Array;
				if (structures == null) return result;

				var capitalService = GetCapitalService();

				foreach (var structure in structures) {
					if (structure == null) continue;

					string name = ReflectionHelper.GetLocaString(_structureDisplayNameField, structure) ?? "";

					var upgrades = _structureUpgradesField?.GetValue(structure) as Array;
					int totalUpgrades = upgrades?.Length ?? 0;

					int unlockedCount = 0;
					if (capitalService != null && upgrades != null) {
						foreach (var upgrade in upgrades) {
							if (upgrade == null) continue;
							if (ReflectionHelper.InvokeBool(_csIsUnlockedMethod, capitalService, upgrade))
								unlockedCount++;
						}
					}

					result.Add(new StructureInfo {
						Name = name,
						TotalUpgrades = totalUpgrades,
						UnlockedCount = unlockedCount,
						StructureObj = structure
					});
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetStructures failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get all upgrades for a structure with their status info.
		/// </summary>
		public static List<UpgradeInfo> GetUpgrades(object structure) {
			EnsureTypes();
			var result = new List<UpgradeInfo>();

			if (structure == null) return result;

			try {
				var upgrades = _structureUpgradesField?.GetValue(structure) as Array;
				if (upgrades == null) return result;

				var capitalService = GetCapitalService();

				foreach (var upgrade in upgrades) {
					if (upgrade == null) continue;

					string name = ReflectionHelper.GetLocaString(_upgradeDisplayNameField, upgrade) ?? "";

					int requiredLevel = ReflectionHelper.GetInt(_upgradeRequiredLevelField, upgrade);

					string priceText = GetPriceText(upgrade);
					UpgradeStatus status = DetermineStatus(structure, upgrade, capitalService);

					result.Add(new UpgradeInfo {
						Name = name,
						Status = status,
						RequiredLevel = requiredLevel,
						PriceText = priceText,
						UpgradeObj = upgrade,
						StructureObj = structure
					});
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetUpgrades failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get all rewards for an upgrade.
		/// </summary>
		public static List<RewardInfo> GetRewards(object upgrade) {
			EnsureTypes();
			var result = new List<RewardInfo>();

			if (upgrade == null) return result;

			try {
				var rewards = _upgradeRewardsField?.GetValue(upgrade) as Array;
				if (rewards == null) return result;

				foreach (var reward in rewards) {
					if (reward == null) continue;

					string name = ReflectionHelper.GetPropString(_rewardDisplayNameProperty, reward) ?? "";
					string description = ReflectionHelper.GetPropString(_rewardDescriptionProperty, reward) ?? "";

					if (!string.IsNullOrEmpty(name)) {
						result.Add(new RewardInfo { Name = name, Description = description });
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetRewards failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Buy an upgrade by firing CapitalUpgradeRequested.OnNext(model).
		/// Returns true if the event was fired successfully.
		/// Only call this when status is Buyable.
		/// </summary>
		public static bool BuyUpgrade(object upgrade) {
			EnsureTypes();

			if (upgrade == null || _wbbCapitalUpgradeRequestedProperty == null) return false;

			try {
				var wbb = WorldMapReflection.GetWorldBlackboardService();
				if (wbb == null) return false;

				var subject = ReflectionHelper.GetProp(_wbbCapitalUpgradeRequestedProperty, wbb);
				if (subject == null) return false;

				var onNextMethod = subject.GetType().GetMethod("OnNext",
					new Type[] { upgrade.GetType() });
				if (onNextMethod == null) return false;

				return ReflectionHelper.InvokeVoid(onNextMethod, subject, upgrade);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] BuyUpgrade failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Determine the status of an upgrade.
		/// </summary>
		private static UpgradeStatus DetermineStatus(object structure, object upgrade, object capitalService) {
			if (capitalService == null) return UpgradeStatus.Locked;

			try {
				// Check if already unlocked
				if (ReflectionHelper.InvokeBool(_csIsUnlockedMethod, capitalService, upgrade))
					return UpgradeStatus.Unlocked;

				// Check if can be bought (reached + affordable)
				if (ReflectionHelper.InvokeBool(_csCanBeBoughtMethod, capitalService, structure, upgrade))
					return UpgradeStatus.Buyable;

				// Check if reached (prerequisites met, level completed) but can't afford
				if (ReflectionHelper.InvokeBool(_csIsReachedMethod, capitalService, structure, upgrade)) {
					// Reached but can't buy means too expensive
					return UpgradeStatus.TooExpensive;
				}

				// Not reached - check if it's a level requirement or prerequisite issue
				int requiredLevel = ReflectionHelper.GetInt(_upgradeRequiredLevelField, upgrade);

				// If required level > 0 and not reached, check if the level itself is the blocker
				// CapitalService.IsReached checks both AllPreviousBought and IsLevelCompleted
				// If previous upgrades in the structure aren't all bought, it's a prerequisite issue
				// Otherwise it's a level requirement issue
				if (requiredLevel > 0 && !IsLevelCompleted(capitalService, requiredLevel))
					return UpgradeStatus.LevelRequired;

				return UpgradeStatus.Locked;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] DetermineStatus failed: {ex.Message}");
				return UpgradeStatus.Locked;
			}
		}

		/// <summary>
		/// Check if a capital level is completed (enough total upgrades bought).
		/// Uses CapitalService.CanUnlockUpgradesFromLevel.
		/// </summary>
		private static bool IsLevelCompleted(object capitalService, int level) {
			return ReflectionHelper.InvokeBool(_csCanUnlockUpgradesFromLevelMethod, capitalService, level);
		}

		/// <summary>
		/// Get the price text for an upgrade, formatted as "50 Food Stockpiles, 25 Machinery".
		/// </summary>
		private static string GetPriceText(object upgrade) {
			if (upgrade == null || _upgradePriceField == null) return "";

			try {
				var priceArray = _upgradePriceField.GetValue(upgrade) as Array;
				if (priceArray == null || priceArray.Length == 0) return "";

				var parts = new List<string>();

				foreach (var currencyRef in priceArray) {
					if (currencyRef == null) continue;

					var currencyModel = ReflectionHelper.GetField(_currencyRefCurrencyField, currencyRef);
					if (currencyModel == null) continue;

					int amount = ReflectionHelper.GetInt(_currencyRefAmountField, currencyRef);
					string displayName = ReflectionHelper.GetPropString(_currencyModelDisplayNameProperty, currencyModel) ?? "";

					if (!string.IsNullOrEmpty(displayName)) {
						parts.Add(Strings.Get("common.amount_and_name", amount, displayName));
					}
				}

				return string.Join(", ", parts.ToArray());
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GetPriceText failed: {ex.Message}");
				return "";
			}
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(CapitalUpgradeReflection), "CapitalUpgradeReflection");
		}
	}
}
