using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.SceneManagement;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    [Header("UI Reference")]
    [Tooltip("Assign a full-screen black image here. Ensure Raycast Target is OFF.")]
    [SerializeField] private Image _fadeImage;
    [SerializeField] private TextMeshProUGUI _transitionMessage;

    [Header("Settings")]
    [SerializeField] private float _fadeDuration = 1.0f;
    [SerializeField] private float _blackScreenDuration = 1.5f;

    [Header("Scene 1 Opening")]
    [Tooltip("Comic images shown in order before the player gains control in Luna's bedroom.")]
    [SerializeField] private Sprite[] _scene1OpeningFrames;
    [Tooltip("Duration for each comic frame. Missing entries use five seconds.")]
    [SerializeField] private float[] _scene1OpeningFrameDurations;
    [Tooltip("How long each new comic frame takes to fade in over the previous frame.")]
    [SerializeField, Min(0f)] private float _scene1OpeningFrameFadeDuration = 0.75f;

    [Header("Scene 1 Opening Audio")]
    [SerializeField] private AudioClip _scene1AlarmClip;
    [Tooltip("Seconds after the opening comic begins before the alarm starts.")]
    [SerializeField, Min(0f)] private float _scene1AlarmDelay = 5f;
    [Tooltip("How long the alarm is allowed to play before it is stopped.")]
    [SerializeField, Min(0f)] private float _scene1AlarmDuration = 11f;

    private bool _isTransitioning;
    private bool _showScene1Opening;
    private Image _scene1OpeningImage;
    private Image _scene1OpeningIncomingImage;
    private AudioSource _scene1AlarmSource;
    private Coroutine _scene1AlarmRoutine;

    public bool IsTransitioning => _isTransitioning;

    private void Awake()
    {
        ConfigureTransitionCanvas();
        ConfigureFullScreenFade();

        if (Instance == null)
        {
            Instance = this;
            EnsureTransitionMessage();
            PrepareScene1Opening();
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnValidate()
    {
        ConfigureFullScreenFade();
    }

    private void ConfigureFullScreenFade()
    {
        if (_fadeImage == null) return;

        RectTransform managerRect = transform as RectTransform;
        if (managerRect != null && managerRect.parent is RectTransform)
        {
            managerRect.anchorMin = Vector2.zero;
            managerRect.anchorMax = Vector2.one;
            managerRect.pivot = new Vector2(0.5f, 0.5f);
            managerRect.anchoredPosition = Vector2.zero;
            managerRect.sizeDelta = Vector2.zero;
            managerRect.localScale = Vector3.one;
        }

        RectTransform fadeRect = _fadeImage.rectTransform;
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.pivot = new Vector2(0.5f, 0.5f);
        fadeRect.anchoredPosition = Vector2.zero;
        fadeRect.sizeDelta = Vector2.zero;
        fadeRect.localScale = Vector3.one;
    }

    private void ConfigureTransitionCanvas()
    {
        Canvas transitionCanvas = GetComponent<Canvas>();
        if (transitionCanvas != null)
        {
            transitionCanvas.overrideSorting = true;
            transitionCanvas.sortingOrder = 32760;
        }
    }

    private void EnsureTransitionMessage()
    {
        if (_fadeImage == null) return;

        if (_transitionMessage == null)
        {
            Transform existingMessage = _fadeImage.transform.Find("TransitionMessage");
            if (existingMessage != null)
            {
                _transitionMessage = existingMessage.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_transitionMessage == null)
        {
            GameObject messageObject = new GameObject(
                "TransitionMessage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            messageObject.layer = _fadeImage.gameObject.layer;
            messageObject.transform.SetParent(_fadeImage.transform, false);
            _transitionMessage = messageObject.GetComponent<TextMeshProUGUI>();
        }

        RectTransform messageRect = _transitionMessage.rectTransform;
        messageRect.anchorMin = Vector2.zero;
        messageRect.anchorMax = Vector2.one;
        messageRect.pivot = new Vector2(0.5f, 0.5f);
        messageRect.anchoredPosition = Vector2.zero;
        messageRect.sizeDelta = Vector2.zero;
        messageRect.localScale = Vector3.one;

        _transitionMessage.alignment = TextAlignmentOptions.Center;
        _transitionMessage.fontSize = 72f;
        _transitionMessage.enableAutoSizing = true;
        _transitionMessage.fontSizeMin = 26f;
        _transitionMessage.fontSizeMax = 72f;
        _transitionMessage.fontStyle = FontStyles.Normal;
        _transitionMessage.richText = true;
        _transitionMessage.margin = new Vector4(120f, 80f, 120f, 80f);
        _transitionMessage.color = new Color(1f, 1f, 1f, 0f);
        _transitionMessage.raycastTarget = false;
        _transitionMessage.text = string.Empty;
        _transitionMessage.gameObject.SetActive(false);
        _transitionMessage.transform.SetAsLastSibling();
    }

    private void SetTransitionMessage(string message)
    {
        EnsureTransitionMessage();
        if (_transitionMessage == null) return;

        bool showMessage = !string.IsNullOrWhiteSpace(message);
        _transitionMessage.text = showMessage ? message : string.Empty;
        _transitionMessage.gameObject.SetActive(showMessage);

        Color messageColor = _transitionMessage.color;
        messageColor.a = 0f;
        _transitionMessage.color = messageColor;
    }

    private void Start()
    {
        EventBus.Instance.Subscribe("SleepTransition", OnSleepTransition);
        EventBus.Instance.Subscribe("FadeOut", StartFadeOut);
        EventBus.Instance.Subscribe("FadeIn", StartFadeIn);

        if (_showScene1Opening)
        {
            StartCoroutine(Scene1OpeningRoutine());
        }
    }

    private void PrepareScene1Opening()
    {
        if (_fadeImage == null ||
            SceneManager.GetActiveScene().name != "Home" ||
            DayManager.ShouldSkipSceneOneOpening)
        {
            return;
        }

        _showScene1Opening = true;
        SetTransitionMessage(null);
        EnsureScene1OpeningImage();
        _fadeImage.gameObject.SetActive(true);

        Color fadeColor = _fadeImage.color;
        fadeColor.a = 1f;
        _fadeImage.color = fadeColor;
        SetScene1OpeningImageAlpha(0f);
    }

    private IEnumerator Scene1OpeningRoutine()
    {
        _isTransitioning = true;
        StartScene1Alarm();

        if (InputManager.Instance != null)
        {
            InputManager.Instance.DisableMovementMap();
            InputManager.Instance.DisableInteractionsMap();
        }

        if (_scene1OpeningImage != null && _scene1OpeningFrames != null)
        {
            for (int frameIndex = 0; frameIndex < _scene1OpeningFrames.Length; frameIndex++)
            {
                Sprite frame = _scene1OpeningFrames[frameIndex];
                if (frame == null) continue;

                yield return ShowScene1OpeningFrame(
                    frame,
                    GetScene1OpeningFrameDuration(frameIndex));
            }
        }

        yield return FadeRoutine(1f, 0f);
        HideScene1OpeningImage();
        SetTransitionMessage(null);

        if (InputManager.Instance != null)
        {
            InputManager.Instance.EnableInteractionsMap();
            InputManager.Instance.EnableMovementMap();
        }

        _showScene1Opening = false;
        _isTransitioning = false;
    }

    private void StartScene1Alarm()
    {
        if (_scene1AlarmClip == null) return;

        if (_scene1AlarmRoutine != null)
        {
            StopCoroutine(_scene1AlarmRoutine);
        }

        _scene1AlarmRoutine = StartCoroutine(Scene1AlarmRoutine());
    }

    private IEnumerator Scene1AlarmRoutine()
    {
        if (_scene1AlarmDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(_scene1AlarmDelay);
        }

        EnsureScene1AlarmSource();
        _scene1AlarmSource.clip = _scene1AlarmClip;
        _scene1AlarmSource.volume = GameManager.GetSavedSfxVolume();
        _scene1AlarmSource.loop = true;
        _scene1AlarmSource.Play();

        if (_scene1AlarmDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(_scene1AlarmDuration);
        }

        _scene1AlarmSource.Stop();
        _scene1AlarmSource.loop = false;
        _scene1AlarmRoutine = null;
    }

    private void EnsureScene1AlarmSource()
    {
        if (_scene1AlarmSource == null)
        {
            _scene1AlarmSource = GetComponent<AudioSource>();
        }

        if (_scene1AlarmSource == null)
        {
            _scene1AlarmSource = gameObject.AddComponent<AudioSource>();
        }

        _scene1AlarmSource.playOnAwake = false;
        _scene1AlarmSource.loop = false;
        _scene1AlarmSource.spatialBlend = 0f;
        _scene1AlarmSource.ignoreListenerPause = true;
    }

    private void EnsureScene1OpeningImage()
    {
        if (_fadeImage == null) return;

        if (_scene1OpeningImage == null)
        {
            _scene1OpeningImage = GetOrCreateScene1OpeningImage("Scene1OpeningComic");
        }

        if (_scene1OpeningIncomingImage == null)
        {
            _scene1OpeningIncomingImage = GetOrCreateScene1OpeningImage("Scene1OpeningComicIncoming");
        }

        _scene1OpeningImage.transform.SetAsFirstSibling();
        _scene1OpeningIncomingImage.transform.SetSiblingIndex(1);
        _scene1OpeningIncomingImage.sprite = null;
        _scene1OpeningIncomingImage.gameObject.SetActive(false);
    }

    private Image GetOrCreateScene1OpeningImage(string objectName)
    {
        Transform existingImage = _fadeImage.transform.Find(objectName);
        Image openingImage = existingImage != null ? existingImage.GetComponent<Image>() : null;

        if (openingImage == null)
        {
            GameObject imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.layer = _fadeImage.gameObject.layer;
            imageObject.transform.SetParent(_fadeImage.transform, false);
            openingImage = imageObject.GetComponent<Image>();
        }

        RectTransform imageRect = openingImage.rectTransform;
        imageRect.anchorMin = Vector2.zero;
        imageRect.anchorMax = Vector2.one;
        imageRect.pivot = new Vector2(0.5f, 0.5f);
        imageRect.anchoredPosition = Vector2.zero;
        imageRect.sizeDelta = Vector2.zero;
        imageRect.localScale = Vector3.one;

        openingImage.type = Image.Type.Simple;
        openingImage.preserveAspect = true;
        openingImage.raycastTarget = false;
        openingImage.color = Color.white;
        return openingImage;
    }

    private IEnumerator ShowScene1OpeningFrame(Sprite frame, float frameDuration)
    {
        float fadeDuration = Mathf.Min(
            Mathf.Max(0f, _scene1OpeningFrameFadeDuration),
            Mathf.Max(0f, frameDuration));

        if (_scene1OpeningImage.sprite == null)
        {
            _scene1OpeningImage.sprite = frame;
            _scene1OpeningImage.gameObject.SetActive(true);
            SetImageAlpha(_scene1OpeningImage, 0f);
            yield return FadeImageAlpha(_scene1OpeningImage, 0f, 1f, fadeDuration);
        }
        else
        {
            _scene1OpeningIncomingImage.sprite = frame;
            _scene1OpeningIncomingImage.gameObject.SetActive(true);
            _scene1OpeningIncomingImage.transform.SetAsLastSibling();
            SetImageAlpha(_scene1OpeningIncomingImage, 0f);

            yield return FadeImageAlpha(_scene1OpeningIncomingImage, 0f, 1f, fadeDuration);

            Image previousImage = _scene1OpeningImage;
            _scene1OpeningImage = _scene1OpeningIncomingImage;
            _scene1OpeningIncomingImage = previousImage;
            _scene1OpeningIncomingImage.sprite = null;
            _scene1OpeningIncomingImage.gameObject.SetActive(false);
        }

        float remainingDuration = Mathf.Max(0f, frameDuration - fadeDuration);
        if (remainingDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(remainingDuration);
        }
    }

    private static IEnumerator FadeImageAlpha(Image image, float startAlpha, float endAlpha, float duration)
    {
        if (image == null) yield break;

        if (duration <= 0f)
        {
            SetImageAlpha(image, endAlpha);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetImageAlpha(image, Mathf.Lerp(startAlpha, endAlpha, elapsed / duration));
            yield return null;
        }

        SetImageAlpha(image, endAlpha);
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;

        Color imageColor = image.color;
        imageColor.a = alpha;
        image.color = imageColor;
    }

    private float GetScene1OpeningFrameDuration(int frameIndex)
    {
        if (_scene1OpeningFrameDurations != null &&
            frameIndex >= 0 &&
            frameIndex < _scene1OpeningFrameDurations.Length)
        {
            return Mathf.Max(0f, _scene1OpeningFrameDurations[frameIndex]);
        }

        return 5f;
    }

    private void SetScene1OpeningImageAlpha(float alpha)
    {
        if (_scene1OpeningImage == null || !_scene1OpeningImage.gameObject.activeSelf) return;

        SetImageAlpha(_scene1OpeningImage, alpha);
    }

    private void HideScene1OpeningImage()
    {
        if (_scene1OpeningImage == null) return;

        _scene1OpeningImage.sprite = null;
        _scene1OpeningImage.gameObject.SetActive(false);

        if (_scene1OpeningIncomingImage != null)
        {
            _scene1OpeningIncomingImage.sprite = null;
            _scene1OpeningIncomingImage.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (_scene1AlarmRoutine != null)
        {
            StopCoroutine(_scene1AlarmRoutine);
            _scene1AlarmRoutine = null;
        }

        if (_scene1AlarmSource != null)
        {
            _scene1AlarmSource.Stop();
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("SleepTransition", OnSleepTransition);
            EventBus.Instance.Unsubscribe("FadeOut", StartFadeOut);
            EventBus.Instance.Unsubscribe("FadeIn", StartFadeIn);
        }
    }

    private void StartFadeOut()
    {
        SetTransitionMessage(null);
        StartCoroutine(FadeRoutine(0f, 1f));
    }

    private void StartFadeIn()
    {
        StartCoroutine(FadeRoutine(1f, 0f));
    }

    private void OnSleepTransition()
    {
        StartCoroutine(SleepTransitionRoutine());
    }

    public void PlaySchoolClass(string completionEvent)
    {
        if (_isTransitioning) return;
        StartCoroutine(SchoolClassRoutine(completionEvent));
    }

    public void PlayMessageCard(string message, float holdDuration)
    {
        if (_isTransitioning)
        {
            ReturnPlayerToIdle();
            return;
        }

        StartCoroutine(MessageCardRoutine(message, Mathf.Max(0f, holdDuration)));
    }

    private IEnumerator MessageCardRoutine(string message, float holdDuration)
    {
        _isTransitioning = true;

        if (InputManager.Instance != null)
        {
            InputManager.Instance.DisableMovementMap();
            InputManager.Instance.DisableInteractionsMap();
        }

        SetTransitionMessage(message);
        yield return FadeRoutine(0f, 1f);
        yield return new WaitForSecondsRealtime(holdDuration);
        yield return FadeRoutine(1f, 0f);
        SetTransitionMessage(null);

        if (InputManager.Instance != null)
        {
            InputManager.Instance.EnableInteractionsMap();
            InputManager.Instance.EnableMovementMap();
        }

        ReturnPlayerToIdle();
        _isTransitioning = false;
    }

    private void ReturnPlayerToIdle()
    {
        PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
        if (playerState != null)
        {
            playerState.ChangeState(PlayerState.Idle);
        }
    }

    private IEnumerator SchoolClassRoutine(string completionEvent)
    {
        _isTransitioning = true;
        InputManager.Instance.DisableMovementMap();
        InputManager.Instance.DisableInteractionsMap();

        SetTransitionMessage("<b>5 Hours Later</b>");
        yield return FadeRoutine(0f, 1f);
        yield return new WaitForSeconds(_blackScreenDuration);
        yield return FadeRoutine(1f, 0f);
        SetTransitionMessage(null);

        if (!string.IsNullOrEmpty(completionEvent) && EventBus.Instance != null)
        {
            EventBus.Instance.Publish(completionEvent);
        }

        InputManager.Instance.EnableInteractionsMap();
        InputManager.Instance.EnableMovementMap();

        PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
        if (playerState != null)
        {
            playerState.ChangeState(PlayerState.Idle);
        }

        _isTransitioning = false;
    }

    private IEnumerator SleepTransitionRoutine()
    {
        InputManager.Instance.DisableMovementMap();
        InputManager.Instance.DisableInteractionsMap();

        // Fade Out
        SetTransitionMessage("<b>ZzzZzzzz</b>");
        yield return FadeRoutine(0f, 1f);

        // Wait in black and trigger next day
        EventBus.Instance.Publish("NextDay");
        yield return new WaitForSeconds(_blackScreenDuration);

        // Fade In
        yield return FadeRoutine(1f, 0f);
        SetTransitionMessage(null);

        InputManager.Instance.EnableInteractionsMap();
        InputManager.Instance.EnableMovementMap();
    }

    private IEnumerator FadeRoutine(float startAlpha, float endAlpha)
    {
        if (_fadeImage == null) yield break;

        _fadeImage.gameObject.SetActive(true);
        float elapsed = 0f;
        Color c = _fadeImage.color;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(startAlpha, endAlpha, elapsed / _fadeDuration);
            _fadeImage.color = c;
            SetMessageAlpha(c.a);
            SetScene1OpeningImageAlpha(c.a);
            yield return null;
        }

        c.a = endAlpha;
        _fadeImage.color = c;
        SetMessageAlpha(c.a);
        SetScene1OpeningImageAlpha(c.a);

    }

    private void SetMessageAlpha(float alpha)
    {
        if (_transitionMessage == null || !_transitionMessage.gameObject.activeSelf) return;

        Color messageColor = _transitionMessage.color;
        messageColor.a = alpha;
        _transitionMessage.color = messageColor;
    }
}
