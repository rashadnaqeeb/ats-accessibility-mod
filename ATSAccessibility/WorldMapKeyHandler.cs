using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Handles keyboard input for world map hex grid navigation.
    /// This is the fallback handler when no popups/menus are open on the world map.
    /// </summary>
    public class WorldMapKeyHandler : IKeyHandler
    {
        private readonly WorldMapNavigator _worldMapNavigator;
        private readonly WorldMapScanner _worldMapScanner;
        private WorldTutorialsOverlay _tutorialsOverlay;

        public WorldMapKeyHandler(WorldMapNavigator worldMapNavigator, WorldMapScanner worldMapScanner)
        {
            _worldMapNavigator = worldMapNavigator;
            _worldMapScanner = worldMapScanner;
        }

        /// <summary>
        /// Set the tutorials overlay reference for F1 key handling.
        /// </summary>
        public void SetTutorialsOverlay(WorldTutorialsOverlay overlay)
        {
            _tutorialsOverlay = overlay;
        }

        /// <summary>
        /// Active when the world map is displayed.
        /// </summary>
        public bool IsActive => WorldMapReflection.IsWorldMapActive();

        /// <summary>
        /// Process world map key events.
        /// </summary>
        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!IsActive || _worldMapNavigator == null) return false;

            // Check if effects panel is open first - it handles its own keys
            if (_worldMapNavigator.ProcessPanelKeyEvent(keyCode))
                return true;

            switch (keyCode)
            {
                // Arrow key navigation (zigzag pattern for up/down)
                case KeyCode.RightArrow:
                    _worldMapNavigator.MoveArrow(1, 0);
                    return true;
                case KeyCode.LeftArrow:
                    _worldMapNavigator.MoveArrow(-1, 0);
                    return true;
                case KeyCode.UpArrow:
                    _worldMapNavigator.MoveArrow(0, 1);
                    return true;
                case KeyCode.DownArrow:
                    _worldMapNavigator.MoveArrow(0, -1);
                    return true;

                // Scanner controls
                case KeyCode.PageUp:
                    if (modifiers.Alt)
                        _worldMapScanner?.ChangeItem(-1);
                    else
                        _worldMapScanner?.ChangeType(-1);
                    return true;
                case KeyCode.PageDown:
                    if (modifiers.Alt)
                        _worldMapScanner?.ChangeItem(1);
                    else
                        _worldMapScanner?.ChangeType(1);
                    return true;
                case KeyCode.Home:
                    _worldMapScanner?.JumpToItem();
                    return true;
                case KeyCode.End:
                    _worldMapScanner?.AnnounceDirection();
                    return true;

                // Select tile (embark)
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    _worldMapNavigator.Interact();
                    return true;

                // Read full tooltip content
                case KeyCode.I:
                    _worldMapNavigator.ReadTooltip();
                    return true;

                // Read embark status and distance to capital
                case KeyCode.D:
                    _worldMapNavigator.ReadEmbarkAndDistance();
                    return true;

                // Open effects panel
                case KeyCode.M:
                    _worldMapNavigator.OpenEffectsPanel();
                    return true;

                // Meta stats announcements
                case KeyCode.L:
                    WorldMapStatsReader.AnnounceLevel();
                    return true;
                case KeyCode.R:
                    WorldMapStatsReader.AnnounceMetaResources();
                    return true;
                case KeyCode.S:
                    WorldMapStatsReader.AnnounceSealInfo();
                    return true;
                case KeyCode.T:
                    WorldMapStatsReader.AnnounceCycleInfo();
                    return true;
                case KeyCode.E:
                    WorldMapStatsReader.OpenCycleEndPopup();
                    return true;

                // Open tutorials HUD and overlay
                case KeyCode.F1:
                    TutorialReflection.ToggleWorldTutorialsHUD();
                    _tutorialsOverlay?.Open();
                    return true;

                default:
                    // Consume all keys - mod has full keyboard control on world map
                    return true;
            }
        }
    }
}
