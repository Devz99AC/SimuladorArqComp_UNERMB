using UnityEngine;
using System; // Necesario para usar los Eventos (Action)

public class GameManager : MonoBehaviour
{
    // SINGLETON: Permite acceder a este script desde cualquier lado como GameManager.Instance
    public static GameManager Instance { get; private set; }

    [Header("Configuración del Ensamble")]
    [Tooltip("Cantidad total de piezas que el jugador debe colocar para ganar")]
    public int totalPartsToInstall = 1; 
    
    // Contador interno
    private int _installedParts = 0;

    // EVENTOS: Noticias que emitimos al resto del juego (pueden ser escuchados por otros scripts)
    public event Action<string> OnPartInstalled; // Avisa: "Se instaló la pieza X"
    public event Action OnAssemblyComplete;      // Avisa: "¡Juego Terminado!"

    private void Awake()
    {
        // Configuración del Singleton para que solo exista uno
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); 
        }
    }

    private void Start()
    {
        Debug.Log($"🏁 Inicio de Simulación. Piezas requeridas para ganar: {totalPartsToInstall}");
    }

    // Esta función la llama el SocketSystem cuando una pieza encaja correctamente
    public void RegisterInstallation(string partName)
    {
        _installedParts++;
        
        Debug.Log($"📈 Progreso: {_installedParts}/{totalPartsToInstall}");

        // 1. Lanzar el evento de progreso
        OnPartInstalled?.Invoke(partName);

        // 2. Verificar si ya ganamos
        if (_installedParts >= totalPartsToInstall)
        {
            FinishSimulation();
        }
    }

    // Lógica de Victoria
    private void FinishSimulation()
    {
        Debug.Log("🎉 ¡ENSAMBLE COMPLETADO! ¡FELICIDADES!");
        
        // Lanzar evento de victoria
        OnAssemblyComplete?.Invoke();

        // --- CONEXIÓN CON LA UI ---
        // Buscamos al UIManager y le decimos que abra el panel de ganar
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowVictory();
        }
        else
        {
            Debug.LogError("❌ GameManager intentó mostrar victoria, pero no encontró una instancia de UIManager en la escena.");
        }
    }
}