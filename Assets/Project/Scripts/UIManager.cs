using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement; // Necesario para cambiar de escena

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Panel de Inspección")]
    public GameObject infoPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;

    [Header("Panel de Victoria")]
    public GameObject victoryPanel;

    [Header("Navegación")]
    [Tooltip("El número de la escena del Menú Principal en la lista de Build Settings (usualmente 0)")]
    public int menuSceneIndex = 0; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        CloseInfoPanel();
        if (victoryPanel != null) victoryPanel.SetActive(false);
    }

    // --- FUNCIONES DE INSPECCIÓN ---
    public void ShowPartInfo(string title, string desc)
    {
        if (infoPanel != null)
        {
            titleText.text = title;
            descriptionText.text = desc;
            infoPanel.SetActive(true);
        }
    }

    public void CloseInfoPanel()
    {
        if (infoPanel != null) infoPanel.SetActive(false);
    }

    // --- FUNCIONES DE VICTORIA Y NAVEGACIÓN ---

    public void ShowVictory()
    {
        if (victoryPanel != null) 
        {
            victoryPanel.SetActive(true);
            CloseInfoPanel(); 
        }
    }

    // Esta es la función que usarás en el botón "Regresar"
    public void ReturnToMenu()
    {
        // Carga la escena que hayas configurado en el inspector
        SceneManager.LoadScene(menuSceneIndex);
    }

    // (Opcional) Dejo esta por si quieres añadir un botón de "Reintentar" separado
    public void RestartSimulation()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}