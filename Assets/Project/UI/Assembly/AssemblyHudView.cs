using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Controlador UI Toolkit del HUD del Laboratorio de Ensamblaje. Reemplaza a
/// UIManager (uGUI). Mantiene el mismo punto de entrada por singleton
/// (<see cref="Instance"/>) para InteractionManager (<c>ShowPartInfo</c>,
/// <c>ShowFeedback</c>). Progreso y victoria llegan por eventos de GameManager
/// (<c>OnProgressChanged</c>, <c>OnAssemblyComplete</c>).
///
/// Piezas del HUD (según mockups): Instrucciones (arriba-izq), Ficha técnica
/// (izq, al clic derecho), progreso (arriba-centro), toast de acierto/rechazo
/// (abajo-centro), botones ayuda/⚙ (arriba-der) y modales Opciones/
/// Victoria. El bloqueo del raycast puntero-sobre-UI y la congelación de la
/// simulación se resuelven con <see cref="UIBlocker"/> (los managers de
/// interacción/cámara lo consultan; los modales activan <c>UIBlocker.ModalOpen</c>).
/// Settings vía <see cref="SettingsService"/>; sonido vía <see cref="UiSound"/>.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class AssemblyHudView : MonoBehaviour
{
    public static AssemblyHudView Instance { get; private set; }

    [Header("Navegación")]
    [SerializeField] private string returnScene = "ModeSelection";

    [Header("Audio")]
    [SerializeField] private AudioMixer mainMixer;
    [SerializeField] private AudioClip uiSound;

    [Header("Instrucciones propias del laboratorio")]
    [Tooltip("Viñetas que se añaden al panel de instrucciones de esta escena (p. ej. cómo abrir una bisagra)")]
    [TextArea(1, 3)]
    [SerializeField] private string[] extraInstructions;

    [Header("Textos propios del laboratorio (vacío = los del UXML)")]
    [Tooltip("Qué cuenta el progreso: \"COMPONENTES\" en un montaje, \"PASOS\" en un mantenimiento")]
    [SerializeField] private string progressNoun = "COMPONENTES";
    [SerializeField] private string victoryTitle;
    [TextArea(2, 4)]
    [SerializeField] private string victoryText;
    [Tooltip("Cuántos tipos de error distintos lista el resumen de la victoria")]
    [SerializeField] private int maxErrorLines = 6;

    private VisualElement _instructionsPanel;
    private VisualElement _infoPanel;
    private Label _infoTitle;
    private Label _infoDesc;
    private ModalPanel _settingsModal;
    private ModalPanel _victoryModal;
    private FeedbackToast _toast;
    private Label _progressLabel;
    private VisualElement _progressFill;
    private Label _errorsLabel;
    private Label _victoryErrorsTitle;
    private VisualElement _victoryErrorsList;
    private GameManager _game;

    // Settings widgets
    private VisualElement _volIcon;
    private Label _volLevel;
    private VisualElement _screenIcon;
    private Label _screenText;
    private readonly List<VisualElement> _volBars = new List<VisualElement>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        UiSound.Configure(uiSound, uiSound, 1f, 1f);
        SettingsService.LoadAndApply(mainMixer);
        UIBlocker.Register(root);

        // --- Paneles ---
        _instructionsPanel = root.Q<VisualElement>("instructions-panel");
        AddExtraInstructions();
        _infoPanel = root.Q<VisualElement>("info-panel");
        _infoTitle = root.Q<Label>("info-title");
        _infoDesc = root.Q<Label>("info-desc");
        _settingsModal = root.Q<ModalPanel>("settings-modal");
        _victoryModal = root.Q<ModalPanel>("victory-modal");
        _toast = root.Q<FeedbackToast>("feedback-toast");
        _progressLabel = root.Q<Label>("progress-label");
        _progressFill = root.Q<VisualElement>("progress-fill");
        _errorsLabel = root.Q<Label>("errors-label");
        _victoryErrorsTitle = root.Q<Label>("victory-errors-title");
        _victoryErrorsList = root.Q<VisualElement>("victory-errors-list");
        if (!string.IsNullOrEmpty(victoryTitle)) root.Q<Label>("victory-title").text = victoryTitle;
        if (!string.IsNullOrEmpty(victoryText)) root.Q<Label>("victory-text").text = victoryText;

        // --- Progreso, errores y victoria por EVENTOS (antes: llamada directa desde GameManager) ---
        _game = GameManager.Instance;
        if (_game != null)
        {
            _game.OnProgressChanged += RefreshProgress;
            _game.OnErrorsChanged += RefreshErrors;
            _game.OnAssemblyComplete += ShowVictory;
            RefreshProgress(_game.InstalledCount, _game.TotalRequired);
            RefreshErrors(_game.Errors.Count);
        }

        root.Q<Button>("instructions-close").clicked += () => SetInstructions(false);
        root.Q<Button>("info-close").clicked += CloseInfoPanel;
        root.Q<Button>("btn-help").clicked += ToggleInstructions;
        root.Q<Button>("btn-settings").clicked += () => _settingsModal.Open();

        // --- Modales (congelan la simulación mientras están abiertos) ---
        root.Q<Button>("settings-close").clicked += () => _settingsModal.Close();
        root.Q<Button>("settings-resume").clicked += () => _settingsModal.Close();
        root.Q<Button>("settings-exit").clicked += ReturnToSelector;
        root.Q<Button>("victory-exit").clicked += ReturnToSelector;
        _settingsModal.OnOpenChanged += OnAnyModalOpenChanged;
        _victoryModal.OnOpenChanged += OnAnyModalOpenChanged;
        RegisterEscapeToClose(_settingsModal);

        // --- Settings (volumen + pantalla), reutilizando SettingsService ---
        _volIcon = root.Q<VisualElement>("vol-icon");
        _volLevel = root.Q<Label>("vol-level");
        _volBars.AddRange(root.Q<VisualElement>("vol-bars").Query<VisualElement>(className: "volume-bar").ToList());
        root.Q<Button>("vol-down").clicked += () => { SettingsService.DecreaseVolume(mainMixer); RefreshVolume(); };
        root.Q<Button>("vol-up").clicked += () => { SettingsService.IncreaseVolume(mainMixer); RefreshVolume(); };

        _screenIcon = root.Q<VisualElement>("screen-icon");
        _screenText = root.Q<Label>("screen-text");
        root.Q<Button>("screen-prev").clicked += ToggleScreen;
        root.Q<Button>("screen-next").clicked += ToggleScreen;

        UiSound.AttachAll(root);

        // Estado inicial: instrucciones visibles, ficha oculta (USS), modales cerrados.
        SetInstructions(true);
        RefreshVolume();
        RefreshScreen();
    }

    private void Update()
    {
        // Tecla I: alterna instrucciones (equivalente al ToggleInstructions original).
        if (!UIBlocker.ModalOpen && Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame)
            ToggleInstructions();
    }

    // Recupera el singleton si Unity recompiló en mitad del Play (los estáticos vuelven a null).
    private void OnEnable()
    {
        if (Instance == null) Instance = this;
    }

    private void OnDisable()
    {
        UIBlocker.Clear();
    }

    private void OnDestroy()
    {
        if (_game != null)
        {
            _game.OnProgressChanged -= RefreshProgress;
            _game.OnErrorsChanged -= RefreshErrors;
            _game.OnAssemblyComplete -= ShowVictory;
        }
        if (Instance == this) Instance = null;
    }

    private void OnAnyModalOpenChanged(bool _)
    {
        // Congelar si CUALQUIER modal está abierto (red de seguridad de UIBlocker).
        UIBlocker.ModalOpen = (_settingsModal != null && _settingsModal.IsOpen)
                           || (_victoryModal != null && _victoryModal.IsOpen);
    }

    // ===== API pública (mismos nombres que UIManager) =====

    /// <summary>Muestra la ficha técnica de una pieza (clic derecho). No abre si hay un modal.</summary>
    public void ShowPartInfo(string title, string desc)
    {
        if (UIBlocker.ModalOpen) return;
        if (_infoPanel == null) return;

        if (_infoTitle != null) _infoTitle.text = string.IsNullOrEmpty(title) ? "COMPONENTE" : title;
        if (_infoDesc != null) _infoDesc.text = HighlightTitle(title, desc);
        // Info e Instrucciones comparten la columna izquierda: no las solapamos.
        if (_instructionsPanel != null) _instructionsPanel.style.display = DisplayStyle.None;
        _infoPanel.style.display = DisplayStyle.Flex;
    }

    public void CloseInfoPanel()
    {
        if (_infoPanel != null) _infoPanel.style.display = DisplayStyle.None;
    }

    public void ShowVictory()
    {
        CloseInfoPanel();
        SetInstructions(false);
        if (_toast != null) _toast.Hide();
        FillErrorSummary();
        if (_victoryModal != null) _victoryModal.Open();
    }

    /// <summary>
    /// Resumen de errores del modal de victoria (sin nota): los motivos agrupados, de más a menos repetido. Repasar el
    /// porqué de cada uno es lo que se aprende (la batería conectada, una antena en el terminal equivocado…).
    /// </summary>
    private void FillErrorSummary()
    {
        if (_victoryErrorsList == null || _game == null) return;
        _victoryErrorsList.Clear();
        int total = _game.Errors.Count;
        if (_victoryErrorsTitle != null)
            _victoryErrorsTitle.text = total == 0 ? "SIN ERRORES" : "ERRORES: " + total;
        if (total == 0)
        {
            _victoryErrorsList.Add(MakeErrorLine("Seguiste el orden y las reglas de seguridad sin un solo rechazo."));
            return;
        }

        var groups = new Dictionary<string, int>();
        var order = new List<string>();
        foreach (var e in _game.Errors)
        {
            string key = string.IsNullOrEmpty(e.reason) ? e.message : e.reason;
            if (!groups.ContainsKey(key)) { groups[key] = 0; order.Add(key); }
            groups[key]++;
        }
        order.Sort((a, b) => groups[b].CompareTo(groups[a]));
        for (int i = 0; i < order.Count && i < maxErrorLines; i++)
            _victoryErrorsList.Add(MakeErrorLine(
                "<color=#E5484D><b>×" + groups[order[i]] + "</b></color>  " + order[i]));
        if (order.Count > maxErrorLines)
            _victoryErrorsList.Add(MakeErrorLine("… y " + (order.Count - maxErrorLines) + " motivo(s) más."));
    }

    private static Label MakeErrorLine(string text)
    {
        var label = new Label(text);
        label.AddToClassList("hud-victory__error");
        return label;
    }

    /// <summary>
    /// Feedback del ensamblaje: icono + texto + motivo (nunca solo color). Los mensajes con
    /// motivo duran más porque traen el "porqué" que hay que leer.
    /// </summary>
    public void ShowFeedback(string message, ToastKind kind, string reason = null)
    {
        if (_toast == null || UIBlocker.ModalOpen) return;
        int duration = kind == ToastKind.Error ? 5000 : string.IsNullOrEmpty(reason) ? 1800 : 4500;
        _toast.Show(message, kind, reason, null, duration);
    }

    private void RefreshProgress(int installed, int total)
    {
        if (_progressLabel != null) _progressLabel.text = progressNoun + " " + installed + " / " + total;
        if (_progressFill != null)
        {
            float pct = total > 0 ? 100f * installed / total : 0f;
            _progressFill.style.width = Length.Percent(pct);
        }
    }

    private void RefreshErrors(int count)
    {
        if (_errorsLabel == null) return;
        _errorsLabel.text = "ERRORES " + count;
        _errorsLabel.EnableInClassList("has-errors", count > 0);
    }

    /// <summary>Viñetas de <see cref="extraInstructions"/>, con el mismo aspecto que las del UXML.</summary>
    private void AddExtraInstructions()
    {
        var body = _instructionsPanel?.Q<VisualElement>(className: "hud-panel__body");
        if (body == null || extraInstructions == null) return;
        foreach (string text in extraInstructions)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            var bullet = new VisualElement();
            bullet.AddToClassList("hud-bullet");
            var dot = new VisualElement();
            dot.AddToClassList("hud-bullet__dot");
            var label = new Label(text);
            label.AddToClassList("hud-bullet__text");
            bullet.Add(dot);
            bullet.Add(label);
            body.Add(bullet);
        }
    }

    public void ToggleInstructions()
    {
        bool visible = _instructionsPanel != null && _instructionsPanel.resolvedStyle.display == DisplayStyle.Flex;
        SetInstructions(!visible);
    }

    // ===== Internos =====

    private void SetInstructions(bool visible)
    {
        // Al mostrar instrucciones, ocultamos la ficha (comparten columna izquierda).
        if (visible && _infoPanel != null) _infoPanel.style.display = DisplayStyle.None;
        if (_instructionsPanel != null)
            _instructionsPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void ReturnToSelector() => SceneManager.LoadScene(returnScene);

    /// <summary>Resalta en naranja la 1ª aparición del título dentro de la descripción (rich text).</summary>
    private static string HighlightTitle(string title, string desc)
    {
        if (string.IsNullOrEmpty(desc) || string.IsNullOrEmpty(title)) return desc;
        if (!desc.Contains(title)) return desc;
        return desc.Replace(title, "<color=#FF4500><b>" + title + "</b></color>");
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
        if (_volLevel != null) _volLevel.text = lvl + "/" + SettingsService.MaxVolume;
        for (int i = 0; i < _volBars.Count; i++)
            _volBars[i].EnableInClassList("is-active", i < lvl);
        if (_volIcon != null)
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
        if (_screenText != null) _screenText.text = fs ? "PANTALLA COMPLETA" : "MODO VENTANA";
        if (_screenIcon != null)
        {
            _screenIcon.EnableInClassList("is-fullscreen", fs);
            _screenIcon.EnableInClassList("is-windowed", !fs);
        }
    }
}
