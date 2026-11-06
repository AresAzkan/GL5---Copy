using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; } // Singleton instance



    [SerializeField]
    private GameObject _dialogueBox; // The dialogue box UI element
    [SerializeField]
    private TextMeshProUGUI _characterNameText; // The text component for displaying the character's name
    [SerializeField]
    private TextMeshProUGUI _dialogueText; // The text component for displaying dialogue

    private List<Message> _currentDialogues; // The list of dialogues for the current conversation

    private string _currentContact; // The contact that is currently being interacted with

    private int _currentDialogueIndex = 0; // The index of the current dialogue being displayed

    private bool _isEndNode = false;

    private Button _dialogueAdvanceButton;
    private CanvasGroup _dialogueCanvasGroup;
    private bool _standaloneMonologueActive;
    private Action _standaloneMonologueClosed;
    private Canvas _dialogueCanvas;
    private bool _previousOverrideSorting;
    private int _previousSortingOrder;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ConfigureDialogueClick();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        EventBus.Instance.Subscribe("Conversation_Started", StartDialogue);
    }

    private void OnDestroy()
    {
        if (_dialogueAdvanceButton != null)
        {
            _dialogueAdvanceButton.onClick.RemoveListener(ContinueDialogue);
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("Conversation_Started", StartDialogue);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (_dialogueBox.activeSelf && InputManager.Instance.ContinueDialogueWasPressed())
        {
            ContinueDialogue();
        }
    }

    public void StartDialogue()
    {
        Debug.Log("dialog activated");
        NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        if (currentNode == null)
        {
            RecoverFromInvalidDialogue("Cannot start dialogue because the current contact has no valid narrative node.");
            return;
        }

        _currentDialogueIndex = 0;
        _currentDialogues = currentNode.Dialogues();
        _currentContact = MessagesManager.Instance.GetCurrentContactName();
        _isEndNode = currentNode.IsEndNode();
        _dialogueBox.SetActive(true);
        InputManager.Instance.DisableMovementMap();

        // Choice nodes can also contain an introductory exchange. Keep the
        // choice panel hidden until every line has been shown, then EndDialogue
        // will reveal the options. This also prevents stale text from the
        // previous conversation remaining behind the choice buttons.
        MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(false);
        ContinueDialogue();
    }

    /// <summary>
    /// Shows a one-line inner monologue in the real dialogue box without
    /// changing the current narrative contact or forcing the phone to close.
    /// E or a click on the dialogue box dismisses it.
    /// </summary>
    public bool ShowInnerMonologue(string text, Action onClosed = null)
    {
        return ShowStandaloneDialogue("[Luna]", text, onClosed);
    }

    /// <summary>
    /// Shows a narration line in the real dialogue box while preserving the
    /// active phone conversation. E or a click dismisses it, then the caller
    /// can resume the remaining phone messages.
    /// </summary>
    public bool ShowNarration(string text, Action onClosed = null)
    {
        return ShowStandaloneDialogue("Narration", text, onClosed);
    }

    /// <summary>
    /// Shows a self-contained world dialogue without changing the active
    /// narrative contact. Intended for ambient NPCs whose lines must not
    /// advance or interrupt a story conversation.
    /// </summary>
    public bool ShowStandaloneLine(string speaker, string text, Action onClosed = null)
    {
        return ShowStandaloneDialogue(speaker, text, onClosed);
    }

    private bool ShowStandaloneDialogue(string speaker, string text, Action onClosed)
    {
        if (_dialogueBox == null || string.IsNullOrWhiteSpace(text) ||
            _dialogueBox.activeSelf || _standaloneMonologueActive)
        {
            return false;
        }

        _standaloneMonologueActive = true;
        _standaloneMonologueClosed = onClosed;
        _currentDialogueIndex = 0;
        _currentDialogues = null;
        _isEndNode = false;

        _characterNameText.text = speaker;
        _dialogueText.text = text;
        SetChoiceMode(false);

        _dialogueCanvas = _dialogueBox.GetComponentInParent<Canvas>();
        if (_dialogueCanvas != null)
        {
            _previousOverrideSorting = _dialogueCanvas.overrideSorting;
            _previousSortingOrder = _dialogueCanvas.sortingOrder;
            _dialogueCanvas.overrideSorting = true;
            _dialogueCanvas.sortingOrder = 100;
        }

        _dialogueBox.SetActive(true);
        InputManager.Instance.DisableMovementMap();
        return true;
    }


    public void ContinueDialogue()
    {
        if (_dialogueBox == null || !_dialogueBox.activeSelf)
        {
            return;
        }

        if (_standaloneMonologueActive)
        {
            EndStandaloneMonologue();
            return;
        }

        NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        if (currentNode == null)
        {
            RecoverFromInvalidDialogue("Cannot continue dialogue because the current contact has no valid narrative node.");
            return;
        }

        if (_currentDialogues == null)
        {
            Debug.LogWarning("[DialogueManager] Continue requested without an active dialogue list.");
            return;
        }

        if (_currentDialogueIndex >= _currentDialogues.Count)
        {
            EndDialogue();
            return;
        }
        Message currentMessage = _currentDialogues[_currentDialogueIndex];
        
        string speaker = "";
        if (currentMessage.Sender() == Sender.Player) 
        {
            speaker = GameManager.Instance.GetPlayerName();
        } 
        else 
        {
            if (!string.IsNullOrEmpty(currentMessage.SpeakerName())) 
            {
                speaker = currentMessage.SpeakerName();
            } 
            else 
            {
                speaker = _currentContact;
            }
        }
        
        _characterNameText.text = speaker;
        _dialogueText.text = currentMessage.Content();
        _currentDialogueIndex++;
    }

    private void EndStandaloneMonologue()
    {
        _standaloneMonologueActive = false;
        _dialogueBox.SetActive(false);
        _currentDialogueIndex = 0;
        _currentDialogues = null;

        if (_dialogueCanvas != null)
        {
            _dialogueCanvas.overrideSorting = _previousOverrideSorting;
            _dialogueCanvas.sortingOrder = _previousSortingOrder;
            _dialogueCanvas = null;
        }

        Action closed = _standaloneMonologueClosed;
        _standaloneMonologueClosed = null;
        closed?.Invoke();
    }

    private void EndDialogue()
    {
        NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        if (currentNode == null)
        {
            RecoverFromInvalidDialogue("Cannot end dialogue because the current contact has no valid narrative node.");
            return;
        }

        if (currentNode.HasOptions())
        {
            MessagesManager.Instance.GetOptionsChanger().SetCurrentNode(currentNode);
            MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(true);
            return;
        }

        bool reachedEndNode = _isEndNode;
        if (reachedEndNode)
        {
            _dialogueBox.SetActive(false);
            UIManager.Instance.ChangeState(0);
            ReturnPlayerToIdle();
        }
        _currentDialogueIndex = 0;
        _currentDialogues = null;
        
        if (!string.IsNullOrEmpty(currentNode.EventOnEnd()))
        {
            EventBus.Instance.Publish(currentNode.EventOnEnd());
        }

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        if (currentContact == null)
        {
            RecoverFromInvalidDialogue("Cannot advance dialogue because no current contact is selected.");
            return;
        }

        int nextNodeId = currentNode.NextNode();
        if (nextNodeId < 0)
        {
            // A terminal end node intentionally has no successor. Its dialogue
            // and completion event are already finished, so this is a normal
            // narrative ending rather than a broken node reference.
            if (reachedEndNode)
            {
                _isEndNode = false;
                return;
            }

            RecoverFromInvalidDialogue($"Narrative node {currentNode.ID()} ended without a valid next node.");
            return;
        }

        currentContact.SetNarrativeNode(nextNodeId);
        currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        if (currentNode != null && currentNode.HasOptions())
        {
            MessagesManager.Instance.GetOptionsChanger().SetCurrentNode(currentNode);
            MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(true);
        }
    }

    private void ConfigureDialogueClick()
    {
        if (_dialogueBox == null)
        {
            Debug.LogWarning("[DialogueManager] Dialogue box reference is missing.");
            return;
        }

        _dialogueAdvanceButton = _dialogueBox.GetComponent<Button>();
        if (_dialogueAdvanceButton == null)
        {
            _dialogueAdvanceButton = _dialogueBox.AddComponent<Button>();
        }

        _dialogueAdvanceButton.targetGraphic = _dialogueBox.GetComponent<Graphic>();
        _dialogueAdvanceButton.transition = Selectable.Transition.None;
        Navigation navigation = _dialogueAdvanceButton.navigation;
        navigation.mode = Navigation.Mode.None;
        _dialogueAdvanceButton.navigation = navigation;
        _dialogueAdvanceButton.onClick.RemoveListener(ContinueDialogue);
        _dialogueAdvanceButton.onClick.AddListener(ContinueDialogue);

        _dialogueCanvasGroup = _dialogueBox.GetComponent<CanvasGroup>();
        if (_dialogueCanvasGroup == null)
        {
            _dialogueCanvasGroup = _dialogueBox.AddComponent<CanvasGroup>();
        }

        SetChoiceMode(false);
    }

    public void SetChoiceMode(bool isChoosing)
    {
        if (_dialogueAdvanceButton != null)
        {
            _dialogueAdvanceButton.interactable = !isChoosing;
        }

        if (_dialogueCanvasGroup != null)
        {
            // Choices are a separate panel layered over the dialogue box. While
            // they are shown, the dialogue background must not consume pointer
            // events intended for those buttons.
            _dialogueCanvasGroup.blocksRaycasts = !isChoosing;
        }
    }

    private void RecoverFromInvalidDialogue(string reason)
    {
        Debug.LogWarning($"[DialogueManager] {reason}");
        _currentDialogueIndex = 0;
        _currentDialogues = null;
        _isEndNode = false;
        _standaloneMonologueActive = false;
        _standaloneMonologueClosed = null;
        SetChoiceMode(false);

        if (_dialogueBox != null)
        {
            _dialogueBox.SetActive(false);
        }

        MessagesManager.Instance?.GetOptionsChanger()?.ActiveDecisionsPanel(false);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ChangeState(0);
        }

        ReturnPlayerToIdle();
    }

    private void ReturnPlayerToIdle()
    {
        PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
        if (playerState != null)
        {
            playerState.ChangeState(PlayerState.Idle);
        }
    }

}
