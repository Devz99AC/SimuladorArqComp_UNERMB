using UnityEngine;
using UnityEngine.SceneManagement; // Necesario para cargar escenas

public class MainMenuManager : MonoBehaviour
{
    [Header("Configuración de Escenas")]
    [Tooltip("Nombre exacto de la escena del simulador (debe estar en Build Settings)")]
    public string simulationSceneName = "MainSimulation"; 

    [Tooltip("Nombre de la escena teórica (Opcional por ahora)")]
    public string theorySceneName = "TheoryModule";

    // --- FUNCIÓN PARA EL BOTÓN 'INICIAR' ---
    public void StartSimulation()
    {
        Debug.Log($"🚀 Iniciando Simulación: Cargando escena '{simulationSceneName}'...");
        
        // Carga la escena. Asegúrate de haberla añadido en File > Build Settings
        SceneManager.LoadScene(simulationSceneName);
    }

    // --- FUNCIÓN PARA EL BOTÓN 'OPCIONES' ---
    public void OpenOptions()
    {
        Debug.Log("⚙️ Abriendo Opciones... (Aquí activarías el panel de opciones)");
        // Ejemplo: optionsPanel.SetActive(true);
    }

    // --- FUNCIÓN PARA EL BOTÓN 'CRÉDITOS' ---
    public void OpenCredits()
    {
        Debug.Log("👥 Abriendo Créditos... (Aquí activarías el panel de créditos)");
        // Ejemplo: creditsPanel.SetActive(true);
    }

    // --- FUNCIÓN PARA EL BOTÓN 'SALIR' ---
    public void QuitApp()
    {
        Debug.Log("👋 Saliendo de la aplicación...");

        // Cierra la app construida (Windows/Mac/WebGL)
        Application.Quit();

        // Esto es solo para que funcione el botón de salir dentro del Editor de Unity
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}