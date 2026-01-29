using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Provides reflection-based access to narration/dialogue internals.
    ///
    /// CRITICAL RULES:
    /// - Cache ONLY reflection metadata (Type, PropertyInfo, MethodInfo) - these survive scene transitions
    /// - NEVER cache instance references (services, controllers) - they are destroyed on scene change
    /// </summary>
    public static class NarrationReflection
    {
        // ========================================
        // CACHED REFLECTION METADATA
        // ========================================

        private static bool _cached = false;

        // HomePopup type check
        private static Type _homePopupType = null;

        // WorldController access
        private static Type _worldControllerType = null;
        private static PropertyInfo _wcInstanceProperty = null;
        private static PropertyInfo _wcWorldServicesProperty = null;

        // IWorldServices properties
        private static PropertyInfo _wsNarrationBlackboardServiceProperty = null;
        private static PropertyInfo _wsNarrationServiceProperty = null;

        // INarrationBlackboardService observables
        private static PropertyInfo _nbbOnDialogueRequestedProperty = null;
        private static PropertyInfo _nbbOnBranchRequestedProperty = null;

        // INarrationService methods
        private static MethodInfo _nsGetNPCMethod = null;
        private static MethodInfo _nsHasAnyImportantTopicsMethod = null;

        // NPCModel fields
        private static FieldInfo _npcDisplayNameField = null;
        private static FieldInfo _npcTitleField = null;

        // DialogueModel members
        private static FieldInfo _dialogueTextField = null;
        private static PropertyInfo _dialogueHasTransitionProperty = null;
        private static MethodInfo _dialogueExecuteTransitionMethod = null;
        private static MethodInfo _dialogueGetTextMethod = null;

        // BranchModel members
        private static FieldInfo _branchChoicesField = null;

        // ChoiceModel members
        private static FieldInfo _choiceTextField = null;
        private static MethodInfo _choiceCanExecuteMethod = null;
        private static MethodInfo _choiceExecuteMethod = null;
        private static MethodInfo _choiceGetTextMethod = null;

        // TextTyper reflection (for GetCurrentDisplayedText)
        private static FieldInfo _textTyperTextMeshField = null;
        private static PropertyInfo _tmpTextProperty = null;
        private static bool _textTyperFieldsCached = false;

        // ========================================
        // INITIALIZATION
        // ========================================

        private static void EnsureCached()
        {
            if (_cached) return;
            _cached = true;

            try
            {
                var assembly = GameReflection.GameAssembly;
                if (assembly == null) return;

                // HomePopup type
                _homePopupType = assembly.GetType("Eremite.Narration.UI.HomePopup");

                // WorldController
                _worldControllerType = assembly.GetType("Eremite.Controller.WorldController");
                if (_worldControllerType != null)
                {
                    _wcInstanceProperty = _worldControllerType.GetProperty("Instance", GameReflection.PublicStatic);
                    _wcWorldServicesProperty = _worldControllerType.GetProperty("WorldServices", GameReflection.PublicInstance);
                }

                // IWorldServices
                var worldServicesType = assembly.GetType("Eremite.Services.World.IWorldServices");
                if (worldServicesType != null)
                {
                    _wsNarrationBlackboardServiceProperty = worldServicesType.GetProperty("NarrationBlackboardService");
                    _wsNarrationServiceProperty = worldServicesType.GetProperty("NarrationService");
                }

                // INarrationBlackboardService
                var nbbType = assembly.GetType("Eremite.Services.Narration.INarrationBlackboardService");
                if (nbbType != null)
                {
                    _nbbOnDialogueRequestedProperty = nbbType.GetProperty("OnDialogueRequested");
                    _nbbOnBranchRequestedProperty = nbbType.GetProperty("OnBranchRequested");
                }

                // INarrationService
                var nsType = assembly.GetType("Eremite.Services.Narration.INarrationService");
                if (nsType != null)
                {
                    _nsGetNPCMethod = nsType.GetMethod("GetNPC", Type.EmptyTypes);
                    _nsHasAnyImportantTopicsMethod = nsType.GetMethod("HasAnyImportantTopics", Type.EmptyTypes);
                }

                // NPCModel
                var npcType = assembly.GetType("Eremite.Model.Narration.NPCModel");
                if (npcType != null)
                {
                    _npcDisplayNameField = npcType.GetField("displayName", GameReflection.PublicInstance);
                    _npcTitleField = npcType.GetField("title", GameReflection.PublicInstance);
                }

                // DialogueModel
                var dialogueType = assembly.GetType("Eremite.Model.Narration.DialogueModel");
                if (dialogueType != null)
                {
                    _dialogueTextField = dialogueType.GetField("text", GameReflection.PublicInstance);
                    _dialogueHasTransitionProperty = dialogueType.GetProperty("HasTransition", GameReflection.PublicInstance);
                    _dialogueExecuteTransitionMethod = dialogueType.GetMethod("ExecuteTransition", Type.EmptyTypes);
                    _dialogueGetTextMethod = dialogueType.GetMethod("GetText", Type.EmptyTypes);
                }

                // BranchModel
                var branchType = assembly.GetType("Eremite.Model.Narration.BranchModel");
                if (branchType != null)
                {
                    _branchChoicesField = branchType.GetField("choices", GameReflection.PublicInstance);
                }

                // ChoiceModel
                var choiceType = assembly.GetType("Eremite.Model.Narration.ChoiceModel");
                if (choiceType != null)
                {
                    _choiceTextField = choiceType.GetField("text", GameReflection.PublicInstance);
                    _choiceCanExecuteMethod = choiceType.GetMethod("CanExecute", Type.EmptyTypes);
                    _choiceExecuteMethod = choiceType.GetMethod("Execute", Type.EmptyTypes);
                    _choiceGetTextMethod = choiceType.GetMethod("GetText", Type.EmptyTypes);
                }

                Debug.Log("[ATSAccessibility] NarrationReflection cached successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] NarrationReflection caching failed: {ex.Message}");
            }
        }

        // ========================================
        // TYPE DETECTION
        // ========================================

        /// <summary>
        /// Check if a popup object is a HomePopup (dialogue popup).
        /// </summary>
        public static bool IsHomePopup(object popup)
        {
            if (popup == null) return false;
            EnsureCached();
            if (_homePopupType == null) return false;
            return _homePopupType.IsInstanceOfType(popup);
        }

        /// <summary>
        /// Get the currently displayed dialogue text from the HomePopup.
        /// This reads directly from the UI, useful when we miss the initial event.
        /// </summary>
        public static string GetCurrentDisplayedText(object popup)
        {
            if (popup == null) return null;

            try
            {
                var popupComponent = popup as Component;
                if (popupComponent == null) return null;

                // Find the TextTyper component in children (path: Content/NPC/Content/Text)
                var textTypers = popupComponent.GetComponentsInChildren<Component>(true);
                foreach (var component in textTypers)
                {
                    if (component.GetType().Name != "TextTyper") continue;

                    // Cache reflection fields on first successful lookup
                    if (!_textTyperFieldsCached)
                    {
                        _textTyperTextMeshField = component.GetType().GetField("textMesh",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                        _textTyperFieldsCached = true;
                    }

                    if (_textTyperTextMeshField == null) continue;

                    var textMesh = _textTyperTextMeshField.GetValue(component);
                    if (textMesh == null) continue;

                    // Cache TMP_Text property on first successful lookup
                    if (_tmpTextProperty == null)
                    {
                        _tmpTextProperty = textMesh.GetType().GetProperty("text",
                            BindingFlags.Public | BindingFlags.Instance);
                    }

                    if (_tmpTextProperty == null) continue;

                    var text = _tmpTextProperty.GetValue(textMesh) as string;
                    if (!string.IsNullOrEmpty(text))
                    {
                        Debug.Log($"[ATSAccessibility] GetCurrentDisplayedText: found text length={text.Length}");
                        return text;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] GetCurrentDisplayedText failed: {ex.Message}");
            }

            return null;
        }

        // ========================================
        // SERVICE ACCESS
        // ========================================

        /// <summary>
        /// Get the WorldServices instance (fresh each time).
        /// </summary>
        private static object GetWorldServices()
        {
            EnsureCached();
            if (_wcInstanceProperty == null || _wcWorldServicesProperty == null) return null;

            try
            {
                var worldController = _wcInstanceProperty.GetValue(null);
                if (worldController == null) return null;
                return _wcWorldServicesProperty.GetValue(worldController);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the NarrationBlackboardService (fresh each time).
        /// </summary>
        private static object GetNarrationBlackboardService()
        {
            EnsureCached();
            var worldServices = GetWorldServices();
            if (worldServices == null || _wsNarrationBlackboardServiceProperty == null) return null;

            try
            {
                return _wsNarrationBlackboardServiceProperty.GetValue(worldServices);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the NarrationService (fresh each time).
        /// </summary>
        private static object GetNarrationService()
        {
            EnsureCached();
            var worldServices = GetWorldServices();
            if (worldServices == null || _wsNarrationServiceProperty == null) return null;

            try
            {
                return _wsNarrationServiceProperty.GetValue(worldServices);
            }
            catch
            {
                return null;
            }
        }

        // ========================================
        // NPC INFO
        // ========================================

        /// <summary>
        /// Get the current NPC name.
        /// </summary>
        public static string GetNPCName()
        {
            EnsureCached();

            try
            {
                var narrationService = GetNarrationService();
                if (narrationService == null || _nsGetNPCMethod == null) return null;

                var npc = _nsGetNPCMethod.Invoke(narrationService, null);
                if (npc == null) return null;

                var locaText = _npcDisplayNameField?.GetValue(npc);
                return GameReflection.GetLocaText(locaText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] NarrationReflection: GetNPCName failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get the current NPC title.
        /// </summary>
        public static string GetNPCTitle()
        {
            EnsureCached();

            try
            {
                var narrationService = GetNarrationService();
                if (narrationService == null || _nsGetNPCMethod == null) return null;

                var npc = _nsGetNPCMethod.Invoke(narrationService, null);
                if (npc == null) return null;

                var locaText = _npcTitleField?.GetValue(npc);
                return GameReflection.GetLocaText(locaText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] NarrationReflection: GetNPCTitle failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if the NPC has any important topics waiting (e.g., Scorched Queen dialogue).
        /// </summary>
        public static bool HasAnyImportantTopics()
        {
            EnsureCached();

            try
            {
                var narrationService = GetNarrationService();
                if (narrationService == null || _nsHasAnyImportantTopicsMethod == null) return false;

                var result = _nsHasAnyImportantTopicsMethod.Invoke(narrationService, null);
                return result is bool b && b;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ATSAccessibility] NarrationReflection: HasAnyImportantTopics failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // DIALOGUE DATA
        // ========================================

        /// <summary>
        /// Get the text from a DialogueModel.
        /// </summary>
        public static string GetDialogueText(object dialogueModel)
        {
            if (dialogueModel == null) return null;
            EnsureCached();

            try
            {
                // Prefer GetText() method as it handles pronouns
                if (_dialogueGetTextMethod != null)
                {
                    return _dialogueGetTextMethod.Invoke(dialogueModel, null) as string;
                }

                // Fallback to raw text field
                var locaText = _dialogueTextField?.GetValue(dialogueModel);
                return GameReflection.GetLocaText(locaText);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] NarrationReflection: GetDialogueText failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Check if the dialogue has a transition (continue button).
        /// </summary>
        public static bool HasTransition(object dialogueModel)
        {
            if (dialogueModel == null) return false;
            EnsureCached();

            try
            {
                if (_dialogueHasTransitionProperty == null) return false;
                var result = _dialogueHasTransitionProperty.GetValue(dialogueModel);
                return result is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Execute the dialogue transition (continue to next).
        /// </summary>
        public static bool ExecuteTransition(object dialogueModel)
        {
            if (dialogueModel == null) return false;
            EnsureCached();

            try
            {
                if (_dialogueExecuteTransitionMethod == null) return false;
                _dialogueExecuteTransitionMethod.Invoke(dialogueModel, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] NarrationReflection: ExecuteTransition failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // CHOICE DATA
        // ========================================

        /// <summary>
        /// Information about a choice for accessibility navigation.
        /// </summary>
        public class ChoiceInfo
        {
            public object Choice;
            public string Text;
        }

        /// <summary>
        /// Get the available choices from a BranchModel.
        /// Only returns choices that can be executed (requirements met).
        /// </summary>
        public static List<ChoiceInfo> GetChoices(object branchModel)
        {
            var choices = new List<ChoiceInfo>();
            if (branchModel == null) return choices;
            EnsureCached();

            try
            {
                Debug.Log($"[ATSAccessibility] GetChoices: branchModel type = {branchModel.GetType().FullName}");
                Debug.Log($"[ATSAccessibility] GetChoices: _branchChoicesField = {_branchChoicesField}");

                var choicesArray = _branchChoicesField?.GetValue(branchModel) as Array;
                Debug.Log($"[ATSAccessibility] GetChoices: choicesArray = {choicesArray}, length = {choicesArray?.Length ?? -1}");

                if (choicesArray == null) return choices;

                foreach (var choice in choicesArray)
                {
                    if (choice == null)
                    {
                        Debug.Log("[ATSAccessibility] GetChoices: choice is null, skipping");
                        continue;
                    }

                    // Check if choice can be executed
                    if (_choiceCanExecuteMethod != null)
                    {
                        var canExecute = _choiceCanExecuteMethod.Invoke(choice, null);
                        Debug.Log($"[ATSAccessibility] GetChoices: choice CanExecute = {canExecute}");
                        if (!(canExecute is bool b && b)) continue;
                    }
                    else
                    {
                        Debug.Log("[ATSAccessibility] GetChoices: _choiceCanExecuteMethod is null");
                    }

                    // Get the choice text
                    string text = null;
                    if (_choiceGetTextMethod != null)
                    {
                        text = _choiceGetTextMethod.Invoke(choice, null) as string;
                    }
                    if (string.IsNullOrEmpty(text))
                    {
                        var locaText = _choiceTextField?.GetValue(choice);
                        text = GameReflection.GetLocaText(locaText);
                    }

                    choices.Add(new ChoiceInfo
                    {
                        Choice = choice,
                        Text = text ?? "Unknown choice"
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] NarrationReflection: GetChoices failed: {ex.Message}");
            }

            return choices;
        }

        /// <summary>
        /// Select and execute a choice.
        /// </summary>
        public static bool SelectChoice(ChoiceInfo choice)
        {
            if (choice?.Choice == null) return false;
            EnsureCached();

            try
            {
                if (_choiceExecuteMethod == null) return false;
                _choiceExecuteMethod.Invoke(choice.Choice, null);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] NarrationReflection: SelectChoice failed: {ex.Message}");
                return false;
            }
        }

        // ========================================
        // EVENT SUBSCRIPTIONS
        // ========================================

        /// <summary>
        /// Subscribe to the OnDialogueRequested event.
        /// </summary>
        public static IDisposable SubscribeToDialogue(Action<object> onDialogue)
        {
            EnsureCached();

            try
            {
                var nbbService = GetNarrationBlackboardService();
                if (nbbService == null || _nbbOnDialogueRequestedProperty == null) return null;

                var observable = _nbbOnDialogueRequestedProperty.GetValue(nbbService);
                if (observable == null) return null;

                return GameReflection.SubscribeToObservable(observable, onDialogue);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] NarrationReflection: SubscribeToDialogue failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Subscribe to the OnBranchRequested event.
        /// </summary>
        public static IDisposable SubscribeToBranch(Action<object> onBranch)
        {
            EnsureCached();

            try
            {
                var nbbService = GetNarrationBlackboardService();
                if (nbbService == null || _nbbOnBranchRequestedProperty == null) return null;

                var observable = _nbbOnBranchRequestedProperty.GetValue(nbbService);
                if (observable == null) return null;

                return GameReflection.SubscribeToObservable(observable, onBranch);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] NarrationReflection: SubscribeToBranch failed: {ex.Message}");
                return null;
            }
        }

        public static int LogCacheStatus()
        {
            return ReflectionValidator.TriggerAndValidate(typeof(NarrationReflection), "NarrationReflection");
        }
    }
}
