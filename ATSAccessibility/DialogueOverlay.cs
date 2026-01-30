using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility
{
    /// <summary>
    /// Accessible overlay for the HomePopup (NPC dialogue navigation).
    /// Provides flat list navigation through header, dialogue text, and choices/continue.
    /// Queues rapid events to prevent missing dialogue.
    /// </summary>
    public class DialogueOverlay : IKeyHandler
    {
        // Item types in the flat list
        private enum ItemType { Header, Dialogue, Continue, Choice }

        private class ListItem
        {
            public ItemType Type;
            public string Text;
            public NarrationReflection.ChoiceInfo Choice;  // Only for Choice type
        }

        // Queued event types
        private enum EventType { Dialogue, Branch }
        private class QueuedEvent
        {
            public EventType Type;
            public object Data;
        }

        // State
        private bool _isOpen;
        private int _currentIndex;
        private List<ListItem> _items = new List<ListItem>();

        // Current popup reference (for reading displayed text)
        private object _currentPopup;

        // Current dialogue/branch for actions
        private object _currentDialogue;
        private object _currentBranch;

        // Event queue to handle rapid successive events
        private Queue<QueuedEvent> _eventQueue = new Queue<QueuedEvent>();
        private bool _processingEvent = false;

        // Event subscriptions
        private IDisposable _dialogueSub;
        private IDisposable _branchSub;

        // Type-ahead for choices
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // ========================================
        // IKeyHandler Implementation
        // ========================================

        public bool IsActive => _isOpen;

        public bool ProcessKey(KeyCode keyCode, KeyboardManager.KeyModifiers modifiers)
        {
            if (!_isOpen) return false;

            // Clear search on navigation keys
            _search.ClearOnNavigationKey(keyCode);

            switch (keyCode)
            {
                case KeyCode.UpArrow:
                    Navigate(-1);
                    return true;

                case KeyCode.DownArrow:
                    Navigate(1);
                    return true;

                case KeyCode.Home:
                    NavigateTo(0);
                    return true;

                case KeyCode.End:
                    NavigateTo(_items.Count - 1);
                    return true;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateCurrent();
                    return true;

                case KeyCode.Backspace:
                    if (_search.RemoveChar())
                    {
                        if (_search.HasBuffer)
                            Speech.Say($"Search: {_search.Buffer}");
                        else
                            Speech.Say("Search cleared");
                    }
                    return true;

                case KeyCode.Escape:
                    if (_search.HasBuffer)
                    {
                        _search.Clear();
                        Speech.Say("Search cleared");
                        return true;
                    }
                    // Pass to game to close popup
                    return false;

                default:
                    // Type-ahead search for choices (A-Z)
                    if (keyCode >= KeyCode.A && keyCode <= KeyCode.Z)
                    {
                        char c = (char)('a' + (keyCode - KeyCode.A));
                        _search.AddChar(c);

                        int match = FindMatchingChoice();
                        if (match >= 0)
                        {
                            _currentIndex = match;
                            AnnounceCurrentItem();
                        }
                        else
                        {
                            Speech.Say($"No match for {_search.Buffer}");
                        }
                        return true;
                    }

                    // Consume all other keys while overlay is active
                    return true;
            }
        }

        // ========================================
        // LIFECYCLE
        // ========================================

        /// <summary>
        /// Open the overlay when HomePopup is shown.
        /// </summary>
        public void Open(object popup)
        {
            if (_isOpen) return;

            _isOpen = true;
            _currentIndex = 0;
            _items.Clear();
            _currentPopup = popup;
            _currentDialogue = null;
            _currentBranch = null;
            _search.Clear();

            // Subscribe to dialogue and branch events
            _dialogueSub = NarrationReflection.SubscribeToDialogue(OnDialogueRequested);
            _branchSub = NarrationReflection.SubscribeToBranch(OnBranchRequested);

            if (_dialogueSub == null || _branchSub == null)
            {
                Debug.LogWarning("[ATSAccessibility] DialogueOverlay: Failed to subscribe to events");
            }

            // Announce the popup opening - first dialogue will be announced via event
            string npcName = NarrationReflection.GetNPCName();
            string npcTitle = NarrationReflection.GetNPCTitle();
            if (!string.IsNullOrEmpty(npcName))
            {
                Speech.Say($"Dialogue with {npcName}");
            }
        }

        /// <summary>
        /// Close the overlay.
        /// </summary>
        public void Close()
        {
            if (!_isOpen) return;

            _isOpen = false;
            _items.Clear();
            _currentPopup = null;
            _currentDialogue = null;
            _currentBranch = null;
            _eventQueue.Clear();
            _processingEvent = false;
            _search.Clear();

            // Dispose subscriptions
            _dialogueSub?.Dispose();
            _branchSub?.Dispose();
            _dialogueSub = null;
            _branchSub = null;
        }

        // ========================================
        // EVENT HANDLERS (with queueing)
        // ========================================

        private void OnDialogueRequested(object dialogue)
        {
            if (!_isOpen) return;  // Guard against stale events

            // Queue the event
            _eventQueue.Enqueue(new QueuedEvent { Type = EventType.Dialogue, Data = dialogue });

            // Process immediately if this is the first/only event
            if (!_processingEvent)
            {
                ProcessNextEvent();
            }
        }

        private void OnBranchRequested(object branch)
        {
            if (!_isOpen) return;  // Guard against stale events

            // Queue the event
            _eventQueue.Enqueue(new QueuedEvent { Type = EventType.Branch, Data = branch });

            // Process immediately if this is the first/only event
            if (!_processingEvent)
            {
                ProcessNextEvent();
            }
        }

        /// <summary>
        /// Process the next queued event.
        /// </summary>
        private void ProcessNextEvent()
        {
            if (_eventQueue.Count == 0)
            {
                _processingEvent = false;
                return;
            }

            _processingEvent = true;

            try
            {
                var evt = _eventQueue.Dequeue();

                _search.Clear();

                if (evt.Type == EventType.Dialogue)
                {
                    _currentDialogue = evt.Data;
                    _currentBranch = null;
                    BuildDialogueList(evt.Data);
                }
                else
                {
                    _currentBranch = evt.Data;
                    _currentDialogue = null;
                    BuildBranchList(evt.Data);
                }

                _currentIndex = 0;
                AnnounceCurrentItem();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ATSAccessibility] DialogueOverlay: ProcessNextEvent failed: {ex.Message}");
            }
        }

        // ========================================
        // LIST BUILDING
        // ========================================

        private void BuildDialogueList(object dialogue)
        {
            _items.Clear();

            // [0] Header: NPC name and title
            string npcName = NarrationReflection.GetNPCName() ?? "Unknown";
            string npcTitle = NarrationReflection.GetNPCTitle();
            string headerText = string.IsNullOrEmpty(npcTitle) ? npcName : $"{npcName}, {npcTitle}";

            _items.Add(new ListItem
            {
                Type = ItemType.Header,
                Text = headerText
            });

            // [1] Dialogue: the actual text
            string dialogueText = NarrationReflection.GetDialogueText(dialogue) ?? "...";
            _items.Add(new ListItem
            {
                Type = ItemType.Dialogue,
                Text = dialogueText
            });

            // [2] Continue: if has transition
            if (NarrationReflection.HasTransition(dialogue))
            {
                _items.Add(new ListItem
                {
                    Type = ItemType.Continue,
                    Text = "Continue"
                });
            }

        }

        private void BuildBranchList(object branch)
        {
            _items.Clear();

            // [0] Header: NPC name and title
            string npcName = NarrationReflection.GetNPCName() ?? "Unknown";
            string npcTitle = NarrationReflection.GetNPCTitle();
            string headerText = string.IsNullOrEmpty(npcTitle) ? npcName : $"{npcName}, {npcTitle}";

            _items.Add(new ListItem
            {
                Type = ItemType.Header,
                Text = headerText
            });

            // [1] Dialogue: read currently displayed text from UI
            // (The text may have been set by a DialogueModel before the branch)
            string displayedText = NarrationReflection.GetCurrentDisplayedText(_currentPopup);
            if (!string.IsNullOrEmpty(displayedText))
            {
                _items.Add(new ListItem
                {
                    Type = ItemType.Dialogue,
                    Text = displayedText
                });
            }

            // Get available choices
            var choices = NarrationReflection.GetChoices(branch);

            // [2+] Choices
            foreach (var choice in choices)
            {
                _items.Add(new ListItem
                {
                    Type = ItemType.Choice,
                    Text = choice.Text,
                    Choice = choice
                });
            }

        }

        // ========================================
        // NAVIGATION
        // ========================================

        private void Navigate(int direction)
        {
            if (_items.Count == 0) return;

            _currentIndex = NavigationUtils.WrapIndex(_currentIndex, direction, _items.Count);
            AnnounceCurrentItem();
        }

        private void NavigateTo(int index)
        {
            if (_items.Count == 0) return;
            _currentIndex = Mathf.Clamp(index, 0, _items.Count - 1);
            AnnounceCurrentItem();
        }

        private void AnnounceCurrentItem()
        {
            if (_currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];
            Speech.Say(item.Text);
        }

        private int FindMatchingChoice()
        {
            if (!_search.HasBuffer) return -1;

            string lowerBuffer = _search.Buffer.ToLowerInvariant();

            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                // Only match Choice items
                if (item.Type != ItemType.Choice) continue;

                if (!string.IsNullOrEmpty(item.Text) &&
                    item.Text.ToLowerInvariant().StartsWith(lowerBuffer))
                {
                    return i;
                }
            }

            return -1;
        }

        // ========================================
        // ACTIONS
        // ========================================

        private void ActivateCurrent()
        {
            if (_items.Count == 0 || _currentIndex < 0 || _currentIndex >= _items.Count) return;

            var item = _items[_currentIndex];

            switch (item.Type)
            {
                case ItemType.Continue:
                    if (_currentDialogue != null)
                    {
                        // Clear queue and allow new events to process immediately
                        _eventQueue.Clear();
                        _processingEvent = false;
                        if (!NarrationReflection.ExecuteTransition(_currentDialogue))
                        {
                            Speech.Say("Cannot continue");
                            SoundManager.PlayFailed();
                        }
                        // Game will fire OnDialogueRequested or OnBranchRequested next
                    }
                    break;

                case ItemType.Choice:
                    if (item.Choice != null)
                    {
                        // Clear queue and allow new events to process immediately
                        _eventQueue.Clear();
                        _processingEvent = false;
                        if (!NarrationReflection.SelectChoice(item.Choice))
                        {
                            Speech.Say("Cannot select");
                            SoundManager.PlayFailed();
                        }
                        // Game will fire OnDialogueRequested or OnBranchRequested next
                    }
                    break;

                case ItemType.Header:
                case ItemType.Dialogue:
                    // For informational items, check if there are queued events
                    if (_eventQueue.Count > 0)
                    {
                        // Process next queued event
                        ProcessNextEvent();
                    }
                    else
                    {
                        // Re-announce for informational items
                        AnnounceCurrentItem();
                    }
                    break;
            }
        }
    }
}
