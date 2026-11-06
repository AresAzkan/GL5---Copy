using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Identifies applications that can dispatch notifications to the phone.
/// Extend this enum as new apps are created in the future.
/// </summary>
public enum PhoneApp
{
    Messages,
    Connect,
    Mail,
    Notes,
    Calendar,
    Bank,
    Social
}

[Serializable]
public class NotificationData
{
    public string AppName;
    public string Message;
    public Sprite AppIcon;
    public float Duration = 5f;

    public NotificationData(string appName, string message, Sprite appIcon = null, float duration = 5f)
    {
        AppName = appName;
        Message = message;
        AppIcon = appIcon;
        Duration = duration;
    }

    public NotificationData(PhoneApp app, string message, Sprite appIcon = null, float duration = 5f)
    {
        AppName = app.ToString();
        Message = message;
        AppIcon = appIcon;
        Duration = duration;
    }
}

[Serializable]
public class AppNotificationConfig
{
    public PhoneApp App;
    public string DisplayName = "Messages";
    public Sprite AppIcon;
}

/// <summary>
/// Manages the on-screen Phone Notifications banner.
/// Scalable to support notifications from any present or future app.
/// </summary>
public class PhoneNotificationController : MonoBehaviour
{
    private const float MinimumPanelHeight = 170f;
    private const float MessageHorizontalPadding = 20f;
    private const float MessageTopOffset = 92f;
    private const float MessageMinimumHeight = 58f;
    private const float MessageBottomPadding = 20f;

    private static PhoneNotificationController _instance;
    public static PhoneNotificationController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<PhoneNotificationController>();
                if (_instance == null)
                {
                    // Search in loaded scene objects (including inactive)
                    GameObject notifGO = GameObject.Find("PhoneNotifications");
                    if (notifGO == null)
                    {
                        var allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
                        foreach (var c in allCanvases)
                        {
                            if (c.gameObject.scene.isLoaded && c.gameObject.name == "PhoneNotifications")
                            {
                                notifGO = c.gameObject;
                                break;
                            }
                        }
                    }

                    if (notifGO != null)
                    {
                        _instance = notifGO.GetComponent<PhoneNotificationController>();
                        if (_instance == null)
                        {
                            _instance = notifGO.AddComponent<PhoneNotificationController>();
                        }
                    }
                }
            }
            return _instance;
        }
        private set
        {
            _instance = value;
        }
    }

    [Header("UI Elements")]
    [SerializeField] private TMP_Text _appNameText;
    [SerializeField] private TMP_Text _notificationMessageText;
    [SerializeField] private Image _appLogoImage;
    [SerializeField] private Image _bgImage;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _panelRect;

    [Header("Animation Settings")]
    [SerializeField] private float _stayDuration = 5f;
    [SerializeField] private float _slideDuration = 0.45f;
    [SerializeField] private float _slideDistance = 600f;
    [SerializeField] private Ease _slideInEase = Ease.OutCubic;
    [SerializeField] private Ease _slideOutEase = Ease.InCubic;
    [SerializeField] private float _defaultDisplayDuration = 5f;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _notificationSound;

    [Header("App Configurations (Scalability)")]
    [SerializeField] private List<AppNotificationConfig> _appConfigs = new List<AppNotificationConfig>
    {
        new AppNotificationConfig { App = PhoneApp.Messages, DisplayName = "Messages" }
    };

    private readonly Queue<NotificationData> _notificationQueue = new Queue<NotificationData>();
    private Coroutine _displayCoroutine = null;
    private Vector2 _onScreenPosition;
    private Vector2 _offScreenPosition;
    private bool _isInitialized = false;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(this);
            return;
        }

        ConfigureScreenSpaceLayout();
        InitializeComponents();
    }

    private void ConfigureScreenSpaceLayout()
    {
        // This prefab used to be nested under a transformed HUD canvas, which
        // made the banner inherit scene-space rotation and resolution-specific
        // coordinates. Keep it as an independent, persistent screen overlay.
        if (transform.parent != null)
        {
            transform.SetParent(null, false);
        }

        if (transform is RectTransform rootRect)
        {
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = Vector2.zero;
            rootRect.localScale = Vector3.one;
            rootRect.localRotation = Quaternion.identity;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.worldCamera = null;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50;
        }

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<NotificationData>("ShowPhoneNotification", ShowNotification);
        }

    }

    private void OnEnable()
    {
        if (_displayCoroutine == null && _notificationQueue.Count > 0)
        {
            _displayCoroutine = StartCoroutine(ProcessNotificationQueue());
        }
    }

    private void OnDisable()
    {
        if (_panelRect != null)
        {
            _panelRect.DOKill();
        }
        if (_canvasGroup != null)
        {
            _canvasGroup.DOKill();
        }
        _displayCoroutine = null;
    }

    private void OnDestroy()
    {
        if (_panelRect != null)
        {
            _panelRect.DOKill();
        }
        if (_canvasGroup != null)
        {
            _canvasGroup.DOKill();
        }
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<NotificationData>("ShowPhoneNotification", ShowNotification);
        }
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void InitializeComponents()
    {
        if (_isInitialized) return;

        if (_appNameText == null)
            _appNameText = transform.Find("AppName")?.GetComponent<TMP_Text>()
                        ?? transform.Find("NotificationPanel/AppName")?.GetComponent<TMP_Text>();

        if (_notificationMessageText == null)
            _notificationMessageText = transform.Find("NotificationMessage")?.GetComponent<TMP_Text>()
                                    ?? transform.Find("NotificationPanel/NotificationMessage")?.GetComponent<TMP_Text>();

        if (_appLogoImage == null)
            _appLogoImage = transform.Find("AppLogo")?.GetComponent<Image>()
                         ?? transform.Find("NotificationPanel/AppLogo")?.GetComponent<Image>();

        if (_bgImage == null)
            _bgImage = transform.Find("BG")?.GetComponent<Image>()
                    ?? transform.Find("NotificationPanel/BG")?.GetComponent<Image>();

        BuildRuntimePopup();

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }

        if (_audioSource != null)
        {
            _audioSource.playOnAwake = false;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50; // Draw on top of other HUD/UI elements
        }

        if (_notificationMessageText != null)
        {
            _notificationMessageText.overflowMode = TextOverflowModes.Overflow;
            _notificationMessageText.textWrappingMode = TextWrappingModes.Normal;
            _notificationMessageText.alignment = TextAlignmentOptions.TopLeft;
        }

        SetupPanelContainer();

        _isInitialized = true;
    }

    private void BuildRuntimePopup()
    {
        // The original notification objects come from several older scene
        // layouts and do not share a reliable parent/anchor space. Rebuild the
        // visible banner once at runtime so every visual is guaranteed to be a
        // child of the same red panel.
        TMP_Text legacyAppName = _appNameText;
        TMP_Text legacyMessage = _notificationMessageText;
        Image legacyLogo = _appLogoImage;
        Image legacyBackground = _bgImage;
        RectTransform legacyPanel = _panelRect;

        Transform existingRuntime = transform.Find("RuntimeNotificationPanel");
        if (existingRuntime != null)
        {
            _panelRect = existingRuntime as RectTransform;
            _bgImage = existingRuntime.GetComponent<Image>();
            _appLogoImage = existingRuntime.Find("AppLogo")?.GetComponent<Image>();
            _appNameText = existingRuntime.Find("AppName")?.GetComponent<TMP_Text>();
            _notificationMessageText = existingRuntime.Find("NotificationMessage")?.GetComponent<TMP_Text>();
            return;
        }

        GameObject panelObject = new GameObject(
            "RuntimeNotificationPanel",
            typeof(RectTransform),
            typeof(Image));
        _panelRect = panelObject.GetComponent<RectTransform>();
        _panelRect.SetParent(transform, false);
        _bgImage = panelObject.GetComponent<Image>();
        _bgImage.color = legacyBackground != null
            ? legacyBackground.color
            : new Color32(222, 127, 132, 255);
        _bgImage.raycastTarget = false;

        GameObject logoObject = new GameObject("AppLogo", typeof(RectTransform), typeof(Image));
        logoObject.transform.SetParent(_panelRect, false);
        _appLogoImage = logoObject.GetComponent<Image>();
        _appLogoImage.sprite = legacyLogo != null ? legacyLogo.sprite : null;
        _appLogoImage.color = Color.white;
        _appLogoImage.preserveAspect = true;
        _appLogoImage.raycastTarget = false;

        GameObject appNameObject = new GameObject("AppName", typeof(RectTransform), typeof(TextMeshProUGUI));
        appNameObject.transform.SetParent(_panelRect, false);
        _appNameText = appNameObject.GetComponent<TextMeshProUGUI>();
        CopyTextStyle(legacyAppName, _appNameText, 26f);
        _appNameText.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject messageObject = new GameObject(
            "NotificationMessage",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        messageObject.transform.SetParent(_panelRect, false);
        _notificationMessageText = messageObject.GetComponent<TextMeshProUGUI>();
        CopyTextStyle(legacyMessage, _notificationMessageText, 25f);
        _notificationMessageText.alignment = TextAlignmentOptions.TopLeft;
        _notificationMessageText.textWrappingMode = TextWrappingModes.Normal;
        _notificationMessageText.overflowMode = TextOverflowModes.Overflow;

        // Hide every part of the old visual hierarchy. The controller itself,
        // its CanvasGroup, event subscriptions and audio remain untouched.
        if (legacyPanel != null && legacyPanel != transform && legacyPanel != _panelRect)
        {
            legacyPanel.gameObject.SetActive(false);
        }
        DisableLegacyVisual(legacyAppName, panelObject);
        DisableLegacyVisual(legacyMessage, panelObject);
        DisableLegacyVisual(legacyLogo, panelObject);
        DisableLegacyVisual(legacyBackground, panelObject);
    }

    private static void CopyTextStyle(TMP_Text source, TMP_Text target, float fallbackFontSize)
    {
        if (target == null) return;

        target.font = source != null && source.font != null
            ? source.font
            : TMP_Settings.defaultFontAsset;
        if (source != null && source.fontSharedMaterial != null)
        {
            target.fontSharedMaterial = source.fontSharedMaterial;
        }
        target.fontSize = source != null && source.fontSize > 0f
            ? source.fontSize
            : fallbackFontSize;
        target.fontStyle = source != null ? source.fontStyle : FontStyles.Normal;
        target.color = source != null ? source.color : Color.white;
        target.enableAutoSizing = false;
        target.richText = true;
        target.raycastTarget = false;
    }

    private static void DisableLegacyVisual(Component component, GameObject runtimePanel)
    {
        if (component == null || component.gameObject == runtimePanel) return;
        if (component.transform.IsChildOf(runtimePanel.transform)) return;

        // Some legacy Graphics live directly on the persistent Canvas object.
        // Disable only that visual component so the Canvas/controller survives.
        if (component is Behaviour behaviour)
        {
            behaviour.enabled = false;
            return;
        }

        component.gameObject.SetActive(false);
    }

    private void SetupPanelContainer()
    {
        if (_panelRect == null)
        {
            Transform existing = transform.Find("NotificationPanel");
            if (existing != null)
            {
                _panelRect = existing as RectTransform;
            }
            else
            {
                GameObject panelGO = new GameObject("NotificationPanel", typeof(RectTransform));
                _panelRect = panelGO.GetComponent<RectTransform>();
                _panelRect.SetParent(transform, false);
                _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                _panelRect.pivot = new Vector2(0.5f, 0.5f);
                _panelRect.anchoredPosition = Vector2.zero;
                _panelRect.sizeDelta = Vector2.zero;
                _panelRect.localScale = Vector3.one;
                _panelRect.localRotation = Quaternion.identity;

                List<Transform> children = new List<Transform>();
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child != _panelRect.transform)
                    {
                        children.Add(child);
                    }
                }
                foreach (var child in children)
                {
                    child.SetParent(_panelRect, false);
                }
            }
        }

        ConfigurePanelLayout();

        _onScreenPosition = _panelRect.anchoredPosition;
        _offScreenPosition = _onScreenPosition + new Vector2(_slideDistance, 0f);

        // Start hidden offscreen to the right
        _panelRect.anchoredPosition = _offScreenPosition;
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    private void ConfigurePanelLayout()
    {
        if (_panelRect == null) return;

        // Pin the complete banner to the upper-right corner of the reference
        // resolution. CanvasScaler then keeps that relationship on every
        // monitor and aspect ratio.
        _panelRect.anchorMin = Vector2.one;
        _panelRect.anchorMax = Vector2.one;
        _panelRect.pivot = Vector2.one;
        _panelRect.anchoredPosition = new Vector2(-40f, -40f);
        _panelRect.sizeDelta = new Vector2(440f, 170f);
        _panelRect.localScale = Vector3.one;
        _panelRect.localRotation = Quaternion.identity;

        // Legacy banners used a separate BG child. The rebuilt banner puts the
        // Image directly on the panel, so never stretch that RectTransform or
        // it would turn the whole panel into a fullscreen overlay.
        if (_bgImage != null && _bgImage.rectTransform != _panelRect)
        {
            ConfigureRect(_bgImage.rectTransform,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }
        ConfigureRect(_appLogoImage != null ? _appLogoImage.rectTransform : null,
            Vector2.up, Vector2.up, Vector2.up, new Vector2(20f, -20f), new Vector2(64f, 64f));
        ConfigureRect(_appNameText != null ? _appNameText.rectTransform : null,
            Vector2.up, Vector2.up, Vector2.up, new Vector2(100f, -18f), new Vector2(315f, 52f));
        ConfigureRect(_notificationMessageText != null ? _notificationMessageText.rectTransform : null,
            Vector2.up, Vector2.up, Vector2.up,
            new Vector2(MessageHorizontalPadding, -MessageTopOffset),
            new Vector2(440f - (MessageHorizontalPadding * 2f), MessageMinimumHeight));
    }

    private static void ConfigureRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        if (rect == null) return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    #region Public API

    public void ShowNotification(PhoneApp app, string message, Sprite appIcon = null, float duration = -1f)
    {
        string appName = GetAppDisplayName(app);
        Sprite icon = appIcon != null ? appIcon : GetDefaultAppIcon(app);
        float dur = duration > 0f ? duration : _stayDuration;
        ShowNotification(new NotificationData(appName, message, icon, dur));
    }

    public void ShowNotification(string appName, string message, Sprite appIcon = null, float duration = -1f)
    {
        float dur = duration > 0f ? duration : _stayDuration;
        ShowNotification(new NotificationData(appName, message, appIcon, dur));
    }

    public void ShowNotification(NotificationData data)
    {
        if (data == null) return;

        InitializeComponents();
        _notificationQueue.Enqueue(data);

        if (_displayCoroutine == null && gameObject.activeInHierarchy)
        {
            _displayCoroutine = StartCoroutine(ProcessNotificationQueue());
        }
    }

    #endregion

    private IEnumerator ProcessNotificationQueue()
    {
        while (_notificationQueue.Count > 0)
        {
            NotificationData data = _notificationQueue.Dequeue();
            yield return StartCoroutine(DisplayNotification(data));
        }
        _displayCoroutine = null;
    }

    private IEnumerator DisplayNotification(NotificationData data)
    {
        // 1. Set AppName
        if (_appNameText != null)
        {
            _appNameText.text = data.AppName;
        }

        // 2. Set AppLogo
        if (_appLogoImage != null)
        {
            Sprite icon = data.AppIcon != null ? data.AppIcon : GetDefaultAppIconByName(data.AppName);
            if (icon != null)
            {
                _appLogoImage.sprite = icon;
                _appLogoImage.enabled = true;
            }
        }

        // 3. Set the complete message and grow the banner downward to fit it.
        if (_notificationMessageText != null)
        {
            SetNotificationMessageAndResize(data.Message);
        }

        // 4. Play audio (if assigned)
        PlayNotificationSound();

        // 5. Setup initial position for slide-in from right
        if (_panelRect != null)
        {
            _panelRect.DOKill();
            _panelRect.anchoredPosition = _offScreenPosition;
        }

        _canvasGroup.DOKill();
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        // 6. Slide In: appear moving from the right into the current position
        if (_panelRect != null)
        {
            _panelRect.DOAnchorPos(_onScreenPosition, _slideDuration).SetEase(_slideInEase);
        }

        yield return new WaitForSeconds(_slideDuration);

        // 7. Stay visible for 2 seconds (or configured duration) in its final position
        float displayTime = data.Duration > 0f ? data.Duration : _stayDuration;
        yield return new WaitForSeconds(displayTime);

        // 8. Slide Out: hide moving back towards the right
        if (_panelRect != null)
        {
            _panelRect.DOAnchorPos(_offScreenPosition, _slideDuration).SetEase(_slideOutEase);
        }

        yield return new WaitForSeconds(_slideDuration);

        // 9. Fully hidden
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
    }

    private void SetNotificationMessageAndResize(string rawMessage)
    {
        if (_notificationMessageText == null || _panelRect == null) return;

        string cleanMessage = string.IsNullOrWhiteSpace(rawMessage)
            ? string.Empty
            : rawMessage.Replace("\r\n", " ").Replace("\n", " ").Trim();

        _notificationMessageText.overflowMode = TextOverflowModes.Overflow;
        _notificationMessageText.textWrappingMode = TextWrappingModes.Normal;
        _notificationMessageText.alignment = TextAlignmentOptions.TopLeft;
        _notificationMessageText.text = cleanMessage;

        RectTransform messageRect = _notificationMessageText.rectTransform;

        // The notification elements can originate from older scene hierarchies.
        // Always make the message a child of the red panel and rebuild its bounds
        // so text cannot drift outside the banner at different resolutions.
        if (messageRect.parent != _panelRect)
        {
            messageRect.SetParent(_panelRect, false);
        }

        float messageWidth = Mathf.Max(
            1f,
            _panelRect.rect.width - (MessageHorizontalPadding * 2f));
        ConfigureRect(
            messageRect,
            Vector2.up,
            Vector2.up,
            Vector2.up,
            new Vector2(MessageHorizontalPadding, -MessageTopOffset),
            new Vector2(messageWidth, MessageMinimumHeight));

        Canvas.ForceUpdateCanvases();
        _notificationMessageText.ForceMeshUpdate();

        float preferredHeight = Mathf.Ceil(
            _notificationMessageText.GetPreferredValues(cleanMessage, messageWidth, 0f).y);
        float messageHeight = Mathf.Max(MessageMinimumHeight, preferredHeight);

        messageRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, messageHeight);

        float panelHeight = Mathf.Max(
            MinimumPanelHeight,
            MessageTopOffset + messageHeight + MessageBottomPadding);
        _panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
    }

    /// <summary>
    /// Calculates how many characters physically fit inside the notification text bounds,
    /// returning only the part that fits followed by ellipsis if truncated.
    /// </summary>
    public string TruncateMessageToFit(TMP_Text textComponent, string rawMessage)
    {
        if (string.IsNullOrEmpty(rawMessage)) return "";

        string clean = rawMessage.Replace("\r\n", " ").Replace("\n", " ").Trim();
        if (textComponent == null) return clean;

        textComponent.text = clean;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
        textComponent.ForceMeshUpdate();

        int charCount = textComponent.textInfo.characterCount;
        if (charCount > 0 && textComponent.textInfo.characterInfo != null)
        {
            int lastVisible = -1;
            for (int i = 0; i < charCount && i < textComponent.textInfo.characterInfo.Length; i++)
            {
                if (textComponent.textInfo.characterInfo[i].isVisible)
                {
                    lastVisible = i;
                }
            }

            if (lastVisible >= 0 && lastVisible < clean.Length - 1)
            {
                return clean.Substring(0, lastVisible + 1).TrimEnd() + "...";
            }
        }

        return clean;
    }

    private void PlayNotificationSound()
    {
        if (_audioSource != null && _notificationSound != null)
        {
            _audioSource.PlayOneShot(_notificationSound, GameManager.GetSavedSfxVolume());
        }
    }

    private string GetAppDisplayName(PhoneApp app)
    {
        var config = _appConfigs.Find(c => c.App == app);
        return config != null ? config.DisplayName : app.ToString();
    }

    private Sprite GetDefaultAppIcon(PhoneApp app)
    {
        var config = _appConfigs.Find(c => c.App == app);
        return config?.AppIcon;
    }

    private Sprite GetDefaultAppIconByName(string appName)
    {
        if (_appConfigs == null) return null;
        var config = _appConfigs.Find(c => string.Equals(c.DisplayName, appName, StringComparison.OrdinalIgnoreCase)
                                        || c.App.ToString().Equals(appName, StringComparison.OrdinalIgnoreCase));
        return config?.AppIcon;
    }
}
