using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private const string MasterVolumePreferenceKey = "MasterVolume";
    private const string MusicVolumePreferenceKey = "MusicVolume";
    private const string SfxVolumePreferenceKey = "SfxVolume";
    private const string VoiceVolumePreferenceKey = "VoiceVolume";
    private const string MusicMutedPreferenceKey = "MusicMuted";
    private const float FallbackChannelVolume = 1f;
    private const float FallbackMusicVolume = 0.35f;

    public static GameManager Instance { get; private set; }

    [SerializeField] private string _playerName = "Luna";

    [Header("Background Music")]
    [Tooltip("The soundtrack used while the player is in one of the enabled music scenes.")]
    [SerializeField] private AudioClip _backgroundMusic;
    [SerializeField, Range(0f, 1f)] private float _defaultMusicVolume = FallbackMusicVolume;
    [Tooltip("The soundtrack plays in these scenes. Leave empty to play it in every scene.")]
    [SerializeField] private List<string> _musicScenes = new List<string> { "Home", "School", "Map" };
    [SerializeField, Min(0f)] private float _musicFadeDuration = 0.75f;

    private AudioSource _musicSource;
    private Coroutine _musicFadeRoutine;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            AudioListener.volume = GetSavedMasterVolume();
            EnsureMusicSource();
        }
        else
        {
            Destroy(gameObject);
            return;
        }

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EventBus.Instance.Subscribe<string>("ChangeScene", ChangeScene);
        UpdateMusicForScene(SceneManager.GetActiveScene().name);
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Returns the player name
    /// </summary>
    /// <returns></returns>
    public string GetPlayerName()
    {
        return _playerName;
    }

    /// <summary>
    /// Set the player name as the given one , only in case is different from the current one
    /// </summary>
    /// <param name="name"></param>
    public void SetPlayerName(string name)
    {
        if (!string.IsNullOrWhiteSpace(name) && _playerName != name) _playerName = name;
    }

    public void ChangeScene(string sceneName)
    {
        if(SceneManager.GetActiveScene().name != sceneName) SceneManager.LoadScene(sceneName);
        //EventBus.Instance.Publish("FadeOut");
    }

    /// <summary>
    /// Method that sets what to do when a scene is loaded. The params are mandatory even if they are not used
    /// </summary>
    /// <param name="scene"></param>
    /// <param name="mode"></param>
    public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateMusicForScene(scene.name);
        //EventBus.Instance.Publish("FadeIn");
    }

    public static float GetSavedMasterVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumePreferenceKey, FallbackChannelVolume));
    }

    public static float GetSavedMusicVolume()
    {
        float fallback = Instance != null ? Instance._defaultMusicVolume : FallbackMusicVolume;
        return Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumePreferenceKey, fallback));
    }

    public static bool GetSavedMusicMuted()
    {
        return PlayerPrefs.GetInt(MusicMutedPreferenceKey, 0) == 1;
    }

    public static float GetSavedSfxVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumePreferenceKey, FallbackChannelVolume));
    }

    public static float GetSavedVoiceVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(VoiceVolumePreferenceKey, FallbackChannelVolume));
    }

    public void SetMasterVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MasterVolumePreferenceKey, volume);
        PlayerPrefs.Save();
        AudioListener.volume = volume;
    }

    public void SetMusicVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MusicVolumePreferenceKey, volume);
        PlayerPrefs.Save();

        EnsureMusicSource();
        StopMusicFade();
        _musicSource.volume = volume;
    }

    public void SetMusicMuted(bool muted)
    {
        PlayerPrefs.SetInt(MusicMutedPreferenceKey, muted ? 1 : 0);
        PlayerPrefs.Save();

        EnsureMusicSource();
        _musicSource.mute = muted;
    }

    public void SetSfxVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SfxVolumePreferenceKey, volume);
        PlayerPrefs.Save();
    }

    public void SetVoiceVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(VoiceVolumePreferenceKey, volume);
        PlayerPrefs.Save();
    }

    public void ToggleMusicMuted()
    {
        SetMusicMuted(!GetSavedMusicMuted());
    }

    public void PlayMusic()
    {
        EnsureMusicSource();
        if (_backgroundMusic == null) return;

        if (_musicSource.clip != _backgroundMusic)
        {
            _musicSource.clip = _backgroundMusic;
        }

        if (!_musicSource.isPlaying)
        {
            _musicSource.Play();
        }

        FadeMusicTo(GetSavedMusicVolume());
    }

    public void StopMusic()
    {
        if (_musicSource == null || !_musicSource.isPlaying) return;
        FadeMusicTo(0f, true);
    }

    public void SetBackgroundMusic(AudioClip clip, bool playImmediately = true)
    {
        _backgroundMusic = clip;
        EnsureMusicSource();
        _musicSource.Stop();
        _musicSource.clip = clip;

        if (playImmediately && ShouldPlayMusicInScene(SceneManager.GetActiveScene().name))
        {
            PlayMusic();
        }
    }

    private void EnsureMusicSource()
    {
        bool initializeSource = false;

        if (_musicSource == null)
        {
            _musicSource = GetComponent<AudioSource>();
            initializeSource = _musicSource != null;
        }

        if (_musicSource == null)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            initializeSource = true;
        }

        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.spatialBlend = 0f;
        _musicSource.ignoreListenerPause = true;
        _musicSource.clip = _backgroundMusic;

        if (initializeSource)
        {
            _musicSource.volume = GetSavedMusicVolume();
            _musicSource.mute = GetSavedMusicMuted();
        }
    }

    private void UpdateMusicForScene(string sceneName)
    {
        if (ShouldPlayMusicInScene(sceneName))
        {
            PlayMusic();
        }
        else
        {
            StopMusic();
        }
    }

    private bool ShouldPlayMusicInScene(string sceneName)
    {
        if (_musicScenes == null || _musicScenes.Count == 0) return true;

        for (int i = 0; i < _musicScenes.Count; i++)
        {
            if (string.Equals(_musicScenes[i], sceneName, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private void FadeMusicTo(float targetVolume, bool stopAfterFade = false)
    {
        StopMusicFade();

        if (_musicFadeDuration <= 0f)
        {
            _musicSource.volume = targetVolume;
            if (stopAfterFade) _musicSource.Stop();
            return;
        }

        _musicFadeRoutine = StartCoroutine(FadeMusicRoutine(targetVolume, stopAfterFade));
    }

    private IEnumerator FadeMusicRoutine(float targetVolume, bool stopAfterFade)
    {
        float startVolume = _musicSource.volume;
        float elapsed = 0f;

        while (elapsed < _musicFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            _musicSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / _musicFadeDuration);
            yield return null;
        }

        _musicSource.volume = targetVolume;
        if (stopAfterFade) _musicSource.Stop();
        _musicFadeRoutine = null;
    }

    private void StopMusicFade()
    {
        if (_musicFadeRoutine == null) return;
        StopCoroutine(_musicFadeRoutine);
        _musicFadeRoutine = null;
    }
}
