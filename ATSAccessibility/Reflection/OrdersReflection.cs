using ATSAccessibility.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Provides reflection-based access to orders system internals.
	///
	/// CRITICAL RULES:
	/// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
	/// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
	/// </summary>
	public static class OrdersReflection {
		// ========================================
		// RICH TEXT STRIPPING
		// ========================================

		private static readonly Regex RichTextRegex = new Regex("<[^>]+>", RegexOptions.Compiled);
		private static readonly Regex ProductionBonusRegex = new Regex(
			@"(\+\d+) to (.+?) production \(from gathering, farming, fishing, or production\)\.?",
			RegexOptions.Compiled);

		public static string StripRichText(string text) {
			if (string.IsNullOrEmpty(text)) return text;
			return RichTextRegex.Replace(text, "");
		}

		/// <summary>
		/// Strip trailing period from localized text (punctuation handled by overlays).
		/// </summary>
		private static string TrimObjectiveText(string text) {
			if (string.IsNullOrEmpty(text)) return text;
			return text.TrimEnd('.');
		}

		/// <summary>
		/// Parse a timer string (e.g. "00:56" or "1:30:00") to total seconds.
		/// Returns -1 if the string is not a valid timer format.
		/// </summary>
		private static int ParseTimerSeconds(string timer) {
			var parts = timer.Split(':');
			if (parts.Length < 2) return -1;
			int total = 0;
			foreach (var part in parts) {
				int val;
				if (!int.TryParse(part, out val)) return -1;
				total = total * 60 + val;
			}
			return total;
		}

		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		private static bool _cached = false;

		// Popup type detection
		private static Type _ordersPopupType = null;
		private static Type _orderPickPopupType = null;

		// IGameServices properties
		private static PropertyInfo _gsOrdersServiceProperty = null;
		private static PropertyInfo _gsGameTimeServiceProperty = null;

		// IOrdersService methods/properties
		private static PropertyInfo _osOrdersProperty = null;
		private static MethodInfo _osCanCompleteMethod = null;
		private static MethodInfo _osCompleteOrderMethod = null;
		private static MethodInfo _osOrderPickedMethod = null;
		private static MethodInfo _osGetPicksForMethod = null;
		private static MethodInfo _osSwitchOrderTrackingMethod = null;

		// IGameTimeService.Time property
		private static PropertyInfo _gtsTimeProperty = null;

		// OrderState fields
		private static FieldInfo _osModelField = null;
		private static FieldInfo _osStartedField = null;
		private static FieldInfo _osPickedField = null;
		private static FieldInfo _osCompletedField = null;
		private static FieldInfo _osIsFailedField = null;
		private static FieldInfo _osTimeLeftField = null;
		private static FieldInfo _osTrackedField = null;
		private static FieldInfo _osObjectivesField = null;
		private static FieldInfo _osPicksField = null;
		private static FieldInfo _osStartTimeField = null;
		private static FieldInfo _osRewardsField = null;
		private static FieldInfo _osShouldBeFailableField = null;

		// OrderPickState fields
		private static FieldInfo _opsModelField = null;
		private static FieldInfo _opsSetIndexField = null;
		private static FieldInfo _opsFailedField = null;
		private static FieldInfo _opsRewardsField = null;

		// OrderModel fields/methods
		private static FieldInfo _omDisplayNameField = null;
		private static FieldInfo _omCanBeFailedField = null;
		private static FieldInfo _omTimeToFailField = null;
		private static FieldInfo _omReputationRewardField = null;
		private static FieldInfo _omUnlockAfterField = null;
		private static FieldInfo _omLogicsSetsField = null;
		private static MethodInfo _omGetLogicsMethod = null;       // GetLogics(OrderState)
		private static MethodInfo _omGetLogicsIntMethod = null;    // GetLogics(int)

		// OrderLogicsSet fields
		private static FieldInfo _olsLogicsField = null;

		// OrderLogic methods/properties
		private static MethodInfo _olGetObjectiveTextMethod = null;
		private static PropertyInfo _olDisplayNameProperty = null;
		private static PropertyInfo _olDescriptionProperty = null;
		private static MethodInfo _olGetAmountTextMethod = null;  // GetAmountText() no args
		private static MethodInfo _olGetAmountTextStateMethod = null;  // GetAmountText(ObjectiveState)
		private static MethodInfo _olIsCompletedMethod = null;
		private static PropertyInfo _olHasStoredAmountProperty = null;
		private static PropertyInfo _olGetStoredAmountProperty = null;
		private static MethodInfo _olGetWarningTextMethod = null;

		// Comply-timer `time` field — lives on derived logics
		// (HostilityForTimeLogic, EnginesLevelForTimeLogic, NeedSatisfiedForTimeLogic, …),
		// not the OrderLogic base. Resolved per-instance at use time.
		private static readonly Dictionary<Type, FieldInfo> _olTimeFieldCache = new Dictionary<Type, FieldInfo>();

		// ReputationGainedFromSourceLogic (for appending source to objective text)
		private static Type _repSourceLogicType = null;
		private static FieldInfo _repSourceField = null;
		private static MethodInfo _repGetSourceTextMethod = null;

		// EffectModel properties
		private static PropertyInfo _emDisplayNameProperty = null;
		private static PropertyInfo _emDescriptionProperty = null;
		private static MethodInfo _emGetAmountTextMethod = null;

		// Settings.GetOrder method
		private static MethodInfo _settingsGetOrderMethod = null;

		// Settings.GetEffect method
		private static MethodInfo _settingsGetEffectMethod = null;

		// GameBlackboardService.OrderPickPopupRequested property
		private static PropertyInfo _gbbOrderPickPopupRequestedProperty = null;

		// OrderPickPopup.order field (the order being shown in the pick popup)
		private static FieldInfo _oppOrderField = null;

		// Popup.Hide method
		private static MethodInfo _popupHideMethod = null;

		// Subject<T>.OnNext method (for firing events)
		private static MethodInfo _subjectOnNextMethod = null;

		// ========================================
		// INITIALIZATION
		// ========================================

		private static void EnsureCached() {
			if (_cached) return;
			_cached = true;

			ReflectionHelper.InitCache("OrdersReflection", assembly => {
				CachePopupTypes(assembly);
				CacheServiceTypes(assembly);
				CacheOrderStateTypes(assembly);
				CacheOrderPickStateTypes(assembly);
				CacheOrderModelTypes(assembly);
				CacheOrderLogicTypes(assembly);
				CacheEffectModelTypes(assembly);
				CacheSettingsTypes(assembly);
				CacheBlackboardTypes(assembly);
			});
		}

		private static void CachePopupTypes(Assembly assembly) {
			_ordersPopupType = assembly.GetType("Eremite.View.HUD.Orders.OrdersPopup");
			_orderPickPopupType = assembly.GetType("Eremite.View.HUD.Orders.OrderPickPopup");

			if (_orderPickPopupType != null) {
				_oppOrderField = _orderPickPopupType.GetField("order", GameReflection.NonPublicInstance);
			}

			var popupType = assembly.GetType("Eremite.View.Popups.Popup");
			if (popupType != null) {
				_popupHideMethod = popupType.GetMethod("Hide", GameReflection.PublicInstance);
			}
		}

		private static void CacheServiceTypes(Assembly assembly) {
			var gameServicesType = assembly.GetType("Eremite.Services.IGameServices");
			if (gameServicesType != null) {
				_gsOrdersServiceProperty = gameServicesType.GetProperty("OrdersService", GameReflection.PublicInstance);
				_gsGameTimeServiceProperty = gameServicesType.GetProperty("GameTimeService", GameReflection.PublicInstance);
			}

			var osType = assembly.GetType("Eremite.Services.IOrdersService");
			if (osType != null) {
				_osOrdersProperty = osType.GetProperty("Orders", GameReflection.PublicInstance);
				_osCanCompleteMethod = osType.GetMethod("CanComplete", GameReflection.PublicInstance);
				_osGetPicksForMethod = osType.GetMethod("GetPicksFor", GameReflection.PublicInstance);
				_osSwitchOrderTrackingMethod = osType.GetMethod("SwitchOrderTracking", GameReflection.PublicInstance);
				_osOrderPickedMethod = osType.GetMethod("OrderPicked", GameReflection.PublicInstance);

				// CompleteOrder(OrderState, OrderModel, bool force) - we'll pass force=false
				var orderStateType = assembly.GetType("Eremite.Model.Orders.OrderState");
				var orderModelType = assembly.GetType("Eremite.Model.Orders.OrderModel");
				if (orderStateType != null && orderModelType != null) {
					_osCompleteOrderMethod = osType.GetMethod("CompleteOrder",
						new[] { orderStateType, orderModelType, typeof(bool) });
				}
			}

			var gtsType = assembly.GetType("Eremite.Services.IGameTimeService");
			if (gtsType != null) {
				_gtsTimeProperty = gtsType.GetProperty("Time", GameReflection.PublicInstance);
			}
		}

		private static void CacheOrderStateTypes(Assembly assembly) {
			var type = assembly.GetType("Eremite.Model.Orders.OrderState");
			if (type != null) {
				_osModelField = type.GetField("model", GameReflection.PublicInstance);
				_osPickedField = type.GetField("picked", GameReflection.PublicInstance);
				_osCompletedField = type.GetField("completed", GameReflection.PublicInstance);
				_osIsFailedField = type.GetField("isFailed", GameReflection.PublicInstance);
				_osTimeLeftField = type.GetField("timeLeft", GameReflection.PublicInstance);
				_osTrackedField = type.GetField("tracked", GameReflection.PublicInstance);
				_osPicksField = type.GetField("picks", GameReflection.PublicInstance);
				_osRewardsField = type.GetField("rewards", GameReflection.PublicInstance);
				_osShouldBeFailableField = type.GetField("shouldBeFailable", GameReflection.PublicInstance);
			}

			// Inherited from BaseOrderState
			var baseType = assembly.GetType("Eremite.Model.Orders.BaseOrderState");
			if (baseType != null) {
				_osStartedField = baseType.GetField("started", GameReflection.PublicInstance);
				_osObjectivesField = baseType.GetField("objectives", GameReflection.PublicInstance);
				_osStartTimeField = baseType.GetField("startTime", GameReflection.PublicInstance);
			}
		}

		private static void CacheOrderPickStateTypes(Assembly assembly) {
			var type = assembly.GetType("Eremite.Model.Orders.OrderPickState");
			if (type != null) {
				_opsModelField = type.GetField("model", GameReflection.PublicInstance);
				_opsSetIndexField = type.GetField("setIndex", GameReflection.PublicInstance);
				_opsFailedField = type.GetField("failed", GameReflection.PublicInstance);
				_opsRewardsField = type.GetField("rewards", GameReflection.PublicInstance);
			}
		}

		private static void CacheOrderModelTypes(Assembly assembly) {
			var type = assembly.GetType("Eremite.Model.Orders.OrderModel");
			if (type != null) {
				_omDisplayNameField = type.GetField("displayName", GameReflection.PublicInstance);
				_omCanBeFailedField = type.GetField("canBeFailed", GameReflection.PublicInstance);
				_omTimeToFailField = type.GetField("timeToFail", GameReflection.PublicInstance);
				_omReputationRewardField = type.GetField("reputationReward", GameReflection.PublicInstance);
				_omUnlockAfterField = type.GetField("unlockAfter", GameReflection.PublicInstance);
				_omLogicsSetsField = type.GetField("logicsSets", GameReflection.PublicInstance);

				// GetLogics(OrderState)
				var orderStateType = assembly.GetType("Eremite.Model.Orders.OrderState");
				if (orderStateType != null) {
					_omGetLogicsMethod = type.GetMethod("GetLogics", new[] { orderStateType });
				}

				// GetLogics(int)
				_omGetLogicsIntMethod = type.GetMethod("GetLogics", new[] { typeof(int) });
			}

			// OrderLogicsSet.logics
			var olsType = assembly.GetType("Eremite.Model.Orders.OrderLogicsSet");
			if (olsType != null) {
				_olsLogicsField = olsType.GetField("logics", GameReflection.PublicInstance);
			}
		}

		private static void CacheOrderLogicTypes(Assembly assembly) {
			var type = assembly.GetType("Eremite.Model.Orders.OrderLogic");
			if (type != null) {
				var objStateType = assembly.GetType("Eremite.Model.Orders.ObjectiveState");
				if (objStateType != null) {
					_olGetObjectiveTextMethod = type.GetMethod("GetObjectiveText", new[] { objStateType });
					_olIsCompletedMethod = type.GetMethod("IsCompleted", new[] { objStateType });
					_olGetAmountTextStateMethod = type.GetMethod("GetAmountText", new[] { objStateType });
				}

				_olDisplayNameProperty = type.GetProperty("DisplayName", GameReflection.PublicInstance);
				_olDescriptionProperty = type.GetProperty("Description", GameReflection.PublicInstance);
				_olHasStoredAmountProperty = type.GetProperty("HasStoredAmount", GameReflection.PublicInstance);
				_olGetStoredAmountProperty = type.GetProperty("GetStoredAmount", GameReflection.PublicInstance);
				_olGetWarningTextMethod = type.GetMethod("GetWarningText", Type.EmptyTypes);

				// GetAmountText() - no parameters
				_olGetAmountTextMethod = type.GetMethod("GetAmountText", Type.EmptyTypes);
			}

			// ReputationGainedFromSourceLogic - source field for "from Orders/Resolve/Relics"
			_repSourceLogicType = assembly.GetType("Eremite.Model.Orders.ReputationGainedFromSourceLogic");
			if (_repSourceLogicType != null) {
				_repSourceField = _repSourceLogicType.GetField("source", GameReflection.PublicInstance);
				_repGetSourceTextMethod = _repSourceLogicType.GetMethod("GetSourceText", GameReflection.NonPublicInstance);
			}
		}

		private static void CacheEffectModelTypes(Assembly assembly) {
			var type = assembly.GetType("Eremite.Model.EffectModel");
			if (type != null) {
				_emDisplayNameProperty = type.GetProperty("DisplayName", GameReflection.PublicInstance);
				_emDescriptionProperty = type.GetProperty("Description", GameReflection.PublicInstance);
				_emGetAmountTextMethod = type.GetMethod("GetAmountText", Type.EmptyTypes);
			}
		}

		private static void CacheSettingsTypes(Assembly assembly) {
			var settingsType = assembly.GetType("Eremite.Model.Settings");
			if (settingsType != null) {
				_settingsGetOrderMethod = settingsType.GetMethod("GetOrder", new[] { typeof(string) });
				_settingsGetEffectMethod = settingsType.GetMethod("GetEffect", new[] { typeof(string) });
			}
		}

		private static void CacheBlackboardTypes(Assembly assembly) {
			var gbbType = assembly.GetType("Eremite.Services.IGameBlackboardService");
			if (gbbType != null) {
				_gbbOrderPickPopupRequestedProperty = gbbType.GetProperty("OrderPickPopupRequested", GameReflection.PublicInstance);
			}
		}

		// ========================================
		// TYPE DETECTION
		// ========================================

		public static bool IsOrdersPopup(object popup) {
			if (popup == null) return false;
			EnsureCached();
			return _ordersPopupType != null && _ordersPopupType.IsInstanceOfType(popup);
		}

		public static bool IsOrderPickPopup(object popup) {
			if (popup == null) return false;
			EnsureCached();
			return _orderPickPopupType != null && _orderPickPopupType.IsInstanceOfType(popup);
		}

		// ========================================
		// SERVICE ACCESS
		// ========================================

		private static object GetOrdersService() => GameReflection.GetService(_gsOrdersServiceProperty);

		private static object GetGameTimeService() => GameReflection.GetService(_gsGameTimeServiceProperty);

		// ========================================
		// DATA ACCESS
		// ========================================

		/// <summary>
		/// Get the list of current orders (OrderState objects).
		/// </summary>
		public static IList GetOrders() {
			EnsureCached();
			var service = GetOrdersService();
			return ReflectionHelper.GetList(_osOrdersProperty, service);
		}

		/// <summary>
		/// Get the OrderModel from Settings for a given OrderState.
		/// </summary>
		public static object GetOrderModel(object orderState) {
			EnsureCached();
			if (orderState == null || _osModelField == null) return null;

			var modelName = ReflectionHelper.GetString(_osModelField, orderState);
			if (string.IsNullOrEmpty(modelName)) return null;

			var settings = GameReflection.GetSettings();
			if (settings == null || _settingsGetOrderMethod == null) return null;

			return ReflectionHelper.Invoke(_settingsGetOrderMethod, settings, modelName);
		}

		/// <summary>
		/// Get the display name of an OrderModel.
		/// </summary>
		public static string GetOrderDisplayName(object orderModel) {
			if (orderModel == null || _omDisplayNameField == null) return null;
			return ReflectionHelper.GetLocaString(_omDisplayNameField, orderModel);
		}

		// ========================================
		// ORDER STATE QUERIES
		// ========================================

		public static bool IsStarted(object orderState) => ReflectionHelper.GetBool(_osStartedField, orderState);

		public static bool IsPicked(object orderState) => ReflectionHelper.GetBool(_osPickedField, orderState);

		public static bool IsCompleted(object orderState) => ReflectionHelper.GetBool(_osCompletedField, orderState);

		public static bool IsFailed(object orderState) => ReflectionHelper.GetBool(_osIsFailedField, orderState);

		public static bool IsTracked(object orderState) => ReflectionHelper.GetBool(_osTrackedField, orderState);

		public static float GetTimeLeft(object orderState) => ReflectionHelper.GetFloat(_osTimeLeftField, orderState);

		public static float GetStartTime(object orderState) => ReflectionHelper.GetFloat(_osStartTimeField, orderState);

		public static bool CanBeFailed(object orderModel) => ReflectionHelper.GetBool(_omCanBeFailedField, orderModel);

		public static bool IsShouldBeFailable(object orderState) => ReflectionHelper.GetBool(_osShouldBeFailableField, orderState);

		public static float GetTimeToFail(object orderModel) => ReflectionHelper.GetFloat(_omTimeToFailField, orderModel);

		public static bool HasUnlockAfter(object orderModel) {
			return ReflectionHelper.GetField(_omUnlockAfterField, orderModel) != null;
		}

		public static string GetUnlockAfterName(object orderModel) {
			var unlockAfter = ReflectionHelper.GetField(_omUnlockAfterField, orderModel);
			if (unlockAfter == null) return null;
			return GetOrderDisplayName(unlockAfter);
		}

		// ========================================
		// GAME TIME
		// ========================================

		public static float GetGameTime() {
			EnsureCached();
			var service = GetGameTimeService();
			return ReflectionHelper.GetPropFloat(_gtsTimeProperty, service);
		}

		// ========================================
		// OBJECTIVES
		// ========================================

		/// <summary>
		/// Get objective texts for an active order (has ObjectiveState).
		/// Uses the same localization-aware number placement as GetPickObjectiveTexts,
		/// but with progress amounts (e.g. "2/3") instead of totals.
		/// </summary>
		public static List<string> GetObjectiveTexts(object orderModel, object orderState) {
			EnsureCached();
			var result = new List<string>();
			if (orderModel == null || orderState == null) return result;

			try {
				// Get logics via GetLogics(OrderState)
				if (_omGetLogicsMethod == null) return result;
				var logics = ReflectionHelper.Invoke(_omGetLogicsMethod, orderModel, orderState) as Array;
				if (logics == null) return result;

				// Get objectives array from state
				var objectives = ReflectionHelper.GetField(_osObjectivesField, orderState) as Array;

				for (int i = 0; i < logics.Length; i++) {
					var logic = logics.GetValue(i);
					if (logic == null) continue;
					if (objectives == null || i >= objectives.Length) continue;

					var objState = objectives.GetValue(i);
					string displayName = ReflectionHelper.GetPropString(_olDisplayNameProperty, logic);

					// Get progress amount (e.g. "2/3") and total amount (e.g. "3")
					string progressAmount = null;
					if (_olGetAmountTextStateMethod != null) {
						var raw = ReflectionHelper.InvokeString(_olGetAmountTextStateMethod, logic, objState);
						if (!string.IsNullOrEmpty(raw))
							progressAmount = StripRichText(raw);
					}

					string totalAmount = null;
					if (_olGetAmountTextMethod != null) {
						var raw = ReflectionHelper.InvokeString(_olGetAmountTextMethod, logic);
						if (!string.IsNullOrEmpty(raw))
							totalAmount = StripRichText(raw);
					}

					// Detect comply timer (e.g. "00:56" → threshold met, counting down).
					// Replace with "total/total" progress and track remaining seconds.
					int complySecondsLeft = -1;
					if (!string.IsNullOrEmpty(progressAmount) && progressAmount.Contains(":")) {
						complySecondsLeft = ParseTimerSeconds(progressAmount);
						if (complySecondsLeft >= 0 && !string.IsNullOrEmpty(totalAmount)) {
							progressAmount = $"{totalAmount}/{totalAmount}";
						}
					}

					// If DisplayName unavailable, fall back to raw GetObjectiveText
					if (string.IsNullOrEmpty(displayName)) {
						string rawText = ReflectionHelper.InvokeString(_olGetObjectiveTextMethod, logic, objState);
						if (!string.IsNullOrEmpty(rawText))
							result.Add(TrimObjectiveText(StripRichText(rawText)));
						continue;
					}

					string formatted = null;

					bool isNumericProgress = !string.IsNullOrEmpty(progressAmount)
						&& progressAmount.Length > 0 && char.IsDigit(progressAmount[0]);

					if (!string.IsNullOrEmpty(progressAmount) && !displayName.Contains(progressAmount)) {
						// If DisplayName already contains totalAmount (e.g. GoodLogic's
						// "Deliver 50 Pack of Luxury Goods"), replace it with progress
						// to get "Deliver 36/50 Pack of Luxury Goods"
						if (!string.IsNullOrEmpty(totalAmount) && displayName.Contains(totalAmount)) {
							formatted = TrimObjectiveText(ReplaceAmount(displayName, totalAmount, progressAmount));
						} else {
							// Amount not in DisplayName - use localization-aware placement
							string typeName = logic.GetType().Name;
							bool skipDescription = typeName.Contains("Building");

							if (!skipDescription && !string.IsNullOrEmpty(totalAmount)) {
								// Try Description with total amount swapped for progress
								string desc = ReflectionHelper.GetPropString(_olDescriptionProperty, logic);
								string strippedDesc = !string.IsNullOrEmpty(desc) ? StripRichText(desc).Trim() : null;

								if (!string.IsNullOrEmpty(strippedDesc) && strippedDesc.Contains(totalAmount)) {
									strippedDesc = ReplaceAmount(strippedDesc, totalAmount, progressAmount);

									// Truncate multi-sentence descriptions
									if (strippedDesc != null) {
										int sentenceEnd = strippedDesc.IndexOf(". ", StringComparison.Ordinal);
										if (sentenceEnd >= 0)
											strippedDesc = strippedDesc.Substring(0, sentenceEnd);
										formatted = TrimObjectiveText(strippedDesc);
									}
								}
							}

							if (formatted == null) {
								// Fallback: type-specific formatting
								if (isNumericProgress) {
									if (typeName.Contains("Building")) {
										formatted = TrimObjectiveText(Strings.Get("reflection.orders.objective_build", progressAmount, displayName));
									} else {
										// Word-order ("Verb {amount} Noun") cannot be reconstructed safely
										// without locale knowledge; defer to the game's own localized
										// objective text and only fall back to bare "{amount} {name}".
										string rawText = ReflectionHelper.InvokeString(_olGetObjectiveTextMethod, logic, objState);
										formatted = !string.IsNullOrEmpty(rawText)
											? TrimObjectiveText(StripRichText(rawText))
											: TrimObjectiveText(Strings.Get("common.amount_and_name", progressAmount, displayName));
									}
								} else {
									// Status word (e.g. "Done"): just use the display name
									formatted = TrimObjectiveText(displayName);
								}

								// Append source for reputation objectives when not already in text
								string source = GetReputationSourceText(logic);
								if (source != null)
									formatted = Strings.Get("reflection.orders.objective_from", formatted, source);
							}
						}
					} else if (!string.IsNullOrEmpty(progressAmount)) {
						// Amount already in DisplayName
						formatted = TrimObjectiveText(displayName);
					} else {
						// No amount text - fall back to raw GetObjectiveText
						string rawText = ReflectionHelper.InvokeString(_olGetObjectiveTextMethod, logic, objState);
						formatted = !string.IsNullOrEmpty(rawText) ? TrimObjectiveText(StripRichText(rawText)) : null;
					}

					// For comply timers, splice the live countdown into the localized
					// objective text by replacing the total-seconds numeral (locale-agnostic)
					// with the remaining-seconds numeral. Falls back to a trailing suffix
					// when the total numeral isn't present in the formatted text.
					if (complySecondsLeft >= 0 && formatted != null) {
						int totalInt = GetComplyTotalSeconds(logic);
						string replaced = null;
						if (totalInt > 0)
							replaced = Regex.Replace(formatted, $@"\b{totalInt}\b", complySecondsLeft.ToString());

						if (!string.IsNullOrEmpty(replaced) && replaced != formatted)
							formatted = replaced;
						else
							formatted = Strings.Get("reflection.orders.objective_with_seconds_remaining", formatted, complySecondsLeft);
					}

					// Prefix completed objectives with checkmark
					if (!isNumericProgress && !string.IsNullOrEmpty(progressAmount) && formatted != null)
						formatted = "✓ " + formatted;

					if (!string.IsNullOrEmpty(formatted))
						result.Add(formatted);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetObjectiveTexts failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Look up the total comply-timer duration (integer seconds) for a comply-time
		/// OrderLogic via its derived-class `time` field. Returns 0 if the logic has no
		/// `time` field (e.g. non-timed logic), signalling the caller to use the suffix
		/// fallback.
		/// </summary>
		private static int GetComplyTotalSeconds(object logic) {
			if (logic == null) return 0;
			Type t = logic.GetType();
			FieldInfo field;
			if (!_olTimeFieldCache.TryGetValue(t, out field)) {
				field = t.GetField("time", BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
				if (field != null && field.FieldType != typeof(float)) field = null;
				_olTimeFieldCache[t] = field;
			}
			if (field == null) return 0;
			return (int)ReflectionHelper.GetFloat(field, logic);
		}

		/// <summary>
		/// Replace totalAmount with progressAmount in text. When progressAmount is numeric
		/// (e.g. "2/3"), substitutes inline. When it's a status word (e.g. "Done"),
		/// removes the amount and appends the status at the end.
		/// </summary>
		private static string ReplaceAmount(string text, string totalAmount, string progressAmount) {
			int pos = text.IndexOf(totalAmount, StringComparison.Ordinal);
			if (pos < 0) return null;

			if (progressAmount.Length > 0 && char.IsDigit(progressAmount[0])) {
				// Numeric: substitute in place
				return text.Substring(0, pos) + progressAmount
					+ text.Substring(pos + totalAmount.Length);
			}

			// Non-numeric (e.g. "Done"): keep the target amount in the text.
			// Caller prefixes with checkmark for completed objectives.
			return text;
		}

		/// <summary>
		/// Get the localized source text for a ReputationGainedFromSourceLogic
		/// (e.g. "Orders", "Resolve", "Relics"). Returns null for other logic types.
		/// </summary>
		private static string GetReputationSourceText(object logic) {
			if (logic == null || _repSourceLogicType == null || !_repSourceLogicType.IsInstanceOfType(logic))
				return null;

			// Try the game's localized GetSourceText() first
			var text = ReflectionHelper.InvokeString(_repGetSourceTextMethod, logic);
			if (!string.IsNullOrEmpty(text))
				return StripRichText(text).Trim();

			// Fallback: read the source enum field directly
			int source = ReflectionHelper.GetEnum(_repSourceField, logic);
			switch (source) {
				case 1: return Strings.Get("common.orders");
				case 2: return Strings.Get("common.resolve");
				case 3: return Strings.Get("common.relics");
			}

			return null;
		}

		/// <summary>
		/// Get objective texts for a pick option (no ObjectiveState yet).
		/// Uses DisplayName + GetAmountText() from each logic.
		/// </summary>
		public static List<string> GetPickObjectiveTexts(object orderModel, int setIndex) {
			EnsureCached();
			var result = new List<string>();
			if (orderModel == null) return result;

			try {
				Array logics = null;

				// Try GetLogics(int setIndex)
				if (_omGetLogicsIntMethod != null)
					logics = ReflectionHelper.Invoke(_omGetLogicsIntMethod, orderModel, setIndex) as Array;

				if (logics == null) return result;

				foreach (var logic in logics) {
					if (logic == null) continue;

					string displayName = ReflectionHelper.GetPropString(_olDisplayNameProperty, logic);
					if (string.IsNullOrEmpty(displayName)) continue;

					// Some logics embed the amount in DisplayName (e.g. "Deliver 10 Amber"),
					// others keep it separate (e.g. "Produce Pipes" + amount "6").
					// For the latter, use the Description property which formats correctly
					// (e.g. "Produce 6 Pipes").
					string amountText = ReflectionHelper.InvokeString(_olGetAmountTextMethod, logic);

					if (!string.IsNullOrEmpty(amountText)) {
						string stripped = StripRichText(amountText);
						if (!string.IsNullOrEmpty(stripped) && !displayName.Contains(stripped)) {
							// Amount not in DisplayName - use type-specific formatting
							string typeName = logic.GetType().Name;
							bool skipDescription = typeName.Contains("Building");

							if (!skipDescription) {
								// Try Description which may have proper localized placement
								// (e.g. "Earn 3 Reputation Points from Orders")
								string desc = ReflectionHelper.GetPropString(_olDescriptionProperty, logic);
								string strippedDesc = !string.IsNullOrEmpty(desc) ? StripRichText(desc).Trim() : null;

								if (!string.IsNullOrEmpty(strippedDesc) && strippedDesc.Contains(stripped)) {
									// Description has the amount placed by localization
									// Truncate multi-sentence descriptions (upgrade tutorial text)
									int sentenceEnd = strippedDesc.IndexOf(". ", StringComparison.Ordinal);
									if (sentenceEnd >= 0)
										strippedDesc = strippedDesc.Substring(0, sentenceEnd);
									result.Add(TrimObjectiveText(strippedDesc));
									continue;
								}
							}

							// Fallback: type-specific formatting
							string formatted;
							if (typeName.Contains("Building")) {
								formatted = TrimObjectiveText(Strings.Get("reflection.orders.objective_build", stripped, displayName));
							} else {
								// Word-order cannot be reconstructed safely without locale knowledge;
								// fall back to bare "{amount} {name}" via the shared key.
								formatted = TrimObjectiveText(Strings.Get("common.amount_and_name", stripped, displayName));
							}

							// Append source for reputation objectives when not already in text
							string source = GetReputationSourceText(logic);
							if (source != null)
								formatted = Strings.Get("reflection.orders.objective_from", formatted, source);

							result.Add(formatted);
						} else {
							result.Add(TrimObjectiveText(displayName));
						}
					} else {
						result.Add(TrimObjectiveText(displayName));
					}
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetPickObjectiveTexts failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get stored amounts for pick objectives (e.g. "Planks: 5 in storage").
		/// Only returns entries for objectives that have stored amounts.
		/// </summary>
		public static List<string> GetPickStoredAmounts(object orderModel, int setIndex) {
			EnsureCached();
			var result = new List<string>();
			if (orderModel == null || _olHasStoredAmountProperty == null || _olGetStoredAmountProperty == null)
				return result;

			try {
				Array logics = null;
				if (_omGetLogicsIntMethod != null)
					logics = ReflectionHelper.Invoke(_omGetLogicsIntMethod, orderModel, setIndex) as Array;
				if (logics == null) return result;

				foreach (var logic in logics) {
					if (logic == null) continue;

					bool hasStored = ReflectionHelper.GetPropBool(_olHasStoredAmountProperty, logic);
					if (!hasStored) continue;

					int stored = ReflectionHelper.GetPropInt(_olGetStoredAmountProperty, logic);
					string displayName = ReflectionHelper.GetPropString(_olDisplayNameProperty, logic);
					if (string.IsNullOrEmpty(displayName)) continue;

					result.Add($"{displayName}: {stored}");
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetPickStoredAmounts failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get warning texts for pick objectives (e.g. missing building warnings).
		/// Only returns non-null warnings.
		/// </summary>
		public static List<string> GetPickWarningTexts(object orderModel, int setIndex) {
			EnsureCached();
			var result = new List<string>();
			if (orderModel == null || _olGetWarningTextMethod == null) return result;

			try {
				Array logics = null;
				if (_omGetLogicsIntMethod != null)
					logics = ReflectionHelper.Invoke(_omGetLogicsIntMethod, orderModel, setIndex) as Array;
				if (logics == null) return result;

				foreach (var logic in logics) {
					if (logic == null) continue;

					string warning = ReflectionHelper.InvokeString(_olGetWarningTextMethod, logic);
					if (string.IsNullOrEmpty(warning)) continue;

					string stripped = StripRichText(warning).Trim();
					if (!string.IsNullOrEmpty(stripped))
						result.Add(stripped);
				}
			} catch (Exception ex) {
				Debug.LogWarning($"[ATSAccessibility] GetPickWarningTexts failed: {ex.Message}");
			}

			return result;
		}

		// ========================================
		// REWARDS
		// ========================================

		/// <summary>
		/// Get reward display texts for an active order's rewards list.
		/// </summary>
		public static List<string> GetRewardTexts(object orderState) {
			EnsureCached();
			return ResolveEffectNames(GetRewardsList(orderState));
		}

		/// <summary>
		/// Get reward display texts for a pick option's rewards list.
		/// </summary>
		public static List<string> GetPickRewardTexts(object pickState) {
			EnsureCached();
			var rewards = ReflectionHelper.GetList(_opsRewardsField, pickState);
			return ResolveEffectNames(rewards);
		}

		/// <summary>
		/// Get the reputation reward text for an order model.
		/// </summary>
		public static string GetReputationRewardText(object orderModel) {
			EnsureCached();
			var effectModel = ReflectionHelper.GetField(_omReputationRewardField, orderModel);
			if (effectModel == null) return null;

			string amountText = ReflectionHelper.InvokeString(_emGetAmountTextMethod, effectModel);
			if (!string.IsNullOrEmpty(amountText))
				return Strings.Get("reflection.orders.reputation_amount", StripRichText(amountText));
			return Strings.Get("reflection.orders.reputation_amount", "1");
		}

		private static IList GetRewardsList(object orderState) => ReflectionHelper.GetList(_osRewardsField, orderState);

		private static List<string> ResolveEffectNames(IList effectNames) {
			var result = new List<string>();
			if (effectNames == null) return result;

			var settings = GameReflection.GetSettings();
			if (settings == null || _settingsGetEffectMethod == null) return result;

			foreach (var nameObj in effectNames) {
				var name = nameObj as string;
				if (string.IsNullOrEmpty(name)) continue;

				var effectModel = ReflectionHelper.Invoke(_settingsGetEffectMethod, settings, name);
				if (effectModel == null) continue;

				// Building blueprints have verbose descriptions listing productions;
				// use DisplayName (just the building name) for those.
				bool isBlueprint = name.Contains("Blueprint");
				string text = GetEffectDisplayText(effectModel, isBlueprint);
				if (!string.IsNullOrEmpty(text))
					result.Add(isBlueprint ? Strings.Get("reflection.orders.blueprint_prefix", text) : text);
			}

			return result;
		}

		private static string GetEffectDisplayText(object effectModel, bool useDisplayName = false) {
			if (effectModel == null) return null;

			string displayName = ReflectionHelper.GetPropString(_emDisplayNameProperty, effectModel);

			if (useDisplayName)
				return displayName;

			// Prefer Description (tooltip text) - gives actual mechanical meaning
			string description = ReflectionHelper.GetPropString(_emDescriptionProperty, effectModel);
			if (!string.IsNullOrEmpty(description)) {
				string stripped = StripRichText(description).Trim();
				if (!string.IsNullOrEmpty(stripped)) {
					// Simplify verbose production bonus descriptions
					stripped = ProductionBonusRegex.Replace(stripped, "$1 bonus to $2 production");
					return TrimObjectiveText(stripped);
				}
			}

			// Fallback to DisplayName + amount
			string amountText = ReflectionHelper.InvokeString(_emGetAmountTextMethod, effectModel);

			if (!string.IsNullOrEmpty(amountText))
				return StripRichText($"{amountText} {displayName}");
			return displayName;
		}

		// ========================================
		// ACTIONS
		// ========================================

		/// <summary>
		/// Check if an order can be completed (delivered).
		/// </summary>
		public static bool CanComplete(object orderState, object orderModel) {
			EnsureCached();
			var service = GetOrdersService();
			if (service == null) return false;
			return ReflectionHelper.InvokeBool(_osCanCompleteMethod, service, orderState, orderModel);
		}

		/// <summary>
		/// Complete (deliver) an order. Returns false if method not found.
		/// </summary>
		public static bool CompleteOrder(object orderState, object orderModel) {
			EnsureCached();
			var service = GetOrdersService();
			if (service == null) return false;
			return ReflectionHelper.InvokeVoid(_osCompleteOrderMethod, service, orderState, orderModel, false);
		}

		/// <summary>
		/// Pick an order option. Returns false if method not found.
		/// </summary>
		public static bool PickOrder(object orderState, object pickState) {
			EnsureCached();
			var service = GetOrdersService();
			if (service == null) return false;
			return ReflectionHelper.InvokeVoid(_osOrderPickedMethod, service, orderState, pickState);
		}

		/// <summary>
		/// Toggle order tracking. Returns false if method not found.
		/// </summary>
		public static bool ToggleTracking(object orderState) {
			EnsureCached();
			var service = GetOrdersService();
			if (service == null) return false;
			bool currentlyTracked = IsTracked(orderState);
			return ReflectionHelper.InvokeVoid(_osSwitchOrderTrackingMethod, service, orderState, !currentlyTracked);
		}

		/// <summary>
		/// Fire the OrderPickPopupRequested event to open the pick popup.
		/// </summary>
		public static bool FireOrderPickPopupRequested(object orderState) {
			EnsureCached();
			if (orderState == null || _gbbOrderPickPopupRequestedProperty == null) return false;

			var blackboard = GameReflection.GetGameBlackboardService();
			if (blackboard == null) return false;

			var subject = ReflectionHelper.GetProp(_gbbOrderPickPopupRequestedProperty, blackboard);
			if (subject == null) return false;

			// Get OnNext method from the subject
			if (_subjectOnNextMethod == null)
				_subjectOnNextMethod = subject.GetType().GetMethod("OnNext", GameReflection.PublicInstance);

			return ReflectionHelper.InvokeVoid(_subjectOnNextMethod, subject, orderState);
		}

		/// <summary>
		/// Get the OrderState from the OrderPickPopup's private 'order' field.
		/// </summary>
		public static object GetPopupOrder(object popup) {
			EnsureCached();
			return ReflectionHelper.GetField(_oppOrderField, popup);
		}

		/// <summary>
		/// Hide a popup by calling Popup.Hide().
		/// </summary>
		public static bool HidePopup(object popup) {
			EnsureCached();
			return ReflectionHelper.InvokeVoid(_popupHideMethod, popup);
		}

		// ========================================
		// PICK ACCESS
		// ========================================

		/// <summary>
		/// Get available picks for an order.
		/// </summary>
		public static IList GetPicksFor(object orderState) {
			EnsureCached();
			var service = GetOrdersService();
			if (service == null) return null;
			return ReflectionHelper.Invoke(_osGetPicksForMethod, service, orderState) as IList;
		}

		// ========================================
		// PICK STATE ACCESS
		// ========================================

		public static string GetPickModel(object pickState) => ReflectionHelper.GetString(_opsModelField, pickState);

		public static int GetPickSetIndex(object pickState) => ReflectionHelper.GetInt(_opsSetIndexField, pickState);

		public static bool IsPickFailed(object pickState) => ReflectionHelper.GetBool(_opsFailedField, pickState);

		/// <summary>
		/// Get the OrderModel for a pick state (resolves pick.model via Settings).
		/// </summary>
		public static object GetPickOrderModel(object pickState) {
			EnsureCached();
			var modelName = GetPickModel(pickState);
			if (string.IsNullOrEmpty(modelName)) return null;

			var settings = GameReflection.GetSettings();
			if (settings == null || _settingsGetOrderMethod == null) return null;

			return ReflectionHelper.Invoke(_settingsGetOrderMethod, settings, modelName);
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(OrdersReflection), "OrdersReflection");
		}
	}
}
