using System;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Shrine-specific reflection (beacon tower abilities).
	/// Extracted from BuildingReflection.cs — routing type-check (IsShrine) stays there.
	/// </summary>
	public static class ShrineReflection {
		// ========================================
		// CACHED REFLECTION METADATA
		// ========================================

		internal static Type _shrineType = null;
		private static FieldInfo _shrineStateField = null;
		private static FieldInfo _shrineModelField = null;
		private static FieldInfo _shrineStateEffectsField = null;
		private static FieldInfo _shrineModelEffectsField = null;
		private static FieldInfo _shrineEffectsStateChargesLeftField = null;
		private static FieldInfo _shrineEffectsModelLabelField = null;
		private static FieldInfo _shrineEffectsModelChargesField = null;
		private static FieldInfo _shrineEffectsModelEffectsField = null;
		private static MethodInfo _shrineUseEffectMethod = null;
		private static FieldInfo _shrineModelChargingLoopField = null;
		private static FieldInfo _shrineModelFinalSoundField = null;
		private static bool _shrineTypesCached = false;

		// ========================================
		// INITIALIZATION
		// ========================================

		internal static void EnsureTypes() {
			if (_shrineTypesCached) return;
			_shrineTypesCached = true;

			ReflectionHelper.InitCache("ShrineReflection", assembly => {
				_shrineType = assembly.GetType("Eremite.Buildings.Shrine");
				if (_shrineType != null) {
					_shrineStateField = _shrineType.GetField("state", GameReflection.PublicInstance);
					_shrineModelField = _shrineType.GetField("model", GameReflection.PublicInstance);
					_shrineUseEffectMethod = _shrineType.GetMethod("UseEffect", GameReflection.PublicInstance);
				}

				var shrineStateType = assembly.GetType("Eremite.Buildings.ShrineState");
				if (shrineStateType != null) {
					_shrineStateEffectsField = shrineStateType.GetField("effects", GameReflection.PublicInstance);
				}

				var shrineModelType = assembly.GetType("Eremite.Buildings.ShrineModel");
				if (shrineModelType != null) {
					_shrineModelEffectsField = shrineModelType.GetField("effects", GameReflection.PublicInstance);
					_shrineModelChargingLoopField = shrineModelType.GetField("effectChargingLoop", GameReflection.PublicInstance);
					_shrineModelFinalSoundField = shrineModelType.GetField("effectChargingFinalSound", GameReflection.PublicInstance);
				}

				var shrineEffectsStateType = assembly.GetType("Eremite.Buildings.ShrineEffectsState");
				if (shrineEffectsStateType != null) {
					_shrineEffectsStateChargesLeftField = shrineEffectsStateType.GetField("chargesLeft", GameReflection.PublicInstance);
				}

				var shrineEffectsModelType = assembly.GetType("Eremite.Buildings.ShrineEffectsModel");
				if (shrineEffectsModelType != null) {
					_shrineEffectsModelLabelField = shrineEffectsModelType.GetField("label", GameReflection.PublicInstance);
					_shrineEffectsModelChargesField = shrineEffectsModelType.GetField("charges", GameReflection.PublicInstance);
					_shrineEffectsModelEffectsField = shrineEffectsModelType.GetField("effects", GameReflection.PublicInstance);
				}
			});
		}

		// ========================================
		// PUBLIC API
		// ========================================

		public static bool IsShrine(object building) {
			if (building == null) return false;
			EnsureTypes();
			if (_shrineType == null) return false;
			return _shrineType.IsInstanceOfType(building);
		}

		public static int GetEffectTierCount(object building) {
			if (!IsShrine(building)) return 0;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_shrineModelField, building);
				if (model == null) return 0;

				var effects = _shrineModelEffectsField?.GetValue(model) as Array;
				return effects?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		public static string GetTierLabel(object building, int tierIndex) {
			if (!IsShrine(building)) return null;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_shrineModelField, building);
				if (model == null) return null;

				var effects = _shrineModelEffectsField?.GetValue(model) as Array;
				if (effects == null || tierIndex >= effects.Length) return null;

				var effectModel = effects.GetValue(tierIndex);
				var label = ReflectionHelper.GetField(_shrineEffectsModelLabelField, effectModel);
				return GameReflection.GetLocaText(label);
			} catch {
				return null;
			}
		}

		public static int GetTierChargesLeft(object building, int tierIndex) {
			if (!IsShrine(building)) return 0;
			EnsureTypes();

			try {
				var state = ReflectionHelper.GetField(_shrineStateField, building);
				if (state == null) return 0;

				var effects = _shrineStateEffectsField?.GetValue(state) as Array;
				if (effects == null || tierIndex >= effects.Length) return 0;

				var effectState = effects.GetValue(tierIndex);
				return ReflectionHelper.GetInt(_shrineEffectsStateChargesLeftField, effectState);
			} catch {
				return 0;
			}
		}

		public static int GetTierMaxCharges(object building, int tierIndex) {
			if (!IsShrine(building)) return 0;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_shrineModelField, building);
				if (model == null) return 0;

				var effects = _shrineModelEffectsField?.GetValue(model) as Array;
				if (effects == null || tierIndex >= effects.Length) return 0;

				var effectModel = effects.GetValue(tierIndex);
				return ReflectionHelper.GetInt(_shrineEffectsModelChargesField, effectModel);
			} catch {
				return 0;
			}
		}

		public static int GetTierEffectCount(object building, int tierIndex) {
			if (!IsShrine(building)) return 0;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_shrineModelField, building);
				if (model == null) return 0;

				var effectTiers = _shrineModelEffectsField?.GetValue(model) as Array;
				if (effectTiers == null || tierIndex >= effectTiers.Length) return 0;

				var effectModel = effectTiers.GetValue(tierIndex);
				var effects = _shrineEffectsModelEffectsField?.GetValue(effectModel) as Array;
				return effects?.Length ?? 0;
			} catch {
				return 0;
			}
		}

		public static bool CanTierEffectBeDrawn(object building, int tierIndex, int effectIndex) {
			if (!IsShrine(building)) return false;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_shrineModelField, building);
				if (model == null) return false;

				var effectTiers = _shrineModelEffectsField?.GetValue(model) as Array;
				if (effectTiers == null || tierIndex >= effectTiers.Length) return false;

				var effectModel = effectTiers.GetValue(tierIndex);
				var effects = _shrineEffectsModelEffectsField?.GetValue(effectModel) as Array;
				if (effects == null || effectIndex >= effects.Length) return false;

				var effect = effects.GetValue(effectIndex);
				var canBeDrawnMethod = effect.GetType().GetMethod("CanBeDrawn", GameReflection.PublicInstance);
				if (canBeDrawnMethod == null) return true;

				return (bool)canBeDrawnMethod.Invoke(effect, null);
			} catch {
				return true;
			}
		}

		public static string GetTierEffectName(object building, int tierIndex, int effectIndex) {
			if (!IsShrine(building)) return null;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_shrineModelField, building);
				if (model == null) return null;

				var effectTiers = _shrineModelEffectsField?.GetValue(model) as Array;
				if (effectTiers == null || tierIndex >= effectTiers.Length) return null;

				var effectModel = effectTiers.GetValue(tierIndex);
				var effects = _shrineEffectsModelEffectsField?.GetValue(effectModel) as Array;
				if (effects == null || effectIndex >= effects.Length) return null;

				var effect = effects.GetValue(effectIndex);
				var effectType = effect.GetType();

				var displayNameProp = effectType.GetProperty("DisplayName", GameReflection.PublicInstance);
				var descriptionProp = effectType.GetProperty("Description", GameReflection.PublicInstance);

				string displayName = displayNameProp?.GetValue(effect) as string;
				string description = descriptionProp?.GetValue(effect) as string;

				string species = ExtractSpeciesFromEffect(effect, effectType, description);
				if (!string.IsNullOrEmpty(species) && !string.IsNullOrEmpty(displayName)) {
					return $"{displayName} {species}";
				}

				return displayName;
			} catch {
				return null;
			}
		}

		public static string GetTierEffectDescription(object building, int tierIndex, int effectIndex) {
			if (!IsShrine(building)) return null;
			EnsureTypes();

			try {
				var model = ReflectionHelper.GetField(_shrineModelField, building);
				if (model == null) return null;

				var effectTiers = _shrineModelEffectsField?.GetValue(model) as Array;
				if (effectTiers == null || tierIndex >= effectTiers.Length) return null;

				var effectModel = effectTiers.GetValue(tierIndex);
				var effects = _shrineEffectsModelEffectsField?.GetValue(effectModel) as Array;
				if (effects == null || effectIndex >= effects.Length) return null;

				var effect = effects.GetValue(effectIndex);
				var descriptionProp = effect.GetType().GetProperty("Description", GameReflection.PublicInstance);
				return descriptionProp?.GetValue(effect) as string;
			} catch {
				return null;
			}
		}

		private static string ExtractSpeciesFromEffect(object effect, Type effectType, string description) {
			if (!string.IsNullOrEmpty(description)) {
				int parenStart = description.IndexOf('(');
				int parenEnd = description.IndexOf(')');
				if (parenStart >= 0 && parenEnd > parenStart) {
					string content = description.Substring(parenStart + 1, parenEnd - parenStart - 1);
					if (!string.IsNullOrEmpty(content) && content.Length < 20 &&
						!content.Contains(" ") && !char.IsDigit(content[0])) {
						return content;
					}
				}
			}

			var raceField = effectType.GetField("race", GameReflection.PublicInstance) ??
						   effectType.GetField("specificRace", GameReflection.PublicInstance);
			if (raceField != null) {
				var raceModel = raceField.GetValue(effect);
				if (raceModel != null) {
					// RaceModel.displayName is a public field, not a property.
					var raceDisplayNameField = raceModel.GetType().GetField("displayName", GameReflection.PublicInstance);
					if (raceDisplayNameField != null) {
						var locaText = raceDisplayNameField.GetValue(raceModel);
						if (locaText != null) {
							var textProp = locaText.GetType().GetProperty("Text", GameReflection.PublicInstance);
							return textProp?.GetValue(locaText) as string;
						}
					}
				}
			}

			return null;
		}

		public static bool UseEffect(object building, int tierIndex, int effectIndex) {
			if (!IsShrine(building)) {
				Debug.Log($"[ATSAccessibility] ShrineReflection.UseEffect: Not a shrine");
				return false;
			}

			EnsureTypes();

			try {
				int maxCharges = GetTierMaxCharges(building, tierIndex);
				int chargesLeft = GetTierChargesLeft(building, tierIndex);
				Debug.Log($"[ATSAccessibility] ShrineReflection.UseEffect: tier={tierIndex}, effect={effectIndex}, maxCharges={maxCharges}, chargesLeft={chargesLeft}");

				if (maxCharges > 0 && chargesLeft <= 0) {
					Debug.Log($"[ATSAccessibility] ShrineReflection.UseEffect: No charges remaining");
					return false;
				}

				var state = ReflectionHelper.GetField(_shrineStateField, building);
				var model = ReflectionHelper.GetField(_shrineModelField, building);
				if (state == null || model == null) {
					Debug.Log($"[ATSAccessibility] ShrineReflection.UseEffect: state={state != null}, model={model != null}");
					return false;
				}

				var stateEffects = _shrineStateEffectsField?.GetValue(state) as Array;
				var modelEffects = _shrineModelEffectsField?.GetValue(model) as Array;
				if (stateEffects == null || modelEffects == null) {
					Debug.Log($"[ATSAccessibility] ShrineReflection.UseEffect: stateEffects={stateEffects != null}, modelEffects={modelEffects != null}");
					return false;
				}
				if (tierIndex >= stateEffects.Length || tierIndex >= modelEffects.Length) {
					Debug.Log($"[ATSAccessibility] ShrineReflection.UseEffect: tierIndex out of bounds");
					return false;
				}

				var effectState = stateEffects.GetValue(tierIndex);
				var effectModel = modelEffects.GetValue(tierIndex);

				if (_shrineUseEffectMethod == null) {
					Debug.Log($"[ATSAccessibility] ShrineReflection.UseEffect: UseEffect method not found");
					return false;
				}

				Debug.Log($"[ATSAccessibility] ShrineReflection.UseEffect: Invoking UseEffect({effectState?.GetType().Name}, {effectModel?.GetType().Name}, {effectIndex})");
				_shrineUseEffectMethod.Invoke(building, new object[] { effectState, effectModel, effectIndex });
				Debug.Log($"[ATSAccessibility] ShrineReflection.UseEffect: Success");
				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] ShrineReflection.UseEffect failed: {ex.Message}\n{ex.StackTrace}");
				return false;
			}
		}

		public static object GetChargingLoopSound(object building) {
			if (!IsShrine(building)) return null;
			EnsureTypes();
			return GetSoundModel(building, _shrineModelChargingLoopField);
		}

		public static object GetFinalSound(object building) {
			if (!IsShrine(building)) return null;
			EnsureTypes();
			return GetSoundModel(building, _shrineModelFinalSoundField);
		}

		private static object GetSoundModel(object building, FieldInfo soundField) {
			if (soundField == null) return null;

			try {
				var model = ReflectionHelper.GetField(_shrineModelField, building);
				if (model == null) return null;

				var soundRef = soundField.GetValue(model);
				if (soundRef == null) return null;

				return ReflectionHelper.Invoke(BuildingReflection.SoundRefGetNextMethod, soundRef);
			} catch {
				return null;
			}
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(ShrineReflection), "ShrineReflection");
		}
	}
}
