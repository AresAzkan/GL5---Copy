using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionsChanger : MonoBehaviour
{
    [SerializeField]
    private GameObject _decisionsPanel;

    [SerializeField]
    private NarrativeNode _currentNode;

    [SerializeField]
    private Button _option1Button;
    [SerializeField]
    private Button _option2Button;

    private readonly List<Button> _optionButtons = new List<Button>();

    private void Awake()
    {
        RegisterButton(_option1Button);
        RegisterButton(_option2Button);
    }


    /// <summary>
    /// Update the buttons texts with the current node info
    /// </summary>
    private void UpdateButtonsTexts()
    {
        if (_currentNode == null) return;

        Option[] options = _currentNode.Options();
        int optionCount = options == null ? 0 : options.Length;
        EnsureButtonCount(optionCount);

        for (int i = 0; i < _optionButtons.Count; i++)
        {
            bool active = i < optionCount;
            Button button = _optionButtons[i];
            button.gameObject.SetActive(active);

            if (!active) continue;

            OptionButtonHelper helper = button.GetComponent<OptionButtonHelper>();
            if (helper == null) helper = button.gameObject.AddComponent<OptionButtonHelper>();
            helper.Configure(i);

            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.text = options[i].text;
        }

        LayoutButtons(optionCount);
    }

    public void ChangeOptions(int selectedOptionId)
    {
        if (_currentNode == null || _currentNode.Options() == null ||
            selectedOptionId < 0 || selectedOptionId >= _currentNode.Options().Length)
        {
            Debug.LogWarning($"[OptionsChanger] Invalid option index {selectedOptionId}.");
            return;
        }

        Option selectedOption = _currentNode.Options()[selectedOptionId];
        
        if (!string.IsNullOrEmpty(selectedOption.eventToPublish))
        {
            PublishOptionEvent(selectedOption);
        }

        DialogueType lastType = _currentNode.DialogueType();
        Contact contact = MessagesManager.Instance.GetCurrentContact();
        if (contact == null)
        {
            Debug.LogWarning("[OptionsChanger] No contact is selected.");
            return;
        }
        contact.SetNarrativeNode(selectedOption.nextNode);
        contact.SetCurrentMessageIndex(0);

        // Remove the choice overlay before the selected branch starts. This also
        // restores click-to-advance on the regular dialogue background.
        ActiveDecisionsPanel(false);

        if(lastType == DialogueType.PHONE)
        {
            foreach (var message in selectedOption.messagesToSend)
            {
                MessagesManager.Instance.SendMessageToCurrentContact(message);
            }
        }
        else
        {
            DialogueManager.Instance.StartDialogue();
        }

        _currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();

        if (_currentNode != null && _currentNode.DialogueType() == DialogueType.PHONE)
        {
            if (_currentNode.Dialogues().Count > 0)
            {
                contact.SetHasNewMessages(true);
                MessagesManager.Instance.GetPhoneStateMachine().ShowDialogueTab();
            }
        }

        if (_currentNode == null || !_currentNode.HasOptions())
        {
            ActiveDecisionsPanel(false);
            return;
        }

        if(_currentNode != null && _currentNode.HasOptions())
        {
            UpdateButtonsTexts();
        }
    }

    public void SetCurrentNode(NarrativeNode node)
    {
        _currentNode = node;
        if(_currentNode != null && _currentNode.HasOptions())
        {
            UpdateButtonsTexts();
        }
    }

    public void ActiveDecisionsPanel(bool isActive)
    {
        if (_decisionsPanel != null)
        {
            _decisionsPanel.SetActive(isActive);
            if (isActive)
            {
                _decisionsPanel.transform.SetAsLastSibling();
            }
        }

        DialogueManager.Instance?.SetChoiceMode(isActive);
    }

    private void RegisterButton(Button button)
    {
        if (button != null && !_optionButtons.Contains(button))
        {
            _optionButtons.Add(button);
        }
    }

    private void EnsureButtonCount(int requiredCount)
    {
        if (_optionButtons.Count == 0)
        {
            RegisterButton(_option1Button);
            RegisterButton(_option2Button);
        }

        if (_optionButtons.Count == 0 || requiredCount <= _optionButtons.Count) return;

        Button template = _optionButtons[_optionButtons.Count - 1];
        Transform parent = template.transform.parent;

        while (_optionButtons.Count < requiredCount)
        {
            Button clone = Instantiate(template, parent);
            clone.name = $"Option{_optionButtons.Count + 1}";
            OptionButtonHelper helper = clone.GetComponent<OptionButtonHelper>();
            if (helper == null) helper = clone.gameObject.AddComponent<OptionButtonHelper>();
            helper.Configure(_optionButtons.Count);
            _optionButtons.Add(clone);
        }
    }

    private void LayoutButtons(int count)
    {
        if (count <= 0) return;

        const float horizontalSpacing = 420f;
        float firstX = -horizontalSpacing * (count - 1) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            if (_optionButtons[i].transform is RectTransform rect)
            {
                rect.anchoredPosition = new Vector2(firstX + horizontalSpacing * i, 0f);
            }
        }
    }

    private void PublishOptionEvent(Option option)
    {
        if (EventBus.Instance == null) return;

        object value = option.EventDataValue();
        switch (option.eventDataType)
        {
            case EventDataType.STRING:
                EventBus.Instance.Publish(option.eventToPublish, (string)value);
                break;
            case EventDataType.INT:
                EventBus.Instance.Publish(option.eventToPublish, (int)value);
                break;
            case EventDataType.FLOAT:
                EventBus.Instance.Publish(option.eventToPublish, (float)value);
                break;
            case EventDataType.BOOL:
                EventBus.Instance.Publish(option.eventToPublish, (bool)value);
                break;
            default:
                EventBus.Instance.Publish(option.eventToPublish);
                break;
        }
    }

}
