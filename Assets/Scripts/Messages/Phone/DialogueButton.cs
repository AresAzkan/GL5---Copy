using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class DialogueButton : MonoBehaviour
{
    private Button _button;
    private float _timer = 0f;
    private Image _image;
    private TextMeshProUGUI _textMesh;
    private bool _narrationDialogueActive;

    void Awake()
    {
        _button = GetComponent<Button>();
        _image = GetComponent<Image>();
        _textMesh = GetComponentInChildren<TextMeshProUGUI>();
        
        _button.onClick.AddListener(OnDialogueButtonPressed);
    }

    void Update()
    {
        UpdateButtonVisibility();
    }

    private void SetVisualsActive(bool active)
    {
        if (_image != null) _image.enabled = active;
        if (_textMesh != null) _textMesh.enabled = active;
        if (_button != null) _button.interactable = active;
    }

    private void UpdateButtonVisibility()
    {
        if (MessagesManager.Instance == null)
            return;

        if (_narrationDialogueActive)
        {
            SetVisualsActive(false);
            _timer = 0f;
            return;
        }

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        if (currentContact == null)
        {
            SetVisualsActive(false);
            _button.gameObject.SetActive(false);
            return;
        }

        if (currentContact.HasNewMessages())
        {
            NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
            if (currentNode != null)
            {
                // If a message is currently animating in the UI, wait for it to finish
                if (MessagesManager.Instance.GetMessagesUI() != null && MessagesManager.Instance.GetMessagesUI().IsAnimating)
                {
                    SetVisualsActive(false);
                    _timer = 0f;
                    return;
                }

                if (currentContact.CurrentMessageIndex() < currentNode.Dialogues().Count)
                {
                    // We keep the GameObject active so Update() continues running
                    _button.gameObject.SetActive(true);
                    
                    Message nextMsg = currentNode.Dialogues()[currentContact.CurrentMessageIndex()];
                    if (IsNarration(nextMsg))
                    {
                        SetVisualsActive(false);
                        _timer = 0f;
                        ShowNarration(currentContact, nextMsg);
                        return;
                    }

                    if (nextMsg.Sender() == Sender.NPC)
                    {
                        // It's the NPC's turn. Hide the button visually and wait 1 second.
                        SetVisualsActive(false);
                        
                        _timer += Time.deltaTime;
                        if (_timer >= 1f)
                        {
                            _timer = 0f;
                            SendNextMessage();
                        }
                    }
                    else
                    {
                        // It's the Player's turn. Show the button, wait for click.
                        SetVisualsActive(true);
                        _timer = 0f;
                    }
                }
                else
                {
                    // All dialogues in this node have been sent and completed animating
                    CompleteCurrentNode(currentContact, currentNode);
                }
            }
            else
            {
                SetVisualsActive(false);
                _button.gameObject.SetActive(false);
                currentContact.SetHasNewMessages(false);
            }
        }
        else
        {
            SetVisualsActive(false);
            _button.gameObject.SetActive(false);
        }
    }

    private void ShowNarration(Contact currentContact, Message narration)
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("[DialogueButton] Cannot show narration because DialogueManager is unavailable.");
            return;
        }

        _narrationDialogueActive = true;
        bool opened = DialogueManager.Instance.ShowNarration(
            narration.Content(),
            () =>
            {
                _narrationDialogueActive = false;
                _timer = 0f;
            });

        if (!opened)
        {
            _narrationDialogueActive = false;
            return;
        }

        // Narration is consumed by the dialogue window, not the phone history.
        // Advancing here lets the normal Update flow continue with any line
        // that follows after the player dismisses the narration.
        currentContact.SetCurrentMessageIndex(currentContact.CurrentMessageIndex() + 1);
    }

    private static bool IsNarration(Message message)
    {
        return message != null &&
               string.Equals(message.SpeakerName(), "Narration", StringComparison.OrdinalIgnoreCase);
    }

    private void OnDialogueButtonPressed()
    {
        if (_button.interactable)
        {
            SendNextMessage();
        }
    }

    private void SendNextMessage()
    {
        if (MessagesManager.Instance == null)
            return;

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        if (currentContact != null && currentContact.HasNewMessages())
        {
            NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
            if (currentNode != null)
            {
                int msgIndex = currentContact.CurrentMessageIndex();
                if (msgIndex < currentNode.Dialogues().Count)
                {
                    Message msg = currentNode.Dialogues()[msgIndex];
                    MessagesManager.Instance.SendMessageToCurrentContact(msg);
                    currentContact.SetCurrentMessageIndex(msgIndex + 1);
                }
            }
        }
    }

    private void CompleteCurrentNode(Contact currentContact, NarrativeNode currentNode)
    {
        currentContact.SetHasNewMessages(false);
        _button.gameObject.SetActive(false);
        SetVisualsActive(false);
        
        if (!currentNode.HasOptions())
        {
            if (!string.IsNullOrEmpty(currentNode.EventOnEnd()))
            {
                EventBus.Instance.Publish(currentNode.EventOnEnd());
            }
            
            int nextNodeId = currentNode.NextNode();
            currentContact.SetNarrativeNode(nextNodeId);
            currentContact.SetCurrentMessageIndex(0);

            if (nextNodeId < 0)
            {
                return;
            }
            
            NarrativeNode next = MessagesManager.Instance.GetCurrentContactNarrativeNode();
            if (next != null)
            {
                if (next.Dialogues().Count > 0)
                {
                    currentContact.SetHasNewMessages(true);
                    gameObject.SetActive(true);
                    MessagesManager.Instance.GetPhoneStateMachine().ShowDialogueTab();
                }
                else if (next.HasOptions())
                {
                    MessagesManager.Instance.GetOptionsChanger().SetCurrentNode(next);
                    MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(true);
                    MessagesManager.Instance.GetPhoneStateMachine().ShowDecisionsTab();
                }
            }
        }
        else
        {
            // Show options panel
            MessagesManager.Instance.GetOptionsChanger().SetCurrentNode(currentNode);
            MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(true);
            MessagesManager.Instance.GetPhoneStateMachine().ShowDecisionsTab();
        }
    }
}
