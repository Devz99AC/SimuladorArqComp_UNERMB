using UnityEngine;
using UnityEngine.UI; 
using TMPro;
using UnityEngine.SceneManagement;

public class TheoryManager : MonoBehaviour
{
    [Header("Datos")]
    public TheoryTopicSO[] topics; 
    private int currentIndex = 0;

    [Header("Referencias UI")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bodyText;
    public Image contentImage;
    
    // --- NUEVA VARIABLE: EL CONTENEDOR QUE SE ROMPE ---
    public RectTransform contentBody; // Arrastra aquí el objeto "ContentBody"

    [Header("Botones Navegación")]
    public Button prevButton; 
    public Button nextButton; 

    [Header("Navegación de Escena")]
    public string backSceneName = "ModeSelection";

    private void Start()
    {
        if (topics.Length > 0) LoadTopic(0);
        else Debug.LogWarning("La lista de Topics está vacía.");
    }

    public void LoadTopic(int index)
    {
        if (index < 0 || index >= topics.Length) return;

        currentIndex = index;
        TheoryTopicSO data = topics[currentIndex];

        // 1. Llenar UI
        if (titleText != null) titleText.text = data.title;
        
        if (bodyText != null) 
        {
            bodyText.text = data.contentText;
        }
        
        if (contentImage != null)
        {
            if (data.topicImage != null)
            {
                contentImage.sprite = data.topicImage;
                contentImage.gameObject.SetActive(true);
                contentImage.preserveAspect = true; 
            }
            else
            {
                contentImage.gameObject.SetActive(false); 
            }
        }

        // 2. Actualizar Botones
        UpdateNavigationButtons();

        // --- 3. EL TRUCO MAGICO: FORZAR RECALCULO ---
        // Esto obliga a Unity a acomodar todo YA, sin esperar al siguiente frame.
        if (contentBody != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentBody);
        }
    }

    private void UpdateNavigationButtons()
    {
        if (prevButton != null) prevButton.interactable = (currentIndex > 0);
        if (nextButton != null) nextButton.interactable = (currentIndex < topics.Length - 1);
    }

    public void NextTopic() { if (currentIndex < topics.Length - 1) LoadTopic(currentIndex + 1); }
    public void PreviousTopic() { if (currentIndex > 0) LoadTopic(currentIndex - 1); }
    public void CloseModule() { SceneManager.LoadScene(backSceneName); }
}