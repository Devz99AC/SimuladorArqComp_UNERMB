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

    [Header("Referencias UI Audio/Video")]
    public Image[] volumeBars;          
    public TextMeshProUGUI screenModeText; 
    
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

    public void IncreaseVolume() { if (currentVolumeLevel < 3) { currentVolumeLevel++; UpdateVolume(true); } }
    public void DecreaseVolume() { if (currentVolumeLevel > 0) { currentVolumeLevel--; UpdateVolume(true); } }

    private void UpdateVolume(bool save)
    {
        float volumeDb = -80f;
        if (currentVolumeLevel == 1) volumeDb = -20f;
        if (currentVolumeLevel == 2) volumeDb = -10f;
        if (currentVolumeLevel == 3) volumeDb = 0f;

        if (mainMixer != null)
        {
            mainMixer.SetFloat("MasterVolume", volumeDb);
        }
        else
        {
            Debug.LogWarning("⚠️ [UIManager] MainMixer is missing! Volume changes will not apply.");
        }
        
        UpdateVolumeUI();

        if (save) SaveSettings();
    }

    private void UpdateVolumeUI()
    {
        if (volumeBars == null || volumeBars.Length == 0) return;
        Color activeColor = new Color(1f, 0.27f, 0f, 1f); 
        Color inactiveColor = new Color(0.8f, 0.8f, 0.8f, 1f); 

        for (int i = 0; i < volumeBars.Length; i++)
        {
            if (i < currentVolumeLevel) volumeBars[i].color = activeColor;
            else volumeBars[i].color = inactiveColor;
        }
    }

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
    }
}