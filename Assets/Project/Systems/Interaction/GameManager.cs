using UnityEngine;
using System; // Necesario para usar los Eventos (Action)

public class GameManager : MonoBehaviour
{
    // SINGLETON: Permite acceder a este script desde cualquier lado como GameManager.Instance
    public static GameManager Instance { get; private set; }

    // El objetivo ya NO se escribe a mano (antes totalPartsToInstall = 4, AUDITORIA §13.1):
    // el laboratorio está completo cuando todos los sockets "required" están ocupados.
    // Añadir una pieza = añadir su socket; no hay contador que sincronizar.

    /// <summary>Lugares que hay que completar (sockets con <c>required</c>).</summary>
    public int TotalRequired
    {
        get
        {
            int total = 0;
            foreach (var s in SocketSystem.All) if (s.required) total++;
            return total;
        }
    }

    /// <summary>
    /// Lugares requeridos ya completados (con lo que exigen también cumplido: la tapa del equipo cerrado no cuenta
    /// al empezar, solo cuando se vuelve a cerrar con todo dentro — ver <see cref="SocketSystem.IsSatisfied"/>).
    /// </summary>
    public int InstalledCount
    {
        get
        {
            int done = 0;
            foreach (var s in SocketSystem.All) if (s.required && s.IsSatisfied) done++;
            return done;
        }
    }

    // EVENTOS: Noticias que emitimos al resto del juego (el HUD se suscribe)
    public event Action<string> OnPartInstalled;      // "Se instaló la pieza X"
    public event Action<string> OnPartRemoved;        // "Se retiró la pieza X"
    public event Action<int, int> OnProgressChanged;  // (instaladas, total)
    public event Action OnAssemblyComplete;           // "¡Ensamble terminado!"

    private bool _finished;

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

    // Recupera el singleton si Unity recompiló en mitad del Play (los estáticos vuelven a null).
    private void OnEnable()
    {
        if (Instance == null) Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        Debug.Log($"🏁 Inicio de Simulación. Lugares a completar: {TotalRequired}");
    }

    // Esta función la llama el SocketSystem cuando una pieza encaja correctamente
    public void RegisterInstallation(string partName)
    {
        int done = InstalledCount, total = TotalRequired;
        Debug.Log($"📈 Progreso: {done}/{total}");

        OnPartInstalled?.Invoke(partName);
        OnProgressChanged?.Invoke(done, total);

        // Verificar si ya ganamos
        if (!_finished && total > 0 && done >= total)
        {
            FinishSimulation();
        }
    }

    // La llama el SocketSystem cuando el estudiante retira una pieza instalada
    public void UnregisterInstallation(string partName)
    {
        OnPartRemoved?.Invoke(partName);
        OnProgressChanged?.Invoke(InstalledCount, TotalRequired);
    }

    // Lógica de Victoria
    private void FinishSimulation()
    {
        _finished = true;
        Debug.Log("🎉 ¡ENSAMBLE COMPLETADO! ¡FELICIDADES!");

        // El HUD (AssemblyHudView) está suscrito y abre el modal de victoria.
        OnAssemblyComplete?.Invoke();
    }
}
