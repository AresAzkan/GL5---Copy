using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    [Header("UI Reference")]
    [Tooltip("Assign a full-screen black image here. Ensure Raycast Target is OFF.")]
    [SerializeField] private Image _fadeImage;

    [Header("Settings")]
    [SerializeField] private float _fadeDuration = 1.0f;
    [SerializeField] private float _blackScreenDuration = 1.5f;

    private bool _isTransitioning;

    private void Awake()
    {
        ConfigureFullScreenFade();

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

    private void Start()
    {
        EventBus.Instance.Subscribe("SleepTransition", OnSleepTransition);
        EventBus.Instance.Subscribe("FadeOut", StartFadeOut);
        EventBus.Instance.Subscribe("FadeIn", StartFadeIn);
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("SleepTransition", OnSleepTransition);
            EventBus.Instance.Unsubscribe("FadeOut", StartFadeOut);
            EventBus.Instance.Unsubscribe("FadeIn", StartFadeIn);
        }
    }

    private void StartFadeOut()
    {
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

    private IEnumerator SchoolClassRoutine(string completionEvent)
    {
        _isTransitioning = true;
        InputManager.Instance.DisableMovementMap();
        InputManager.Instance.DisableInteractionsMap();

        yield return FadeRoutine(0f, 1f);
        yield return new WaitForSeconds(_blackScreenDuration);
        yield return FadeRoutine(1f, 0f);

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
        yield return FadeRoutine(0f, 1f);

        // Wait in black and trigger next day
        EventBus.Instance.Publish("NextDay");
        yield return new WaitForSeconds(_blackScreenDuration);

        // Fade In
        yield return FadeRoutine(1f, 0f);

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
            yield return null;
        }

        c.a = endAlpha;
        _fadeImage.color = c;

    }
}
