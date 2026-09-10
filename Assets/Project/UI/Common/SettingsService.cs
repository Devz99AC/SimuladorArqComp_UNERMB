using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Servicio único de settings (audio + pantalla). Centraliza la lógica que
/// hoy está CLONADA en MainMenuManager y UIManager (~90 líneas, AUDITORIA §3.2).
/// POCO estático: no es MonoBehaviour. El AudioMixer sigue viviendo serializado
/// en el manager de escena y se pasa como parámetro; aquí residen las constantes
/// de PlayerPrefs y el mapeo de niveles a dB (fuente única de verdad).
/// </summary>
public static class SettingsService
{
    // --- Claves y parámetros (antes strings sueltos duplicados) ---
    public const string PrefVolume = "VolumeLevel";
    public const string PrefFullscreen = "Fullscreen";
    public const string MixerParam = "MasterVolume";

    // --- Rangos y valores por defecto (idénticos al comportamiento actual) ---
    public const int MinVolume = 0;
    public const int MaxVolume = 3;
    public const int DefaultVolumeLevel = 3;
    public const bool DefaultFullscreen = true;

    // Mapeo nivel -> dB: 0=-80, 1=-20, 2=-10, 3=0 (idéntico a los managers).
    private static readonly float[] VolumeDb = { -80f, -20f, -10f, 0f };

    /// <summary>Nivel de volumen vigente (0-3).</summary>
    public static int VolumeLevel { get; private set; } = DefaultVolumeLevel;

    /// <summary>Estado de pantalla completa vigente.</summary>
    public static bool Fullscreen { get; private set; } = DefaultFullscreen;

    /// <summary>dB del AudioMixer para un nivel dado (clamp defensivo).</summary>
    public static float DbForLevel(int level) => VolumeDb[Mathf.Clamp(level, MinVolume, MaxVolume)];

    /// <summary>Lee los valores persistidos a memoria (sin aplicarlos).</summary>
    public static void Load()
    {
        VolumeLevel = Mathf.Clamp(PlayerPrefs.GetInt(PrefVolume, DefaultVolumeLevel), MinVolume, MaxVolume);
        Fullscreen = PlayerPrefs.GetInt(PrefFullscreen, DefaultFullscreen ? 1 : 0) == 1;
    }

    /// <summary>Persiste los valores actuales a PlayerPrefs.</summary>
    public static void Save()
    {
        PlayerPrefs.SetInt(PrefVolume, VolumeLevel);
        PlayerPrefs.SetInt(PrefFullscreen, Fullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>Carga desde disco y aplica todo (llamar al arrancar cada escena).</summary>
    public static void LoadAndApply(AudioMixer mixer)
    {
        Load();
        if (mixer != null) mixer.SetFloat(MixerParam, DbForLevel(VolumeLevel));
        Screen.fullScreen = Fullscreen;
    }

    /// <summary>Fija un nivel concreto, lo aplica al mixer y (opcional) persiste.</summary>
    public static void ApplyVolume(AudioMixer mixer, int level, bool save = false)
    {
        VolumeLevel = Mathf.Clamp(level, MinVolume, MaxVolume);
        if (mixer != null) mixer.SetFloat(MixerParam, DbForLevel(VolumeLevel));
        if (save) Save();
    }

    public static int IncreaseVolume(AudioMixer mixer, bool save = true)
    {
        if (VolumeLevel < MaxVolume) ApplyVolume(mixer, VolumeLevel + 1, save);
        return VolumeLevel;
    }

    public static int DecreaseVolume(AudioMixer mixer, bool save = true)
    {
        if (VolumeLevel > MinVolume) ApplyVolume(mixer, VolumeLevel - 1, save);
        return VolumeLevel;
    }

    /// <summary>Fija el modo pantalla, lo aplica y (opcional) persiste.</summary>
    public static void ApplyFullscreen(bool value, bool save = false)
    {
        Fullscreen = value;
        Screen.fullScreen = value;
        if (save) Save();
    }

    public static bool ToggleFullscreen(bool save = true)
    {
        ApplyFullscreen(!Fullscreen, save);
        return Fullscreen;
    }
}
