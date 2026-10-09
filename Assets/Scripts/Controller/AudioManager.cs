using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

// Gestor de audio para el juego Sabacc.
// Singleton que maneja todos los efectos de sonido y música.
// Incluye persistencia de configuración con PlayerPrefs.
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource musicSource;

    [Header("Sonidos de Cartas")]
    public AudioClip cardDeal;
    public AudioClip cardFlip;
    public AudioClip cardSlide;
    public AudioClip cardShuffle;
    public AudioClip cardSelect;
    public AudioClip cardDeselect;

    [Header("Sonidos de Fichas/Apuestas")]
    public AudioClip chipsPlace;
    public AudioClip chipsCollect;
    public AudioClip chipsSingle;

    [Header("Sonidos de Acciones")]
    public AudioClip buttonClick;
    public AudioClip check;
    public AudioClip fold;
    public AudioClip call;
    public AudioClip raise;

    [Header("Sonidos de Eventos")]
    public AudioClip shift;
    public AudioClip turnStart;
    public AudioClip roundStart;
    public AudioClip roundEnd;
    public AudioClip win;
    public AudioClip lose;
    public AudioClip sabacc;
    public AudioClip bombedOut;
    public AudioClip gameOver;

    [Header("Música")]
    public AudioClip backgroundMusic;
    public AudioClip tensionMusic;
    public AudioClip menuMusic;

    [Header("Configuración")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.5f;

    // Claves para PlayerPrefs
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_MUTED_KEY = "SFXMuted";
    private const string MUSIC_MUTED_KEY = "MusicMuted";

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
            LoadSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void InitializeAudioSources()
    {
        // Crear AudioSources si no existen
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
        }

        sfxSource.volume = sfxVolume;
        musicSource.volume = musicVolume;
    }

    // PERSISTENCIA CON PLAYERPREFS 

    private void LoadSettings()
    {
        // Cargar volúmenes (con valores por defecto si no existen)
        sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
        musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 0.5f);
        
        // Aplicar volúmenes
        if (sfxSource != null) sfxSource.volume = sfxVolume;
        if (musicSource != null) musicSource.volume = musicVolume;
        
        // Cargar estado de mute
        bool sfxMuted = PlayerPrefs.GetInt(SFX_MUTED_KEY, 0) == 1;
        bool musicMuted = PlayerPrefs.GetInt(MUSIC_MUTED_KEY, 0) == 1;
        
        if (sfxSource != null) sfxSource.mute = sfxMuted;
        if (musicSource != null) musicSource.mute = musicMuted;
        
        Debug.Log($"[AudioManager] Configuración cargada - SFX: {sfxVolume}, Music: {musicVolume}");
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, sfxVolume);
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, musicVolume);
        PlayerPrefs.SetInt(SFX_MUTED_KEY, (sfxSource != null && sfxSource.mute) ? 1 : 0);
        PlayerPrefs.SetInt(MUSIC_MUTED_KEY, (musicSource != null && musicSource.mute) ? 1 : 0);
        PlayerPrefs.Save();
    }

    // GETTERS PARA EL PANEL DE CONFIGURACIÓN

    public float GetSFXVolume()
    {
        return sfxVolume;
    }

    public float GetMusicVolume()
    {
        return musicVolume;
    }

    public bool IsSFXMuted()
    {
        return sfxSource != null && sfxSource.mute;
    }

    public bool IsMusicMuted()
    {
        return musicSource != null && musicSource.mute;
    }

    // MÉTODOS PÚBLICOS PARA REPRODUCIR SONIDOS

    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, sfxVolume);
        }
    }

    public void PlaySFX(AudioClip clip, float volume)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, volume * sfxVolume);
        }
    }

    //  SONIDOS DE CARTAS 

    public void PlayCardDeal()
    {
        PlaySFX(cardDeal);
    }

    public void PlayCardFlip()
    {
        PlaySFX(cardFlip);
    }

    public void PlayCardSlide()
    {
        PlaySFX(cardSlide);
    }

    public void PlayCardShuffle()
    {
        PlaySFX(cardShuffle);
    }

    public void PlayCardSelect()
    {
        PlaySFX(cardSelect);
    }

    public void PlayCardDeselect()
    {
        PlaySFX(cardDeselect);
    }

    // ===== SONIDOS DE FICHAS =====

    public void PlayChipsPlace()
    {
        PlaySFX(chipsPlace);
    }

    public void PlayChipsCollect()
    {
        PlaySFX(chipsCollect);
    }

    public void PlayChipsSingle()
    {
        PlaySFX(chipsSingle);
    }

    // ===== SONIDOS DE ACCIONES =====

    public void PlayButtonClick()
    {
        PlaySFX(buttonClick);
    }

    public void PlayCheck()
    {
        PlaySFX(check);
    }

    public void PlayFold()
    {
        PlaySFX(fold);
    }

    public void PlayCall()
    {
        PlaySFX(call);
    }

    public void PlayRaise()
    {
        PlaySFX(raise);
    }

    // ===== SONIDOS DE EVENTOS =====

    public void PlayShift()
    {
        PlaySFX(shift);
    }

    public void PlayTurnStart()
    {
        PlaySFX(turnStart);
    }

    public void PlayRoundStart()
    {
        PlaySFX(roundStart);
    }

    public void PlayRoundEnd()
    {
        PlaySFX(roundEnd);
    }

    public void PlayWin()
    {
        PlaySFX(win);
    }

    public void PlayLose()
    {
        PlaySFX(lose);
    }

    public void PlaySabacc()
    {
        PlaySFX(sabacc);
    }

    public void PlayBombedOut()
    {
        PlaySFX(bombedOut);
    }

    public void PlayGameOver()
    {
        PlaySFX(gameOver);
    }

    // ===== MÚSICA =====

    public void PlayBackgroundMusic()
    {
        if (backgroundMusic != null && musicSource != null)
        {
            musicSource.clip = backgroundMusic;
            musicSource.Play();
        }
    }

    public void PlayTensionMusic()
    {
        if (tensionMusic != null && musicSource != null)
        {
            musicSource.clip = tensionMusic;
            musicSource.Play();
        }
    }

    public void PlayMenuMusic()
    {
        if (menuMusic != null && musicSource != null)
        {
            // Solo cambia y reproduce si no es el clip actual o no está reproduciendo
            if (musicSource.clip != menuMusic || !musicSource.isPlaying) // Evita reiniciar si ya está sonando y que haya un corte
            {
                musicSource.clip = menuMusic;
                musicSource.Play();
            }
        }
    }

    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    public void PauseMusic()
    {
        if (musicSource != null)
        {
            musicSource.Pause();
        }
    }

    public void ResumeMusic()
    {
        if (musicSource != null)
        {
            musicSource.UnPause();
        }
    }

    // ===== CONTROL DE VOLUMEN =====

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (sfxSource != null)
        {
            sfxSource.volume = sfxVolume;
        }
        SaveSettings();
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
        }
        SaveSettings();
    }

    public void MuteAll(bool mute)
    {
        if (sfxSource != null) sfxSource.mute = mute;
        if (musicSource != null) musicSource.mute = mute;
        SaveSettings();
    }

    public void MuteSFX(bool mute)
    {
        if (sfxSource != null) sfxSource.mute = mute;
        SaveSettings();
    }

    public void MuteMusic(bool mute)
    {
        if (musicSource != null) musicSource.mute = mute;
        SaveSettings();
    }

    // ===== TRANSICIONES DE MÚSICA =====
    private Coroutine currentFadeCoroutine;

    public void FadeOutMusic(float duration = 1f)
    {
        if (currentFadeCoroutine != null)
            StopCoroutine(currentFadeCoroutine);
        currentFadeCoroutine = StartCoroutine(FadeOutRoutine(duration));
    }

    public void FadeInMusic(AudioClip clip, float duration = 1f)
    {
        if (currentFadeCoroutine != null)
            StopCoroutine(currentFadeCoroutine);
        currentFadeCoroutine = StartCoroutine(FadeInRoutine(clip, duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        if (musicSource == null || !musicSource.isPlaying) yield break;

        float startVolume = musicSource.volume;
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime; // Usa unscaled para que funcione en pausa
            musicSource.volume = Mathf.Lerp(startVolume, 0f, time / duration);
            yield return null;
        }

        musicSource.Stop();
        musicSource.volume = startVolume; // Restaurar volumen original
    }

    private IEnumerator FadeInRoutine(AudioClip clip, float duration)
    {
        if (musicSource == null || clip == null) yield break;

        musicSource.clip = clip;
        musicSource.Play();
        musicSource.volume = 0f;

        float targetVolume = musicVolume;
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            musicSource.volume = Mathf.Lerp(0f, targetVolume, time / duration);
            yield return null;
        }

        musicSource.volume = targetVolume;
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        switch (scene.name)
        {
            case "Inicio":
            case "Configuracion":
            case "Creditos":
                PlayMenuMusic(); 
                break;
            case "Tutorial":
            //case "MainScene":  
                FadeInMusic(backgroundMusic, 0.8f);
                break;
        }
    }

    public void LoadSceneWithMusicFade(string sceneName, float fadeDuration = 0.5f)
    {
        FadeOutMusic(fadeDuration);
        Instance.StartCoroutine(DelayedLoad(sceneName, fadeDuration));
    }

    private IEnumerator DelayedLoad(string sceneName, float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(sceneName);
    }
}
