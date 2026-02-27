# MetaRewardsPopupReader.cs
Reads and announces the MetaRewardsPopup (end-of-settlement rewards screen).
Handles the animated reveal of rewards by polling until the count stabilizes.

## class MetaRewardsPopupReader (line 15)

### Fields
- private static bool _isPolling (line 18)
- private static bool _isReady (line 19)
- private static string _cachedAnnouncement (line 20)

### Properties
- public static bool IsPolling { get; } (line 25)
- public static bool IsReady { get; } (line 30)

### Methods
- private static bool IsMetaRewardsOrLevelUpPopup(string popupName) (line 35)
  Returns true if name contains "MetaRewards" or "MetaLevelUp".
- public static void Reset() (line 42)
  Resets all state when popup closes.
- public static bool ProcessKeyEvent(KeyCode keyCode, GameObject popup) (line 52)
  Handles Enter (close popup when ready, or say "Please wait"), arrow keys (re-read cached announcement). Returns true if key consumed.
- private static void ClosePopup(GameObject popup) (line 92)
  Prefers clicking the closeButton field for proper tutorial flow. Falls back to calling Hide() directly.
- public static IEnumerator AnnounceMetaRewardsPopup(GameObject popup, MonoBehaviour runner) (line 132)
  Coroutine. Reads level/exp text immediately, then polls reward slots every 0.5s up to 6s until count stabilizes. On world map: auto-closes after announcing (preserves tutorial tooltip access). Otherwise: waits for user to close. Combined announcement: level, exp, rewards, "Press enter or escape to close".
- private static List<string> GetRewardsFromMetaRewardSlots(GameObject popup) (line 244)
  Accesses rewardsSlots field (List<MetaRewardSlot>) via reflection to get all slots including those inactive during animation. Also checks upgradesSlot (ProgressionSlot) for level-up unlocks. Returns list of "Amount DisplayName" strings.
