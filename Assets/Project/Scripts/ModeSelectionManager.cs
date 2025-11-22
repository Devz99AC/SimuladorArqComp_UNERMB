using UnityEngine;
using UnityEngine.SceneManagement;

public class ModeSelectionManager : MonoBehaviour
{
    [Header("Nombres de Escenas")]
    [Tooltip("Nombre exacto de la escena del Menú Principal")]
    public string mainMenuScene = "MainMenu";

    [Tooltip("Nombre exacto de la escena del Simulador 3D")]
    public string simulationScene = "MainSimulation";

    [Tooltip("Nombre exacto de la escena del Módulo Teórico")]
    public string theoryScene = "TheoryModule";



    public void GoToSimulation()
    {
        Debug.Log($"🚀 Cargando Simulador: {simulationScene}...");
        SceneManager.LoadScene(simulationScene);
    }

    public void GoToTheory()
    {
        Debug.Log($"📚 Cargando Teoría: {theoryScene}...");

        if (Application.CanStreamedLevelBeLoaded(theoryScene))
        {
            SceneManager.LoadScene(theoryScene);
        }
        else
        {
            Debug.LogError($"❌ La escena '{theoryScene}' no se encuentra en Build Settings o no existe.");
        }
    }

    public void BackToMenu()
    {
        Debug.Log("🔙 Regresando al Menú Principal...");
        SceneManager.LoadScene(mainMenuScene);
    }
}