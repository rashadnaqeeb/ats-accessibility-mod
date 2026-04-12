using ATSAccessibility.Utils;
using ATSAccessibility.Reflection;
using ATSAccessibility.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ATSAccessibility.Overlays {
	/// <summary>
	/// Accessible overlay for the HomePopup (NPC dialogue navigation).
	/// Provides flat list navigation through header, dialogue text, and choices/continue.
	/// Queues rapid events to prevent missing dialogue.
	/// </summary>
	public class DialogueOverlay: MenuBase {
		private enum ItemType { Header, Dialogue, Continue, Choice }

		private class ListItem {
			public ItemType Type;
			public string Text;
			public NarrationReflection.ChoiceInfo Choice;
		}

		private enum EventType { Dialogue, Branch }
		private class QueuedEvent {
			public EventType Type;
			public object Data;
		}

		// State
		private List<ListItem> _items = new List<ListItem>();
		private object _currentPopup;
		private object _currentDialogue;
		private object _currentBranch;

		// Event queue
		private Queue<QueuedEvent> _eventQueue = new Queue<QueuedEvent>();
		private bool _processingEvent = false;

		// Event subscriptions
		private IDisposable _dialogueSub;
		private IDisposable _branchSub;

		// ========================================
		// MENUBASE OVERRIDES
		// ========================================

		protected override string OverlayName => Strings.Get("overlay.dialogue.title");
		protected override string EmptyMessage => "";

		protected override int GetItemCount() => _items.Count;

		protected override string GetLabel(int index) {
			if (index < 0 || index >= _items.Count) return null;
			return _items[index].Text;
		}

		protected override void RefreshData() { }

		protected override EnterAction OnEnter(int index) => EnterAction.Action;

		protected override void OnAction(int index) {
			if (_items.Count == 0 || index < 0 || index >= _items.Count) return;

			var item = _items[index];

			switch (item.Type) {
				case ItemType.Continue:
					if (_currentDialogue != null) {
						_eventQueue.Clear();
						_processingEvent = false;
						if (!NarrationReflection.ExecuteTransition(_currentDialogue)) {
							Speech.Say(Strings.Get("overlay.dialogue.cannot_continue"));
							SoundManager.PlayFailed();
						}
					}
					break;

				case ItemType.Choice:
					if (item.Choice != null) {
						_eventQueue.Clear();
						_processingEvent = false;
						if (!NarrationReflection.SelectChoice(item.Choice)) {
							Speech.Say(Strings.Get("common.cannot_select"));
							SoundManager.PlayFailed();
						}
					}
					break;

				case ItemType.Header:
				case ItemType.Dialogue:
					if (_eventQueue.Count > 0)
						ProcessNextEvent();
					else
						AnnounceCurrentItem();
					break;
			}
		}

		protected override void StorePopup(object popup) {
			_currentPopup = popup;
		}

		protected override string GetOpenAnnouncement() {
			string npcName = NarrationReflection.GetNPCName();
			if (!string.IsNullOrEmpty(npcName))
				return Strings.Get("overlay.dialogue.open", npcName);
			return null;
		}

		protected override void OnOpened() {
			_currentDialogue = null;
			_currentBranch = null;

			_dialogueSub = NarrationReflection.SubscribeToDialogue(OnDialogueRequested);
			_branchSub = NarrationReflection.SubscribeToBranch(OnBranchRequested);

			if (_dialogueSub == null || _branchSub == null) {
				Debug.LogWarning("[ATSAccessibility] DialogueOverlay: Failed to subscribe to events");
			}
		}

		protected override void OnClosed() {
			_dialogueSub?.Dispose();
			_branchSub?.Dispose();
			_dialogueSub = null;
			_branchSub = null;

			_items.Clear();
			_currentPopup = null;
			_currentDialogue = null;
			_currentBranch = null;
			_eventQueue.Clear();
			_processingEvent = false;
		}

		protected override EscapeAction OnEscape() => EscapeAction.PassThrough;

		protected override void AnnounceCurrentItem() {
			if (CurrentIndex < 0 || CurrentIndex >= _items.Count) return;
			Speech.Say(_items[CurrentIndex].Text);
		}

		// ========================================
		// SEARCH
		// ========================================

		protected override string GetSearchName(int index) {
			if (index < 0 || index >= _items.Count) return null;
			return _items[index].Type == ItemType.Choice ? _items[index].Text : null;
		}

		// ========================================
		// EVENT HANDLERS
		// ========================================

		private void OnDialogueRequested(object dialogue) {
			if (!IsOpen) return;

			_eventQueue.Enqueue(new QueuedEvent { Type = EventType.Dialogue, Data = dialogue });

			if (!_processingEvent)
				ProcessNextEvent();
		}

		private void OnBranchRequested(object branch) {
			if (!IsOpen) return;

			_eventQueue.Enqueue(new QueuedEvent { Type = EventType.Branch, Data = branch });

			if (!_processingEvent)
				ProcessNextEvent();
		}

		private void ProcessNextEvent() {
			if (_eventQueue.Count == 0) {
				_processingEvent = false;
				return;
			}

			_processingEvent = true;

			try {
				var evt = _eventQueue.Dequeue();

				_search.Clear();

				if (evt.Type == EventType.Dialogue) {
					_currentDialogue = evt.Data;
					_currentBranch = null;
					BuildDialogueList(evt.Data);
				} else {
					_currentBranch = evt.Data;
					_currentDialogue = null;
					BuildBranchList(evt.Data);
				}

				CurrentIndex = 0;
				AnnounceCurrentItem();
			} catch (Exception ex) {
				Debug.LogError($"[ATSAccessibility] DialogueOverlay: ProcessNextEvent failed: {ex.Message}");
			}
		}

		// ========================================
		// LIST BUILDING
		// ========================================

		private void BuildDialogueList(object dialogue) {
			_items.Clear();

			string npcName = NarrationReflection.GetNPCName() ?? Strings.Get("common.unknown");
			string npcTitle = NarrationReflection.GetNPCTitle();
			string headerText = string.IsNullOrEmpty(npcTitle) ? npcName : Strings.Get("overlay.dialogue.header_with_title", npcName, npcTitle);

			_items.Add(new ListItem {
				Type = ItemType.Header,
				Text = headerText
			});

			string dialogueText = NarrationReflection.GetDialogueText(dialogue) ?? Strings.Get("overlay.dialogue.ellipsis");
			_items.Add(new ListItem {
				Type = ItemType.Dialogue,
				Text = dialogueText
			});

			if (NarrationReflection.HasTransition(dialogue)) {
				_items.Add(new ListItem {
					Type = ItemType.Continue,
					Text = Strings.Get("overlay.dialogue.continue")
				});
			}
		}

		private void BuildBranchList(object branch) {
			_items.Clear();

			string npcName = NarrationReflection.GetNPCName() ?? Strings.Get("common.unknown");
			string npcTitle = NarrationReflection.GetNPCTitle();
			string headerText = string.IsNullOrEmpty(npcTitle) ? npcName : Strings.Get("overlay.dialogue.header_with_title", npcName, npcTitle);

			_items.Add(new ListItem {
				Type = ItemType.Header,
				Text = headerText
			});

			string displayedText = NarrationReflection.GetCurrentDisplayedText(_currentPopup);
			if (!string.IsNullOrEmpty(displayedText)) {
				_items.Add(new ListItem {
					Type = ItemType.Dialogue,
					Text = displayedText
				});
			}

			var choices = NarrationReflection.GetChoices(branch);
			foreach (var choice in choices) {
				_items.Add(new ListItem {
					Type = ItemType.Choice,
					Text = choice.Text,
					Choice = choice
				});
			}
		}
	}
}
