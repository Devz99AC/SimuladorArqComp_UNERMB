using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class MainMenuManager : MonoBehaviour
{
    [Header("Escenas")]
    public string simulationSceneName = "MainSimulation";

    [Header("Paneles")]
    public GameObject mainPanel;
    public GameObject optionsPanel;
    public GameObject creditsPanel;

    [Header("Navegación")]
    public GameObject firstOptionButton; 
    public GameObject firstCreditButton; 
    public GameObject firstMainButton;   

    [Header("Audio")]
    public AudioMixer mainMixer;
    public Image[] volumeBars; 
    private int currentVolumeLevel = 3; 

    [Header("Video")]
    public TextMeshProUGUI screenModeText; 
    private bool isFullscreen = true;

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

    private void OpenModal(GameObject panelToOpen, GameObject focusButton)
    {
        if (_mainCanvasGroup != null) 
        {
            _mainCanvasGroup.interactable = false; 
            _mainCanvasGroup.blocksRaycasts = false; 
        }
        panelToOpen.SetActive(true); 
        if (focusButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(focusButton);
        }
    }

    private void CloseModal(GameObject panelToClose)
    {
        panelToClose.SetActive(false); 
        if (_mainCanvasGroup != null) 
        {
            _mainCanvasGroup.interactable = true; 
            _mainCanvasGroup.blocksRaycasts = true;
        }
        if (firstMainButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstMainButton);
        }
    }

    public void OpenOptions() => OpenModal(optionsPanel, firstOptionButton);
    public void CloseOptions() => CloseModal(optionsPanel);
    public void OpenCredits() => OpenModal(creditsPanel, firstCreditButton);
    public void CloseCredits() => CloseModal(creditsPanel);

    public void IncreaseVolume() { if (currentVolumeLevel < 3) { currentVolumeLevel++; UpdateVolume(true); } }
    public void DecreaseVolume() { if (currentVolumeLevel > 0) { currentVolumeLevel--; UpdateVolume(true); } }

    private void UpdateVolume(bool save)
    {
        float volumeDb = -80f;
        if (currentVolumeLevel == 1) volumeDb = -20f;
        if (currentVolumeLevel == 2) volumeDb = -10f;
        if (currentVolumeLevel == 3) volumeDb = 0f;
        
        if(mainMixer != null) mainMixer.SetFloat("MasterVolume", volumeDb);
        UpdateVolumeUI();

        if (save) SaveSettings();
    }

    private void UpdateVolumeUI()
    {
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

    public void StartSimulation() { SceneManager.LoadScene(simulationSceneName); }
    public void QuitApp() { Application.Quit(); }
}