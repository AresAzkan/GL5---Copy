using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System;

public enum UIState
{
    DEFAULT,
    PHONE,
    MAP,
    MENU,
    CONVERSATION,
    NONE

}

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // High-visibility warning red used by every unavailable interaction.
    public static readonly Color UnavailableInteractionColor = new Color(1f, 0.22f, 0.3f, 1f);

    [Header("UI Elements")]
    [SerializeField] private GameObject _pauseMenuPanel;
    [SerializeField] private GameObject _phone;
    [SerializeField] private CanvasGroup _interactionCanvasGroup;
    [SerializeField] private TMP_Text _interactionText;
    [SerializeField] private GameObject _map;
    [SerializeField] private Sprite _appStoreIcon;
    [SerializeField] private Sprite _connectAppIcon;

    [Header("Messages Animations")]
    [SerializeField] private float _appearTime = 0.3f;
    [SerializeField] private float _disappearTime = 0.5f;

    [Header("Blur Options")]
    [SerializeField] private Volume _blurVolume;
    [SerializeField] private float _fadeDuration;
    private InputAction _menuAction;
    private Tween _fadeTween;
    private Tween _blurTween;
    private Tween _delayTween;
    private PauseSettingsMenu _pauseSettingsMenu;
    private GameObject _simpleChoicePanel;
    private TMP_Text _simpleChoicePrompt;
    private Button[] _simpleChoiceButtons;
    private Action<int> _simpleChoiceCallback;
    private CanvasGroup _phoneCanvasGroup;
    private Button _settingsAppButton;
    private Button _appStoreButton;
    private Button _connectAppButton;
    private bool _connectDownloadAvailable;
    private bool _connectInstalled;
    private bool _connectProfileCreated;
    private bool _simpleChoicesReturnToPhone;
    private TextMeshProUGUI _dayTrackerText;
    private int _displayedDay = -1;

    private UIState _currentState = UIState.DEFAULT;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ConfigurePhoneLayout();
        ConfigureDecisionsLayout();
        ConfigureSimpleChoices();
        ConfigureDialogueLayout();
        ConfigureObjectivesLayout();
        ConfigureDayTracker();
        ConfigureSettingsMenu();
        ConfigureMapButtons();

        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.SetActive(false);
        }

        if (_interactionCanvasGroup != null)
        {
            _interactionCanvasGroup.alpha = 0f;
        }

        EventBus.Instance.Subscribe("OpenMap", ShowMap);
        EventBus.Instance.Subscribe("CloseMap", HideMap);
        EventBus.Instance.Subscribe("D1_StudentConnectConversationComplete", EnableConnectDownload);
        EventBus.Instance.Subscribe("D1_Profile_End", CompleteConnectProfile);

    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe("OpenMap", ShowMap);
        EventBus.Instance.Unsubscribe("CloseMap", HideMap);
        EventBus.Instance.Unsubscribe("D1_StudentConnectConversationComplete", EnableConnectDownload);
        EventBus.Instance.Unsubscribe("D1_Profile_End", CompleteConnectProfile);
    }

    private void Update()
    {
        RefreshDayTracker();

        if(InputManager.Instance.MenuWasTapped())
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (_currentState == UIState.DEFAULT) ChangeState(3);
        else if (_currentState == UIState.MENU)
        {
            if (_pauseSettingsMenu != null && _pauseSettingsMenu.IsSettingsOpen)
            {
                _pauseSettingsMenu.CloseSettings();
                return;
            }

            ChangeState(0);
        }
    }

    public void ShowMessage(string message, Color colorText)
    {
        if (_interactionCanvasGroup == null || _interactionText == null) return;

        // Stop every previous animation
        _fadeTween?.Kill();
        _delayTween?.Kill();
        _interactionCanvasGroup.DOKill();

        // Update the info
        _interactionText.text = message;
        _interactionText.color = colorText;

        // animate until the alpha is 1
        _fadeTween = _interactionCanvasGroup.DOFade(1f, _appearTime);
    }

    public void HideMessage()
    {
        if (_interactionCanvasGroup == null) return;

        // Stop every previous animation
        _fadeTween?.Kill();
        _delayTween?.Kill();
        _interactionCanvasGroup.DOKill();

        // Animate until the alpha is 0
        _fadeTween = _interactionCanvasGroup.DOFade(0f, _disappearTime);
    }

    public void ShowTemporaryMessage(string message, Color textColor, float duration)
    {
        if (_interactionCanvasGroup == null || _interactionText == null) return;

        ShowMessage(message, textColor);

        // After 'duration' it hides the text
        _delayTween = DOVirtual.DelayedCall(duration, () => 
        {
            HideMessage();
        });
    }

    public void LockMouse()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UnlockMouse()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ShowMap()
    {
        ChangeState(2);
    }

    public void HideMap()
    {
        ChangeState(0);
        ReturnPlayerToIdle();
    }

    private void ConfigureMapButtons()
    {
        if (_map == null) return;

        foreach (Button button in _map.GetComponentsInChildren<Button>(true))
        {
            if (button.gameObject.name != "Home" && button.gameObject.name != "School")
            {
                continue;
            }

            string destination = button.gameObject.name;

            // Replace the old prefab events so both map buttons follow the same
            // travel rules regardless of which scene opened the map.
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => HandleMapDestination(destination));
        }
    }

    private void HandleMapDestination(string destination)
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (string.Equals(currentScene, destination, StringComparison.OrdinalIgnoreCase))
        {
            string monologue = destination == "Home"
                ? "I need to go to school or I will be late."
                : "School is finished. I ain't going back.";

            if (DialogueManager.Instance == null ||
                !DialogueManager.Instance.ShowInnerMonologue(monologue))
            {
                ShowTemporaryMessage(monologue, Color.white, 5f);
            }

            return;
        }

        HideMap();

        string travelEvent = destination == "School"
            ? "Day1_GoToSchool"
            : "Day1_ComebackHome";

        EventBus.Instance?.Publish(travelEvent);
        GameManager.Instance.ChangeScene(destination);
    }

    private static void ReturnPlayerToIdle()
    {
        PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
        if (playerState != null)
        {
            playerState.ChangeState(PlayerState.Idle);
        }
    }

    /// <summary>
    /// Displays a lightweight two-option menu using a private clone of the
    /// existing decision UI. This keeps one-off interactions visually
    /// consistent without changing the active narrative node.
    /// </summary>
    public bool ShowSimpleChoices(
        string prompt,
        string firstChoice,
        string secondChoice,
        Action<int> onChoiceSelected)
    {
        if (_simpleChoicePanel == null)
        {
            ConfigureSimpleChoices();
        }

        if (_simpleChoicePanel == null || _simpleChoiceButtons == null || _simpleChoiceButtons.Length < 2)
        {
            Debug.LogWarning("[UIManager] The simple-choice UI could not be created.");
            return false;
        }

        _simpleChoicesReturnToPhone = false;
        _simpleChoiceCallback = onChoiceSelected;

        if (_simpleChoicePrompt != null)
        {
            _simpleChoicePrompt.text = prompt;
        }

        SetSimpleChoiceButton(0, firstChoice);
        SetSimpleChoiceButton(1, secondChoice);

        _simpleChoicePanel.SetActive(true);
        ChangeState((int)UIState.NONE);

        // NONE intentionally has no general input policy. A modal choice must
        // also block the phone and pause bindings until a choice is made.
        InputManager.Instance.DisablePhone();
        InputManager.Instance.DisableMenuAction();
        return true;
    }

    public bool ShowPhoneChoices(
        string prompt,
        string firstChoice,
        string secondChoice,
        Action<int> onChoiceSelected)
    {
        if (_currentState != UIState.PHONE) return false;

        if (_simpleChoicePanel == null)
        {
            ConfigureSimpleChoices();
        }

        if (_simpleChoicePanel == null || _simpleChoiceButtons == null || _simpleChoiceButtons.Length < 2)
        {
            Debug.LogWarning("[UIManager] The phone choice UI could not be created.");
            return false;
        }

        _simpleChoicesReturnToPhone = true;
        _simpleChoiceCallback = onChoiceSelected;

        if (_simpleChoicePrompt != null)
        {
            _simpleChoicePrompt.text = prompt;
        }

        SetSimpleChoiceButton(0, firstChoice);
        SetSimpleChoiceButton(1, secondChoice);
        _simpleChoicePanel.SetActive(true);
        SetPhoneAppInteraction(false);
        return true;
    }

    public void OpenSettingsFromPhone()
    {
        if (_currentState != UIState.PHONE || _pauseSettingsMenu == null) return;

        ChangeState((int)UIState.MENU);
        _pauseSettingsMenu.OpenSettings(ReturnToPhoneFromSettings);
    }

    private void ReturnToPhoneFromSettings()
    {
        ChangeState((int)UIState.PHONE);
    }

    public void OpenAppStoreMonologue()
    {
        if (_currentState != UIState.PHONE || DialogueManager.Instance == null) return;

        if (_connectDownloadAvailable && !_connectInstalled)
        {
            bool choicesOpened = ShowPhoneChoices(
                "<b>App Store</b>\nConnect is available to download.",
                "Close App Store",
                "Download Connect",
                HandleAppStoreChoice);

            if (choicesOpened) return;
        }

        SetPhoneAppInteraction(false);
        bool opened = DialogueManager.Instance.ShowInnerMonologue(
            "I don't want to install anything.",
            CloseAppStoreMonologue);

        if (!opened)
        {
            SetPhoneAppInteraction(true);
        }
    }

    private void CloseAppStoreMonologue()
    {
        SetPhoneAppInteraction(true);
    }

    private void EnableConnectDownload()
    {
        _connectDownloadAvailable = true;
        Debug.Log("[UIManager] Connect is now available in the App Store.");
    }

    private void CompleteConnectProfile()
    {
        _connectProfileCreated = true;
        Debug.Log("[UIManager] Luna's Connect profile setup is complete.");
    }

    /// <summary>
    /// Restores the phone state required when the main-menu testing shortcut
    /// begins after Day 1. This deliberately avoids replaying download/profile
    /// dialogue or notifications while making Connect available immediately.
    /// </summary>
    public void ApplyCompletedConnectSetupForTestSkip()
    {
        _connectDownloadAvailable = true;
        _connectInstalled = true;
        _connectProfileCreated = true;

        if (_connectAppButton != null)
        {
            _connectAppButton.gameObject.SetActive(true);
        }

        UpdatePhoneAppLayout();
        Debug.Log("[UIManager] Restored completed Connect setup for a menu day skip.");
    }

    private void HandleAppStoreChoice(int choiceIndex)
    {
        const int DownloadConnectChoice = 1;
        if (choiceIndex != DownloadConnectChoice || _connectInstalled) return;

        _connectInstalled = true;
        if (_connectAppButton != null)
        {
            _connectAppButton.gameObject.SetActive(true);
        }

        UpdatePhoneAppLayout();
        EventBus.Instance?.Publish("D1_ConnectAvailable");
        EventBus.Instance?.Publish("D1_ConnectDownloaded");
        ShowTemporaryMessage("Connect installed.", Color.white, 2.5f);
        Debug.Log("[UIManager] Connect was downloaded and added to the phone.");
    }


    public void ChangeState(int newState) 
    {
        if ((UIState)newState == _currentState) return;

        Debug.Log("[UIManager] Previous:" + _currentState);
        ExitCurrentState();
        _currentState = (UIState)newState;
        Debug.Log("[UIManager] Current:" + _currentState);
        EnterNewState();
        HideMessage();
    }



    private void ExitCurrentState()
    {
        switch (_currentState)
        {
            case UIState.DEFAULT:
                {
                    InputManager.Instance.DisableMovementMap();
                    InputManager.Instance.DisableInteract();
                    UnlockMouse();
                }
                break;
            case UIState.PHONE:
                {
                    _phone.SetActive(false);
                    EnableBlur(false);
                    EventBus.Instance.Publish("Phone_Closed",0);
                }
                break;
            case UIState.MAP:
                {
                    _map.SetActive(false);
                }
                break;
            case UIState.CONVERSATION:
                {
                    EventBus.Instance.Publish("Conversation_Ended");
                }
                break;
            case UIState.MENU:
                {
                    Time.timeScale = 1f;
                    if (_pauseSettingsMenu != null) _pauseSettingsMenu.HideAll();
                    else _pauseMenuPanel.SetActive(false);
                }
                break;
        }
    }

    private void EnterNewState()
    {
        switch (_currentState)
        {
            case UIState.DEFAULT:
                {
                    InputManager.Instance.EnableMovementMap();
                    InputManager.Instance.EnablePhone();
                    InputManager.Instance.EnableMenuAction();
                    InputManager.Instance.EnableInteract();
                    LockMouse();
                }
                break;
            case UIState.PHONE:
                {
                    EventBus.Instance.Publish("Phone_Opened");
                    _phone.SetActive(true);
                    EnableBlur(true);
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisableMenuAction();
                    InputManager.Instance.EnablePhone();
                }
                break;
            case UIState.MAP:
                {
                    _map.SetActive(true);
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisablePhone();
                }
                break;
            case UIState.CONVERSATION:
                {
                    Debug.Log("[UIManager] Conversation");
                    EventBus.Instance.Publish("Conversation_Started");
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisablePhone();
                }
                break;
            case UIState.MENU:
                {
                    _pauseMenuPanel.SetActive(true);
                    _pauseSettingsMenu?.ShowMainMenu();
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisablePhone();
                }
                break;
        }

    }


    private void EnableBlur(bool enable)
    {
        if (_blurVolume == null) {
            Debug.LogWarning("UIManager: _blurVolume not assigned");
            return;
        }

        _blurTween?.Kill();
        float targetWeight = enable ? 1f : 0f;

        DOTween.To(() => _blurVolume.weight, x => _blurVolume.weight = x, targetWeight, _fadeDuration).SetUpdate(true);
    }

    private void ConfigurePhoneLayout()
    {
        if (_phone == null) return;
        CenterOverlay(_phone.transform as RectTransform);

        _phoneCanvasGroup = _phone.GetComponent<CanvasGroup>();
        if (_phoneCanvasGroup == null)
        {
            _phoneCanvasGroup = _phone.AddComponent<CanvasGroup>();
        }

        Button settingsButton = _phone.transform
            .Find("StartScreen/ButtonPanel/Settings")
            ?.GetComponent<Button>();

        _settingsAppButton = settingsButton;

        Button messagesButton = _phone.transform
            .Find("StartScreen/ButtonPanel/Messages")
            ?.GetComponent<Button>();

        if (messagesButton != null)
        {
            messagesButton.onClick = new Button.ButtonClickedEvent();
            messagesButton.onClick.AddListener(OpenPhoneMessages);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OpenSettingsFromPhone);
            settingsButton.onClick.AddListener(OpenSettingsFromPhone);
        }

        ConfigureAppStoreButton(settingsButton);
        ConfigureConnectAppButton(settingsButton);
        UpdatePhoneAppLayout();
    }

    private void ConfigureAppStoreButton(Button settingsButton)
    {
        if (settingsButton == null)
        {
            Debug.LogWarning("[UIManager] Settings button was not found; App Store button cannot be created.");
            return;
        }

        Transform buttonPanel = settingsButton.transform.parent;
        Button appStoreButton = buttonPanel
            .Find("AppStore")
            ?.GetComponent<Button>();

        if (appStoreButton == null)
        {
            appStoreButton = Instantiate(settingsButton, buttonPanel);
            appStoreButton.name = "AppStore";
        }

        appStoreButton.onClick = new Button.ButtonClickedEvent();
        appStoreButton.onClick.AddListener(OpenAppStoreMonologue);
        _appStoreButton = appStoreButton;

        Image appStoreImage = appStoreButton.GetComponent<Image>();
        if (appStoreImage != null && _appStoreIcon != null)
        {
            appStoreImage.sprite = _appStoreIcon;
            appStoreImage.preserveAspect = true;
            appStoreButton.targetGraphic = appStoreImage;
        }
        else if (_appStoreIcon == null)
        {
            Debug.LogWarning("[UIManager] App Store icon is not assigned.");
        }

    }

    private void ConfigureConnectAppButton(Button settingsButton)
    {
        if (settingsButton == null) return;

        Transform buttonPanel = settingsButton.transform.parent;
        _connectAppButton = buttonPanel
            .Find("Connect")
            ?.GetComponent<Button>();

        if (_connectAppButton == null)
        {
            _connectAppButton = Instantiate(settingsButton, buttonPanel);
            _connectAppButton.name = "Connect";
        }

        _connectAppButton.onClick = new Button.ButtonClickedEvent();
        _connectAppButton.onClick.AddListener(OpenConnectApp);

        Image connectImage = _connectAppButton.GetComponent<Image>();
        if (connectImage != null && _connectAppIcon != null)
        {
            connectImage.sprite = _connectAppIcon;
            connectImage.preserveAspect = true;
            _connectAppButton.targetGraphic = connectImage;
        }
        else if (_connectAppIcon == null)
        {
            Debug.LogWarning("[UIManager] Connect app icon is not assigned.");
        }

        _connectAppButton.gameObject.SetActive(_connectInstalled);
    }

    private void OpenPhoneMessages()
    {
        if (MessagesManager.Instance == null) return;

        MessagesManager.Instance.SetMessagingChannel(MessagingChannel.Phone);
        PhoneStateMachine phoneState = MessagesManager.Instance.GetPhoneStateMachine();
        phoneState?.ChangeState((int)PhoneState.CONTACTS);
        MessagesManager.Instance.GetMessagesUI()?.RefreshContacts();
    }

    private void OpenConnectApp()
    {
        if (!_connectInstalled || MessagesManager.Instance == null) return;

        MessagesManager messages = MessagesManager.Instance;
        messages.SetMessagingChannel(MessagingChannel.Connect);

        PhoneStateMachine phoneState = messages.GetPhoneStateMachine();
        MessagesUI messagesUI = messages.GetMessagesUI();
        if (phoneState == null || messagesUI == null) return;

        if (_connectProfileCreated)
        {
            phoneState.ChangeState((int)PhoneState.CONTACTS);
            messagesUI.RefreshContacts();
            return;
        }

        Contact connectContact = messages.GetContactByName("Connect");
        if (connectContact == null || connectContact.NarrativeNode() < 1000 || connectContact.NarrativeNode() > 1003)
        {
            messages.AdvanceContactNarrativeNodeByEvent("Connect", 1000);
        }

        messages.SetCurrentContact("Connect");
        phoneState.ChangeState((int)PhoneState.MESSAGING);
        messagesUI.SetContactName("Connect");
        messagesUI.CreateMessages();

        NarrativeNode profileNode = messages.GetCurrentContactNarrativeNode();
        if (profileNode != null && profileNode.HasOptions())
        {
            messages.GetOptionsChanger().SetCurrentNode(profileNode);
            messages.GetOptionsChanger().ActiveDecisionsPanel(true);
            phoneState.ShowDecisionsTab();
        }
        else if (connectContact != null && connectContact.HasNewMessages())
        {
            phoneState.ShowDialogueTab();
        }
    }

    private void UpdatePhoneAppLayout()
    {
        if (_settingsAppButton == null || _appStoreButton == null) return;

        float appStoreX = _connectInstalled ? -100f : -55f;
        float settingsX = _connectInstalled ? 100f : 55f;

        if (_appStoreButton.transform is RectTransform appStoreRect)
        {
            appStoreRect.anchoredPosition = new Vector2(appStoreX, -110f);
        }

        if (_settingsAppButton.transform is RectTransform settingsRect)
        {
            settingsRect.anchoredPosition = new Vector2(settingsX, -110f);
        }

        if (_connectAppButton != null && _connectAppButton.transform is RectTransform connectRect)
        {
            connectRect.anchoredPosition = new Vector2(0f, -110f);
        }
    }

    private void SetPhoneAppInteraction(bool interactable)
    {
        if (_phoneCanvasGroup != null)
        {
            _phoneCanvasGroup.interactable = interactable;
            _phoneCanvasGroup.blocksRaycasts = true;
        }

        if (interactable && _currentState == UIState.PHONE)
        {
            InputManager.Instance.EnablePhone();
        }
        else
        {
            InputManager.Instance.DisablePhone();
        }
    }

    private void ConfigureDecisionsLayout()
    {
        Transform decisions = transform.Find("DecissionsUI");
        CenterOverlay(decisions as RectTransform);
    }

    private void ConfigureSimpleChoices()
    {
        if (_simpleChoicePanel != null) return;

        Transform decisions = transform.Find("DecissionsUI");
        if (decisions == null)
        {
            Debug.LogWarning("[UIManager] DecissionsUI was not found; simple choices are unavailable.");
            return;
        }

        _simpleChoicePanel = Instantiate(decisions.gameObject, transform);
        _simpleChoicePanel.name = "SimpleChoicesUI";
        _simpleChoicePanel.SetActive(false);
        _simpleChoicePanel.transform.SetAsLastSibling();
        CenterOverlay(_simpleChoicePanel.transform as RectTransform);

        Canvas simpleCanvas = _simpleChoicePanel.GetComponent<Canvas>();
        if (simpleCanvas != null)
        {
            simpleCanvas.overrideSorting = true;
            simpleCanvas.sortingOrder = 50;
        }

        _simpleChoicePrompt = _simpleChoicePanel.transform
            .Find("MainText")
            ?.GetComponent<TMP_Text>();

        Transform buttonsRoot = _simpleChoicePanel.transform.Find("Buttons");
        if (buttonsRoot == null)
        {
            Debug.LogWarning("[UIManager] DecissionsUI has no Buttons container.");
            Destroy(_simpleChoicePanel);
            _simpleChoicePanel = null;
            return;
        }

        OptionButtonHelper[] narrativeHelpers = buttonsRoot.GetComponentsInChildren<OptionButtonHelper>(true);
        foreach (OptionButtonHelper helper in narrativeHelpers)
        {
            // These cloned buttons are intentionally independent from the
            // narrative OptionsChanger.
            helper.enabled = false;
        }

        _simpleChoiceButtons = buttonsRoot.GetComponentsInChildren<Button>(true);
        Array.Sort(_simpleChoiceButtons, (left, right) =>
            left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));

        for (int i = 0; i < _simpleChoiceButtons.Length; i++)
        {
            int choiceIndex = i;
            Button button = _simpleChoiceButtons[i];
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => CompleteSimpleChoice(choiceIndex));
            button.gameObject.SetActive(i < 2);
        }
    }

    private void SetSimpleChoiceButton(int index, string label)
    {
        Button button = _simpleChoiceButtons[index];
        button.gameObject.SetActive(true);

        TMP_Text buttonLabel = button.GetComponentInChildren<TMP_Text>(true);
        if (buttonLabel != null)
        {
            buttonLabel.text = label;
        }
    }

    private void CompleteSimpleChoice(int choiceIndex)
    {
        if (_simpleChoicePanel != null)
        {
            _simpleChoicePanel.SetActive(false);
        }

        Action<int> callback = _simpleChoiceCallback;
        _simpleChoiceCallback = null;

        bool returnToPhone = _simpleChoicesReturnToPhone;
        _simpleChoicesReturnToPhone = false;
        if (returnToPhone)
        {
            SetPhoneAppInteraction(true);
        }
        else
        {
            ChangeState((int)UIState.DEFAULT);
        }

        callback?.Invoke(choiceIndex);
    }

    private void ConfigureDialogueLayout()
    {
        StretchOverlay(transform.Find("Dialogues") as RectTransform);
    }

    private void ConfigureObjectivesLayout()
    {
        RectTransform objectivesRoot = transform.Find("HUD/ObjectivesTab") as RectTransform;
        if (objectivesRoot == null) return;

        StretchOverlay(objectivesRoot);

        RectTransform background = objectivesRoot.Find("BackGround") as RectTransform;
        RectTransform title = objectivesRoot.Find("Title") as RectTransform;
        RectTransform objective = objectivesRoot.Find("Objective") as RectTransform;

        AnchorTopLeft(background, new Vector2(10f, -10f), new Vector2(0f, 1f));
        AnchorTopLeft(title, new Vector2(30f, -22f), new Vector2(0f, 1f));
        AnchorTopLeft(objective, new Vector2(30f, -82f), new Vector2(0f, 1f));

        if (background != null)
        {
            background.sizeDelta = new Vector2(600f, 150f);
        }

        if (title != null)
        {
            title.sizeDelta = new Vector2(550f, 52f);
            TextMeshProUGUI titleText = title.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
            {
                titleText.margin = Vector4.zero;
                titleText.alignment = TextAlignmentOptions.TopLeft;
            }
        }

        if (objective != null)
        {
            objective.sizeDelta = new Vector2(550f, 50f);
            TextMeshProUGUI objectiveText = objective.GetComponent<TextMeshProUGUI>();
            if (objectiveText != null)
            {
                objectiveText.margin = Vector4.zero;
                objectiveText.alignment = TextAlignmentOptions.TopLeft;
                objectiveText.textWrappingMode = TextWrappingModes.Normal;
            }
        }
    }

    private void ConfigureDayTracker()
    {
        Transform hud = transform.Find("HUD");
        if (hud == null) return;

        Transform existing = hud.Find("DayTracker");
        if (existing != null)
        {
            _dayTrackerText = existing.GetComponentInChildren<TextMeshProUGUI>(true);
            RefreshDayTracker();
            return;
        }

        GameObject borderObject = new GameObject(
            "DayTracker",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        borderObject.layer = hud.gameObject.layer;
        borderObject.transform.SetParent(hud, false);

        RectTransform borderRect = borderObject.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0.5f, 1f);
        borderRect.anchorMax = new Vector2(0.5f, 1f);
        borderRect.pivot = new Vector2(0.5f, 1f);
        borderRect.anchoredPosition = new Vector2(0f, -14f);
        borderRect.sizeDelta = new Vector2(190f, 62f);
        borderRect.localScale = Vector3.one;

        Image border = borderObject.GetComponent<Image>();
        border.color = new Color(0.2f, 0.78f, 0.78f, 0.95f);
        border.raycastTarget = false;

        GameObject backgroundObject = new GameObject(
            "Background",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        backgroundObject.layer = hud.gameObject.layer;
        backgroundObject.transform.SetParent(borderObject.transform, false);

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.offsetMin = new Vector2(3f, 3f);
        backgroundRect.offsetMax = new Vector2(-3f, -3f);

        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0.18f, 0.23f, 0.27f, 0.88f);
        background.raycastTarget = false;

        GameObject textObject = new GameObject(
            "DayText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = hud.gameObject.layer;
        textObject.transform.SetParent(backgroundObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);

        _dayTrackerText = textObject.GetComponent<TextMeshProUGUI>();
        _dayTrackerText.alignment = TextAlignmentOptions.Center;
        _dayTrackerText.color = Color.white;
        _dayTrackerText.fontSize = 30f;
        _dayTrackerText.fontStyle = FontStyles.Bold;
        _dayTrackerText.enableAutoSizing = true;
        _dayTrackerText.fontSizeMin = 20f;
        _dayTrackerText.fontSizeMax = 30f;
        _dayTrackerText.raycastTarget = false;

        TextMeshProUGUI objectiveTemplate =
            transform.Find("HUD/ObjectivesTab/Objective")?.GetComponent<TextMeshProUGUI>();
        if (objectiveTemplate != null && objectiveTemplate.font != null)
        {
            _dayTrackerText.font = objectiveTemplate.font;
        }

        borderObject.transform.SetAsLastSibling();
        RefreshDayTracker();
    }

    private void RefreshDayTracker()
    {
        if (_dayTrackerText == null || DayManager.Instance == null) return;

        int currentDay = DayManager.Instance.GetCurrentDay();
        if (currentDay == _displayedDay) return;

        _displayedDay = currentDay;
        _dayTrackerText.text = $"DAY {currentDay}";
    }

    private static void StretchOverlay(RectTransform rect)
    {
        if (rect == null) return;

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void AnchorTopLeft(RectTransform rect, Vector2 position, Vector2 pivot)
    {
        if (rect == null) return;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.localScale = Vector3.one;
    }

    private static void CenterOverlay(RectTransform rect)
    {
        if (rect == null) return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private void ConfigureSettingsMenu()
    {
        if (_pauseMenuPanel == null) return;

        Transform menuRoot = _pauseMenuPanel.transform.parent;
        if (menuRoot == null) return;

        _pauseSettingsMenu = menuRoot.GetComponent<PauseSettingsMenu>();
        if (_pauseSettingsMenu == null)
        {
            _pauseSettingsMenu = menuRoot.gameObject.AddComponent<PauseSettingsMenu>();
        }

        _pauseSettingsMenu.Initialize(_pauseMenuPanel);
    }
}

public class PauseSettingsMenu : MonoBehaviour
{
    private GameObject _mainPanel;
    private GameObject _settingsPanel;
    private Slider _sensitivitySlider;
    private TextMeshProUGUI _sensitivityLabel;
    private Slider _masterVolumeSlider;
    private TextMeshProUGUI _masterVolumeLabel;
    private Slider _musicVolumeSlider;
    private TextMeshProUGUI _musicVolumeLabel;
    private Slider _sfxVolumeSlider;
    private TextMeshProUGUI _sfxVolumeLabel;
    private Slider _voiceVolumeSlider;
    private TextMeshProUGUI _voiceVolumeLabel;
    private System.Action _closeSettingsOverride;
    private bool _initialized;

    public bool IsSettingsOpen => _settingsPanel != null && _settingsPanel.activeSelf;

    public void Initialize(GameObject mainPanel)
    {
        if (_initialized || mainPanel == null) return;

        _mainPanel = mainPanel;
        CenterRectTransform(_mainPanel.transform.parent as RectTransform);
        CenterRectTransform(_mainPanel.transform as RectTransform);

        Button exitButton = _mainPanel.GetComponentInChildren<Button>(true);
        if (exitButton == null)
        {
            Debug.LogWarning("[PauseSettingsMenu] The pause menu has no button to use as a style template.");
            return;
        }

        CreateSettingsPanel(exitButton);
        CreateSettingsButton(exitButton);
        _initialized = true;
    }

    public void OpenSettings()
    {
        _closeSettingsOverride = null;
        if (_mainPanel == null || _settingsPanel == null) return;
        _mainPanel.SetActive(false);
        _settingsPanel.SetActive(true);
    }

    public void OpenSettings(System.Action closeAction)
    {
        _closeSettingsOverride = closeAction;
        if (_mainPanel == null || _settingsPanel == null) return;
        _mainPanel.SetActive(false);
        _settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        if (_closeSettingsOverride != null)
        {
            System.Action closeAction = _closeSettingsOverride;
            _closeSettingsOverride = null;
            HideAll();
            closeAction.Invoke();
            return;
        }

        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        _closeSettingsOverride = null;
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_mainPanel != null) _mainPanel.SetActive(true);
    }

    public void HideAll()
    {
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_mainPanel != null) _mainPanel.SetActive(false);
    }

    private void CreateSettingsButton(Button template)
    {
        Button settingsButton = Instantiate(template, _mainPanel.transform);
        settingsButton.name = "SettingsButton";
        settingsButton.onClick = new Button.ButtonClickedEvent();
        settingsButton.onClick.AddListener(OpenSettings);

        QuitGame quitComponent = settingsButton.GetComponent<QuitGame>();
        if (quitComponent != null) Destroy(quitComponent);

        if (settingsButton.transform is RectTransform rect)
        {
            rect.anchoredPosition = new Vector2(0f, 70f);
        }

        TextMeshProUGUI label = settingsButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null) label.text = "Settings";
    }

    private void CreateSettingsPanel(Button template)
    {
        _settingsPanel = Instantiate(_mainPanel, _mainPanel.transform.parent);
        _settingsPanel.name = "SettingsPanel";

        if (_settingsPanel.transform is RectTransform panelRect)
        {
            CenterRectTransform(panelRect);
            panelRect.sizeDelta = new Vector2(700f, 900f);
        }

        Button backButton = _settingsPanel.GetComponentInChildren<Button>(true);
        backButton.name = "BackButton";
        backButton.onClick = new Button.ButtonClickedEvent();
        backButton.onClick.AddListener(CloseSettings);

        QuitGame quitComponent = backButton.GetComponent<QuitGame>();
        if (quitComponent != null) Destroy(quitComponent);

        if (backButton.transform is RectTransform backRect)
        {
            backRect.anchoredPosition = new Vector2(0f, -370f);
        }

        TextMeshProUGUI backLabel = backButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (backLabel != null)
        {
            backLabel.text = "Back";

            TextMeshProUGUI title = Instantiate(backLabel, _settingsPanel.transform);
            title.name = "SettingsTitle";
            title.text = "Settings";
            title.fontSize = 42f;
            title.fontStyle = FontStyles.Bold;
            title.color = Color.white;

            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 370f);
            titleRect.sizeDelta = new Vector2(600f, 80f);

            CreateSensitivityControl(backLabel);
            CreateMasterVolumeControl(backLabel);
            CreateMusicVolumeControl(backLabel);
            CreateSfxVolumeControl(backLabel);
            CreateVoiceVolumeControl(backLabel);
        }

        _settingsPanel.SetActive(false);
    }

    private void CreateSensitivityControl(TextMeshProUGUI textTemplate)
    {
        float savedSensitivity = PlayerController.GetSavedMouseSensitivity();
        _sensitivitySlider = CreateSettingsSlider(
            "CameraSensitivitySlider",
            "CameraSensitivityLabel",
            textTemplate,
            new Vector2(0f, 270f),
            new Vector2(0f, 225f),
            PlayerController.MinimumMouseSensitivity,
            PlayerController.MaximumMouseSensitivity,
            savedSensitivity,
            OnSensitivityChanged,
            out _sensitivityLabel);
        OnSensitivityChanged(savedSensitivity);
    }

    private void OnSensitivityChanged(float sensitivity)
    {
        PlayerController.SetSavedMouseSensitivity(sensitivity);
        if (_sensitivityLabel != null)
        {
            _sensitivityLabel.text = $"Camera Sensitivity: {sensitivity:0.00}";
        }
    }

    private void CreateMusicVolumeControl(TextMeshProUGUI textTemplate)
    {
        float savedVolume = GameManager.GetSavedMusicVolume();
        _musicVolumeSlider = CreateSettingsSlider(
            "MusicVolumeSlider",
            "MusicVolumeLabel",
            textTemplate,
            new Vector2(0f, 20f),
            new Vector2(0f, -25f),
            0f,
            1f,
            savedVolume,
            OnMusicVolumeChanged,
            out _musicVolumeLabel);
        OnMusicVolumeChanged(savedVolume);
    }

    private void OnMusicVolumeChanged(float volume)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetMusicVolume(volume);
        }

        if (_musicVolumeLabel != null)
        {
            _musicVolumeLabel.text = $"Music Volume: {Mathf.RoundToInt(volume * 100f)}%";
        }
    }

    private void CreateMasterVolumeControl(TextMeshProUGUI textTemplate)
    {
        float savedVolume = GameManager.GetSavedMasterVolume();
        _masterVolumeSlider = CreateSettingsSlider(
            "MasterVolumeSlider",
            "MasterVolumeLabel",
            textTemplate,
            new Vector2(0f, 145f),
            new Vector2(0f, 100f),
            0f,
            1f,
            savedVolume,
            OnMasterVolumeChanged,
            out _masterVolumeLabel);
        OnMasterVolumeChanged(savedVolume);
    }

    private void CreateSfxVolumeControl(TextMeshProUGUI textTemplate)
    {
        float savedVolume = GameManager.GetSavedSfxVolume();
        _sfxVolumeSlider = CreateSettingsSlider(
            "SfxVolumeSlider",
            "SfxVolumeLabel",
            textTemplate,
            new Vector2(0f, -105f),
            new Vector2(0f, -150f),
            0f,
            1f,
            savedVolume,
            OnSfxVolumeChanged,
            out _sfxVolumeLabel);
        OnSfxVolumeChanged(savedVolume);
    }

    private void CreateVoiceVolumeControl(TextMeshProUGUI textTemplate)
    {
        float savedVolume = GameManager.GetSavedVoiceVolume();
        _voiceVolumeSlider = CreateSettingsSlider(
            "VoiceVolumeSlider",
            "VoiceVolumeLabel",
            textTemplate,
            new Vector2(0f, -230f),
            new Vector2(0f, -275f),
            0f,
            1f,
            savedVolume,
            OnVoiceVolumeChanged,
            out _voiceVolumeLabel);
        OnVoiceVolumeChanged(savedVolume);
    }

    private void OnMasterVolumeChanged(float volume)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetMasterVolume(volume);
        }
        else
        {
            AudioListener.volume = volume;
        }

        if (_masterVolumeLabel != null)
        {
            _masterVolumeLabel.text = $"Master Volume: {Mathf.RoundToInt(volume * 100f)}%";
        }
    }

    private void OnSfxVolumeChanged(float volume)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetSfxVolume(volume);
        }

        if (_sfxVolumeLabel != null)
        {
            _sfxVolumeLabel.text = $"SFX Volume: {Mathf.RoundToInt(volume * 100f)}%";
        }
    }

    private void OnVoiceVolumeChanged(float volume)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetVoiceVolume(volume);
        }

        if (_voiceVolumeLabel != null)
        {
            _voiceVolumeLabel.text = $"Voice Volume: {Mathf.RoundToInt(volume * 100f)}%";
        }
    }

    private Slider CreateSettingsSlider(
        string sliderName,
        string labelName,
        TextMeshProUGUI textTemplate,
        Vector2 labelPosition,
        Vector2 sliderPosition,
        float minValue,
        float maxValue,
        float initialValue,
        UnityEngine.Events.UnityAction<float> onValueChanged,
        out TextMeshProUGUI label)
    {
        label = Instantiate(textTemplate, _settingsPanel.transform);
        label.name = labelName;
        label.fontSize = 26f;
        label.fontStyle = FontStyles.Normal;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = labelPosition;
        labelRect.sizeDelta = new Vector2(600f, 45f);

        GameObject sliderObject = new GameObject(sliderName, typeof(RectTransform), typeof(Slider));
        sliderObject.layer = _settingsPanel.layer;
        sliderObject.transform.SetParent(_settingsPanel.transform, false);

        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = sliderPosition;
        sliderRect.sizeDelta = new Vector2(460f, 36f);

        Image background = CreateSliderImage("Background", sliderObject.transform, new Color(0.12f, 0.12f, 0.12f, 0.95f));
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = new Vector2(0f, 12f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.layer = sliderObject.layer;
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
        fillAreaRect.anchoredPosition = Vector2.zero;
        fillAreaRect.sizeDelta = new Vector2(-24f, 12f);

        Image fill = CreateSliderImage("Fill", fillArea.transform, new Color(0.18f, 0.82f, 0.8f, 1f));
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.pivot = new Vector2(0.5f, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.layer = sliderObject.layer;
        handleArea.transform.SetParent(sliderObject.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.pivot = new Vector2(0.5f, 0.5f);
        handleAreaRect.anchoredPosition = Vector2.zero;
        handleAreaRect.sizeDelta = new Vector2(-24f, 0f);

        Image handle = CreateSliderImage("Handle", handleArea.transform, Color.white);
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = new Vector2(0f, 0.5f);
        handleRect.anchorMax = new Vector2(0f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(26f, 34f);

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.wholeNumbers = false;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.SetValueWithoutNotify(initialValue);
        slider.onValueChanged.AddListener(onValueChanged);
        return slider;
    }

    private static Image CreateSliderImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = parent.gameObject.layer;
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void CenterRectTransform(RectTransform rect)
    {
        if (rect == null) return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
