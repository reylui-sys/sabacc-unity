using UnityEngine;
using UnityEngine.UI;

// Panel de configuración de sonido.
// Conecta sliders y toggles de UI con el AudioManager.
// Asigna este script a tu panel de configuración de audio.
public class SoundSettingsPanel : MonoBehaviour
{
    [Header("Sliders de Volumen")]
    public Slider sfxSlider;
    public Slider musicSlider;

    [Header("Toggles de Mute (Opcional)")]
    public Toggle sfxMuteToggle;
    public Toggle musicMuteToggle;

    void Start()
    {
        InitializeUI();
        ConnectEvents();
    }

    void OnEnable()
    {
        // Actualizar valores cuando el panel se activa
        InitializeUI();
    }

    /// <summary>Inicializa los valores de UI con la configuración actual</summary>
    void InitializeUI()
    {
        if (AudioManager.Instance == null) return;

        // Configurar sliders
        if (sfxSlider != null)
        {
            sfxSlider.value = AudioManager.Instance.GetSFXVolume();
        }

        if (musicSlider != null)
        {
            musicSlider.value = AudioManager.Instance.GetMusicVolume();
        }

        // Configurar toggles (isOn = true significa NO muteado)
        if (sfxMuteToggle != null)
        {
            sfxMuteToggle.isOn = !AudioManager.Instance.IsSFXMuted();
        }

        if (musicMuteToggle != null)
        {
            musicMuteToggle.isOn = !AudioManager.Instance.IsMusicMuted();
        }
    }

    /// <summary>Conecta los eventos de UI</summary>
    void ConnectEvents()
    {
        if (sfxSlider != null)
        {
            sfxSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        if (musicSlider != null)
        {
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxMuteToggle != null)
        {
            sfxMuteToggle.onValueChanged.AddListener(OnSFXMuteChanged);
        }

        if (musicMuteToggle != null)
        {
            musicMuteToggle.onValueChanged.AddListener(OnMusicMuteChanged);
        }
    }

    // ===== CALLBACKS DE UI =====

    void OnSFXVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(value);
        }
    }

    void OnMusicVolumeChanged(float value)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(value);
        }
    }

    void OnSFXMuteChanged(bool isOn)
    {
        if (AudioManager.Instance != null)
        {
            // isOn = true significa que el sonido está activado (no muteado)
            AudioManager.Instance.MuteSFX(!isOn);
        }
    }

    void OnMusicMuteChanged(bool isOn)
    {
        if (AudioManager.Instance != null)
        {
            // isOn = true significa que la música está activada (no muteada)
            AudioManager.Instance.MuteMusic(!isOn);
        }
    }

    // ===== MÉTODOS PÚBLICOS PARA BOTONES =====

    /// <summary>Restaura los valores por defecto</summary>
    public void ResetToDefaults()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(1f);
            AudioManager.Instance.SetMusicVolume(0.5f);
            AudioManager.Instance.MuteSFX(false);
            AudioManager.Instance.MuteMusic(false);
        }

        InitializeUI();
    }

    /// <summary>Reproduce un sonido de prueba para SFX</summary>
    public void PlayTestSound()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayButtonClick();
        }
    }
}
