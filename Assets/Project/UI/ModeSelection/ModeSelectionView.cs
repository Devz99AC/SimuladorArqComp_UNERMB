using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// Controlador UI Toolkit de la selección de modo. Reemplaza a
/// ModeSelectionManager (uGUI). Reutiliza el layout/estilos del MainMenu;
/// solo navega a Teoría, Ensamblaje o vuelve al Menú.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ModeSelectionView : MonoBehaviour
{
    [Header("Escenas")]
    [SerializeField] private string theoryScene = "TheoryModule";
    [SerializeField] private string simulationScene = "AssemblyLab";
    [SerializeField] private string mainMenuScene = "MainMenu";

    [Header("Audio")]
    [SerializeField] private AudioClip uiSound;

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        UiSound.Configure(uiSound, uiSound, 1f, 1f);

        var btnTheory = root.Q<Button>("btn-theory");
        btnTheory.clicked += () => SceneManager.LoadScene(theoryScene);
        root.Q<Button>("btn-assembly").clicked += () => SceneManager.LoadScene(simulationScene);
        root.Q<Button>("btn-back").clicked += () => SceneManager.LoadScene(mainMenuScene);

        // Sonido + foco-sigue-al-ratón (un solo ítem resaltado, ratón o teclado)
        UiSound.AttachAll(root);
        root.Query<Button>(className: "menu-item").ForEach(b =>
            b.RegisterCallback<PointerEnterEvent>(_ => b.Focus()));

        // Foco inicial
        root.schedule.Execute(() => btnTheory.Focus()).ExecuteLater(0);
    }
}
