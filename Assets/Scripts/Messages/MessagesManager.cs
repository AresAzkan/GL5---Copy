using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Name = System.String;

public enum MessagingChannel
{
    Phone,
    Connect
}

/// <summary>
/// Holds the list of contacts and their messages, and manages the current contact being interacted with.
/// </summary>
public class MessagesManager : MonoBehaviour
{
    public static MessagesManager Instance { get; private set; }

    private string _currentContact; // The contact that is currently being interacted with

    private MessagesUI _messagesUI; // Reference to the MessagesUI component for updating the UI

    private PhoneStateMachine _phoneStateMachine; // Reference to the PhoneStateMachine component

    private OptionsChanger _optionsChanger; // Reference to the OptionsChanger component for updating the options UI

    [SerializeField]
    private Dictionary<Name, List<Message>> _contactsMessages = new(); // Dictionary of List of messages for each contact, keyed by contact name

    [SerializeField]
    private Dictionary<Name, List<Message>> _connectMessages = new();

    private MessagingChannel _currentMessagingChannel = MessagingChannel.Phone;

    [SerializeField]
    private List<Contact> _contacts = new(); // List of all contacts in the game

    [SerializeField]
    private Dictionary<int, NarrativeNode> _narrativeNodes = new(); // All the narrative nodes in the game, keyed by node ID

    [SerializeField]
    private string contactsFilePath = "contacts.json";

    [SerializeField]
    private string narrativeNodesFilePath = "narrativeNodes.json";
    
    [SerializeField]
    private string phoneMessagesFilePath = "messages.json";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        CreateNarrativeNodes();
        CreateContacts();
        CreateMessages();
    }

    private void CreateNarrativeNodes()
    {
        List<NarrativeNode> nodesList = DataLoader.LoadNarrativeNodes(narrativeNodesFilePath);

        if (nodesList != null)
        {
            foreach (NarrativeNode node in nodesList)
            {
                _narrativeNodes[node.ID()] = node;
            }
        }
        else
        {
            _narrativeNodes = new Dictionary<int, NarrativeNode>();
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Gets the MessagesUI component attached to the same GameObject as this script
        _messagesUI = GetComponent<MessagesUI>();

        // Gets the PhoneStateMachine component attached to the same GameObject as this script
        _phoneStateMachine = FindAnyObjectByType<PhoneStateMachine>(FindObjectsInactive.Include);

        // Gets the OptionsChanger component attached to the same GameObject as this script
        _optionsChanger = GetComponent<OptionsChanger>();

        EventBus.Instance.Subscribe("Campaign_Completed", OnNarrativeCompleted);
    }

    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("Campaign_Completed", OnNarrativeCompleted);
        }
    }

    private void OnNarrativeCompleted()
    {
        PhoneNotificationController.Instance?.ShowNotification(
            PhoneApp.Messages,
            "Narrative 2.0 complete",
            null,
            5f);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowTemporaryMessage(
                "You reached the end of Luna's five-day story.",
                new Color(1f, 0.92f, 0.35f),
                8f);
        }
    }

    /// <summary>
    /// Returns the list of all contacts in the game.
    /// </summary>
    /// <returns></returns>
    public List<Contact> GetContacts()
    {
        // Sort the contacts list so it is always in order of the last message date
        SortContacts();

        // Both apps share the same visual contact list, but their contacts and
        // histories are deliberately kept in separate channels.
        List<Contact> contactsList = _contacts
            .Where(contact => GetChannelForContact(contact.Name()) == _currentMessagingChannel)
            .ToList();
        return contactsList;
    }

    public void SetMessagingChannel(MessagingChannel channel)
    {
        _currentMessagingChannel = channel;
        _currentContact = null;
        _messagesUI?.RefreshContacts();
    }

    public MessagingChannel GetMessagingChannel()
    {
        return _currentMessagingChannel;
    }

    /// <summary>
    /// Sets the current contact that the user is interacting with.
    /// </summary>
    /// <param name="contactName"></param>
    public void SetCurrentContact(string contactName)
    {
        _currentContact = contactName;
    }

    /// <summary>
    /// Returns the current contact that the user is interacting with.
    /// </summary>
    /// <returns></returns>
    public Contact GetCurrentContact()
    {
        return GetContactByName(_currentContact);
    }

    public Contact GetContactByName(string contactName)
    {
        if (string.IsNullOrEmpty(contactName)) return null;

        var found = _contacts.Find(contact => string.Equals(contact.Name(), contactName, StringComparison.OrdinalIgnoreCase));
        if (found == null)
        {
            string normalized = contactName.Replace(" ", "").ToLower().Replace("unkown", "unknown");
            found = _contacts.Find(contact => contact.Name().Replace(" ", "").ToLower().Replace("unkown", "unknown") == normalized);
        }
        return found;
    }

    public string GetCurrentContactName()
    {
        return _currentContact;
    }

    /// <summary>
    /// Returns the current contact that the user is interacting with.
    /// </summary>
    /// <returns></returns>
    public List<Message> GetCurrentContactMessages()
    {
        if (string.IsNullOrEmpty(_currentContact)) return new List<Message>();

        Contact currentContact = GetContactByName(_currentContact);
        if (currentContact != null)
        {
            Dictionary<Name, List<Message>> messageStore = GetMessageStore(currentContact.Name());
            if (!messageStore.ContainsKey(currentContact.Name()))
            {
                messageStore[currentContact.Name()] = new List<Message>();
            }
            return messageStore[currentContact.Name()];
        }
        return new List<Message>();
    }

    /// <summary>
    /// Adds a message directly to a contact's history, updating timestamp and sorting contacts.
    /// Also updates the active chat UI if this contact is currently open.
    /// </summary>
    public void AddMessageToContact(string contactName, Message message)
    {
        if (string.IsNullOrEmpty(contactName) || message == null) return;

        Contact contact = GetContactByName(contactName);
        string keyName = contact != null ? contact.Name() : contactName;

        Dictionary<Name, List<Message>> messageStore = GetMessageStore(keyName);
        if (!messageStore.ContainsKey(keyName))
        {
            messageStore[keyName] = new List<Message>();
        }

        messageStore[keyName].Add(message);

        if (contact != null)
        {
            contact.SetLastMessageDate(message.Timestamp());
        }

        SortContacts();

        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindFirstObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }

        // If this contact is currently open on screen in MessagesUI, show it live
        if (_currentContact != null &&
            string.Equals(_currentContact, keyName, StringComparison.OrdinalIgnoreCase) &&
            GetChannelForContact(keyName) == _currentMessagingChannel &&
            _messagesUI != null)
        {
            _messagesUI.AddMessage(message);
        }
    }

    /// <summary>
    /// Advances a contact's narrative node as a result of a game event.
    /// If the new node belongs to a mobile conversation and starts with an NPC message,
    /// directly adds messages up to the first player message and fires a phone notification.
    /// </summary>
    public void AdvanceContactNarrativeNodeByEvent(string contactName, int newNarrativeNodeId)
    {
        Contact contact = GetContactByName(contactName);
        if (contact == null)
        {
            Debug.Log($"[MessagesManager] Contact '{contactName}' not found in contacts list. Creating new Contact.");
            contact = new Contact();
            contact.SetName(contactName);
            contact.SetImagePath("Assets/Sprites/UnknownProfile.png");
            _contacts.Add(contact);
            GetMessageStore(contact.Name())[contact.Name()] = new List<Message>();
        }

        contact.SetNarrativeNode(newNarrativeNodeId);
        contact.SetCurrentMessageIndex(0);

        NarrativeNode newNode = GetNarrativeNode(newNarrativeNodeId);
        if (newNode == null)
        {
            Debug.LogWarning($"[MessagesManager] NarrativeNode '{newNarrativeNodeId}' not found for contact '{contactName}'.");
            contact.SetHasNewMessages(false);
            return;
        }

        // Check if the new narrative node belongs to a mobile phone conversation
        if (newNode.DialogueType() == DialogueType.PHONE)
        {
            List<Message> dialogues = newNode.Dialogues();
            if (dialogues != null && dialogues.Count > 0 && dialogues[0].Sender() == Sender.NPC)
            {
                int addedCount = 0;
                Message firstNpcMessage = null;
                int nextIndex = 0;

                for (int i = 0; i < dialogues.Count; i++)
                {
                    Message msg = dialogues[i];
                    if (msg.Sender() == Sender.NPC && !IsNarration(msg))
                    {
                        if (firstNpcMessage == null)
                        {
                            firstNpcMessage = msg;
                        }
                        AddMessageToContact(contact.Name(), msg);
                        addedCount++;
                        nextIndex = i + 1;
                    }
                    else
                    {
                        // Player messages and narration are interactive boundaries.
                        // Narration must be shown in the dialogue window when the
                        // player opens the conversation, never stored as a bubble.
                        nextIndex = i;
                        break;
                    }
                }

                contact.SetCurrentMessageIndex(nextIndex);

                // Keep the conversation pending even when every NPC line was
                // preloaded into its history. Opening the contact must still let
                // DialogueButton complete the node and publish EventOnEnd (for
                // example D2_NoahTeaser_End, which unlocks the bed for Day 3).
                bool hasMore = addedCount > 0 ||
                               (nextIndex < dialogues.Count) ||
                               newNode.HasOptions();
                contact.SetHasNewMessages(hasMore);

                SortContacts();

                // Trigger Phone Notification
                if (addedCount == 1 && firstNpcMessage != null)
                {
                    string rawSenderName = !string.IsNullOrEmpty(firstNpcMessage.SpeakerName()) 
                        ? firstNpcMessage.SpeakerName() 
                        : (!string.IsNullOrEmpty(contact.Name()) ? contact.Name() : contactName);

                    string formattedContactName = FormatContactNameForNotification(rawSenderName);
                    string notificationBody = $"{formattedContactName}: {firstNpcMessage.Content()}";

                    PhoneApp notificationApp = GetChannelForContact(contact.Name()) == MessagingChannel.Connect
                        ? PhoneApp.Connect
                        : PhoneApp.Messages;
                    PhoneNotificationController.Instance?.ShowNotification(notificationApp, notificationBody);
                }
                else if (addedCount > 1)
                {
                    PhoneApp notificationApp = GetChannelForContact(contact.Name()) == MessagingChannel.Connect
                        ? PhoneApp.Connect
                        : PhoneApp.Messages;
                    PhoneNotificationController.Instance?.ShowNotification(notificationApp, "New unread messages");
                }
            }
            else
            {
                // First message is player or no dialogues
                contact.SetCurrentMessageIndex(0);
                contact.SetHasNewMessages(dialogues != null && dialogues.Count > 0);
            }
        }
        else
        {
            // In-person conversation
            contact.SetHasNewMessages(false);
        }

        SortContacts();

        // Refresh contact list in MessagesUI so new/updated contact appears immediately
        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindFirstObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }
        _messagesUI?.RefreshContacts();

        Debug.Log($"[MessagesManager] Successfully advanced contact '{contactName}' to node {newNarrativeNodeId} by event.");
    }

    /// <summary>
    /// Moves a contact to a completed narrative node without adding messages,
    /// unread badges, or notifications. Used by the laptop test-skip so the
    /// following day starts from the same functional state as a played day.
    /// </summary>
    public void SetContactNarrativeStateSilently(string contactName, int narrativeNodeId)
    {
        Contact contact = GetContactByName(contactName);
        if (contact == null)
        {
            contact = new Contact();
            contact.SetName(contactName);
            contact.SetImagePath("Assets/Sprites/UnknownProfile.png");
            _contacts.Add(contact);
            GetMessageStore(contact.Name())[contact.Name()] = new List<Message>();
        }

        contact.SetNarrativeNode(narrativeNodeId);

        NarrativeNode node = GetNarrativeNode(narrativeNodeId);
        int completedMessageIndex = node?.Dialogues()?.Count ?? 0;
        contact.SetCurrentMessageIndex(completedMessageIndex);
        contact.SetHasNewMessages(false);

        SortContacts();

        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindFirstObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }

        _messagesUI?.RefreshContacts();
        Debug.Log($"[MessagesManager] Silently set '{contactName}' to completed node {narrativeNodeId}.");
    }

    /// <summary>
    /// Clears only the Connect/social histories before rebuilding a menu
    /// day-skip snapshot. The original phone conversations with Mom, Dad and
    /// Maya are deliberately preserved.
    /// </summary>
    public void BeginTestSkipHistoryRebuild()
    {
        _connectMessages.Clear();

        foreach (Contact contact in _contacts)
        {
            if (GetChannelForContact(contact.Name()) != MessagingChannel.Connect) continue;

            _connectMessages[contact.Name()] = new List<Message>();
            contact.SetLastMessageDate(default);
            contact.SetHasNewMessages(false);
            contact.SetCurrentMessageIndex(0);
        }
    }

    /// <summary>
    /// Adds completed phone dialogue to a contact without animations,
    /// notifications or narrative events. Narration and in-person nodes are
    /// excluded because they were never chat bubbles during normal play.
    /// </summary>
    public void AppendTestSkipHistory(string contactName, int storyDay, params int[] narrativeNodeIds)
    {
        if (string.IsNullOrWhiteSpace(contactName) || narrativeNodeIds == null) return;

        Contact contact = GetContactByName(contactName);
        if (contact == null)
        {
            contact = new Contact();
            contact.SetName(contactName);
            contact.SetImagePath("Assets/Sprites/UnknownProfile.png");
            _contacts.Add(contact);
        }

        Dictionary<Name, List<Message>> messageStore = GetMessageStore(contact.Name());
        if (!messageStore.TryGetValue(contact.Name(), out List<Message> history))
        {
            history = new List<Message>();
            messageStore[contact.Name()] = history;
        }

        foreach (int nodeId in narrativeNodeIds)
        {
            NarrativeNode node = GetNarrativeNode(nodeId);
            if (node == null || node.DialogueType() != DialogueType.PHONE) continue;

            foreach (Message sourceMessage in node.Dialogues())
            {
                if (sourceMessage == null || IsNarration(sourceMessage)) continue;

                int messageNumber = history.Count;
                Date timestamp = new()
                {
                    day = Mathf.Clamp(storyDay, 1, 31),
                    month = 10,
                    year = 2026,
                    hour = 8 + Mathf.Min(14, messageNumber / 6),
                    minute = (messageNumber * 7) % 60
                };

                Message historicalMessage = new(
                    sourceMessage.Content(),
                    sourceMessage.Sender(),
                    sourceMessage.SpeakerName(),
                    timestamp);

                history.Add(historicalMessage);
                contact.SetLastMessageDate(timestamp);
            }
        }

        contact.SetHasNewMessages(false);
    }

    public void CompleteTestSkipHistoryRebuild()
    {
        SortContacts();

        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindFirstObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }

        _messagesUI?.RefreshContacts();
        Debug.Log("[MessagesManager] Rebuilt completed chat history for the menu day skip.");
    }

    /// <summary>
    /// Formats a contact or speaker name for single-message notifications:
    /// Maximum 10 characters. If greater than 10 characters, truncates to 9 characters and appends '-'.
    /// </summary>
    private string FormatContactNameForNotification(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        string trimmed = name.Trim();
        if (trimmed.Length > 10)
        {
            return trimmed.Substring(0, 9) + "-";
        }
        return trimmed;
    }

    public NarrativeNode GetNarrativeNode(int nodeId)
    {
        if (_narrativeNodes.ContainsKey(nodeId))
        {
            return _narrativeNodes[nodeId];
        }
        else
        {
            Debug.LogWarning($"Narrative node with ID {nodeId} not found.");
            return null;
        }
    }

    public NarrativeNode GetCurrentContactNarrativeNode()
    {
        Contact currentContact = GetCurrentContact();
        if (currentContact != null)
        {
            return GetNarrativeNode(currentContact.NarrativeNode());
        }
        else
        {
            Debug.LogWarning("No current contact selected.");
            return null;
        }
    }

    public bool ContactHasMessages(string contactName)
    {
        Dictionary<Name, List<Message>> messageStore = GetMessageStore(contactName);
        if (messageStore.ContainsKey(contactName))
        {
            return messageStore[contactName].Count > 0;
        }
        return false;
    }

    /// <summary>
    /// Update the message list of the current contact with a new message
    /// </summary>
    /// <param name="message"></param>
    public void SendMessageToCurrentContact(Message message)
    {
        if (string.IsNullOrEmpty(_currentContact) || message == null)
        {
            Debug.LogWarning("No current contact selected or null message.");
            return;
        }

        Contact contact = GetContactByName(_currentContact);
        string keyName = contact != null ? contact.Name() : _currentContact;

        Dictionary<Name, List<Message>> messageStore = GetMessageStore(keyName);
        if (!messageStore.ContainsKey(keyName))
        {
            messageStore[keyName] = new List<Message>();
        }
        messageStore[keyName].Add(message);

        if (contact != null)
        {
            contact.SetLastMessageDate(message.Timestamp());
        }

        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindFirstObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }

        _messagesUI?.AddMessage(message);
        SortContacts();
        _messagesUI?.RefreshContacts();
    }

    /// <summary>
    /// Sorts the contacts list based on the last message date, with the most recent messages appearing first.
    /// </summary>
    private void SortContacts()
    { 
        _contacts = _contacts.OrderByDescending(val => val.LastMessageDate()).ToList();
    }

    private static MessagingChannel GetChannelForContact(string contactName)
    {
        if (string.Equals(contactName, "Mom", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contactName, "Dad", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contactName, "Maya", StringComparison.OrdinalIgnoreCase))
        {
            return MessagingChannel.Phone;
        }

        return MessagingChannel.Connect;
    }

    private static bool IsNarration(Message message)
    {
        return message != null &&
               string.Equals(message.SpeakerName(), "Narration", StringComparison.OrdinalIgnoreCase);
    }

    private Dictionary<Name, List<Message>> GetMessageStore(string contactName)
    {
        return GetChannelForContact(contactName) == MessagingChannel.Phone
            ? _contactsMessages
            : _connectMessages;
    }

    /// <summary>
    /// Returns the PhoneStateMachine component attached to the same GameObject as this script.
    /// </summary>
    /// <returns></returns>
    public PhoneStateMachine GetPhoneStateMachine()
    {
        return _phoneStateMachine;
    }

    /// <summary>
    /// Returns the MessagesUI component attached to the same GameObject as this script.
    /// </summary>
    /// <returns></returns>
    public MessagesUI GetMessagesUI()
    {
        return _messagesUI;
    }

    public OptionsChanger GetOptionsChanger()
    {
        return _optionsChanger;
    }

    /// <summary>
    /// Creates the contacts by loading them from the specified JSON file and adding them to the contacts dictionary.
    /// </summary>
    private void CreateContacts()
    {
        // read the contacts info from the JSON
        _contacts = DataLoader.LoadContacts(contactsFilePath);
    }

    private void CreateMessages()
    {
        List<ContactPhoneMessages> contactPhoneMessages = DataLoader.LoadPhoneMessages(phoneMessagesFilePath);

        if (contactPhoneMessages != null)
        {
            foreach (ContactPhoneMessages contactMessages in contactPhoneMessages)
            {
                Dictionary<Name, List<Message>> messageStore = GetMessageStore(contactMessages.ContactName());
                messageStore[contactMessages.ContactName()] = contactMessages.Messages() ?? new List<Message>();
                if (contactMessages.Messages() != null && contactMessages.Messages().Count > 0)
                {
                    Message lastMsg = contactMessages.Messages()[contactMessages.Messages().Count - 1];
                    Contact contact = GetContactByName(contactMessages.ContactName());
                    if (contact != null)
                    {
                        contact.SetLastMessageDate(lastMsg.Timestamp());
                    }
                }
            }
        }

        // Ensure every contact in _contacts has an entry in _contactsMessages
        if (_contacts != null)
        {
            foreach (Contact contact in _contacts)
            {
                Dictionary<Name, List<Message>> messageStore = GetMessageStore(contact.Name());
                if (!messageStore.ContainsKey(contact.Name()))
                {
                    messageStore[contact.Name()] = new List<Message>();
                }
            }
        }
    }
}
