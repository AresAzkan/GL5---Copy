using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.UI;

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
        _currentDialogues = null;
        _dialogueBox.SetActive(true);
        InputManager.Instance.DisableMovementMap();
        
        if (currentNode.HasOptions())
        {
            MessagesManager.Instance.GetOptionsChanger().SetCurrentNode(currentNode);
            MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(true);
        }
        else
        {
            MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(false);
            _currentDialogues = currentNode.Dialogues();
            _currentContact = MessagesManager.Instance.GetCurrentContactName();
            _isEndNode = currentNode.IsEndNode();
            ContinueDialogue();
        }
    }


    public void ContinueDialogue()
    {
        if (_dialogueBox == null || !_dialogueBox.activeSelf)
        {
            return;
        }

        NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        if (currentNode == null)
        {
            RecoverFromInvalidDialogue("Cannot continue dialogue because the current contact has no valid narrative node.");
            return;
        }

        // A choice node is advanced only by choosing one of its buttons. The node's
        // regular nextNode is intentionally -1, so treating Continue as EndDialogue
        // here would corrupt the contact's narrative state.
        if (currentNode.HasOptions())
        {
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
