using UnityEngine;

public class PCPart : MonoBehaviour
{
    [Header("Estado")]
    public bool IsInstalled { get; private set; } = false;
    
    // Guardamos la posición original por si hay que devolverla a la mesa
    private Vector3 _initialPosition;

    private void Start()
    {
        _initialPosition = transform.position;
    }

    // Este método lo llamará el InteractionManager al soltar la pieza
    public void TryToSnap()
    {
        Debug.Log("Intentando encajar la pieza: " + gameObject.name);
        
        // LÓGICA TEMPORAL PARA GREYBOXING:
        // Por ahora, simplemente la devolvemos a su sitio si la soltamos
        // (En el siguiente paso haremos que detecte el Socket real)
        
        transform.position = _initialPosition; 
    }
}