using UnityEngine;

public class PCPart : MonoBehaviour
{
    [Header("Identidad")]
    // ESTAS SON LAS DOS VARIABLES QUE TE FALTAN:
    public string partName = "Componente Genérico"; // <--- El Socket busca esto
    public ConnectionTypeSO connectionType;          // <--- El Socket busca esto también

    [Header("Estado")]
    public bool IsInstalled { get; private set; } = false;
    
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;

    private void Start()
    {
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
    }

    public void TryToSnap()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 0.5f);
        
        SocketSystem bestSocket = null;
        float closestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            SocketSystem socket = hit.GetComponent<SocketSystem>();
            
            // Aquí le pasamos "this" (este script), así que socket leerá nuestras variables públicas
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
            Debug.Log("❌ No encaja o está lejos.");
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