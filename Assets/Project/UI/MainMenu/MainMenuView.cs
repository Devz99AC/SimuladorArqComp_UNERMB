using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Controlador UI Toolkit del menú principal. Reemplaza a MainMenuManager
/// (uGUI). Solo presentación + delegación: toda la lógica de settings vive en
/// SettingsService (dedup §3.2), el sonido en UiSound (§2.4) y el bloqueo de
/// interacción en ModalPanel (§7.2). Las imágenes/iconos se resuelven por USS.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MainMenuView : MonoBehaviour
{
    [Header("Navegación")]
    [SerializeField] private string simulationSceneName = "ModeSelection";

    [Header("Audio")]
    [SerializeField] private AudioMixer mainMixer;
    [SerializeField] private AudioClip uiSound;

    private ModalPanel _optionsModal;
    private ModalPanel _creditsModal;
    private VisualElement _volIcon;
    private Label _volLevel;
    private VisualElement _screenIcon;
    private Label _screenText;
    private readonly List<VisualElement> _volBars = new List<VisualElement>();

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        // Sonido de UI (mismos clips que el original: "breaker de botones")
        UiSound.Configure(uiSound, uiSound, 1f, 1f);

        // Cargar y aplicar settings persistentes (volumen + pantalla)
        SettingsService.LoadAndApply(mainMixer);

        // --- Botones del menú principal ---
        var btnStart = root.Q<Button>("btn-start");
        btnStart.clicked += () => SceneManager.LoadScene(simulationSceneName);
        root.Q<Button>("btn-options").clicked += () => _optionsModal.Open();
        root.Q<Button>("btn-credits").clicked += () => _creditsModal.Open();
        root.Q<Button>("btn-quit").clicked += QuitApp;

        // --- Modales ---
        _optionsModal = root.Q<ModalPanel>("options-modal");
        _creditsModal = root.Q<ModalPanel>("credits-modal");
        root.Q<Button>("options-close").clicked += () => _optionsModal.Close();
        root.Q<Button>("credits-close").clicked += () => _creditsModal.Close();
        RegisterEscapeToClose(_optionsModal);
        RegisterEscapeToClose(_creditsModal);

        // --- Volumen ---
        _volIcon = root.Q<VisualElement>("vol-icon");
        _volLevel = root.Q<Label>("vol-level");
        _volBars.AddRange(root.Q<VisualElement>("vol-bars").Query<VisualElement>(className: "volume-bar").ToList());
        root.Q<Button>("vol-down").clicked += () => { SettingsService.DecreaseVolume(mainMixer); RefreshVolume(); };
        root.Q<Button>("vol-up").clicked += () => { SettingsService.IncreaseVolume(mainMixer); RefreshVolume(); };

        // --- Pantalla ---
        _screenIcon = root.Q<VisualElement>("screen-icon");
        _screenText = root.Q<Label>("screen-text");
        root.Q<Button>("screen-prev").clicked += ToggleScreen;
        root.Q<Button>("screen-next").clicked += ToggleScreen;

        // Sonido de hover/click en todos los botones
        UiSound.AttachAll(root);

        // El ratón mueve el foco (replica AutoSelectOnHover): un solo ítem
        // resaltado a la vez, con ratón o con teclado. Evita el resaltado pegado.
        root.Query<Button>(className: "menu-item").ForEach(b =>
            b.RegisterCallback<PointerEnterEvent>(_ => b.Focus()));

        RefreshVolume();
        RefreshScreen();

        // Foco inicial para navegación por teclado (siguiente frame, ya construido el panel)
        root.schedule.Execute(() => btnStart.Focus()).ExecuteLater(0);
    }

    private void RegisterEscapeToClose(ModalPanel modal)
    {
        modal.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Escape) { modal.Close(); evt.StopPropagation(); }
        });
    }

    private void RefreshVolume()
    {
        int lvl = SettingsService.VolumeLevel;
        _volLevel.text = lvl + "/" + SettingsService.MaxVolume;
        for (int i = 0; i < _volBars.Count; i++)
            _volBars[i].EnableInClassList("is-active", i < lvl);
        for (int i = SettingsService.MinVolume; i <= SettingsService.MaxVolume; i++)
            _volIcon.EnableInClassList("level-" + i, i == lvl);
    }

    private void ToggleScreen()
    {
        SettingsService.ToggleFullscreen();
        RefreshScreen();
    }

    private void RefreshScreen()
    {
        bool fs = SettingsService.Fullscreen;
        _screenText.text = fs ? "PANTALLA COMPLETA" : "MODO VENTANA";
        _screenIcon.EnableInClassList("is-fullscreen", fs);
        _screenIcon.EnableInClassList("is-windowed", !fs);
    }

    private void QuitApp()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
