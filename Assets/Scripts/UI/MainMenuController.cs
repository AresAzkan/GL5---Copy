using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    private const string MasterVolumeKey = "MasterVolume";
    private const string MusicVolumeKey = "MusicVolume";
    private const string SfxVolumeKey = "SfxVolume";
    private const string VoiceVolumeKey = "VoiceVolume";

    private static readonly Color BackgroundColor = new(0.035f, 0.055f, 0.09f, 1f);
    private static readonly Color PanelColor = new(0.075f, 0.105f, 0.145f, 0.96f);
    private static readonly Color AccentColor = new(0.18f, 0.78f, 0.78f, 1f);
    private static readonly Color AccentHoverColor = new(0.27f, 0.92f, 0.9f, 1f);
    private static readonly Color SecondaryColor = new(0.12f, 0.17f, 0.22f, 1f);
    private static readonly Color ExitColor = new(0.34f, 0.12f, 0.16f, 1f);
    private static readonly Color TextColor = new(0.94f, 0.97f, 1f, 1f);
    private static readonly Color MutedTextColor = new(0.62f, 0.7f, 0.76f, 1f);

    private GameObject _mainPanel;
    private GameObject _settingsPanel;
    private GameObject _daysPanel;
    private GameObject _routePanel;
    private TextMeshProUGUI _routeDayLabel;
    private int _selectedDay = 1;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;

        EnsureEventSystem();
        BuildMenu();
    }

    private void BuildMenu()
    {
        Canvas canvas = CreateCanvas();
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();

        CreateImage("Background", canvasRect, BackgroundColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform glow = CreateImage(
            "Decorative Glow", canvasRect, new Color(0.08f, 0.55f, 0.58f, 0.12f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(-500f, 80f), new Vector2(760f, 760f));
        glow.localRotation = Quaternion.Euler(0f, 0f, 18f);

        RectTransform sideBand = CreateImage(
            "Accent Band", canvasRect, new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.25f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(510f, -60f), new Vector2(220f, 1250f));
        sideBand.localRotation = Quaternion.Euler(0f, 0f, -16f);

        _mainPanel = CreatePanel("Main Panel", canvasRect);
        BuildMainPanel(_mainPanel.transform as RectTransform);

        _settingsPanel = CreatePanel("Settings Panel", canvasRect);
        BuildSettingsPanel(_settingsPanel.transform as RectTransform);
        _settingsPanel.SetActive(false);

        _daysPanel = CreatePanel("Days Panel", canvasRect);
        BuildDaysPanel(_daysPanel.transform as RectTransform);
        _daysPanel.SetActive(false);

        _routePanel = CreatePanel("Route Panel", canvasRect);
        BuildRoutePanel(_routePanel.transform as RectTransform);
        _routePanel.SetActive(false);
    }

    private void BuildMainPanel(RectTransform panel)
    {
        CreateText("Eyebrow", panel, "A FIVE-DAY INTERACTIVE STORY", 20f, AccentColor,
            new Vector2(0f, 270f), new Vector2(620f, 40f), FontStyles.Bold, TextAlignmentOptions.Center);
        CreateText("Title", panel, "BEFORE YOU SEND", 58f, TextColor,
            new Vector2(0f, 205f), new Vector2(760f, 100f), FontStyles.Bold, TextAlignmentOptions.Center);
        CreateImage("Title Divider", panel, AccentColor,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 146f), new Vector2(170f, 4f));
        CreateText("Subtitle", panel, "Every message leaves a trace.", 24f, MutedTextColor,
            new Vector2(0f, 105f), new Vector2(620f, 50f), FontStyles.Normal, TextAlignmentOptions.Center);

        CreateButton(panel, "Start Game", new Vector2(0f, 30f), AccentColor, StartGame);
        CreateButton(panel, "Days", new Vector2(0f, -48f), SecondaryColor, ShowDays);
        CreateButton(panel, "Settings", new Vector2(0f, -126f), SecondaryColor, ShowSettings);
        CreateButton(panel, "Exit", new Vector2(0f, -204f), ExitColor, ExitGame);

        CreateText("Footer", panel, "CHOICES  •  TRUST  •  DIGITAL SAFETY", 17f, MutedTextColor,
            new Vector2(0f, -295f), new Vector2(620f, 40f), FontStyles.Normal, TextAlignmentOptions.Center);
    }

    private void BuildSettingsPanel(RectTransform panel)
    {
        CreateText("Settings Title", panel, "SETTINGS", 48f, TextColor,
            new Vector2(0f, 265f), new Vector2(620f, 80f), FontStyles.Bold, TextAlignmentOptions.Center);
        CreateImage("Settings Divider", panel, AccentColor,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(120f, 4f));

        CreateVolumeSlider(panel, "Master Volume", 135f, MasterVolumeKey, 1f, value => AudioListener.volume = value);
        CreateVolumeSlider(panel, "Music Volume", 50f, MusicVolumeKey, 0.35f, null);
        CreateVolumeSlider(panel, "SFX Volume", -35f, SfxVolumeKey, 1f, null);
        CreateVolumeSlider(panel, "Voice Volume", -120f, VoiceVolumeKey, 1f, null);
        CreateButton(panel, "Back", new Vector2(0f, -235f), SecondaryColor, ShowMainMenu);
    }

    private void BuildDaysPanel(RectTransform panel)
    {
        CreateText("Days Title", panel, "SELECT DAY", 48f, TextColor,
            new Vector2(0f, 285f), new Vector2(620f, 70f), FontStyles.Bold, TextAlignmentOptions.Center);
        CreateText("Days Help", panel, "Testing shortcut — skipped choices are prepared automatically.", 19f, MutedTextColor,
            new Vector2(0f, 235f), new Vector2(620f, 48f), FontStyles.Normal, TextAlignmentOptions.Center);
        CreateImage("Days Divider", panel, AccentColor,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 205f), new Vector2(120f, 4f));

        CreateButton(panel, "Day 1 — Start", new Vector2(0f, 140f), AccentColor, () => SelectDay(1));
        CreateButton(panel, "Day 2", new Vector2(0f, 68f), SecondaryColor, () => SelectDay(2));
        CreateButton(panel, "Day 3", new Vector2(0f, -4f), SecondaryColor, () => SelectDay(3));
        CreateButton(panel, "Day 4", new Vector2(0f, -76f), SecondaryColor, () => SelectDay(4));
        CreateButton(panel, "Day 5", new Vector2(0f, -148f), SecondaryColor, () => SelectDay(5));
        CreateButton(panel, "Back", new Vector2(0f, -255f), ExitColor, ShowMainMenu);
    }

    private void BuildRoutePanel(RectTransform panel)
    {
        CreateText("Route Title", panel, "PREVIOUS CHOICES", 46f, TextColor,
            new Vector2(0f, 265f), new Vector2(620f, 70f), FontStyles.Bold, TextAlignmentOptions.Center);
        _routeDayLabel = CreateText("Route Day", panel, "Preparing Day 5", 23f, AccentColor,
            new Vector2(0f, 205f), new Vector2(620f, 45f), FontStyles.Bold, TextAlignmentOptions.Center);
        CreateText("Route Help", panel, "Choose how every earlier decision should be counted.", 19f, MutedTextColor,
            new Vector2(0f, 160f), new Vector2(620f, 45f), FontStyles.Normal, TextAlignmentOptions.Center);

        CreateButton(panel, "Safe Route", new Vector2(0f, 70f), AccentColor,
            () => BeginSkippedDay(DayManager.TestRoute.Safe));
        CreateButton(panel, "Neutral Route", new Vector2(0f, -18f), SecondaryColor,
            () => BeginSkippedDay(DayManager.TestRoute.Neutral));
        CreateButton(panel, "Risky Route", new Vector2(0f, -106f), ExitColor,
            () => BeginSkippedDay(DayManager.TestRoute.Risky));
        CreateButton(panel, "Back", new Vector2(0f, -235f), SecondaryColor, ShowDays);
    }

    private static Canvas CreateCanvas()
    {
        GameObject canvasObject = new("Main Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static GameObject CreatePanel(string name, RectTransform parent)
    {
        RectTransform panel = CreateImage(name, parent, PanelColor,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 720f));
        return panel.gameObject;
    }

    private static RectTransform CreateImage(string name, Transform parent, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject imageObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.layer = 5;
        imageObject.transform.SetParent(parent, false);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, string content,
        float fontSize, Color color, Vector2 position, Vector2 size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject textObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.layer = 5;
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(12f, fontSize * 0.58f);
        text.fontSizeMax = fontSize;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(Transform parent, string label, Vector2 position,
        Color normalColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new(label + " Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.layer = 5;
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(390f, 66f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = normalColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = normalColor == AccentColor ? AccentHoverColor : normalColor * 1.18f;
        colors.pressedColor = normalColor * 0.82f;
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(normalColor.r, normalColor.g, normalColor.b, 0.45f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        CreateText(label + " Label", buttonObject.transform, label.ToUpperInvariant(), 24f, TextColor,
            Vector2.zero, new Vector2(360f, 58f), FontStyles.Bold, TextAlignmentOptions.Center);
        return button;
    }

    private static void CreateVolumeSlider(RectTransform parent, string label, float y,
        string preferenceKey, float fallback, UnityEngine.Events.UnityAction<float> extraAction)
    {
        CreateText(label + " Label", parent, label, 22f, TextColor,
            new Vector2(-170f, y + 18f), new Vector2(260f, 40f), FontStyles.Normal, TextAlignmentOptions.Left);

        TextMeshProUGUI valueLabel = CreateText(label + " Value", parent, "100%", 18f, MutedTextColor,
            new Vector2(240f, y + 18f), new Vector2(90f, 40f), FontStyles.Normal, TextAlignmentOptions.Right);

        GameObject sliderObject = new(label + " Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.layer = 5;
        sliderObject.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = sliderRect.anchorMax = sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.anchoredPosition = new Vector2(25f, y - 12f);
        sliderRect.sizeDelta = new Vector2(500f, 30f);

        RectTransform track = CreateImage("Track", sliderRect, new Color(0.18f, 0.24f, 0.29f, 1f),
            new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(0f, 8f));

        RectTransform fillArea = new GameObject("Fill Area", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.gameObject.layer = 5;
        fillArea.SetParent(sliderRect, false);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.offsetMin = new Vector2(0f, -4f);
        fillArea.offsetMax = new Vector2(-10f, 4f);

        RectTransform fill = CreateImage("Fill", fillArea, AccentColor,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        RectTransform handleArea = new GameObject("Handle Slide Area", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.gameObject.layer = 5;
        handleArea.SetParent(sliderRect, false);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(10f, 0f);
        handleArea.offsetMax = new Vector2(-10f, 0f);

        RectTransform handle = CreateImage("Handle", handleArea, TextColor,
            new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(22f, 28f));

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;

        float initialValue = Mathf.Clamp01(PlayerPrefs.GetFloat(preferenceKey, fallback));
        slider.SetValueWithoutNotify(initialValue);
        valueLabel.text = Mathf.RoundToInt(initialValue * 100f) + "%";
        slider.onValueChanged.AddListener(value =>
        {
            PlayerPrefs.SetFloat(preferenceKey, value);
            PlayerPrefs.Save();
            valueLabel.text = Mathf.RoundToInt(value * 100f) + "%";
            extraAction?.Invoke(value);
        });
        track.SetAsFirstSibling();
    }

    private static void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        GameObject eventSystemObject = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventSystemObject.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    private static void StartGame()
    {
        DayManager.ClearQueuedTestStart();
        SceneManager.LoadScene("Home");
    }

    private void SelectDay(int day)
    {
        _selectedDay = Mathf.Clamp(day, 1, 5);

        if (_selectedDay == 1)
        {
            StartGame();
            return;
        }

        if (_routeDayLabel != null)
        {
            _routeDayLabel.text = $"Preparing Day {_selectedDay}";
        }

        ShowOnly(_routePanel);
    }

    private void BeginSkippedDay(DayManager.TestRoute route)
    {
        DayManager.QueueTestStart(_selectedDay, route);
        SceneManager.LoadScene("Home");
    }

    private void ShowSettings()
    {
        ShowOnly(_settingsPanel);
    }

    private void ShowDays()
    {
        ShowOnly(_daysPanel);
    }

    private void ShowMainMenu()
    {
        ShowOnly(_mainPanel);
    }

    private void ShowOnly(GameObject panelToShow)
    {
        _mainPanel.SetActive(panelToShow == _mainPanel);
        _settingsPanel.SetActive(panelToShow == _settingsPanel);
        _daysPanel.SetActive(panelToShow == _daysPanel);
        _routePanel.SetActive(panelToShow == _routePanel);
    }

    private static void ExitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
