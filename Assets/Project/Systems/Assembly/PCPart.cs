using UnityEngine;

public class PCPart : MonoBehaviour
{
    [Header("Identidad Técnica")]
    public string partName = "Componente Genérico"; // Nombre interno para logs
    public ConnectionTypeSO connectionType;          // El "Carnet" (Rojo/Azul)

    [Header("Información Educativa (UI)")]
    public string title = "Título del Componente";   // Título para la ficha (Ej: CPU Intel i9)
    [TextArea(4, 10)]                                // Área de texto grande para escribir cómodo
    public string description = "Escribe aquí la descripción teórica y funcional del componente...";

    [Header("Estado")]
    public bool IsInstalled { get; private set; } = false;
    
    // Variables para recordar dónde estaba antes de agarrarlo
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;

    private void Start()
    {
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
    }

    // Método llamado por InteractionManager al soltar la pieza
    public void TryToSnap()
    {
        // 1. Buscar Sockets cercanos en un radio de 0.5 metros
        Collider[] hits = Physics.OverlapSphere(transform.position, 0.5f);
        
        SocketSystem bestSocket = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            // Buscamos si el objeto tiene el componente SocketSystem
            SocketSystem socket = hit.GetComponent<SocketSystem>();
            
            // Si es un socket y nos acepta (tipo correcto y vacío)
            // Nota: Le pasamos 'this' para que el socket pueda leer nuestras variables
            if (socket != null && socket.CanAccept(this))
            {
                // Calculamos distancia para elegir el más cercano si hay varios
                float dist = Vector3.Distance(transform.position, socket.snapPoint.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestSocket = socket;
                }
            }
        }

        // 2. Resultado de la búsqueda
        if (bestSocket != null)
        {
            // ¡ENCAJE EXITOSO!
            bestSocket.InstallPart(this);
            IsInstalled = true;
        }
        else
        {
            // FALLO: No encaja o está lejos
            // Debug.Log("❌ No se encontró socket válido. Regresando a la mesa.");
            ResetToTable();
        }
    }

    public void ResetToTable()
    {
        // Regresa suavemente o directo a su posición original
        transform.position = _initialPosition;
        transform.rotation = _initialRotation;
        IsInstalled = false;
    }
}