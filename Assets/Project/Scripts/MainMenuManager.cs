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
    public GameObject creditsPanel; // NUEVO: Referencia al panel de créditos

    [Header("Navegación (Foco Inicial)")]
    public GameObject firstOptionButton; 
    public GameObject firstCreditButton; // NUEVO: Botón donde aterriza el foco en créditos (la X)
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

        // Estado Inicial: Solo menú visible
        if (_mainCanvasGroup != null) _mainCanvasGroup.interactable = true;
        optionsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false); // Asegurar cerrado
        
        isFullscreen = Screen.fullScreen;
        UpdateScreenUI();
        UpdateVolumeUI();
    }

    // --- SISTEMA DE VENTANAS MODALES ---

    private void OpenModal(GameObject panelToOpen, GameObject focusButton)
    {
        // 1. Bloquear menú principal
        if (_mainCanvasGroup != null) 
        {
            _mainCanvasGroup.interactable = false; 
            _mainCanvasGroup.blocksRaycasts = false; 
        }

        // 2. Abrir panel
        panelToOpen.SetActive(true); 

        // 3. Mover foco
        if (focusButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(focusButton);
        }
    }

    private void CloseModal(GameObject panelToClose)
    {
        // 1. Cerrar panel
        panelToClose.SetActive(false); 

        // 2. Desbloquear menú principal
        if (_mainCanvasGroup != null) 
        {
            _mainCanvasGroup.interactable = true; 
            _mainCanvasGroup.blocksRaycasts = true;
        }

        // 3. Devolver foco al inicio
        if (firstMainButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstMainButton);
        }
    }

    // --- FUNCIONES PÚBLICAS PARA BOTONES ---

    public void OpenOptions() => OpenModal(optionsPanel, firstOptionButton);
    public void CloseOptions() => CloseModal(optionsPanel);

    public void OpenCredits() => OpenModal(creditsPanel, firstCreditButton); // NUEVO
    public void CloseCredits() => CloseModal(creditsPanel); // NUEVO

    // --- LOGICA DE AUDIO/VIDEO 
    public void IncreaseVolume() { if (currentVolumeLevel < 3) { currentVolumeLevel++; UpdateVolume(); } }
    public void DecreaseVolume() { if (currentVolumeLevel > 0) { currentVolumeLevel--; UpdateVolume(); } }

    private void UpdateVolume()
    {
        float volumeDb = -80f;
        if (currentVolumeLevel == 1) volumeDb = -20f;
        if (currentVolumeLevel == 2) volumeDb = -10f;
        if (currentVolumeLevel == 3) volumeDb = 0f;
        if(mainMixer != null) mainMixer.SetFloat("MasterVolume", volumeDb);
        UpdateVolumeUI();
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
    }

    private void UpdateScreenUI()
    {
        if (screenModeText != null)
            screenModeText.text = isFullscreen ? "PANTALLA COMPLETA" : "MODO VENTANA";
    }

    public void StartSimulation() { SceneManager.LoadScene(simulationSceneName); }
    public void QuitApp() { Application.Quit(); }
}