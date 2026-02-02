using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Handles keyboard input for settlement map navigation.
    /// This is the fallback handler when no popups/menus are open during gameplay.
    /// </summary>
    public class SettlementKeyHandler : IKeyHandler
    {
        private readonly MapNavigator _mapNavigator;
        private readonly MapScanner _mapScanner;
        private readonly InfoPanelMenu _infoPanelMenu;
        private readonly MenuHub _menuHub;
        private readonly RewardsPanel _rewardsPanel;
        private readonly BuildingMenuPanel _buildingMenuPanel;
        private readonly MoveModeController _moveModeController;
        private readonly AnnouncementHistoryPanel _announcementHistoryPanel;
        private readonly ConfirmationDialog _confirmationDialog;
        private readonly HarvestMarkHandler _harvestMarkHandler;

        private bool _hasBookmark;
        private int _bookmarkX;
        private int _bookmarkY;

        // Worker building cycling
        private int _workerBuildingIndex = -1;
        private int _workerCategoryIndex = 0;  // 0=All, 1=Gathering, 2=Production, 3=Service, 4=Events
        private static readonly string[] WorkerCategories = { "All", "Gathering", "Production", "Service", "Events" };

        private static readonly Dictionary<string, int> BuildingTypeToWorkerCategory = new Dictionary<string, int>
        {
            { "Camp", 1 }, { "GathererHut", 1 }, { "Farm", 1 }, { "FishingHut", 1 },
            { "Mine", 1 }, { "Extractor", 1 }, { "RainCatcher", 1 }, { "Collector", 1 },
            { "Workshop", 2 }, { "BlightPost", 2 },
            { "Hearth", 3 }, { "Institution", 3 }, { "Storage", 3 },
            { "Port", 4 }, { "Relic", 4 },
        };

        public SettlementKeyHandler(
            MapNavigator mapNavigator,
            MapScanner mapScanner,
            InfoPanelMenu infoPanelMenu,
            MenuHub menuHub,
            RewardsPanel rewardsPanel,
            BuildingMenuPanel buildingMenuPanel,
            MoveModeController moveModeController,
            AnnouncementHistoryPanel announcementHistoryPanel,
            ConfirmationDialog confirmationDialog,
            HarvestMarkHandler harvestMarkHandler)
        {
            _mapNavigator = mapNavigator;
            _mapScanner = mapScanner;
            _infoPanelMenu = infoPanelMenu;
            _menuHub = menuHub;
            _rewardsPanel = rewardsPanel;
            _buildingMenuPanel = buildingMenuPanel;
            _moveModeController = moveModeController;
            _announcementHistoryPanel = announcementHistoryPanel;
            _confirmationDialog = confirmationDialog;
            _harvestMarkHandler = harvestMarkHandler;
        }

        /// <summary>
        /// Active when the settlement game is running.
        /// </summary>
        public bool IsActive => GameReflection.GetIsGameActive();

        /// <summary>
        /// Process settlement map key events.
        /// </summary>
        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!IsActive) return false;

            switch (keyCode)
            {
                // Arrow key navigation
                case KeyCode.UpArrow:
                    if (modifiers.Control)
                        _mapNavigator.SkipToNextChange(0, 1);
                    else
                        _mapNavigator.MoveCursor(0, 1);
                    return true;
                case KeyCode.DownArrow:
                    if (modifiers.Control)
                        _mapNavigator.SkipToNextChange(0, -1);
                    else
                        _mapNavigator.MoveCursor(0, -1);
                    return true;
                case KeyCode.LeftArrow:
                    if (modifiers.Control)
                        _mapNavigator.SkipToNextChange(-1, 0);
                    else
                        _mapNavigator.MoveCursor(-1, 0);
                    return true;
                case KeyCode.RightArrow:
                    if (modifiers.Control)
                        _mapNavigator.SkipToNextChange(1, 0);
                    else
                        _mapNavigator.MoveCursor(1, 0);
                    return true;

                // Position announcement / coordinate toggle
                case KeyCode.K:
                    if (modifiers.Alt)
                    {
                        Plugin.AnnounceCoordinates.Value = !Plugin.AnnounceCoordinates.Value;
                        Speech.Say(Plugin.AnnounceCoordinates.Value ? "Coordinates on" : "Coordinates off");
                    }
                    else
                    {
                        _mapNavigator.AnnounceCurrentPosition();
                    }
                    return true;

                // Game speed controls
                case KeyCode.Space:
                    if (modifiers.Shift)
                    {
                        // Shift+Space: destroy building or remove resource node at cursor
                        var buildingToDestroy = GameReflection.GetBuildingAtPosition(_mapNavigator.CursorX, _mapNavigator.CursorY);
                        if (buildingToDestroy != null)
                        {
                            // Building found — existing destroy logic
                            if (!BuildingReflection.CanBeDestroyed(buildingToDestroy))
                            {
                                string name = GameReflection.GetDisplayName(GameReflection.GetBuildingModel(buildingToDestroy));
                                Speech.Say($"Cannot destroy {name}");
                            }
                            else
                            {
                                string name = GameReflection.GetDisplayName(GameReflection.GetBuildingModel(buildingToDestroy));
                                var refundGoods = BuildingReflection.GetDestructionRefund(buildingToDestroy);
                                _confirmationDialog.Show(name, () =>
                                {
                                    if (BuildingReflection.DestroyBuilding(buildingToDestroy))
                                    {
                                        SoundManager.PlayBuildingDestroyed();
                                        Speech.Say($"Destroyed: {name}");
                                    }
                                    else
                                    {
                                        Speech.Say("Destruction failed");
                                    }
                                }, refundGoods);
                            }
                        }
                        else
                        {
                            // No building — check for resource node
                            var objectAtPos = GameReflection.GetObjectOn(_mapNavigator.CursorX, _mapNavigator.CursorY);
                            if (objectAtPos != null && GameReflection.IsRemovableResource(objectAtPos))
                            {
                                string name = GameReflection.GetResourceNodeDisplayName(objectAtPos) ?? "Resource";
                                _confirmationDialog.Show(name, () =>
                                {
                                    if (GameReflection.RemoveResourceNode(objectAtPos))
                                    {
                                        SoundManager.PlayResourceRemoved();
                                        Speech.Say($"Removed: {name}");
                                    }
                                    else
                                    {
                                        Speech.Say("Removal failed");
                                    }
                                });
                            }
                            else if (objectAtPos != null && objectAtPos.GetType().Name == "NaturalResource")
                            {
                                Speech.Say("Cannot remove trees");
                            }
                            else if (objectAtPos != null && objectAtPos.GetType().Name == "Ore")
                            {
                                Speech.Say("Cannot remove ore");
                            }
                            else
                            {
                                Speech.Say("Nothing to remove");
                            }
                        }
                        return true;
                    }
                    GameReflection.TogglePause();
                    Speech.Say(GameReflection.IsPaused() ? "Paused" : "Unpaused");
                    return true;
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                    GameReflection.SetSpeed(1);
                    Speech.Say("1x");
                    return true;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                    GameReflection.SetSpeed(2);
                    Speech.Say("1.5x");
                    return true;
                case KeyCode.Alpha3:
                case KeyCode.Keypad3:
                    GameReflection.SetSpeed(3);
                    Speech.Say("2x");
                    return true;
                case KeyCode.Alpha4:
                case KeyCode.Keypad4:
                    GameReflection.SetSpeed(4);
                    Speech.Say("3x");
                    return true;

                // Stats hotkeys (Alt+S/V/O handled by SettlementInfoHandler)
                case KeyCode.S:
                    if (modifiers.Shift)
                    {
                        _infoPanelMenu?.OpenStatsPanel();
                        return true;
                    }
                    return true;
                case KeyCode.V:
                    if (modifiers.Shift)
                    {
                        _infoPanelMenu?.OpenVillagersPanel();
                        return true;
                    }
                    return true;
                case KeyCode.T:
                    StatsReader.AnnounceTimeSummary();
                    return true;

                // Map Scanner controls
                case KeyCode.PageUp:
                    if (modifiers.Control)
                        _mapScanner?.ChangeCategory(-1);
                    else if (modifiers.Shift)
                        _mapScanner?.ChangeSubcategory(-1);
                    else if (modifiers.Alt)
                        _mapScanner?.ChangeItem(-1);
                    else
                        _mapScanner?.ChangeGroup(-1);
                    return true;
                case KeyCode.PageDown:
                    if (modifiers.Control)
                        _mapScanner?.ChangeCategory(1);
                    else if (modifiers.Shift)
                        _mapScanner?.ChangeSubcategory(1);
                    else if (modifiers.Alt)
                        _mapScanner?.ChangeItem(1);
                    else
                        _mapScanner?.ChangeGroup(1);
                    return true;
                case KeyCode.Home:
                    if (modifiers.Alt)
                    {
                        Plugin.ScannerAutoMove.Value = !Plugin.ScannerAutoMove.Value;
                        Speech.Say(Plugin.ScannerAutoMove.Value ? "Auto-move on" : "Auto-move off");
                    }
                    else
                    {
                        _mapScanner?.MoveCursorToItem();
                    }
                    return true;
                case KeyCode.End:
                    if (modifiers.Alt)
                    {
                        if (!_hasBookmark)
                            Speech.Say("No bookmark");
                        else
                            _mapScanner?.AnnounceDistanceFrom(_bookmarkX, _bookmarkY, "of bookmark");
                    }
                    else
                    {
                        _mapScanner?.AnnounceDistance();
                    }
                    return true;

                // Tile info
                case KeyCode.I:
                    if (modifiers.Alt)
                        _mapScanner?.ReadCurrentItemInfo();
                    else
                        TileInfoReader.ReadCurrentTile(_mapNavigator.CursorX, _mapNavigator.CursorY);
                    return true;
                case KeyCode.E:
                    _mapNavigator.AnnounceEntrance();
                    return true;
                case KeyCode.R:
                    _mapNavigator.RotateBuilding(clockwise: !modifiers.Shift);
                    return true;

                // Building range/orientation info
                case KeyCode.D:
                    var buildingAtCursor = GameReflection.GetBuildingAtPosition(_mapNavigator.CursorX, _mapNavigator.CursorY);
                    if (buildingAtCursor != null)
                    {
                        string rangeInfo = RangeInfoHelper.GetBuildingRangeInfo(buildingAtCursor);
                        Speech.Say(rangeInfo);
                    }
                    else
                    {
                        string resourceRangeInfo = RangeInfoHelper.GetResourceRangeInfo(_mapNavigator.CursorX, _mapNavigator.CursorY);
                        Speech.Say(resourceRangeInfo);
                    }
                    return true;

                // Bookmark / blight info
                case KeyCode.B:
                    if (modifiers.Shift)
                    {
                        _bookmarkX = _mapNavigator.CursorX;
                        _bookmarkY = _mapNavigator.CursorY;
                        _hasBookmark = true;
                        Speech.Say("Bookmark set");
                    }
                    else if (modifiers.Alt)
                    {
                        string blightInfo = BlightInfoHelper.GetBlightInfo(_mapNavigator.CursorX, _mapNavigator.CursorY);
                        Speech.Say(blightInfo);
                    }
                    else
                    {
                        if (!_hasBookmark)
                        {
                            Speech.Say("No bookmark");
                        }
                        else
                        {
                            _mapNavigator.SetCursorPosition(_bookmarkX, _bookmarkY);
                            _mapNavigator.MoveCursor(0, 0);
                        }
                    }
                    return true;

                // Tracked orders (Alt+O handled by SettlementInfoHandler)
                case KeyCode.O:
                    if (modifiers.Shift)
                    {
                        GameReflection.OpenOrdersPopup();
                        return true;
                    }
                    return true;

                // Rainpunk info/control
                case KeyCode.P:
                    if (modifiers.Shift)
                    {
                        string result = RainpunkHelper.StopAllEnginesAtBuilding(
                            _mapNavigator.CursorX, _mapNavigator.CursorY);
                        Speech.Say(result);
                    }
                    else
                    {
                        string info = RainpunkHelper.GetRainpunkInfo(
                            _mapNavigator.CursorX, _mapNavigator.CursorY);
                        Speech.Say(info);
                    }
                    return true;

                // Worker info/management
                case KeyCode.W:
                    if (modifiers.Shift)
                    {
                        _infoPanelMenu?.OpenWorkersPanel();
                        return true;
                    }
                    var workerBuilding = GameReflection.GetBuildingAtPosition(_mapNavigator.CursorX, _mapNavigator.CursorY);
                    Speech.Say(WorkerInfoHelper.GetWorkerSummary(workerBuilding));
                    return true;

                case KeyCode.Equals:
                case KeyCode.KeypadPlus:
                    if (modifiers.Shift)
                    {
                        var addBuilding = GameReflection.GetBuildingAtPosition(_mapNavigator.CursorX, _mapNavigator.CursorY);
                        Speech.Say(WorkerInfoHelper.AddWorker(addBuilding));
                    }
                    else
                    {
                        Speech.Say(WorkerInfoHelper.CycleRace(1));
                    }
                    return true;

                case KeyCode.Minus:
                case KeyCode.KeypadMinus:
                    if (modifiers.Shift)
                    {
                        var removeBuilding = GameReflection.GetBuildingAtPosition(_mapNavigator.CursorX, _mapNavigator.CursorY);
                        Speech.Say(WorkerInfoHelper.RemoveWorker(removeBuilding));
                    }
                    else
                    {
                        Speech.Say(WorkerInfoHelper.CycleRace(-1));
                    }
                    return true;

                // Building activation, harvest mark, or lake retrieve
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    var objectAtCursor = GameReflection.GetObjectOn(_mapNavigator.CursorX, _mapNavigator.CursorY);
                    if (objectAtCursor != null && objectAtCursor.GetType().Name == "NaturalResource")
                    {
                        bool isMarked = GameReflection.IsNaturalResourceMarked(objectAtCursor);
                        _harvestMarkHandler.EnterMode(isMarked);
                        return true;
                    }
                    if (objectAtCursor != null && objectAtCursor.GetType().Name == "Lake")
                    {
                        var storedGoods = GameReflection.GetLakeStoredGoods(objectAtCursor);
                        if (storedGoods.Count == 0)
                        {
                            Speech.Say("No fish to retrieve");
                            return true;
                        }

                        int charges = GameReflection.GetLakeChargesLeft(objectAtCursor);
                        string lakeName = GameReflection.GetResourceNodeDisplayName(objectAtCursor) ?? "Lake";

                        var message = new System.Text.StringBuilder();
                        message.Append($"Retrieve {lakeName}? {charges} charges lost. Stored: ");
                        for (int i = 0; i < storedGoods.Count; i++)
                        {
                            if (i > 0) message.Append(", ");
                            message.Append($"{storedGoods[i].amount} {storedGoods[i].name}");
                        }
                        message.Append(". Enter to confirm, Escape to cancel");

                        _confirmationDialog.ShowMessage(message.ToString(), () =>
                        {
                            if (GameReflection.ForceDepliteLake(objectAtCursor))
                            {
                                SoundManager.PlayPortNetsRetrieved();
                                Speech.Say($"Retrieved: {lakeName}");
                            }
                            else
                            {
                                Speech.Say("Retrieval failed");
                            }
                        });
                        return true;
                    }
                    _mapNavigator.ActivateBuilding();
                    return true;

                // Panel/menu openers
                case KeyCode.F1:
                    _infoPanelMenu?.Open();
                    return true;
                case KeyCode.F2:
                    _menuHub?.Open();
                    return true;
                case KeyCode.F3:
                    _rewardsPanel?.Open();
                    return true;
                case KeyCode.Tab:
                    _buildingMenuPanel?.Open();
                    return true;
                case KeyCode.N:
                    if (modifiers.Alt)
                    {
                        _announcementHistoryPanel?.Open();
                        return true;
                    }
                    return true;

                // Reset cursor to hearth
                case KeyCode.H:
                    if (modifiers.Alt)
                    {
                        _mapNavigator.ResetCursor();
                        _mapNavigator.MoveCursor(0, 0);
                        return true;
                    }
                    return true;

                // Direct tree mark toggle
                case KeyCode.Backspace:
                    ToggleTreeMark();
                    return true;

                // Move building mode
                case KeyCode.M:
                    if (modifiers.Shift)
                    {
                        _infoPanelMenu?.OpenModifiersPanel();
                        return true;
                    }
                    var building = GameReflection.GetBuildingAtPosition(_mapNavigator.CursorX, _mapNavigator.CursorY);
                    if (building != null)
                        _moveModeController?.EnterMoveMode(building);
                    else
                        Speech.Say("No building here");
                    return true;

                // Worker building cycling
                case KeyCode.Period:
                    if (modifiers.Shift)
                        CycleWorkerCategory(1);
                    else
                        CycleWorkerBuilding(1);
                    return true;
                case KeyCode.Comma:
                    if (modifiers.Shift)
                        CycleWorkerCategory(-1);
                    else
                        CycleWorkerBuilding(-1);
                    return true;

                default:
                    // Consume all keys - mod has full keyboard control in settlement
                    return true;
            }
        }

        private void CycleWorkerCategory(int direction)
        {
            _workerCategoryIndex = NavigationUtils.WrapIndex(_workerCategoryIndex, direction, WorkerCategories.Length);
            _workerBuildingIndex = -1;
            Speech.Say(WorkerCategories[_workerCategoryIndex]);
        }

        private void CycleWorkerBuilding(int direction)
        {
            var allBuildings = GameReflection.GetAllBuildingObjects();

            var filtered = new List<(object building, string name, Vector2Int pos)>();

            foreach (var building in allBuildings)
            {
                if (!BuildingReflection.IsProductionBuilding(building)) continue;
                if (GameReflection.IsBuildingUnfinished(building)) continue;
                if (BuildingReflection.GetMaxWorkers(building) <= 0) continue;

                if (_workerCategoryIndex > 0)
                {
                    string typeName = building.GetType().Name;
                    int cat;
                    if (!BuildingTypeToWorkerCategory.TryGetValue(typeName, out cat) || cat != _workerCategoryIndex)
                        continue;
                }

                string name = GameReflection.GetBuildingDisplayName(building);
                if (string.IsNullOrEmpty(name)) continue;

                Vector2Int pos = GameReflection.GetBuildingPosition(building);
                if (pos.x < 0 || pos.y < 0) continue;

                filtered.Add((building, name, pos));
            }

            // Stable sort: alphabetical by name, then by position
            filtered.Sort((a, b) =>
            {
                int cmp = string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase);
                if (cmp != 0) return cmp;
                cmp = a.pos.x.CompareTo(b.pos.x);
                if (cmp != 0) return cmp;
                return a.pos.y.CompareTo(b.pos.y);
            });

            if (filtered.Count == 0)
            {
                if (_workerCategoryIndex > 0)
                    Speech.Say($"No {WorkerCategories[_workerCategoryIndex].ToLowerInvariant()} buildings");
                else
                    Speech.Say("No buildings with worker slots");
                return;
            }

            // Advance index, handling initial -1
            if (_workerBuildingIndex < 0)
                _workerBuildingIndex = direction > 0 ? 0 : filtered.Count - 1;
            else
                _workerBuildingIndex = NavigationUtils.WrapIndex(_workerBuildingIndex, direction, filtered.Count);

            var selected = filtered[_workerBuildingIndex];
            _mapNavigator.SetCursorPosition(selected.pos.x, selected.pos.y);

            string summary = WorkerInfoHelper.GetWorkerSummary(selected.building);
            Speech.Say($"{selected.name}, {summary}");
        }

        private void ToggleTreeMark()
        {
            var pos = new Vector2Int(_mapNavigator.CursorX, _mapNavigator.CursorY);
            var resource = GameReflection.GetNaturalResourceAt(pos);
            if (resource == null)
            {
                Speech.Say("No tree here");
                return;
            }

            if (GameReflection.IsNaturalResourceMarked(resource))
            {
                GameReflection.UnmarkNaturalResourceAt(pos);
                Speech.Say("Unmarked");
            }
            else
            {
                GameReflection.MarkNaturalResourceAt(pos);
                if (GameReflection.IsNaturalResourceGladeEdge(pos))
                    Speech.Say("Marked, glade edge");
                else
                    Speech.Say("Marked");
            }
        }

    }
}
