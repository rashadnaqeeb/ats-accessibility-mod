using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Key handler for MetaRewardsPopup and MetaLevelUpPopup.
    /// Registered above GameResultOverlay so players can close the level-up
    /// popup before interacting with the game result screen.
    /// </summary>
    public class MetaRewardsOverlay : IKeyHandler
    {
        private GameObject _currentPopup;
        private MonoBehaviour _coroutineRunner;

        public MetaRewardsOverlay(MonoBehaviour coroutineRunner)
        {
            _coroutineRunner = coroutineRunner;
        }

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _currentPopup != null && _currentPopup.activeInHierarchy;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (_currentPopup == null) return false;

            // Delegate to MetaRewardsPopupReader for all key handling
            if (MetaRewardsPopupReader.ProcessKeyEvent(keyCode, _currentPopup))
            {
                return true;
            }

            // Escape passes through to let game close the popup
            if (keyCode == KeyCode.Escape)
            {
                return false;
            }

            // Consume all other keys while popup is active
            return true;
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Check if a popup is MetaRewardsPopup or MetaLevelUpPopup.
        /// </summary>
        public static bool IsMetaRewardsOrLevelUpPopup(string popupName)
        {
            return popupName != null &&
                   (popupName.Contains("MetaRewards") || popupName.Contains("MetaLevelUp"));
        }

        /// <summary>
        /// Called when a popup is shown. Opens the overlay if it's a MetaRewards/LevelUp popup.
        /// </summary>
        public void OnPopupShown(object popup)
        {
            var component = popup as Component;
            if (component == null) return;

            var popupGO = component.gameObject;
            if (popupGO == null) return;

            if (!IsMetaRewardsOrLevelUpPopup(popupGO.name)) return;

            // Already tracking this popup
            if (popupGO == _currentPopup) return;

            _currentPopup = popupGO;
            Debug.Log($"[ATSAccessibility] MetaRewardsOverlay opened: {popupGO.name}");

            // Start the announcement coroutine
            _coroutineRunner?.StartCoroutine(
                MetaRewardsPopupReader.AnnounceMetaRewardsPopup(popupGO, _coroutineRunner));
        }

        /// <summary>
        /// Called when a popup is hidden. Closes the overlay if it was tracking this popup.
        /// </summary>
        public void OnPopupHidden(object popup)
        {
            var component = popup as Component;
            if (component == null) return;

            var popupGO = component.gameObject;
            if (popupGO == null) return;

            if (popupGO != _currentPopup) return;

            Debug.Log($"[ATSAccessibility] MetaRewardsOverlay closed: {popupGO.name}");
            MetaRewardsPopupReader.Reset();
            _currentPopup = null;
        }

        /// <summary>
        /// Reset all state.
        /// </summary>
        public void Reset()
        {
            MetaRewardsPopupReader.Reset();
            _currentPopup = null;
        }
    }
}
