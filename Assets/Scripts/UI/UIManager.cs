using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;
using UnityEngine.Rendering;
using UnityEngine.UI;

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

    [Header("UI Elements")]
    [SerializeField] private GameObject _pauseMenuPanel;
    [SerializeField] private GameObject _phone;
    [SerializeField] private CanvasGroup _interactionCanvasGroup;
    [SerializeField] private TMP_Text _interactionText;
    [SerializeField] private GameObject _map;

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
        ConfigureDialogueLayout();
        ConfigureObjectivesLayout();
        ConfigureSettingsMenu();

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

    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe("OpenMap", ShowMap);
        EventBus.Instance.Unsubscribe("CloseMap", HideMap);
    }

    private void Update()
    {
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
    }

    private void ConfigureDecisionsLayout()
    {
        Transform decisions = transform.Find("DecissionsUI");
        CenterOverlay(decisions as RectTransform);
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

        AnchorTopLeft(objectivesRoot.Find("BackGround") as RectTransform, new Vector2(10f, -10f), new Vector2(0f, 1f));
        AnchorTopLeft(objectivesRoot.Find("Title") as RectTransform, new Vector2(333f, -56f), new Vector2(0.5f, 0.5f));
        AnchorTopLeft(objectivesRoot.Find("Objective") as RectTransform, new Vector2(141f, -141f), new Vector2(0.5f, 0.5f));
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
        if (_mainPanel == null || _settingsPanel == null) return;
        _mainPanel.SetActive(false);
        _settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
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
            panelRect.sizeDelta = new Vector2(700f, 600f);
        }

        Button backButton = _settingsPanel.GetComponentInChildren<Button>(true);
        backButton.name = "BackButton";
        backButton.onClick = new Button.ButtonClickedEvent();
        backButton.onClick.AddListener(CloseSettings);

        QuitGame quitComponent = backButton.GetComponent<QuitGame>();
        if (quitComponent != null) Destroy(quitComponent);

        if (backButton.transform is RectTransform backRect)
        {
            backRect.anchoredPosition = new Vector2(0f, -240f);
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

            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 235f);
            titleRect.sizeDelta = new Vector2(600f, 80f);

            CreateSensitivityControl(backLabel);
        }

        _settingsPanel.SetActive(false);
    }

    private void CreateSensitivityControl(TextMeshProUGUI textTemplate)
    {
        _sensitivityLabel = Instantiate(textTemplate, _settingsPanel.transform);
        _sensitivityLabel.name = "CameraSensitivityLabel";
        _sensitivityLabel.fontSize = 30f;
        _sensitivityLabel.fontStyle = FontStyles.Normal;
        _sensitivityLabel.alignment = TextAlignmentOptions.Center;

        RectTransform labelRect = _sensitivityLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, 90f);
        labelRect.sizeDelta = new Vector2(600f, 60f);

        GameObject sliderObject = new GameObject("CameraSensitivitySlider", typeof(RectTransform), typeof(Slider));
        sliderObject.layer = _settingsPanel.layer;
        sliderObject.transform.SetParent(_settingsPanel.transform, false);

        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(0f, 25f);
        sliderRect.sizeDelta = new Vector2(460f, 44f);

        Image background = CreateSliderImage("Background", sliderObject.transform, new Color(0.12f, 0.12f, 0.12f, 0.95f));
        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = new Vector2(0f, 0.5f);
        backgroundRect.anchorMax = new Vector2(1f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = new Vector2(0f, 14f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.layer = sliderObject.layer;
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
        fillAreaRect.anchoredPosition = Vector2.zero;
        fillAreaRect.sizeDelta = new Vector2(-24f, 14f);

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
        handleRect.sizeDelta = new Vector2(28f, 42f);

        _sensitivitySlider = sliderObject.GetComponent<Slider>();
        _sensitivitySlider.minValue = PlayerController.MinimumMouseSensitivity;
        _sensitivitySlider.maxValue = PlayerController.MaximumMouseSensitivity;
        _sensitivitySlider.wholeNumbers = false;
        _sensitivitySlider.fillRect = fillRect;
        _sensitivitySlider.handleRect = handleRect;
        _sensitivitySlider.targetGraphic = handle;
        _sensitivitySlider.direction = Slider.Direction.LeftToRight;

        float savedSensitivity = PlayerController.GetSavedMouseSensitivity();
        _sensitivitySlider.SetValueWithoutNotify(savedSensitivity);
        _sensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
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
