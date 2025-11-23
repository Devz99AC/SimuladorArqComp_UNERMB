using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class MainMenuManager : MonoBehaviour
{
    [Header("Escenas")]
    public string simulationSceneName = "ModeSelection";

    [Header("Paneles")]
    public GameObject mainPanel;
    public GameObject optionsPanel;
    public GameObject creditsPanel;

    [Header("Navegación")]
    public GameObject firstOptionButton; 
    public GameObject firstCreditButton; 
    public GameObject firstMainButton;   

    [Header("Estilo Visual")]
    public Color iconsColor = Color.white; 

    [Header("Audio - Lógica")]
    public AudioMixer mainMixer;
    public Image[] volumeBars; 
    private int currentVolumeLevel = 3; 

    [Header("Audio - Iconos")]
    public Image audioIconImage;      
    public Sprite iconSoundOff;  // Nivel 0 (Mute)
    public Sprite iconSoundLow;  // Nivel 1 (Nuevo)
    public Sprite iconSoundMed;  // Nivel 2 (Nuevo)
    public Sprite iconSoundHigh; // Nivel 3 (Antes era iconSoundOn)

    [Header("Video - Lógica")]
    public TextMeshProUGUI screenModeText; 
    private bool isFullscreen = true;

    [Header("Video - Iconos")]
    public Image screenIconImage;     
    public Sprite iconFullscreen;     
    public Sprite iconWindowed;       

    private CanvasGroup _mainCanvasGroup;

    private void Start()
    {
        if (mainPanel != null) _mainCanvasGroup = mainPanel.GetComponent<CanvasGroup>();

        if (_mainCanvasGroup != null) _mainCanvasGroup.interactable = true;
        optionsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
        
        LoadSettings();
    }

    private void LoadSettings()
    {
        currentVolumeLevel = PlayerPrefs.GetInt("VolumeLevel", 3);
        UpdateVolume(false);

        int fullscreenInt = PlayerPrefs.GetInt("Fullscreen", 1);
        isFullscreen = (fullscreenInt == 1);
        
        Screen.fullScreen = isFullscreen;
        UpdateScreenUI();
        UpdateVolumeUI();
    }

    private void SaveSettings()
    {
        PlayerPrefs.SetInt("VolumeLevel", currentVolumeLevel);
        PlayerPrefs.SetInt("Fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save(); 
    }

    // --- VENTANAS MODALES ---
    private void OpenModal(GameObject panel, GameObject focus)
    {
        if (_mainCanvasGroup) { _mainCanvasGroup.interactable = false; _mainCanvasGroup.blocksRaycasts = false; }
        panel.SetActive(true);
        if (focus) { EventSystem.current.SetSelectedGameObject(null); EventSystem.current.SetSelectedGameObject(focus); }
    }

    private void CloseModal(GameObject panel)
    {
        panel.SetActive(false);
        if (_mainCanvasGroup) { _mainCanvasGroup.interactable = true; _mainCanvasGroup.blocksRaycasts = true; }
        if (firstMainButton) { EventSystem.current.SetSelectedGameObject(null); EventSystem.current.SetSelectedGameObject(firstMainButton); }
    }

    public void OpenOptions() => OpenModal(optionsPanel, firstOptionButton);
    public void CloseOptions() => CloseModal(optionsPanel);
    public void OpenCredits() => OpenModal(creditsPanel, firstCreditButton);
    public void CloseCredits() => CloseModal(creditsPanel);

    // --- AUDIO ---
    public void IncreaseVolume() { if (currentVolumeLevel < 3) { currentVolumeLevel++; UpdateVolume(true); } }
    public void DecreaseVolume() { if (currentVolumeLevel > 0) { currentVolumeLevel--; UpdateVolume(true); } }

    private void UpdateVolume(bool save)
    {
        float db = -80f;
        if (currentVolumeLevel == 1) db = -20f;
        if (currentVolumeLevel == 2) db = -10f;
        if (currentVolumeLevel == 3) db = 0f;
        
        if(mainMixer != null) mainMixer.SetFloat("MasterVolume", db);
        UpdateVolumeUI();
        if (save) SaveSettings();
    }

    private void UpdateVolumeUI()
    {
        // 1. Actualizar Barras (Opcional si solo usas el icono)
        Color active = new Color(1f, 0.27f, 0f, 1f);
        Color inactive = new Color(0.8f, 0.8f, 0.8f, 1f);
        for (int i = 0; i < volumeBars.Length; i++) volumeBars[i].color = (i < currentVolumeLevel) ? active : inactive;

        // 2. Actualizar Icono según Nivel (0, 1, 2, 3)
        if (audioIconImage != null)
        {
            audioIconImage.color = iconsColor;

            switch (currentVolumeLevel)
            {
                case 0: audioIconImage.sprite = iconSoundOff; break;
                case 1: audioIconImage.sprite = iconSoundLow; break;
                case 2: audioIconImage.sprite = iconSoundMed; break;
                case 3: audioIconImage.sprite = iconSoundHigh; break;
            }
        }
    }

    // --- VIDEO ---
    public void ToggleScreenMode()
    {
        isFullscreen = !isFullscreen;
        Screen.fullScreen = isFullscreen;
        UpdateScreenUI();
        SaveSettings();
    }

    private void UpdateScreenUI()
    {
        if (screenModeText != null)
            screenModeText.text = isFullscreen ? "PANTALLA COMPLETA" : "MODO VENTANA";

        if (screenIconImage != null)
        {
            screenIconImage.color = iconsColor;
            if (isFullscreen) screenIconImage.sprite = iconFullscreen;
            else screenIconImage.sprite = iconWindowed;
        }
    }

    public void StartSimulation() { SceneManager.LoadScene(simulationSceneName); }
    public void QuitApp() { Application.Quit(); }
}