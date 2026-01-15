// =============================================================================
// GAME TYPES REFERENCE - Discovered types in Against the Storm (Eremite namespace)
// =============================================================================

/*
 * This file documents all game types discovered during mod development.
 * All access is via reflection since we don't have direct references.
 *
 * Assembly: Assembly-CSharp
 * Namespace: Eremite.*
 */

namespace Reference
{
    /// <summary>
    /// Documentation of discovered game types - NOT actual code
    /// </summary>
    public static class GameTypesReference
    {
        // =====================================================================
        // CONTROLLER HIERARCHY
        // =====================================================================

        /*
        Eremite.Controller.GameController
        ---------------------------------
        The main controller for in-game (settlement) state.

        Static Members:
          - Instance : GameController (singleton)
          - IsGameActive : bool (true when in settlement)

        Instance Members:
          - GameServices : GameServices
          - OnGameStarted : Observable<object> (UniRx)

        Access Pattern:
          var gcType = assembly.GetType("Eremite.Controller.GameController");
          var instance = gcType.GetProperty("Instance", Static).GetValue(null);
          var isActive = (bool)gcType.GetProperty("IsGameActive", Static).GetValue(null);


        Eremite.Controller.MainController
        ---------------------------------
        The main controller for app-level state (menus, etc.)

        Static Members:
          - Instance : MainController (singleton)

        Instance Members:
          - AppServices : AppServices

        Access Pattern:
          var mcType = assembly.GetType("Eremite.Controller.MainController");
          var instance = mcType.GetProperty("Instance", Static).GetValue(null);


        Eremite.Controller.MetaController
        ---------------------------------
        Controller for meta-progression (unlocks, etc.)

        Static Members:
          - Instance : MetaController (singleton)

        Instance Members:
          - MetaServices : MetaServices
        */

        // =====================================================================
        // SERVICE CONTAINERS
        // =====================================================================

        /*
        Eremite.Services.GameServices
        -----------------------------
        Container for all in-game services.

        Properties:
          - MapService : MapService
          - GladesService : GladesService
          - ResourcesService : ResourcesService
          - ModeService : ModeService
          - BuildingsService : BuildingsService
          - ... many more


        Eremite.Services.AppServices
        ----------------------------
        Container for app-level services.

        Properties:
          - PopupsService : PopupsService


        Eremite.Services.MetaServices
        ----------------------------
        Container for meta-progression services.

        Properties:
          - MetaPerksService : MetaPerksService
        */

        // =====================================================================
        // MAP SERVICES
        // =====================================================================

        /*
        Eremite.Services.MapService
        ---------------------------
        Service for accessing map data.

        Methods:
          - GetField(int x, int y) : Field
            Returns the field (tile) at the given coordinate.
            Returns null if out of bounds.

          - GetObjectOn(int x, int y) : object
            Returns the object on the tile (building, resource, etc.)
            Returns null if empty.

        Map Size: 70x70 grid (coordinates 0-69)


        Eremite.Services.GladesService
        ------------------------------
        Service for fog of war / glades.

        Methods:
          - IsGlade(Vector2Int pos) : bool
            Returns true if position is in unexplored glade.

          - GetGlade(Vector2Int pos) : Glade
            Returns the glade object at position, or null.


        Field (Map Tile)
        ----------------
        Represents a single tile on the map.

        Properties:
          - Type : FieldType (enum - terrain type)
          - IsTraversable : bool

        Field.Type values include:
          - Grass, Forest, Water, Mountain, etc.


        Glade
        -----
        Represents an unexplored area.

        Fields:
          - Danger : uint (danger level 0-10+)
        */

        // =====================================================================
        // POPUP/UI TYPES
        // =====================================================================

        /*
        Eremite.Services.PopupsService
        ------------------------------
        Manages all popups in the game.

        Properties:
          - AnyPopupShown : IReadOnlyReactiveProperty<object>
            Observable that fires when any popup is shown.

          - AnyPopupHidden : IReadOnlyReactiveProperty<object>
            Observable that fires when any popup is hidden.

        Methods:
          - PopupShown(object popup) : void
            Called internally when popup is shown.

          - PopupClosed(object popup) : void
            Called internally when popup is closed.


        Eremite.View.Popups.Popup
        -------------------------
        Base class for all popups.

        Methods:
          - AnimateShow() : void
            Called when popup is displayed.

          - Hide() : void
            Called when popup is hidden.


        Eremite.View.UI.TabsPanel
        -------------------------
        Container for tabbed interfaces.

        Fields:
          - current : TabsButton (the active tab)

        Methods:
          - OnButtonClicked(TabsButton button) : void
            Called when a tab is clicked.


        Eremite.View.UI.TabsButton
        --------------------------
        Individual tab button.

        Fields:
          - content : GameObject (the tab's content panel)
        */

        // =====================================================================
        // DIALOGUE TYPES
        // =====================================================================

        /*
        Eremite.Tutorial.Views.TutorialTooltip
        --------------------------------------
        Tutorial popups/tooltips.

        Methods:
          - Show() : void
          - Hide() : void

        Contains TMP_Text children for title/body text.


        Eremite.View.DecisionPopup
        --------------------------
        Decision/confirmation dialogs.

        Methods:
          - Show() : void
          - Confirm() : void (user confirmed)
          - Cancel() : void (user cancelled)

        Contains TMP_Text children for title/body text.
        */

        // =====================================================================
        // GAME STATE
        // =====================================================================

        /*
        Eremite.Services.ModeService
        ----------------------------
        Tracks the current game mode.

        Properties:
          - Idle : IReadOnlyReactiveProperty<bool>
            true = normal gameplay
            false = special mode (building placement, etc.)
        */

        // =====================================================================
        // TYPE LOOKUP PATTERNS
        // =====================================================================

        public static void ExampleTypeLookup()
        {
            // Find Assembly-CSharp
            System.Reflection.Assembly gameAssembly = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (asm.GetName().Name == "Assembly-CSharp")
                {
                    gameAssembly = asm;
                    break;
                }
            }

            if (gameAssembly == null) return;

            // Direct type lookup (preferred - fast, doesn't trigger all type initializers)
            var gameControllerType = gameAssembly.GetType("Eremite.Controller.GameController");
            var mapServiceType = gameAssembly.GetType("Eremite.Services.MapService");
            var popupType = gameAssembly.GetType("Eremite.View.Popups.Popup");

            // AVOID: Iterating all types (slow, can cause issues)
            // foreach (var type in assembly.GetTypes()) { ... }  // BAD!
        }

        // =====================================================================
        // FULL TYPE NAME REFERENCE
        // =====================================================================

        public static class TypeNames
        {
            // Controllers
            public const string GameController = "Eremite.Controller.GameController";
            public const string MainController = "Eremite.Controller.MainController";
            public const string MetaController = "Eremite.Controller.MetaController";

            // Service Containers
            public const string GameServices = "Eremite.Services.GameServices";
            public const string AppServices = "Eremite.Services.AppServices";
            public const string MetaServices = "Eremite.Services.MetaServices";

            // Map Services
            public const string MapService = "Eremite.Services.MapService";
            public const string GladesService = "Eremite.Services.GladesService";
            public const string ResourcesService = "Eremite.Services.ResourcesService";
            public const string BuildingsService = "Eremite.Services.BuildingsService";

            // UI Services
            public const string PopupsService = "Eremite.Services.PopupsService";
            public const string ModeService = "Eremite.Services.ModeService";

            // Popup Types
            public const string PopupBase = "Eremite.View.Popups.Popup";
            public const string TabsPanel = "Eremite.View.UI.TabsPanel";
            public const string TabsButton = "Eremite.View.UI.TabsButton";

            // Dialogue Types
            public const string TutorialTooltip = "Eremite.Tutorial.Views.TutorialTooltip";
            public const string DecisionPopup = "Eremite.View.DecisionPopup";

            // Meta
            public const string MetaPerksService = "Eremite.Services.MetaPerksService";
        }
    }
}
