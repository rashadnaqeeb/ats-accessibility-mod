using System;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Main handler for building panel accessibility.
    /// Subscribes to building panel open/close events and routes keyboard input
    /// to the appropriate building-specific navigator.
    /// </summary>
    public class BuildingPanelHandler : IKeyHandler
    {
        // ========================================
        // EVENT SUBSCRIPTIONS
        // ========================================

        private IDisposable _shownSubscription;
        private IDisposable _closedSubscription;
        private bool _subscribed = false;

        // ========================================
        // CURRENT STATE
        // ========================================

        private IBuildingNavigator _currentNavigator;
        private object _currentBuilding;
        private bool _isCleaningUp = false;

        // ========================================
        // NAVIGATORS
        // ========================================

        private ProductionNavigator _productionNavigator;
        private SimpleNavigator _simpleNavigator;
        private HearthNavigator _hearthNavigator;
        private HouseNavigator _houseNavigator;
        private RelicNavigator _relicNavigator;
        private PortNavigator _portNavigator;
        private FishingHutNavigator _fishingHutNavigator;
        private StorageNavigator _storageNavigator;
        private InstitutionNavigator _institutionNavigator;
        private ShrineNavigator _shrineNavigator;
        private PoroNavigator _poroNavigator;
        private WaterNavigator _waterNavigator;
        private HydrantNavigator _hydrantNavigator;

        // ========================================
        // IKEYHANDLER IMPLEMENTATION
        // ========================================

        /// <summary>
        /// Handler is active when a building panel is open and we have a navigator.
        /// Note: This property is side-effect free - cleanup is done in ProcessKey.
        /// </summary>
        public bool IsActive => _currentNavigator != null && _currentBuilding != null;

        /// <summary>
        /// Process a key event.
        /// </summary>
        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!IsActive) return false;

            // Check if the game's panel is still open - if not, clean up
            if (!BuildingReflection.IsBuildingPanelOpen())
            {
                CleanupNavigator();
                return false;
            }

            return _currentNavigator.ProcessKey(keyCode, modifiers);
        }

        /// <summary>
        /// Clean up the navigator state without announcing (used when game closed panel).
        /// </summary>
        private void CleanupNavigator()
        {
            if (_isCleaningUp || _currentNavigator == null) return;

            _isCleaningUp = true;
            try
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Cleaning up - game panel already closed");
                _currentNavigator?.Close();
                _currentNavigator = null;
                _currentBuilding = null;
            }
            finally
            {
                _isCleaningUp = false;
            }
        }

        // ========================================
        // INITIALIZATION
        // ========================================

        public BuildingPanelHandler()
        {
            // Create navigators
            _productionNavigator = new ProductionNavigator();
            _simpleNavigator = new SimpleNavigator();
            _hearthNavigator = new HearthNavigator();
            _houseNavigator = new HouseNavigator();
            _relicNavigator = new RelicNavigator();
            _portNavigator = new PortNavigator();
            _fishingHutNavigator = new FishingHutNavigator();
            _storageNavigator = new StorageNavigator();
            _institutionNavigator = new InstitutionNavigator();
            _shrineNavigator = new ShrineNavigator();
            _poroNavigator = new PoroNavigator();
            _waterNavigator = new WaterNavigator();
            _hydrantNavigator = new HydrantNavigator();
        }

        /// <summary>
        /// Try to subscribe to building panel events.
        /// Called periodically from AccessibilityCore.Update().
        /// </summary>
        public void TrySubscribe()
        {
            if (_subscribed) return;

            try
            {
                _shownSubscription = BuildingReflection.SubscribeToBuildingPanelShown(OnBuildingPanelShown);
                _closedSubscription = BuildingReflection.SubscribeToBuildingPanelClosed(OnBuildingPanelClosed);

                if (_shownSubscription != null && _closedSubscription != null)
                {
                    _subscribed = true;
                    Debug.Log("[ATSAccessibility] BuildingPanelHandler: Subscribed to building panel events");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] BuildingPanelHandler subscription failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Dispose subscriptions.
        /// </summary>
        public void Dispose()
        {
            _shownSubscription?.Dispose();
            _closedSubscription?.Dispose();
            _shownSubscription = null;
            _closedSubscription = null;
            _subscribed = false;

            // Clean up all navigators to release building references
            CleanupAllNavigators();
            _currentNavigator = null;
            _currentBuilding = null;
        }

        /// <summary>
        /// Close all navigators to release stale building references.
        /// </summary>
        private void CleanupAllNavigators()
        {
            _productionNavigator?.Close();
            _simpleNavigator?.Close();
            _hearthNavigator?.Close();
            _houseNavigator?.Close();
            _relicNavigator?.Close();
            _portNavigator?.Close();
            _fishingHutNavigator?.Close();
            _storageNavigator?.Close();
            _institutionNavigator?.Close();
            _shrineNavigator?.Close();
            _poroNavigator?.Close();
            _waterNavigator?.Close();
            _hydrantNavigator?.Close();
        }

        // ========================================
        // EVENT HANDLERS
        // ========================================

        private void OnBuildingPanelShown(object building)
        {
            Debug.Log($"[ATSAccessibility] BuildingPanelHandler: Panel shown for {BuildingReflection.GetBuildingTypeName(building)}");

            _currentBuilding = building;

            // Select appropriate navigator based on building type
            _currentNavigator = SelectNavigator(building);

            if (_currentNavigator != null)
            {
                _currentNavigator.Open(building);
            }
            else
            {
                // Fallback - announce building name at least
                string name = BuildingReflection.GetBuildingName(building) ?? "Building";
                Speech.Say(name);
            }
        }

        private void OnBuildingPanelClosed(object building)
        {
            if (_isCleaningUp || _currentNavigator == null) return;

            _isCleaningUp = true;
            try
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Panel closed");

                _currentNavigator?.Close();
                _currentNavigator = null;
                _currentBuilding = null;

                Speech.Say("Panel closed");
            }
            finally
            {
                _isCleaningUp = false;
            }
        }

        // ========================================
        // NAVIGATOR SELECTION
        // ========================================

        private IBuildingNavigator SelectNavigator(object building)
        {
            if (building == null) return null;

            string typeName = BuildingReflection.GetBuildingTypeName(building);
            Debug.Log($"[ATSAccessibility] BuildingPanelHandler: Selecting navigator for {typeName}");

            // Special building types first (more specific checks before generic ones)

            // Hearth (Ancient Hearth, Small Hearth)
            if (BuildingReflection.IsHearth(building))
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using HearthNavigator");
                return _hearthNavigator;
            }

            // House/Shelter
            if (BuildingReflection.IsHouse(building))
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using HouseNavigator");
                return _houseNavigator;
            }

            // Relic (glade events)
            if (BuildingReflection.IsRelic(building))
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using RelicNavigator");
                return _relicNavigator;
            }

            // Port
            if (BuildingReflection.IsPort(building))
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using PortNavigator");
                return _portNavigator;
            }

            // FishingHut (has bait system - needs special handling)
            if (BuildingReflection.IsFishingHut(building))
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using FishingHutNavigator");
                return _fishingHutNavigator;
            }

            // Poro (creature needs system)
            if (BuildingReflection.IsPoro(building))
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using PoroNavigator");
                return _poroNavigator;
            }

            // Shrine (tiered effects)
            if (BuildingReflection.IsShrine(building))
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using ShrineNavigator");
                return _shrineNavigator;
            }

            // Institution (Tavern, Temple - need services)
            if (BuildingReflection.IsInstitution(building))
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using InstitutionNavigator");
                return _institutionNavigator;
            }

            // Storage (main warehouse)
            if (BuildingReflection.IsStorage(building))
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using StorageNavigator");
                return _storageNavigator;
            }

            // RainCatcher (water production)
            if (BuildingReflection.IsRainCatcher(building))
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using WaterNavigator for RainCatcher");
                return _waterNavigator;
            }

            // Extractor (water extraction from springs)
            if (BuildingReflection.IsExtractor(building))
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using WaterNavigator for Extractor");
                return _waterNavigator;
            }

            // Hydrant (blight fuel)
            if (BuildingReflection.IsHydrant(building))
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using HydrantNavigator");
                return _hydrantNavigator;
            }

            // Decoration (simple display building)
            if (BuildingReflection.IsDecoration(building))
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using SimpleNavigator for Decoration");
                return _simpleNavigator;
            }

            // Production buildings (Workshop, Farm, Mine, Camp, etc.)
            // Note: Most special building types above are also ProductionBuilding subclasses,
            // so they must be checked first
            if (BuildingReflection.IsProductionBuilding(building))
            {
                Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using ProductionNavigator");
                return _productionNavigator;
            }

            // Fall back to simple navigator for other building types
            // This provides basic Info section navigation
            Debug.Log("[ATSAccessibility] BuildingPanelHandler: Using SimpleNavigator");
            return _simpleNavigator;
        }
    }
}
