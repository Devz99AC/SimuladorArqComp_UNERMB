using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Controlador UI Toolkit de la selección de modo. Reemplaza a
/// ModeSelectionManager (uGUI). Reutiliza el layout/estilos del MainMenu;
/// navega a Teoría, abre el selector de laboratorios o vuelve al Menú.
///
/// El selector es un modal con una tarjeta por laboratorio, construida desde
/// <see cref="LabCatalogSO"/> (data-driven: añadir un laboratorio = añadir un
/// asset al catálogo). Sin catálogo, el botón entra directo a la escena de
/// ensamblaje como antes.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ModeSelectionView : MonoBehaviour
{
    [Header("Escenas")]
    [SerializeField] private string theoryScene = "TheoryModule";
    [SerializeField] private string simulationScene = "AssemblyLab";
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("Laboratorios")]
    [Tooltip("Laboratorios del selector. Vacío = el botón entra directo a simulationScene.")]
    [SerializeField] private LabCatalogSO labCatalog;

    [Header("Audio")]
    [SerializeField] private AudioClip uiSound;

    private ModalPanel _labsModal;
    private Button _btnAssembly;
    private Button _firstLabButton;

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        UiSound.Configure(uiSound, uiSound, 1f, 1f);

        var btnTheory = root.Q<Button>("btn-theory");
        btnTheory.clicked += () => SceneManager.LoadScene(theoryScene);
        _btnAssembly = root.Q<Button>("btn-assembly");
        _btnAssembly.clicked += OpenLabs;
        root.Q<Button>("btn-back").clicked += () => SceneManager.LoadScene(mainMenuScene);

        // Selector de laboratorios
        _labsModal = root.Q<ModalPanel>("labs-modal");
        root.Q<Button>("labs-close").clicked += CloseLabs;
        _labsModal.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Escape) { CloseLabs(); evt.StopPropagation(); }
        });
        BuildLabCards(root.Q<VisualElement>("labs-cards"));

        // Sonido + foco-sigue-al-ratón (un solo ítem resaltado, ratón o teclado)
        UiSound.AttachAll(root);
        root.Query<Button>(className: "menu-item").ForEach(b =>
            b.RegisterCallback<PointerEnterEvent>(_ => b.Focus()));

        // Foco inicial
        root.schedule.Execute(() => btnTheory.Focus()).ExecuteLater(0);
    }

    private void OpenLabs()
    {
        if (labCatalog == null || labCatalog.labs == null || labCatalog.labs.Length == 0)
        {
            SceneManager.LoadScene(simulationScene);
            return;
        }
        _labsModal.Open();
        if (_firstLabButton != null) _labsModal.schedule.Execute(() => _firstLabButton.Focus()).ExecuteLater(0);
    }

    private void CloseLabs()
    {
        _labsModal.Close();
        _btnAssembly?.Focus();
    }

    private void BuildLabCards(VisualElement container)
    {
        container.Clear();
        if (labCatalog == null || labCatalog.labs == null) return;

        for (int i = 0; i < labCatalog.labs.Length; i++)
        {
            LabDefinitionSO lab = labCatalog.labs[i];
            if (lab == null) continue;
            bool ready = lab.available && !string.IsNullOrEmpty(lab.sceneName);

            var card = new VisualElement();
            card.AddToClassList("lab-card");
            card.EnableInClassList("is-soon", !ready);

            // Cabecera visual: imagen del laboratorio o, sin ella, rayas + número (identidad del menú)
            var media = new VisualElement { pickingMode = PickingMode.Ignore };
            media.AddToClassList("lab-card__media");
            if (lab.preview != null)
            {
                media.style.backgroundImage = new StyleBackground(lab.preview);
            }
            else
            {
                var stripes = new DiagonalStripes { stripeWidth = 10f, gap = 14f, speed = ready ? 8f : 0f, pickingMode = PickingMode.Ignore };
                stripes.AddToClassList("lab-card__stripes");
                media.Add(stripes);
                var number = new Label((i + 1).ToString("00")) { pickingMode = PickingMode.Ignore };
                number.AddToClassList("lab-card__number");
                media.Add(number);
            }
            card.Add(media);

            var body = new VisualElement();
            body.AddToClassList("lab-card__body");
            card.Add(body);

            AddLabel(body, ready ? lab.tag : lab.tag + " · PRÓXIMAMENTE", "lab-card__tag");
            AddLabel(body, lab.title, "lab-card__title");
            AddLabel(body, lab.description, "lab-card__desc");

            if (lab.learns != null)
            {
                foreach (string point in lab.learns)
                {
                    if (string.IsNullOrEmpty(point)) continue;
                    var row = new VisualElement();
                    row.AddToClassList("lab-card__point");
                    var dot = new VisualElement();
                    dot.AddToClassList("lab-card__dot");
                    row.Add(dot);
                    AddLabel(row, point, "lab-card__point-text");
                    body.Add(row);
                }
            }

            var spacer = new VisualElement();
            spacer.AddToClassList("lab-card__spacer");
            body.Add(spacer);

            var enter = new Button { text = ready ? "ENTRAR" : "PRÓXIMAMENTE" };
            enter.AddToClassList("lab-card__enter");
            enter.SetEnabled(ready);
            if (ready)
            {
                string scene = lab.sceneName;
                enter.clicked += () => SceneManager.LoadScene(scene);
                if (_firstLabButton == null) _firstLabButton = enter;
            }
            body.Add(enter);

            container.Add(card);
        }
    }

    private static void AddLabel(VisualElement parent, string text, string className)
    {
        if (string.IsNullOrEmpty(text)) return;
        var label = new Label(text);
        label.AddToClassList(className);
        parent.Add(label);
    }
}
