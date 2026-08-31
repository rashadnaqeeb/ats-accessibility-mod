using ATSAccessibility.Panels;
using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ATSAccessibility.Handlers {
	/// <summary>
	/// Handles keyboard input for settlement map navigation.
	/// This is the fallback handler when no popups/menus are open during gameplay.
	/// </summary>
	public class SettlementKeyHandler: IKeyHandler, IHelpProvider {
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

		private readonly bool[] _numberedBookmarkSet = new bool[10];
		private readonly int[] _numberedBookmarkX = new int[10];
		private readonly int[] _numberedBookmarkY = new int[10];

		// Scanner search input state
		private bool _searchInputActive = false;
		private readonly StringBuilder _searchBuffer = new StringBuilder();

		// Worker building cycling
		private int _workerBuildingIndex = -1;
		private int _workerCategoryIndex = 0;  // 0=All, 1=Gathering, 2=Production, 3=Service, 4=Events
		private static string[] WorkerCategories => new[] {
			Strings.Get("common.all"),
			Strings.Get("common.gathering"),
			Strings.Get("common.production"),
			Strings.Get("handler.settlekey.worker_cat_service"),
			Strings.Get("handler.settlekey.worker_cat_events"),
		};

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
			HarvestMarkHandler harvestMarkHandler) {
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
		/// Reset per-settlement state. Called on scene unload: a settlement can end
		/// while search input mode is armed (it would silently eat every key in the
		/// next settlement), and bookmarks/worker cycling hold coordinates that are
		/// meaningless on a different map.
		/// </summary>
		public void Reset() {
			_searchInputActive = false;
			_searchBuffer.Length = 0;
			_hasBookmark = false;
			for (int i = 0; i < _numberedBookmarkSet.Length; i++)
				_numberedBookmarkSet[i] = false;
			_workerBuildingIndex = -1;
			_workerCategoryIndex = 0;
			// Scanner caches and committed search results also carry old-map
			// coordinates.
			_mapScanner?.Reset();
		}

		// ========================================
		// IHELPPROVIDER
		// ========================================

		private static readonly List<HelpEntry> _helpEntries = new List<HelpEntry> {
			HelpEntry.Loca("Ctrl+Arrows", "handler.settlement_key.help.skip_to_change"),
			HelpEntry.Loca("K", "handler.settlement_key.help.announce_position"),
			HelpEntry.Loca("Alt+K", "handler.settlement_key.help.toggle_coords"),
			HelpEntry.Loca("I", "handler.settlement_key.help.tile_info"),
			HelpEntry.Loca("Space", "handler.settlement_key.help.pause"),
			HelpEntry.Loca("1-4", "handler.settlement_key.help.game_speed"),
			HelpEntry.Loca("S", "handler.settlement_key.help.quick_summary"),
			HelpEntry.Loca("V", "handler.settlement_key.help.species_resolve"),
			HelpEntry.Loca("T", "handler.settlement_key.help.time_summary"),
			HelpEntry.Loca("Shift+T", "handler.settlement_key.help.trends_popup"),
			HelpEntry.Loca("O", "handler.settlement_key.help.tracked_orders"),
			HelpEntry.Loca("M", "handler.settlement_key.help.move_building"),
			HelpEntry.Loca("R", "handler.settlement_key.help.rotate_building"),
			HelpEntry.Loca("Shift+R", "handler.settlement_key.help.rotate_ccw"),
			HelpEntry.Loca("Shift+Space", "handler.settlement_key.help.destroy_or_remove"),
			HelpEntry.Loca("E", "handler.settlement_key.help.entrance_info"),
			HelpEntry.Loca("W", "handler.settlement_key.help.worker_summary"),
			HelpEntry.Loca("+/-", "handler.settlement_key.help.cycle_race_priority"),
			HelpEntry.Loca("Shift+/-", "handler.settlement_key.help.add_remove_worker"),
			HelpEntry.Loca("Period/Comma", "handler.settlement_key.help.cycle_worker_bldgs"),
			HelpEntry.Loca("Shift+Period/Comma", "handler.settlement_key.help.cycle_worker_cat"),
			HelpEntry.Loca("Backspace", "handler.settlement_key.help.toggle_tree_mark"),
			HelpEntry.Loca("D", "handler.settlement_key.help.range_info"),
			HelpEntry.Loca("Alt+D", "handler.settlement_key.help.blight_info"),
			HelpEntry.Loca("P", "handler.settlement_key.help.rainpunk_info"),
			HelpEntry.Loca("Shift+P", "handler.settlement_key.help.stop_engines"),
			HelpEntry.Loca("Shift+B", "handler.settlement_key.help.set_bookmark"),
			HelpEntry.Loca("B", "handler.settlement_key.help.jump_bookmark"),
			HelpEntry.Loca("Alt+B", "handler.settlement_key.help.dir_bookmark"),
			HelpEntry.Loca("Ctrl+0-9", "handler.settlement_key.help.set_n_bookmark"),
			HelpEntry.Loca("Shift+0-9", "handler.settlement_key.help.jump_n_bookmark"),
			HelpEntry.Loca("Alt+0-9", "handler.settlement_key.help.dir_n_bookmark"),
			HelpEntry.Loca("Alt+H", "handler.settlement_key.help.reset_hearth"),
			HelpEntry.Loca("Shift+N", "handler.settlement_key.help.jump_latest_event"),
			HelpEntry.Loca("Alt+N", "handler.settlement_key.help.announce_history"),
			HelpEntry.Loca("Ctrl+PageUp/Down", "handler.settlement_key.help.scanner_category"),
			HelpEntry.Loca("Shift+PageUp/Down", "handler.settlement_key.help.scanner_subcategory"),
			HelpEntry.Loca("PageUp/Down", "handler.settlement_key.help.scanner_group"),
			HelpEntry.Loca("Alt+PageUp/Down", "handler.settlement_key.help.scanner_item"),
			HelpEntry.Loca("Home", "handler.settlement_key.help.scanner_jump"),
			HelpEntry.Loca("Alt+Home", "handler.settlement_key.help.scanner_automove"),
			HelpEntry.Loca("End", "handler.settlement_key.help.scanner_distance"),
			HelpEntry.Loca("Alt+I", "handler.settlement_key.help.scanner_item_info"),
			HelpEntry.Loca("Ctrl+F", "handler.settlement_key.help.scanner_search"),
			HelpEntry.Loca("F1", "handler.settlement_key.help.info_panels"),
			HelpEntry.Loca("F2", "handler.settlement_key.help.quick_menu"),
			HelpEntry.Loca("F3", "handler.settlement_key.help.rewards_panel"),
			HelpEntry.Loca("Tab", "handler.settlement_key.help.building_menu"),
			HelpEntry.Loca("Shift+S", "handler.settlement_key.help.stats_panel"),
			HelpEntry.Loca("Shift+V", "handler.settlement_key.help.villagers_panel"),
			HelpEntry.Loca("Shift+W", "handler.settlement_key.help.workers_panel"),
			HelpEntry.Loca("Shift+M", "handler.settlement_key.help.modifiers_panel"),
			HelpEntry.Loca("Shift+O", "handler.settlement_key.help.orders_popup"),
		};

		public HelpBehavior HelpBehavior => HelpBehavior.Terminator;
		public string HelpContextName => "Settlement Map";
		public IReadOnlyList<HelpEntry> GetHelpEntries() => _helpEntries;
		public IReadOnlyList<string> GetPassthroughKeys() => null;

		/// <summary>
		/// Active when the settlement game is running.
		/// </summary>
		public bool IsActive => GameReflection.GetIsGameActive();

		/// <summary>
		/// Process settlement map key events.
		/// </summary>
		public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			if (!IsActive) return false;

			if (_searchInputActive)
				return HandleSearchInput(keyCode, modifiers);

			switch (keyCode) {
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
					if (modifiers.Alt) {
						Plugin.AnnounceCoordinates.Value = !Plugin.AnnounceCoordinates.Value;
						Speech.Say(Plugin.AnnounceCoordinates.Value ? Strings.Get("handler.settlekey.coords_on") : Strings.Get("handler.settlekey.coords_off"));
					} else {
						_mapNavigator.AnnounceCurrentPosition();
					}
					return true;

				// Game speed controls
				case KeyCode.Space:
					if (modifiers.Shift) {
						// Shift+Space: destroy building or remove resource node at cursor
						var buildingToDestroy = ConstructionReflection.GetBuildingAtPosition(_mapNavigator.CursorX, _mapNavigator.CursorY);
						if (buildingToDestroy != null) {
							// Building found — existing destroy logic
							if (!BuildingReflection.CanBeDestroyed(buildingToDestroy)) {
								string name = GameReflection.GetDisplayName(ConstructionReflection.GetBuildingModel(buildingToDestroy));
								Speech.Say(Strings.Get("handler.settlekey.cannot_destroy", name));
							} else {
								string name = GameReflection.GetDisplayName(ConstructionReflection.GetBuildingModel(buildingToDestroy));
								var refundGoods = BuildingReflection.GetDestructionRefund(buildingToDestroy);
								_confirmationDialog.Show(name, () => {
									if (BuildingReflection.DestroyBuilding(buildingToDestroy)) {
										SoundManager.PlayBuildingDestroyed();
										Speech.Say(Strings.Get("handler.settlekey.destroyed", name));
									} else {
										Speech.Say(Strings.Get("handler.settlekey.destroy_failed"));
									}
								}, refundGoods);
							}
						} else {
							// No building — check for resource node
							var objectAtPos = GameReflection.GetObjectOn(_mapNavigator.CursorX, _mapNavigator.CursorY);
							if (objectAtPos != null && ConstructionReflection.IsRemovableResource(objectAtPos)) {
								string name = ConstructionReflection.GetResourceNodeDisplayName(objectAtPos) ?? Strings.Get("handler.settlekey.resource_fallback");
								_confirmationDialog.Show(name, () => {
									if (ConstructionReflection.RemoveResourceNode(objectAtPos)) {
										SoundManager.PlayResourceRemoved();
										Speech.Say(Strings.Get("handler.settlekey.removed", name));
									} else {
										Speech.Say(Strings.Get("common.removal_failed"));
									}
								});
							} else if (objectAtPos != null && objectAtPos.GetType().Name == "NaturalResource") {
								Speech.Say(Strings.Get("handler.settlekey.cannot_remove_trees"));
							} else if (objectAtPos != null && objectAtPos.GetType().Name == "Ore") {
								Speech.Say(Strings.Get("handler.settlekey.cannot_remove_ore"));
							} else {
								Speech.Say(Strings.Get("handler.settlekey.nothing_to_remove"));
							}
						}
						return true;
					}
					GameReflection.TogglePause();
					Speech.Say(GameReflection.IsPaused() ? Strings.Get("common.paused") : Strings.Get("common.unpaused"));
					return true;
				// Keypad keys: speed control only, no modifier checks
				case KeyCode.Keypad1:
					GameReflection.SetSpeed(1);
					Speech.Say(Strings.Get("handler.settlekey.speed_1"));
					return true;
				case KeyCode.Keypad2:
					GameReflection.SetSpeed(2);
					Speech.Say(Strings.Get("handler.settlekey.speed_2"));
					return true;
				case KeyCode.Keypad3:
					GameReflection.SetSpeed(3);
					Speech.Say(Strings.Get("handler.settlekey.speed_3"));
					return true;
				case KeyCode.Keypad4:
					GameReflection.SetSpeed(4);
					Speech.Say(Strings.Get("handler.settlekey.speed_4"));
					return true;

				// Alpha number keys: bookmarks with modifiers, speed 1-4 unmodified
				case KeyCode.Alpha0:
				case KeyCode.Alpha1:
				case KeyCode.Alpha2:
				case KeyCode.Alpha3:
				case KeyCode.Alpha4:
				case KeyCode.Alpha5:
				case KeyCode.Alpha6:
				case KeyCode.Alpha7:
				case KeyCode.Alpha8:
				case KeyCode.Alpha9:
					int slot = keyCode - KeyCode.Alpha0;
					if (modifiers.Control) {
						SetNumberedBookmark(slot);
					} else if (modifiers.Shift) {
						JumpToNumberedBookmark(slot);
					} else if (modifiers.Alt) {
						if (_numberedBookmarkSet[slot])
							AnnounceDirectionTo(_numberedBookmarkX[slot], _numberedBookmarkY[slot]);
						else
							Speech.Say(Strings.Get("handler.settlekey.no_bookmark_slot", slot));
					} else if (slot >= 1 && slot <= 4) {
						// Unmodified 1-4: speed control
						GameReflection.SetSpeed(slot);
						string[] speedLabels = {
							"",
							Strings.Get("handler.settlekey.speed_1"),
							Strings.Get("handler.settlekey.speed_2"),
							Strings.Get("handler.settlekey.speed_3"),
							Strings.Get("handler.settlekey.speed_4"),
						};
						Speech.Say(speedLabels[slot]);
					}
					// Unmodified 0, 5-9: consumed silently
					return true;

				// Stats hotkeys (Alt+S/V/O handled by SettlementInfoHandler)
				case KeyCode.S:
					if (modifiers.Shift) {
						_infoPanelMenu?.OpenStatsPanel();
						return true;
					}
					StatsReader.AnnounceQuickSummary();
					return true;
				case KeyCode.V:
					if (modifiers.Shift) {
						_infoPanelMenu?.OpenVillagersPanel();
						return true;
					}
					StatsReader.AnnounceNextSpeciesResolve();
					return true;
				case KeyCode.T:
					if (modifiers.Shift) {
						GameReflection.OpenTrendsPopup();
						return true;
					}
					StatsReader.AnnounceTimeSummary();
					return true;

				// Map Scanner controls
				case KeyCode.PageUp:
					if (modifiers.Control) {
						if (_mapScanner != null && _mapScanner.IsInSearchResults) {
							_mapScanner.ClearSearchResults();
							_mapScanner.ChangeCategory(-1);
						} else {
							_mapScanner?.ChangeCategory(-1);
						}
					} else if (modifiers.Shift)
						_mapScanner?.ChangeSubcategory(-1);
					else if (modifiers.Alt)
						_mapScanner?.ChangeItem(-1);
					else
						_mapScanner?.ChangeGroup(-1);
					return true;
				case KeyCode.PageDown:
					if (modifiers.Control) {
						if (_mapScanner != null && _mapScanner.IsInSearchResults) {
							_mapScanner.ClearSearchResults();
							_mapScanner.ChangeCategory(1);
						} else {
							_mapScanner?.ChangeCategory(1);
						}
					} else if (modifiers.Shift)
						_mapScanner?.ChangeSubcategory(1);
					else if (modifiers.Alt)
						_mapScanner?.ChangeItem(1);
					else
						_mapScanner?.ChangeGroup(1);
					return true;
				case KeyCode.Home:
					if (modifiers.Alt) {
						Plugin.ScannerAutoMove.Value = !Plugin.ScannerAutoMove.Value;
						Speech.Say(Plugin.ScannerAutoMove.Value ? Strings.Get("handler.settlekey.automove_on") : Strings.Get("handler.settlekey.automove_off"));
					} else {
						_mapScanner?.MoveCursorToItem();
					}
					return true;
				case KeyCode.End:
					_mapScanner?.AnnounceDistance();
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

				// Building range/orientation info, blight info
				case KeyCode.D:
					if (modifiers.Alt) {
						string blightInfo = BlightInfoHelper.GetBlightInfo(_mapNavigator.CursorX, _mapNavigator.CursorY);
						Speech.Say(blightInfo);
					} else {
						var buildingAtCursor = ConstructionReflection.GetBuildingAtPosition(_mapNavigator.CursorX, _mapNavigator.CursorY);
						if (buildingAtCursor != null) {
							string rangeInfo = RangeInfoHelper.GetBuildingRangeInfo(buildingAtCursor);
							Speech.Say(rangeInfo);
						} else {
							string resourceRangeInfo = RangeInfoHelper.GetResourceRangeInfo(_mapNavigator.CursorX, _mapNavigator.CursorY);
							Speech.Say(resourceRangeInfo);
						}
					}
					return true;

				// Bookmark / direction
				case KeyCode.B:
					if (modifiers.Shift) {
						_bookmarkX = _mapNavigator.CursorX;
						_bookmarkY = _mapNavigator.CursorY;
						_hasBookmark = true;
						Speech.Say(Strings.Get("handler.settlekey.bookmark_set"));
					} else if (modifiers.Alt) {
						if (!_hasBookmark)
							Speech.Say(Strings.Get("handler.settlekey.no_bookmark"));
						else
							AnnounceDirectionTo(_bookmarkX, _bookmarkY);
					} else {
						if (!_hasBookmark) {
							Speech.Say(Strings.Get("handler.settlekey.no_bookmark"));
						} else {
							_mapNavigator.SetCursorPosition(_bookmarkX, _bookmarkY);
							_mapNavigator.MoveCursor(0, 0);
						}
					}
					return true;

				// Tracked orders (Alt+O handled by SettlementInfoHandler)
				case KeyCode.O:
					if (modifiers.Shift) {
						GameReflection.OpenOrdersPopup();
						return true;
					}
					SettlementInfoHandler.AnnounceTrackedOrders();
					return true;

				// Rainpunk info/control
				case KeyCode.P:
					if (modifiers.Shift) {
						string result = RainpunkHelper.StopAllEnginesAtBuilding(
							_mapNavigator.CursorX, _mapNavigator.CursorY);
						Speech.Say(result);
					} else {
						string info = RainpunkHelper.GetRainpunkInfo(
							_mapNavigator.CursorX, _mapNavigator.CursorY);
						Speech.Say(info);
					}
					return true;

				// Worker info/management
				case KeyCode.W:
					if (modifiers.Shift) {
						_infoPanelMenu?.OpenWorkersPanel();
						return true;
					}
					var workerBuilding = ConstructionReflection.GetBuildingAtPosition(_mapNavigator.CursorX, _mapNavigator.CursorY);
					Speech.Say(WorkerInfoHelper.GetWorkerSummary(workerBuilding));
					return true;

				case KeyCode.Equals:
				case KeyCode.KeypadPlus: {
						var plusObj = GameReflection.GetObjectOn(_mapNavigator.CursorX, _mapNavigator.CursorY);
						string plusType = plusObj?.GetType().Name;
						if (plusType == "ResourceDeposit" || plusType == "Lake") {
							AdjustNodePriority(plusObj, +1, modifiers.Shift);
						} else {
							var plusBuilding = ConstructionReflection.GetBuildingAtPosition(_mapNavigator.CursorX, _mapNavigator.CursorY);
							if (plusBuilding != null && ConstructionReflection.IsBuildingUnfinished(plusBuilding)) {
								AdjustConstructionPriority(plusBuilding, +1, modifiers.Shift);
							} else if (modifiers.Shift) {
								Speech.Say(WorkerInfoHelper.AddWorker(plusBuilding));
							} else {
								Speech.Say(WorkerInfoHelper.CycleRace(1));
							}
						}
						return true;
					}

				case KeyCode.Minus:
				case KeyCode.KeypadMinus: {
						var minusObj = GameReflection.GetObjectOn(_mapNavigator.CursorX, _mapNavigator.CursorY);
						string minusType = minusObj?.GetType().Name;
						if (minusType == "ResourceDeposit" || minusType == "Lake") {
							AdjustNodePriority(minusObj, -1, modifiers.Shift);
						} else {
							var minusBuilding = ConstructionReflection.GetBuildingAtPosition(_mapNavigator.CursorX, _mapNavigator.CursorY);
							if (minusBuilding != null && ConstructionReflection.IsBuildingUnfinished(minusBuilding)) {
								AdjustConstructionPriority(minusBuilding, -1, modifiers.Shift);
							} else if (modifiers.Shift) {
								Speech.Say(WorkerInfoHelper.RemoveWorker(minusBuilding));
							} else {
								Speech.Say(WorkerInfoHelper.CycleRace(-1));
							}
						}
						return true;
					}

				// Building activation, harvest mark, or lake retrieve
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					var objectAtCursor = GameReflection.GetObjectOn(_mapNavigator.CursorX, _mapNavigator.CursorY);
					if (objectAtCursor != null && objectAtCursor.GetType().Name == "NaturalResource") {
						bool isMarked = MapReflection.IsNaturalResourceMarked(objectAtCursor);
						_harvestMarkHandler.EnterMode(isMarked);
						return true;
					}
					if (objectAtCursor != null && objectAtCursor.GetType().Name == "Lake") {
						var storedGoods = ConstructionReflection.GetLakeStoredGoods(objectAtCursor);
						if (storedGoods.Count == 0) {
							Speech.Say(Strings.Get("handler.settlekey.no_fish"));
							return true;
						}

						int charges = ConstructionReflection.GetLakeChargesLeft(objectAtCursor);
						string lakeName = ConstructionReflection.GetResourceNodeDisplayName(objectAtCursor) ?? Strings.Get("handler.settlekey.lake_fallback");

						var message = new System.Text.StringBuilder();
						message.Append(Strings.Get("handler.settlekey.lake_confirm_prefix", lakeName, charges));
						message.Append(' ');
						for (int i = 0; i < storedGoods.Count; i++) {
							if (i > 0) message.Append(", ");
							message.Append(Strings.Get("handler.settlekey.lake_good", storedGoods[i].amount, storedGoods[i].name));
						}
						message.Append(Strings.Get("handler.settlekey.lake_confirm_suffix"));

						_confirmationDialog.ShowMessage(message.ToString(), () => {
							if (ConstructionReflection.ForceDepliteLake(objectAtCursor)) {
								SoundManager.PlayPortNetsRetrieved();
								Speech.Say(Strings.Get("handler.settlekey.lake_retrieved", lakeName));
							} else {
								Speech.Say(Strings.Get("handler.settlekey.lake_retrieve_failed"));
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
					_rewardsPanel?.Toggle();
					return true;
				case KeyCode.Tab:
					_buildingMenuPanel?.Toggle();
					return true;
				case KeyCode.N:
					if (modifiers.Alt) {
						_announcementHistoryPanel?.Open();
						return true;
					}
					if (modifiers.Shift) {
						_announcementHistoryPanel?.JumpToLatestEventLocation();
						return true;
					}
					return true;

				// Reset cursor to hearth
				case KeyCode.H:
					if (modifiers.Alt) {
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
					if (modifiers.Shift) {
						_infoPanelMenu?.OpenModifiersPanel();
						return true;
					}
					var building = ConstructionReflection.GetBuildingAtPosition(_mapNavigator.CursorX, _mapNavigator.CursorY);
					if (building != null)
						_moveModeController?.EnterMoveMode(building);
					else
						Speech.Say(Strings.Get("common.no_building_here"));
					return true;

				// Scanner search
				case KeyCode.F:
					if (modifiers.Control) {
						if (_mapScanner != null && _mapScanner.IsInSearchResults)
							_mapScanner.ClearSearchResults();
						_searchBuffer.Clear();
						_searchInputActive = true;
						Speech.Say(Strings.Get("handler.settlekey.search"));
					}
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

		private bool HandleSearchInput(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers) {
			switch (keyCode) {
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					_searchInputActive = false;
					_mapScanner?.CommitSearch(_searchBuffer.ToString().Trim());
					return true;

				case KeyCode.Escape:
					_searchInputActive = false;
					_searchBuffer.Clear();
					Speech.Say(Strings.Get("common.search_cancelled"));
					InputBlocker.BlockCancelOnce = true;
					return true;

				case KeyCode.Backspace:
					if (_searchBuffer.Length > 0) {
						_searchBuffer.Remove(_searchBuffer.Length - 1, 1);
						Speech.Say(_searchBuffer.Length > 0 ? _searchBuffer.ToString() : Strings.Get("common.empty_lower"));
					}
					return true;

				case KeyCode.Space:
					_searchBuffer.Append(' ');
					Speech.Say(_searchBuffer.ToString());
					return true;

				default:
					char? ch = KeyCodeToChar(keyCode);
					if (ch.HasValue) {
						_searchBuffer.Append(ch.Value);
						Speech.Say(_searchBuffer.ToString());
					}
					// Consume all keys during text input
					return true;
			}
		}

		private static char? KeyCodeToChar(KeyCode keyCode) {
			if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
				return (char)('a' + (keyCode - KeyCode.A));
			if (keyCode >= KeyCode.Alpha0 && keyCode <= KeyCode.Alpha9)
				return (char)('0' + (keyCode - KeyCode.Alpha0));
			if (keyCode >= KeyCode.Keypad0 && keyCode <= KeyCode.Keypad9)
				return (char)('0' + (keyCode - KeyCode.Keypad0));
			return null;
		}

		private void SetNumberedBookmark(int slot) {
			_numberedBookmarkSet[slot] = true;
			_numberedBookmarkX[slot] = _mapNavigator.CursorX;
			_numberedBookmarkY[slot] = _mapNavigator.CursorY;
			Speech.Say(Strings.Get("handler.settlekey.bookmark_set_n", slot));
		}

		private void JumpToNumberedBookmark(int slot) {
			if (!_numberedBookmarkSet[slot]) {
				Speech.Say(Strings.Get("handler.settlekey.no_bookmark_slot", slot));
				return;
			}
			_mapNavigator.SetCursorPosition(_numberedBookmarkX[slot], _numberedBookmarkY[slot]);
			_mapNavigator.MoveCursor(0, 0);
		}

		private void AnnounceDirectionTo(int targetX, int targetY) {
			int dx = targetX - _mapNavigator.CursorX;
			int dy = targetY - _mapNavigator.CursorY;
			int distance = Math.Max(Math.Abs(dx), Math.Abs(dy));

			if (distance == 0) {
				Speech.Say(Strings.Get("common.here_lower"));
			} else {
				string direction = NavigationUtils.GetDirection(dx, dy);
				Speech.Say(Strings.Get("handler.settlekey.direction", distance, direction));
			}
		}

		private void CycleWorkerCategory(int direction) {
			_workerCategoryIndex = NavigationUtils.WrapIndex(_workerCategoryIndex, direction, WorkerCategories.Length);
			_workerBuildingIndex = -1;
			Speech.Say(WorkerCategories[_workerCategoryIndex]);
		}

		private void CycleWorkerBuilding(int direction) {
			var allBuildings = ConstructionReflection.GetAllBuildingObjects();

			var filtered = new List<(object building, string name, Vector2Int pos)>();

			foreach (var building in allBuildings) {
				if (!BuildingReflection.IsProductionBuilding(building)) continue;
				if (ConstructionReflection.IsBuildingUnfinished(building)) continue;
				if (BuildingReflection.GetMaxWorkers(building) <= 0) continue;

				if (_workerCategoryIndex > 0) {
					string typeName = building.GetType().Name;
					int cat;
					if (!BuildingTypeToWorkerCategory.TryGetValue(typeName, out cat) || cat != _workerCategoryIndex)
						continue;
				}

				string name = ConstructionReflection.GetBuildingDisplayName(building);
				if (string.IsNullOrEmpty(name)) continue;

				Vector2Int pos = ConstructionReflection.GetBuildingPosition(building);
				if (pos.x < 0 || pos.y < 0) continue;

				filtered.Add((building, name, pos));
			}

			// Stable sort: alphabetical by name, then by position
			filtered.Sort((a, b) => {
				int cmp = string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase);
				if (cmp != 0) return cmp;
				cmp = a.pos.x.CompareTo(b.pos.x);
				if (cmp != 0) return cmp;
				return a.pos.y.CompareTo(b.pos.y);
			});

			if (filtered.Count == 0) {
				if (_workerCategoryIndex > 0)
					Speech.Say(Strings.Get("handler.settlekey.no_category_buildings", WorkerCategories[_workerCategoryIndex].ToLowerInvariant()));
				else
					Speech.Say(Strings.Get("handler.settlekey.no_worker_buildings"));
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
			Speech.Say(Strings.Get("handler.settlekey.worker_cycle", selected.name, summary));
		}

		private void ToggleTreeMark() {
			var pos = new Vector2Int(_mapNavigator.CursorX, _mapNavigator.CursorY);
			var resource = MapReflection.GetNaturalResourceAt(pos);
			if (resource == null) {
				Speech.Say(Strings.Get("common.no_tree_here"));
				return;
			}

			if (MapReflection.IsNaturalResourceMarked(resource)) {
				MapReflection.UnmarkNaturalResourceAt(pos);
				Speech.Say(Strings.Get("handler.settlekey.unmarked"));
			} else {
				MapReflection.MarkNaturalResourceAt(pos);
				if (MapReflection.IsNaturalResourceGladeEdge(pos))
					Speech.Say(Strings.Get("handler.settlekey.marked_glade_edge"));
				else
					Speech.Say(Strings.Get("handler.settlekey.marked"));
			}
		}

		private void AdjustNodePriority(object node, int delta, bool global) {
			AdjustPriority(node, delta, global,
				ConstructionReflection.GetResourceNodePriority,
				ConstructionReflection.SetResourceNodePriority,
				ConstructionReflection.SetGlobalResourceNodePriority,
				ConstructionReflection.GetResourceNodeDisplayName);
		}

		private void AdjustConstructionPriority(object building, int delta, bool global) {
			AdjustPriority(building, delta, global,
				ConstructionReflection.GetBuildingConstructionPriority,
				ConstructionReflection.SetBuildingConstructionPriority,
				ConstructionReflection.SetGlobalBuildingConstructionPriority,
				ConstructionReflection.GetBuildingDisplayName);
		}

		private static void AdjustPriority(object target, int delta, bool global,
			Func<object, int> getPriority, Func<object, int, bool> setPriority,
			Func<object, int, bool> setGlobalPriority, Func<object, string> getDisplayName) {
			int current = getPriority(target);
			int newPrio = Math.Max(-5, Math.Min(5, current + delta));

			if (newPrio == current) {
				Speech.Say(delta > 0 ? Strings.Get("common.maximum") : Strings.Get("common.minimum"));
				return;
			}

			if (global) {
				setGlobalPriority(target, newPrio);
				string name = getDisplayName(target);
				Speech.Say(Strings.Get("handler.settlekey.priority_global_all", name, FormatNodePriority(newPrio)));
			} else {
				setPriority(target, newPrio);
				Speech.Say(Strings.Get("handler.settlekey.priority_local", FormatNodePriority(newPrio)));
			}
		}

		private static string FormatNodePriority(int priority) {
			if (priority == -5) return Strings.Get("common.priority_lowest");
			if (priority == 5) return Strings.Get("common.priority_highest");
			if (priority == 0) return Strings.Get("common.priority_default");
			return priority.ToString();
		}

	}
}
