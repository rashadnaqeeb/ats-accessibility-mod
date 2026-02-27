using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility.Reflection {
	/// <summary>
	/// Reflection helper for GameResultPopup data extraction.
	/// Provides access to win/loss state, progression data, score breakdown, and world event info.
	/// </summary>
	public static class GameResultReflection {
		// Cached reflection metadata (safe to cache)
		private static bool _typesCached;

		// GameResultPopup type
		private static Type _gameResultPopupType;

		// GameResultPopup fields
		private static FieldInfo _headerTextField;
		private static FieldInfo _descTextField;
		private static FieldInfo _menuButtonField;
		private static FieldInfo _continueButtonField;

		// GameMB.StateService access
		private static PropertyInfo _gameMBStateServiceProperty;
		private static PropertyInfo _stateServiceGameObjectivesProperty;
		private static FieldInfo _gameObjectivesHasWonField;
		private static FieldInfo _gameObjectivesHasLostField;

		// GameMB.GameSealService for sealed biome check
		private static PropertyInfo _gameMBGameSealServiceProperty;
		private static MethodInfo _isSealedBiomeMethod;

		// TutorialService for tutorial check
		private static PropertyInfo _mbTutorialServiceProperty;
		private static MethodInfo _isAnyTutorialMethod;

		// GameMB.Biome for tutorial check
		private static PropertyInfo _gameMBBiomeProperty;

		// GameGoalsService for completed goals
		private static PropertyInfo _gameServicesGameGoalsServiceProperty;
		private static MethodInfo _getUnshownCompletedGoalsMethod;

		// MetaStateService for progression data
		private static PropertyInfo _metaStateServiceProperty;
		private static PropertyInfo _mssEconomyProperty;
		private static PropertyInfo _mssLevelProperty;
		private static FieldInfo _economyCurrentCycleExpField;
		private static FieldInfo _levelLevelField;
		private static FieldInfo _levelExpField;
		private static FieldInfo _levelTargetExpField;

		// Settings.GetGoal for goal display names
		private static MethodInfo _settingsGetGoalMethod;
		private static FieldInfo _goalDisplayNameField;

		// ScoreCalculator for score breakdown
		private static Type _scoreCalculatorType;
		private static MethodInfo _getScoreMethod;
		private static FieldInfo _scoreDataLabelField;
		private static FieldInfo _scoreDataPointsField;
		private static FieldInfo _scoreDataAmountField;

		// WorldStateService for world event
		private static PropertyInfo _mbWorldStateServiceProperty;
		private static PropertyInfo _wsssCycleProperty;
		private static FieldInfo _cycleActiveCycleGoalsField;
		private static FieldInfo _goalStateModelField;
		private static FieldInfo _goalStateCompletedField;
		private static FieldInfo _goalDescriptionField;
		private static MethodInfo _goalGetObjectivesBreakdownMethod;

		// TMP_Text.text property
		private static PropertyInfo _tmpTextProperty;

		// MetaCurrency data
		private static FieldInfo _metaCurrencyNameField;
		private static FieldInfo _metaCurrencyAmountField;
		private static PropertyInfo _stateConditionsProperty;
		private static FieldInfo _conditionsRewardsField;

		// BiomeService for seal fragments
		private static PropertyInfo _gameMBBiomeServiceProperty;
		private static PropertyInfo _biomeServiceDifficultyProperty;
		private static FieldInfo _difficultySealFragmentsField;

		// ConditionsService for custom game check
		private static PropertyInfo _gameMBConditionsServiceProperty;
		private static MethodInfo _isCustomGameMethod;
		private static MethodInfo _isChallangeMethod;

		// ========================================
		// DETECTION
		// ========================================

		/// <summary>
		/// Check if the given popup is a GameResultPopup.
		/// </summary>
		public static bool IsGameResultPopup(object popup) {
			if (popup == null) return false;
			EnsureTypes();
			return _gameResultPopupType != null && _gameResultPopupType.IsInstanceOfType(popup);
		}

		// ========================================
		// WIN/LOSS STATE
		// ========================================

		/// <summary>
		/// Check if the player has won the settlement.
		/// </summary>
		public static bool HasWon() {
			EnsureTypes();
			try {
				var stateService = GetStateService();
				if (stateService == null) return false;

				var gameObjectives = ReflectionHelper.GetProp(_stateServiceGameObjectivesProperty, stateService);
				if (gameObjectives == null) return false;

				return ReflectionHelper.GetBool(_gameObjectivesHasWonField, gameObjectives);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GameResultReflection.HasWon failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Check if the player has lost the settlement.
		/// </summary>
		public static bool HasLost() {
			EnsureTypes();
			try {
				var stateService = GetStateService();
				if (stateService == null) return false;

				var gameObjectives = ReflectionHelper.GetProp(_stateServiceGameObjectivesProperty, stateService);
				if (gameObjectives == null) return false;

				return ReflectionHelper.GetBool(_gameObjectivesHasLostField, gameObjectives);
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GameResultReflection.HasLost failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Check if current game is a sealed biome run.
		/// </summary>
		public static bool IsSealedBiome() {
			EnsureTypes();
			try {
				var gameSealService = GetGameSealService();
				if (gameSealService == null) return false;

				return ReflectionHelper.InvokeBool(_isSealedBiomeMethod, gameSealService);
			} catch {
				return false;
			}
		}

		/// <summary>
		/// Check if current game is a tutorial.
		/// </summary>
		public static bool IsTutorial() {
			EnsureTypes();
			try {
				var tutorialService = GetTutorialService();
				var biome = GetBiome();
				if (tutorialService == null || biome == null) return false;

				return ReflectionHelper.InvokeBool(_isAnyTutorialMethod, tutorialService, biome);
			} catch {
				return false;
			}
		}

		// ========================================
		// TEXT CONTENT
		// ========================================

		/// <summary>
		/// Get the header text from the popup (Victory/Defeat).
		/// </summary>
		public static string GetHeaderText(object popup) {
			EnsureTypes();
			try {
				if (popup == null || _headerTextField == null) return null;

				var headerText = _headerTextField.GetValue(popup);
				if (headerText == null) return null;

				return GetTMPText(headerText);
			} catch {
				return null;
			}
		}

		/// <summary>
		/// Get the description/flavor text from the popup.
		/// </summary>
		public static string GetDescriptionText(object popup) {
			EnsureTypes();
			try {
				if (popup == null || _descTextField == null) return null;

				var descText = _descTextField.GetValue(popup);
				if (descText == null) return null;

				return GetTMPText(descText);
			} catch {
				return null;
			}
		}

		private static string GetTMPText(object tmpText) {
			if (tmpText == null) return null;

			if (_tmpTextProperty == null) {
				_tmpTextProperty = tmpText.GetType().GetProperty("text", GameReflection.PublicInstance);
			}

			return ReflectionHelper.GetPropString(_tmpTextProperty, tmpText);
		}

		// ========================================
		// PROGRESSION DATA
		// ========================================

		/// <summary>
		/// Get the experience gained this game.
		/// </summary>
		public static int GetGainedExp() {
			EnsureTypes();
			try {
				var metaStateService = GetMetaStateService();
				if (metaStateService == null) return 0;

				var economy = ReflectionHelper.GetProp(_mssEconomyProperty, metaStateService);
				if (economy == null) return 0;

				return ReflectionHelper.GetInt(_economyCurrentCycleExpField, economy);
			} catch {
				return 0;
			}
		}

		/// <summary>
		/// Get level info: (current level, current exp, target exp).
		/// </summary>
		public static (int level, int exp, int targetExp) GetLevelInfo() {
			EnsureTypes();
			try {
				var metaStateService = GetMetaStateService();
				if (metaStateService == null) return (0, 0, 0);

				var level = ReflectionHelper.GetProp(_mssLevelProperty, metaStateService);
				if (level == null) return (0, 0, 0);

				int currentLevel = ReflectionHelper.GetInt(_levelLevelField, level);
				int currentExp = ReflectionHelper.GetInt(_levelExpField, level);
				int targetExp = ReflectionHelper.GetInt(_levelTargetExpField, level);

				return (currentLevel, currentExp, targetExp);
			} catch {
				return (0, 0, 0);
			}
		}

		/// <summary>
		/// Get list of completed goal display names.
		/// </summary>
		public static List<string> GetCompletedGoals() {
			EnsureTypes();
			var result = new List<string>();

			try {
				var gameGoalsService = GetGameGoalsService();
				if (gameGoalsService == null) return result;

				// Call GetUnshownCompletedGoals - returns List<string> of goal IDs
				var goalIds = ReflectionHelper.Invoke(_getUnshownCompletedGoalsMethod, gameGoalsService) as IList;
				if (goalIds == null) return result;

				var settings = GameReflection.GetSettings();
				if (settings == null || _settingsGetGoalMethod == null) return result;

				foreach (var goalId in goalIds) {
					if (goalId == null) continue;

					var goal = ReflectionHelper.Invoke(_settingsGetGoalMethod, settings, goalId);
					if (goal == null) continue;

					// Get display name
					var displayName = ReflectionHelper.GetLocaString(_goalDisplayNameField, goal);
					if (!string.IsNullOrEmpty(displayName)) {
						result.Add(displayName);
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GameResultReflection.GetCompletedGoals failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get meta currencies earned: (name, amount).
		/// </summary>
		public static List<(string name, int amount)> GetMetaCurrencies() {
			EnsureTypes();
			var result = new List<(string name, int amount)>();

			try {
				var stateService = GetStateService();
				if (stateService == null) return result;

				// Get Conditions.rewards
				var conditions = ReflectionHelper.GetProp(_stateConditionsProperty, stateService);
				if (conditions == null) return result;

				var rewards = ReflectionHelper.GetList(_conditionsRewardsField, conditions);
				if (rewards == null) return result;

				foreach (var reward in rewards) {
					if (reward == null) continue;

					string name = ReflectionHelper.GetString(_metaCurrencyNameField, reward);
					int amount = ReflectionHelper.GetInt(_metaCurrencyAmountField, reward);

					if (!string.IsNullOrEmpty(name) && amount > 0) {
						// Get display name from settings
						string displayName = GetMetaCurrencyDisplayName(name);
						result.Add((displayName ?? name, amount));
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GameResultReflection.GetMetaCurrencies failed: {ex.Message}");
			}

			return result;
		}

		private static string GetMetaCurrencyDisplayName(string currencyName) {
			return GameReflection.GetMetaCurrencyDisplayName(currencyName);
		}

		/// <summary>
		/// Get stored meta currencies (goods in storage that convert to meta currency).
		/// These are separate from field rewards - they're goods collected during the game.
		/// Returns list of (displayName, amount) tuples.
		/// </summary>
		public static List<(string name, int amount)> GetStoredMetaCurrencies() {
			EnsureTypes();
			var result = new List<(string name, int amount)>();

			try {
				var settings = GameReflection.GetSettings();
				if (settings == null) return result;

				// Get Settings.metaCurrencies array
				var metaCurrenciesField = settings.GetType().GetField("metaCurrencies",
					System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
				if (metaCurrenciesField == null) return result;

				var metaCurrencies = metaCurrenciesField.GetValue(settings) as Array;
				if (metaCurrencies == null) return result;

				// Get storage service
				var storageService = GameReflection.GetStorageService();
				if (storageService == null) return result;

				var getAmountMethod = storageService.GetType().GetMethod("GetAmount",
					new[] { typeof(string) });
				if (getAmountMethod == null) return result;

				foreach (var currencyModel in metaCurrencies) {
					if (currencyModel == null) continue;

					// Get the good field from MetaCurrencyModel
					var goodField = currencyModel.GetType().GetField("good",
						System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
					var good = goodField?.GetValue(currencyModel);
					if (good == null) continue;

					// Get good.Name
					var nameProperty = good.GetType().GetProperty("Name",
						System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
					string goodName = nameProperty?.GetValue(good) as string;
					if (string.IsNullOrEmpty(goodName)) continue;

					// Get amount from storage
					int amount = ReflectionHelper.InvokeInt(getAmountMethod, storageService, goodName);

					if (amount > 0) {
						// Get display name for the meta currency
						var mcNameProperty = currencyModel.GetType().GetProperty("Name",
							System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
						string mcName = mcNameProperty?.GetValue(currencyModel) as string;
						string displayName = GetMetaCurrencyDisplayName(mcName) ?? mcName;

						result.Add((displayName, amount));
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GameResultReflection.GetStoredMetaCurrencies failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get seal fragments earned (0 if not applicable).
		/// </summary>
		public static int GetSealFragments() {
			EnsureTypes();
			try {
				// Custom games and challenges don't award seal fragments
				if (IsCustomGame() || IsChallenge()) return 0;
				if (!HasWon()) return 0;

				var biomeService = GetBiomeService();
				if (biomeService == null) return 0;

				var difficulty = ReflectionHelper.GetProp(_biomeServiceDifficultyProperty, biomeService);
				if (difficulty == null) return 0;

				return ReflectionHelper.GetInt(_difficultySealFragmentsField, difficulty);
			} catch {
				return 0;
			}
		}

		private static bool IsCustomGame() {
			try {
				var conditionsService = GetConditionsService();
				if (conditionsService == null) return false;

				return ReflectionHelper.InvokeBool(_isCustomGameMethod, conditionsService);
			} catch {
				return false;
			}
		}

		private static bool IsChallenge() {
			try {
				var conditionsService = GetConditionsService();
				if (conditionsService == null) return false;

				return ReflectionHelper.InvokeBool(_isChallangeMethod, conditionsService);
			} catch {
				return false;
			}
		}

		// ========================================
		// SCORE DATA
		// ========================================

		/// <summary>
		/// Data structure for a score entry.
		/// </summary>
		public struct ScoreEntry {
			public string Label;
			public int Points;
			public int Amount;
		}

		/// <summary>
		/// Get the score breakdown (empty if tutorial).
		/// </summary>
		public static List<ScoreEntry> GetScoreBreakdown() {
			EnsureTypes();
			var result = new List<ScoreEntry>();

			// No score in tutorial
			if (IsTutorial()) return result;

			try {
				if (_scoreCalculatorType == null || _getScoreMethod == null) return result;

				// Create ScoreCalculator instance and call GetScore()
				var calculator = Activator.CreateInstance(_scoreCalculatorType);
				var scoreList = ReflectionHelper.Invoke(_getScoreMethod, calculator) as IList;
				if (scoreList == null) return result;

				foreach (var scoreData in scoreList) {
					if (scoreData == null) continue;

					string label = ReflectionHelper.GetString(_scoreDataLabelField, scoreData);
					int points = ReflectionHelper.GetInt(_scoreDataPointsField, scoreData);
					int amount = ReflectionHelper.GetInt(_scoreDataAmountField, scoreData);

					if (!string.IsNullOrEmpty(label)) {
						result.Add(new ScoreEntry { Label = label, Points = points, Amount = amount });
					}
				}
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GameResultReflection.GetScoreBreakdown failed: {ex.Message}");
			}

			return result;
		}

		/// <summary>
		/// Get the total score.
		/// </summary>
		public static int GetTotalScore() {
			var breakdown = GetScoreBreakdown();
			return breakdown.Sum(s => s.Points);
		}

		// ========================================
		// WORLD EVENT DATA
		// ========================================

		/// <summary>
		/// Data structure for world event info.
		/// </summary>
		public struct WorldEventInfo {
			public string Name;
			public string Description;
			public bool Completed;
			public List<(string key, string value)> Objectives;
		}

		/// <summary>
		/// Check if there is an active world event.
		/// </summary>
		public static bool HasActiveWorldEvent() {
			var goalStates = GetActiveCycleGoals();
			return goalStates != null && goalStates.Count > 0;
		}

		/// <summary>
		/// Get world event information (null if no active event).
		/// </summary>
		public static WorldEventInfo? GetWorldEventInfo() {
			EnsureTypes();
			try {
				var goalStates = GetActiveCycleGoals();
				if (goalStates == null || goalStates.Count == 0) return null;

				// Get first active goal
				var goalState = goalStates[0];
				if (goalState == null) return null;

				string modelName = ReflectionHelper.GetString(_goalStateModelField, goalState);
				bool completed = ReflectionHelper.GetBool(_goalStateCompletedField, goalState);

				if (string.IsNullOrEmpty(modelName)) return null;

				var settings = GameReflection.GetSettings();
				if (settings == null || _settingsGetGoalMethod == null) return null;

				var goalModel = ReflectionHelper.Invoke(_settingsGetGoalMethod, settings, modelName);
				if (goalModel == null) return null;

				// Get display name
				string name = ReflectionHelper.GetLocaString(_goalDisplayNameField, goalModel) ?? modelName;

				// Get description
				string description = ReflectionHelper.GetLocaString(_goalDescriptionField, goalModel);

				// Get objectives breakdown
				var objectives = new List<(string key, string value)>();
				if (_goalGetObjectivesBreakdownMethod != null) {
					var breakdownObj = ReflectionHelper.Invoke(_goalGetObjectivesBreakdownMethod, goalModel);
					if (breakdownObj is IDictionary dict) {
						foreach (DictionaryEntry entry in dict) {
							string key = entry.Key?.ToString();
							string value = entry.Value?.ToString();
							if (!string.IsNullOrEmpty(key)) {
								objectives.Add((key, value ?? ""));
							}
						}
					}
				}

				return new WorldEventInfo {
					Name = name,
					Description = description,
					Completed = completed,
					Objectives = objectives
				};
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GameResultReflection.GetWorldEventInfo failed: {ex.Message}");
				return null;
			}
		}

		private static IList GetActiveCycleGoals() {
			try {
				var worldStateService = GetWorldStateService();
				if (worldStateService == null) return null;

				var cycle = ReflectionHelper.GetProp(_wsssCycleProperty, worldStateService);
				if (cycle == null) return null;

				return ReflectionHelper.GetList(_cycleActiveCycleGoalsField, cycle);
			} catch {
				return null;
			}
		}

		// ========================================
		// BUTTON AVAILABILITY
		// ========================================

		/// <summary>
		/// Check if the Continue button is available (win + not ironman + not tutorial).
		/// </summary>
		public static bool IsContinueButtonAvailable(object popup) {
			EnsureTypes();
			try {
				if (popup == null || _continueButtonField == null) return false;

				var continueButton = _continueButtonField.GetValue(popup);
				if (continueButton == null) return false;

				// Check if button GameObject is active
				var gameObjectProp = continueButton.GetType().GetProperty("gameObject", GameReflection.PublicInstance);
				var gameObject = gameObjectProp?.GetValue(continueButton) as GameObject;

				return gameObject != null && gameObject.activeSelf;
			} catch {
				return false;
			}
		}

		// ========================================
		// ACTIONS
		// ========================================

		/// <summary>
		/// Click the Menu button (return to world map).
		/// </summary>
		public static bool ClickMenuButton(object popup) {
			EnsureTypes();
			try {
				if (popup == null || _menuButtonField == null) return false;

				var menuButton = _menuButtonField.GetValue(popup);
				if (menuButton == null) return false;

				// Get onClick and invoke
				var onClickProp = menuButton.GetType().GetProperty("onClick", GameReflection.PublicInstance);
				var onClick = onClickProp?.GetValue(menuButton);
				if (onClick == null) return false;

				var invokeMethod = onClick.GetType().GetMethod("Invoke", Type.EmptyTypes);
				if (invokeMethod == null) return false;
				invokeMethod.Invoke(onClick, null);

				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GameResultReflection.ClickMenuButton failed: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Click the Continue button (continue playing after win).
		/// </summary>
		public static bool ClickContinueButton(object popup) {
			EnsureTypes();
			try {
				if (popup == null || _continueButtonField == null) return false;

				var continueButton = _continueButtonField.GetValue(popup);
				if (continueButton == null) return false;

				// Get onClick and invoke
				var onClickProp = continueButton.GetType().GetProperty("onClick", GameReflection.PublicInstance);
				var onClick = onClickProp?.GetValue(continueButton);
				if (onClick == null) return false;

				var invokeMethod = onClick.GetType().GetMethod("Invoke", Type.EmptyTypes);
				if (invokeMethod == null) return false;
				invokeMethod.Invoke(onClick, null);

				return true;
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] GameResultReflection.ClickContinueButton failed: {ex.Message}");
				return false;
			}
		}

		// ========================================
		// SERVICE ACCESSORS
		// ========================================

		private static object GetStateService() {
			// GameMB.StateService
			var gameMB = GameReflection.GameAssembly?.GetType("Eremite.GameMB");
			if (gameMB == null) return null;

			if (_gameMBStateServiceProperty == null) {
				_gameMBStateServiceProperty = gameMB.GetProperty("StateService", GameReflection.PublicStatic);
			}

			return _gameMBStateServiceProperty?.GetValue(null);
		}

		private static object GetGameSealService() {
			var gameMB = GameReflection.GameAssembly?.GetType("Eremite.GameMB");
			if (gameMB == null) return null;

			if (_gameMBGameSealServiceProperty == null) {
				_gameMBGameSealServiceProperty = gameMB.GetProperty("GameSealService", GameReflection.PublicStatic);
			}

			return _gameMBGameSealServiceProperty?.GetValue(null);
		}

		private static object GetTutorialService() {
			var mbType = GameReflection.GameAssembly?.GetType("Eremite.MB");
			if (mbType == null) return null;

			if (_mbTutorialServiceProperty == null) {
				_mbTutorialServiceProperty = mbType.GetProperty("TutorialService", GameReflection.PublicStatic);
			}

			return _mbTutorialServiceProperty?.GetValue(null);
		}

		private static object GetBiome() {
			var gameMB = GameReflection.GameAssembly?.GetType("Eremite.GameMB");
			if (gameMB == null) return null;

			if (_gameMBBiomeProperty == null) {
				_gameMBBiomeProperty = gameMB.GetProperty("Biome", GameReflection.PublicStatic);
			}

			return _gameMBBiomeProperty?.GetValue(null);
		}

		private static object GetGameGoalsService() {
			var gameServices = GameReflection.GetGameServices();
			if (gameServices == null) return null;

			if (_gameServicesGameGoalsServiceProperty == null) {
				var iGameServicesType = GameReflection.GameAssembly?.GetType("Eremite.Services.IGameServices");
				_gameServicesGameGoalsServiceProperty = iGameServicesType?.GetProperty("GameGoalsService", GameReflection.PublicInstance);
			}

			return _gameServicesGameGoalsServiceProperty?.GetValue(gameServices);
		}

		private static object GetMetaStateService() {
			var metaServices = GameReflection.GetMetaServices();
			if (metaServices == null) return null;

			if (_metaStateServiceProperty == null) {
				var metaServicesType = GameReflection.GameAssembly?.GetType("Eremite.Services.IMetaServices");
				_metaStateServiceProperty = metaServicesType?.GetProperty("MetaStateService", GameReflection.PublicInstance);
			}

			return _metaStateServiceProperty?.GetValue(metaServices);
		}

		private static object GetWorldStateService() {
			var mbType = GameReflection.GameAssembly?.GetType("Eremite.MB");
			if (mbType == null) return null;

			if (_mbWorldStateServiceProperty == null) {
				_mbWorldStateServiceProperty = mbType.GetProperty("WorldStateService", GameReflection.PublicStatic);
			}

			return _mbWorldStateServiceProperty?.GetValue(null);
		}

		private static object GetBiomeService() {
			var gameMB = GameReflection.GameAssembly?.GetType("Eremite.GameMB");
			if (gameMB == null) return null;

			if (_gameMBBiomeServiceProperty == null) {
				_gameMBBiomeServiceProperty = gameMB.GetProperty("BiomeService", GameReflection.PublicStatic);
			}

			return _gameMBBiomeServiceProperty?.GetValue(null);
		}

		private static object GetConditionsService() {
			var gameMB = GameReflection.GameAssembly?.GetType("Eremite.GameMB");
			if (gameMB == null) return null;

			if (_gameMBConditionsServiceProperty == null) {
				_gameMBConditionsServiceProperty = gameMB.GetProperty("ConditionsService", GameReflection.PublicStatic);
			}

			return _gameMBConditionsServiceProperty?.GetValue(null);
		}

		// ========================================
		// REFLECTION CACHING
		// ========================================

		private static void EnsureTypes() {
			if (_typesCached) return;

			ReflectionHelper.InitCache("GameResultReflection", assembly => {
				// GameResultPopup
				_gameResultPopupType = assembly.GetType("Eremite.View.HUD.Result.GameResultPopup");
				if (_gameResultPopupType != null) {
					_headerTextField = _gameResultPopupType.GetField("headerText", GameReflection.NonPublicInstance);
					_descTextField = _gameResultPopupType.GetField("descText", GameReflection.NonPublicInstance);
					_menuButtonField = _gameResultPopupType.GetField("menuButton", GameReflection.NonPublicInstance);
					_continueButtonField = _gameResultPopupType.GetField("continueButton", GameReflection.NonPublicInstance);
				}

				// GameObjectivesState
				var gameObjectivesStateType = assembly.GetType("Eremite.Model.State.GameObjectivesState");
				if (gameObjectivesStateType != null) {
					_gameObjectivesHasWonField = gameObjectivesStateType.GetField("hasWon", GameReflection.PublicInstance);
					_gameObjectivesHasLostField = gameObjectivesStateType.GetField("hasLost", GameReflection.PublicInstance);
				}

				// IStateService.GameObjectives
				var stateServiceType = assembly.GetType("Eremite.Services.IStateService");
				if (stateServiceType != null) {
					_stateServiceGameObjectivesProperty = stateServiceType.GetProperty("GameObjectives", GameReflection.PublicInstance);
					_stateConditionsProperty = stateServiceType.GetProperty("Conditions", GameReflection.PublicInstance);
				}

				// ConditionsState.rewards
				var conditionsStateType = assembly.GetType("Eremite.Model.State.ConditionsState");
				if (conditionsStateType != null) {
					_conditionsRewardsField = conditionsStateType.GetField("rewards", GameReflection.PublicInstance);
				}

				// MetaCurrency
				var metaCurrencyType = assembly.GetType("Eremite.Model.MetaCurrency");
				if (metaCurrencyType != null) {
					_metaCurrencyNameField = metaCurrencyType.GetField("name", GameReflection.PublicInstance);
					_metaCurrencyAmountField = metaCurrencyType.GetField("amount", GameReflection.PublicInstance);
				}

				// IGameSealService.IsSealedBiome
				var gameSealServiceType = assembly.GetType("Eremite.Services.IGameSealService");
				if (gameSealServiceType != null) {
					_isSealedBiomeMethod = gameSealServiceType.GetMethod("IsSealedBiome", GameReflection.PublicInstance);
				}

				// ITutorialService.IsAnyTutorial
				var tutorialServiceType = assembly.GetType("Eremite.Services.ITutorialService");
				if (tutorialServiceType != null) {
					_isAnyTutorialMethod = tutorialServiceType.GetMethod("IsAnyTutorial", GameReflection.PublicInstance);
				}

				// IGameGoalsService.GetUnshownCompletedGoals
				var gameGoalsServiceType = assembly.GetType("Eremite.Services.IGameGoalsService");
				if (gameGoalsServiceType != null) {
					_getUnshownCompletedGoalsMethod = gameGoalsServiceType.GetMethod("GetUnshownCompletedGoals", GameReflection.PublicInstance);
				}

				// MetaEconomyState.currentCycleExp
				var economyStateType = assembly.GetType("Eremite.Model.State.MetaEconomyState");
				if (economyStateType != null) {
					_economyCurrentCycleExpField = economyStateType.GetField("currentCycleExp", GameReflection.PublicInstance);
				}

				// LevelState fields
				var levelStateType = assembly.GetType("Eremite.Model.State.LevelState");
				if (levelStateType != null) {
					_levelLevelField = levelStateType.GetField("level", GameReflection.PublicInstance);
					_levelExpField = levelStateType.GetField("exp", GameReflection.PublicInstance);
					_levelTargetExpField = levelStateType.GetField("targetExp", GameReflection.PublicInstance);
				}

				// IMetaStateService properties
				var metaStateServiceType = assembly.GetType("Eremite.Services.IMetaStateService");
				if (metaStateServiceType != null) {
					_mssEconomyProperty = metaStateServiceType.GetProperty("Economy", GameReflection.PublicInstance);
					_mssLevelProperty = metaStateServiceType.GetProperty("Level", GameReflection.PublicInstance);
				}

				// Settings.GetGoal
				var settingsType = assembly.GetType("Eremite.Model.Settings");
				if (settingsType != null) {
					_settingsGetGoalMethod = settingsType.GetMethod("GetGoal", GameReflection.PublicInstance, null, new[] { typeof(string) }, null);
				}

				// GoalModel.displayName and description
				var goalModelType = assembly.GetType("Eremite.Model.Goals.GoalModel");
				if (goalModelType != null) {
					_goalDisplayNameField = goalModelType.GetField("displayName", GameReflection.PublicInstance);
					_goalDescriptionField = goalModelType.GetField("description", GameReflection.NonPublicInstance);
					_goalGetObjectivesBreakdownMethod = goalModelType.GetMethod("GetObjectivesBreakdown", GameReflection.PublicInstance);
				}

				// GoalState.model and completed
				var goalStateType = assembly.GetType("Eremite.Model.Goals.GoalState");
				if (goalStateType != null) {
					_goalStateModelField = goalStateType.GetField("model", GameReflection.PublicInstance);
					_goalStateCompletedField = goalStateType.GetField("completed", GameReflection.PublicInstance);
				}

				// ScoreCalculator and ScoreData
				_scoreCalculatorType = assembly.GetType("Eremite.View.HUD.Result.ScoreCalculator");
				if (_scoreCalculatorType != null) {
					_getScoreMethod = _scoreCalculatorType.GetMethod("GetScore", GameReflection.PublicInstance);
				}

				var scoreDataType = assembly.GetType("Eremite.View.HUD.Result.ScoreData");
				if (scoreDataType != null) {
					_scoreDataLabelField = scoreDataType.GetField("label", GameReflection.PublicInstance);
					_scoreDataPointsField = scoreDataType.GetField("points", GameReflection.PublicInstance);
					_scoreDataAmountField = scoreDataType.GetField("amount", GameReflection.PublicInstance);
				}

				// IWorldStateService.Cycle
				var worldStateServiceType = assembly.GetType("Eremite.Services.World.IWorldStateService");
				if (worldStateServiceType != null) {
					_wsssCycleProperty = worldStateServiceType.GetProperty("Cycle", GameReflection.PublicInstance);
				}

				// CycleState.activeCycleGoals
				var cycleStateType = assembly.GetType("Eremite.WorldMap.CycleState");
				if (cycleStateType != null) {
					_cycleActiveCycleGoalsField = cycleStateType.GetField("activeCycleGoals", GameReflection.PublicInstance);
				}

				// IBiomeService.Difficulty
				var biomeServiceType = assembly.GetType("Eremite.Services.IBiomeService");
				if (biomeServiceType != null) {
					_biomeServiceDifficultyProperty = biomeServiceType.GetProperty("Difficulty", GameReflection.PublicInstance);
				}

				// DifficultyModel.sealFramentsForWin
				var difficultyModelType = assembly.GetType("Eremite.Model.DifficultyModel");
				if (difficultyModelType != null) {
					_difficultySealFragmentsField = difficultyModelType.GetField("sealFramentsForWin", GameReflection.PublicInstance);
				}

				// IConditionsService.IsCustomGame and IsChallange
				var conditionsServiceType = assembly.GetType("Eremite.Services.IConditionsService");
				if (conditionsServiceType != null) {
					_isCustomGameMethod = conditionsServiceType.GetMethod("IsCustomGame", GameReflection.PublicInstance);
					_isChallangeMethod = conditionsServiceType.GetMethod("IsChallange", GameReflection.PublicInstance);
				}
			});

			_typesCached = true;
		}

		public static int LogCacheStatus() {
			return ReflectionValidator.TriggerAndValidate(typeof(GameResultReflection), "GameResultReflection");
		}
	}
}
