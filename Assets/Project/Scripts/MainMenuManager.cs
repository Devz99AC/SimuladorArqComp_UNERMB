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
    public GameObject mainPanel;    // El panel de botones principales
    public GameObject optionsPanel; // La ventana de opciones

    [Header("Navegación (Teclado/Mando)")]
    public GameObject firstOptionButton; 
    public GameObject firstMainButton;   

    [Header("Audio")]
    public AudioMixer mainMixer;
    public Image[] volumeBars; 
    private int currentVolumeLevel = 3; 

    [Header("Video")]
    public TextMeshProUGUI screenModeText; 
    private bool isFullscreen = true;

    // Variable interna para el bloqueo de clics
    private CanvasGroup _mainCanvasGroup;

    private void Start()
    {
        // Buscamos el CanvasGroup en el panel principal para poder bloquearlo
        if (mainPanel != null)
            _mainCanvasGroup = mainPanel.GetComponent<CanvasGroup>();

        // Estado Inicial: Menú activo, Opciones cerrado
        if (_mainCanvasGroup != null) _mainCanvasGroup.interactable = true;
        optionsPanel.SetActive(false);
        
        // Cargar valores visuales
        isFullscreen = Screen.fullScreen;
        UpdateScreenUI();
        UpdateVolumeUI();
    }

    // --- NAVEGACIÓN (MODO VENTANA SUPERPUESTA) ---

    public void OpenOptions() 
    { 
        // 1. NO apagamos el mainPanel, solo lo bloqueamos para que no se pueda clicar
        if (_mainCanvasGroup != null) 
        {
            _mainCanvasGroup.interactable = false; // Se ve, pero no se toca
            _mainCanvasGroup.blocksRaycasts = false; // El mouse lo atraviesa
        }

        // 2. Abrimos la ventana de opciones encima
        optionsPanel.SetActive(true); 

        // 3. Mover foco del teclado
        if (firstOptionButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstOptionButton);
        }
    }

    public void CloseOptions() 
    { 
        // 1. Cerramos opciones
        optionsPanel.SetActive(false); 

        // 2. Reactivamos el menú principal
        if (_mainCanvasGroup != null) 
        {
            _mainCanvasGroup.interactable = true; // Ya se puede tocar
            _mainCanvasGroup.blocksRaycasts = true;
        }

        // 3. Devolver foco
        if (firstMainButton != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(firstMainButton);
        }
    }

    // --- EL RESTO DEL CÓDIGO SIGUE IGUAL ---
    
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