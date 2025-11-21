using UnityEngine;

public class PCPart : MonoBehaviour
{
    [Header("Configuración Física")]
    [Tooltip("Desmárcalo para objetos fijos como la Motherboard base")]
    public bool isDraggable = true; 

    [Header("Identidad Técnica")]
    public string partName = "Componente Genérico"; 
    public ConnectionTypeSO connectionType;          

    [Header("Información Educativa (UI)")]
    public string title = "Título del Componente";   
    [TextArea(4, 10)]                                
    public string description = "Descripción...";

    [Header("Estado")]
    public bool IsInstalled { get; private set; } = false;
    
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;

    private void Start()
    {
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        
        // TRUCO: Si la pieza no se puede arrastrar (como la Motherboard), 
        // le decimos al sistema que ya está "Instalada" para que no intente moverla.
        if (!isDraggable) IsInstalled = true; 
    }

    public void TryToSnap()
    {
        // Seguridad extra: Si no es arrastrable, no hacemos nada
        if (!isDraggable) return;

        Collider[] hits = Physics.OverlapSphere(transform.position, 0.5f);
        SocketSystem bestSocket = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            SocketSystem socket = hit.GetComponent<SocketSystem>();
            if (socket != null && socket.CanAccept(this))
            {
                float dist = Vector3.Distance(transform.position, socket.snapPoint.position);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    bestSocket = socket;
                }
            }
        }

        if (bestSocket != null)
        {
            bestSocket.InstallPart(this);
            IsInstalled = true;
        }
        else
        {
            ResetToTable();
        }
    }

    public void ResetToTable()
    {
        transform.position = _initialPosition;
        transform.rotation = _initialRotation;
        IsInstalled = false;
    }
}