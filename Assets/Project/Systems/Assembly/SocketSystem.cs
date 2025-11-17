using UnityEngine;

public class SocketSystem : MonoBehaviour
{
    [Header("Configuración")]
    [Tooltip("El tipo de pieza que este socket acepta (ej: Type_CPU)")]
    public ConnectionTypeSO allowedType; 
    
    [Tooltip("El punto exacto donde la pieza se anclará (Hijo vacío)")]
    public Transform snapPoint; 

    [Header("Estado")]
    public bool isOccupied = false;

    // Función 1: Validación (¿Puedo entrar?)
    public bool CanAccept(PCPart part)
    {
        // Si ya hay algo puesto, rechazar.
        if (isOccupied) return false;

        // Si el tipo de conexión no coincide (Rojo con Azul), rechazar.
        if (part.connectionType != allowedType) return false;

        // Si todo coincide, aprobar.
        return true;
    }

    // Función 2: Instalación (¡Pasa!)
    public void InstallPart(PCPart part)
    {
        isOccupied = true;

        part.transform.SetParent(snapPoint);
        part.transform.localPosition = Vector3.zero;
        part.transform.localRotation = Quaternion.identity;

        GameManager.Instance.RegisterInstallation(part.partName);

        Debug.Log($"✅ ÉXITO: {part.partName} conectado correctamente.");
        
        // Opcional: Aquí podrías reproducir un sonido de "Click"
    }
}