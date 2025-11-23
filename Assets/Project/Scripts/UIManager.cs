using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.UI; 
using UnityEngine.Audio; 

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Paneles Principales")]
    public GameObject infoPanel;       
    public GameObject victoryPanel;    
    public GameObject pausePanel;      
    public GameObject instructionsPanel; 

    [Header("Elementos Ficha Técnica")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    [Header("Configuración General")]
    public AudioMixer mainMixer; 
    public string selectorSceneName = "ModeSelection"; 

    [Header("Estilo Visual")]
    public Color iconsColor = Color.white; // Color para los iconos

    [Header("Referencias UI Audio")]
    public Image[] volumeBars;          
    public Image audioIconImage;        // Imagen central bocina
    public Sprite iconSoundOff;         // Nivel 0
    public Sprite iconSoundLow;         // Nivel 1
    public Sprite iconSoundMed;         // Nivel 2
    public Sprite iconSoundHigh;        // Nivel 3

    [Header("Referencias UI Video")]
    public TextMeshProUGUI screenModeText; 
    public Image screenIconImage;       // Imagen central pantalla
    public Sprite iconFullscreen;       
    public Sprite iconWindowed;         
    
    private int currentVolumeLevel = 3; 
    private bool isFullscreen = true;   

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        CloseAllPanels();
        if (instructionsPanel) instructionsPanel.SetActive(true);
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

    // --- NAVEGACIÓN ---
    private void CloseAllPanels()
    {
        if (infoPanel) infoPanel.SetActive(false);
        if (victoryPanel) victoryPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);
    }

    public void ReturnToSelector() => SceneManager.LoadScene(selectorSceneName);

    public void OpenPauseMenu()
    {
        CloseAllPanels(); 
        if (instructionsPanel) instructionsPanel.SetActive(false); 
        if (pausePanel) pausePanel.SetActive(true);
    }

    public void ClosePauseMenu() => pausePanel.SetActive(false);

    public void ToggleInstructions()
    {
        if (instructionsPanel != null)
        {
            bool isActive = instructionsPanel.activeSelf;
            if (!isActive) 
            {
                if (infoPanel) infoPanel.SetActive(false);
                if (pausePanel) pausePanel.SetActive(false);
            }
            instructionsPanel.SetActive(!isActive);
        }
    }

    public void ShowPartInfo(string title, string desc)
    {
        if (pausePanel != null && pausePanel.activeSelf) return;
        if (infoPanel != null)
        {
            titleText.text = title;
            descriptionText.text = desc;
            infoPanel.SetActive(true);
        }
    }

    public void CloseInfoPanel() => infoPanel.SetActive(false);

    public void ShowVictory()
    {
        CloseAllPanels();
        if (instructionsPanel) instructionsPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(true);
    }

    // --- LÓGICA AUDIO (IGUAL AL MENÚ PRINCIPAL) ---
    public void IncreaseVolume() { if (currentVolumeLevel < 3) { currentVolumeLevel++; UpdateVolume(true); } }
    public void DecreaseVolume() { if (currentVolumeLevel > 0) { currentVolumeLevel--; UpdateVolume(true); } }

    private void UpdateVolume(bool save)
    {
        float volumeDb = -80f;
        if (currentVolumeLevel == 1) volumeDb = -20f;
        if (currentVolumeLevel == 2) volumeDb = -10f;
        if (currentVolumeLevel == 3) volumeDb = 0f;

        if (mainMixer != null) mainMixer.SetFloat("MasterVolume", volumeDb);
        else Debug.LogWarning("⚠️ [UIManager] MainMixer is missing!");
        
        UpdateVolumeUI();

        if (save) SaveSettings();
    }

    private void UpdateVolumeUI()
    {
        // 1. Barras
        if (volumeBars != null && volumeBars.Length > 0)
        {
            Color activeColor = new Color(1f, 0.27f, 0f, 1f); 
            Color inactiveColor = new Color(0.8f, 0.8f, 0.8f, 1f); 
            for (int i = 0; i < volumeBars.Length; i++)
                volumeBars[i].color = (i < currentVolumeLevel) ? activeColor : inactiveColor;
        }

        // 2. Icono Dinámico
        if (audioIconImage != null)
        {
            audioIconImage.color = iconsColor; // Aplicar color

            switch (currentVolumeLevel)
            {
                case 0: audioIconImage.sprite = iconSoundOff; break;
                case 1: audioIconImage.sprite = iconSoundLow; break;
                case 2: audioIconImage.sprite = iconSoundMed; break;
                case 3: audioIconImage.sprite = iconSoundHigh; break;
            }
        }
    }

    // --- LÓGICA VIDEO (IGUAL AL MENÚ PRINCIPAL) ---
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
            screenIconImage.color = iconsColor; // Aplicar color
            if (isFullscreen) screenIconImage.sprite = iconFullscreen;
            else screenIconImage.sprite = iconWindowed;
        }
    }
}