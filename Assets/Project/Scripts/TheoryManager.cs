using UnityEngine;
using UnityEngine.UI; 
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class TheoryManager : MonoBehaviour
{
    [Header("Datos")]
    public TheoryTopicSO[] topics; 
    private int currentIndex = 0;

    [Header("Vistas (Paneles)")]
    public GameObject indexPanel;   // El panel con la lista de botones
    public GameObject contentPanel; // La tarjeta de lectura

    [Header("Configuración del Índice")]
    public Transform indexContainer; 
    public GameObject topicButtonPrefab; 

    [Header("Referencias UI Contenido")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI bodyText;
    public Image contentImage;
    public RectTransform contentBody; 

    [Header("Botones Navegación Contenido")]
    public Button prevButton; 
    public Button nextButton; 
    
    [Header("Navegación General")]
    public string backSceneName = "ModeSelection";

    private void Start()
    {
        // 1. Generar la lista de botones
        GenerateIndex();

        // 2. ESTADO INICIAL: Abrir Índice, cerrar Contenido
        OpenIndex();
    }

    private void GenerateIndex()
    {
        // Limpiar por si acaso
        foreach (Transform child in indexContainer) Destroy(child.gameObject);

        for (int i = 0; i < topics.Length; i++)
        {
            GameObject newBtn = Instantiate(topicButtonPrefab, indexContainer);
            
            TextMeshProUGUI btnText = newBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = topics[i].title;

            int indexToLoad = i; 
            newBtn.GetComponent<Button>().onClick.AddListener(() => LoadTopic(indexToLoad));
        }
    }

    // --- NAVEGACIÓN ENTRE VISTAS ---

    public void OpenIndex()
    {
        contentPanel.SetActive(false); // Ocultamos la lectura
        indexPanel.SetActive(true);    // Mostramos la lista
    }

    public void LoadTopic(int index)
    {
        if (index < 0 || index >= topics.Length) return;

        // Cambiar de vista: Ocultar índice, mostrar lectura
        indexPanel.SetActive(false);
        contentPanel.SetActive(true);

        currentIndex = index;
        TheoryTopicSO data = topics[currentIndex];

        if (titleText) titleText.text = data.title;
        if (bodyText) bodyText.text = data.contentText;
        
        if (contentImage)
        {
            if (data.topicImage != null)
            {
                contentImage.sprite = data.topicImage;
                contentImage.gameObject.SetActive(true);
                contentImage.preserveAspect = true; 
            }
            else contentImage.gameObject.SetActive(false); 
        }

        UpdateNavigationButtons();
        if (contentBody) LayoutRebuilder.ForceRebuildLayoutImmediate(contentBody);
    }

    private void UpdateNavigationButtons()
    {
        if (prevButton) prevButton.interactable = (currentIndex > 0);
        if (nextButton) nextButton.interactable = (currentIndex < topics.Length - 1);
    }

    // --- FUNCIONES PARA LOS BOTONES ---

    public void NextTopic() { if (currentIndex < topics.Length - 1) LoadTopic(currentIndex + 1); }
    public void PreviousTopic() { if (currentIndex > 0) LoadTopic(currentIndex - 1); }
    
    // 1. Botón "X" del ÍNDICE -> Sale de la escena
    public void ExitModule() 
    { 
        Debug.Log("🔙 Saliendo al Selector de Módulos...");
        SceneManager.LoadScene(backSceneName); 
    }

    // 2. Botón "X" del CONTENIDO -> Vuelve al Índice
    public void BackToIndex()
    {
        OpenIndex();
    }
}