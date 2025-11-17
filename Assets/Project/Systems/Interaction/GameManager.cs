using UnityEngine;
using System; // Necesario para usar Actions (Eventos)

public class GameManager : MonoBehaviour
{
    // SINGLETON: Permite acceder a este script desde cualquier lado usando GameManager.Instance
    public static GameManager Instance { get; private set; }

    [Header("Estado del Ensamble")]
    public int totalPartsToInstall = 0;
    private int _installedParts = 0;

    // EVENTOS: Noticias que emitimos al resto del juego
    public event Action<string> OnPartInstalled; // Avisa: "Se instaló X"
    public event Action OnAssemblyComplete;      // Avisa: "¡Ganaste!"

    private void Awake()
    {
        // Configuración del Singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject); // Solo puede haber un Manager
        }
    }

    private void Start()
    {
        // Opcional: Contar automáticamente cuántos sockets hay en la escena al empezar
        // totalPartsToInstall = FindObjectsOfType<SocketSystem>().Length;
        Debug.Log($"🏁 Inicio de Simulación. Piezas requeridas: {totalPartsToInstall}");
    }

    // Esta función la llamará el SocketSystem cuando algo encaje
    public void RegisterInstallation(string partName)
    {
        _installedParts++;
        
        Debug.Log($"📈 Progreso: {_installedParts}/{totalPartsToInstall}");

        // Lanzamos el evento para quien quiera escuchar (ej. la UI)
        OnPartInstalled?.Invoke(partName);

        // Verificamos victoria
        if (_installedParts >= totalPartsToInstall)
        {
            FinishSimulation();
        }
    }

    private void FinishSimulation()
    {
        Debug.Log("🎉 ¡ENSAMBLE COMPLETADO! ¡FELICIDADES!");
        OnAssemblyComplete?.Invoke();
        // Aquí luego activaremos fuegos artificiales o el panel de victoria
    }
}