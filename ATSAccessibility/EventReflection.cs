using System;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility {
	/// <summary>
	/// Provides cached reflection metadata for EventAnnouncer's game service access.
	/// Caches PropertyInfo/MethodInfo/FieldInfo (safe to cache across scenes),
	/// but never caches service instances (destroyed on scene change).
	/// </summary>
	public static class EventReflection {
		// ========================================
		// GROUP 1: SERVICE PROPERTIES (from IGameServices)
		// ========================================

		private static bool _reflectionCached = false;
		private static PropertyInfo _calendarServiceProperty;
		private static PropertyInfo _hostilityServiceProperty;
		private static PropertyInfo _tradeServiceProperty;
		private static PropertyInfo _ordersServiceProperty;
		private static PropertyInfo _gladesServiceProperty;
		private static PropertyInfo _reputationServiceProperty;
		private static PropertyInfo _newsServiceProperty;
		private static PropertyInfo _newcomersServiceProperty;
		private static PropertyInfo _reputationRewardsServiceProperty;
		private static PropertyInfo _cornerstonesServiceProperty;
		private static PropertyInfo _monitorsServiceProperty;
		private static PropertyInfo _villagersServiceProperty;

		public static PropertyInfo CalendarServiceProperty => _calendarServiceProperty;
		public static PropertyInfo HostilityServiceProperty => _hostilityServiceProperty;
		public static PropertyInfo TradeServiceProperty => _tradeServiceProperty;
		public static PropertyInfo OrdersServiceProperty => _ordersServiceProperty;
		public static PropertyInfo GladesServiceProperty => _gladesServiceProperty;
		public static PropertyInfo ReputationServiceProperty => _reputationServiceProperty;
		public static PropertyInfo NewsServiceProperty => _newsServiceProperty;
		public static PropertyInfo NewcomersServiceProperty => _newcomersServiceProperty;
		public static PropertyInfo ReputationRewardsServiceProperty => _reputationRewardsServiceProperty;
		public static PropertyInfo CornerstonesServiceProperty => _cornerstonesServiceProperty;
		public static PropertyInfo MonitorsServiceProperty => _monitorsServiceProperty;
		public static PropertyInfo VillagersServiceProperty => _villagersServiceProperty;

		public static void EnsureReflectionCached() {
			if (_reflectionCached) return;

			var assembly = GameReflection.GameAssembly;
			if (assembly == null) return;

			var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
			if (gameServicesType != null) {
				_calendarServiceProperty = gameServicesType.GetProperty("CalendarService");
				_hostilityServiceProperty = gameServicesType.GetProperty("HostilityService");
				_tradeServiceProperty = gameServicesType.GetProperty("TradeService");
				_ordersServiceProperty = gameServicesType.GetProperty("OrdersService");
				_gladesServiceProperty = gameServicesType.GetProperty("GladesService");
				_reputationServiceProperty = gameServicesType.GetProperty("ReputationService");
				_newsServiceProperty = gameServicesType.GetProperty("NewsService");
				_newcomersServiceProperty = gameServicesType.GetProperty("NewcomersService");
				_reputationRewardsServiceProperty = gameServicesType.GetProperty("ReputationRewardsService");
				_cornerstonesServiceProperty = gameServicesType.GetProperty("CornerstonesService");
				_monitorsServiceProperty = gameServicesType.GetProperty("MonitorsService");
				_villagersServiceProperty = gameServicesType.GetProperty("VillagersService");
			}

			_reflectionCached = true;
		}

		// ========================================
		// GROUP 2: VILLAGER REFLECTION
		// ========================================

		private static bool _villagerReflectionCached = false;
		private static MethodInfo _villagerGetDisplayNameMethod;
		private static FieldInfo _villagerStateField;
		private static FieldInfo _villagerStateLossTypeField;
		private static FieldInfo _villagerStateLossReasonField;
		private static FieldInfo _villagerLastWorkIdField;

		public static MethodInfo VillagerGetDisplayNameMethod => _villagerGetDisplayNameMethod;
		public static FieldInfo VillagerStateField => _villagerStateField;
		public static FieldInfo VillagerStateLossTypeField => _villagerStateLossTypeField;
		public static FieldInfo VillagerStateLossReasonField => _villagerStateLossReasonField;
		public static FieldInfo VillagerLastWorkIdField => _villagerLastWorkIdField;

		public static void EnsureVillagerReflectionCached(object villager) {
			if (_villagerReflectionCached || villager == null) return;

			var villagerType = villager.GetType();
			_villagerGetDisplayNameMethod = villagerType.GetMethod("GetDisplayName");
			_villagerStateField = villagerType.GetField("state");

			var stateObj = _villagerStateField?.GetValue(villager);
			if (stateObj != null) {
				var stateType = stateObj.GetType();
				_villagerStateLossTypeField = stateType.GetField("lossType");
				_villagerStateLossReasonField = stateType.GetField("lossReasonKey");
				_villagerLastWorkIdField = stateType.GetField("lastWorkId");
			}

			_villagerReflectionCached = true;
		}

		// ========================================
		// GROUP 3: ALERT FIELDS
		// ========================================

		private static bool _alertFieldsCached = false;
		private static FieldInfo _alertTextField;
		private static FieldInfo _alertDismissedField;
		private static FieldInfo _alertShowTimeField;

		public static FieldInfo AlertTextField => _alertTextField;
		public static FieldInfo AlertDismissedField => _alertDismissedField;
		public static FieldInfo AlertShowTimeField => _alertShowTimeField;

		public static void EnsureAlertFieldsCached(object alert) {
			if (_alertFieldsCached || alert == null) return;

			var alertType = alert.GetType();
			_alertTextField = alertType.GetField("text");
			_alertDismissedField = alertType.GetField("dismissed");
			_alertShowTimeField = alertType.GetField("showTime");
			_alertFieldsCached = true;
		}

		// ========================================
		// GROUP 4: GLADE FIELDS
		// ========================================

		private static bool _gladeFieldsCached = false;
		private static FieldInfo _gladeFieldsField;

		public static FieldInfo GladeFieldsField => _gladeFieldsField;

		public static void EnsureGladeFieldsCached(object gladeState) {
			if (_gladeFieldsCached || gladeState == null) return;

			_gladeFieldsField = gladeState.GetType().GetField("fields");
			_gladeFieldsCached = true;
		}

		// ========================================
		// GROUP 5: GLADE DANGER LEVEL
		// ========================================

		private static bool _gladesGetDangerLevelCached = false;
		private static MethodInfo _gladesGetDangerLevelMethod;

		public static MethodInfo GladesGetDangerLevelMethod => _gladesGetDangerLevelMethod;

		public static void EnsureGladesGetDangerLevelCached(object gladesService) {
			if (_gladesGetDangerLevelCached || gladesService == null) return;

			_gladesGetDangerLevelMethod = gladesService.GetType().GetMethod("GetDangerLevel");
			_gladesGetDangerLevelCached = true;
		}

		// ========================================
		// GROUP 6: EFFECT PROPERTIES (plague announcements)
		// ========================================

		private static bool _effectPropsCached = false;
		private static PropertyInfo _effectDisplayNameProperty;
		private static PropertyInfo _effectDescriptionProperty;

		public static void EnsureEffectPropertyCached() {
			if (_effectPropsCached) return;
			_effectPropsCached = true;

			try {
				var effectModelType = GameReflection.GameAssembly?.GetType("Eremite.Model.EffectModel");
				if (effectModelType != null) {
					_effectDisplayNameProperty = effectModelType.GetProperty("DisplayName", GameReflection.PublicInstance);
					_effectDescriptionProperty = effectModelType.GetProperty("Description", GameReflection.PublicInstance);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] EventReflection: Failed to cache effect properties: {ex.Message}");
			}
		}

		public static string GetEffectDisplayName(object effectModel) {
			if (effectModel == null) return null;
			EnsureEffectPropertyCached();
			if (_effectDisplayNameProperty == null) return null;

			try { return _effectDisplayNameProperty.GetValue(effectModel)?.ToString(); } catch { return null; }
		}

		public static string GetEffectDescription(object effectModel) {
			if (effectModel == null) return null;
			EnsureEffectPropertyCached();
			if (_effectDescriptionProperty == null) return null;

			try {
				return _effectDescriptionProperty.GetValue(effectModel)?.ToString();
			} catch { return null; }
		}

		// ========================================
		// CACHE RESET
		// ========================================

		/// <summary>
		/// Reset all cached flags so reflection gets re-cached on next game.
		/// Called from EventAnnouncer.Dispose().
		/// </summary>
		public static void ResetCache() {
			_reflectionCached = false;
			_villagerReflectionCached = false;
			_alertFieldsCached = false;
			_gladeFieldsCached = false;
			_gladesGetDangerLevelCached = false;
			_effectPropsCached = false;
		}
	}
}
