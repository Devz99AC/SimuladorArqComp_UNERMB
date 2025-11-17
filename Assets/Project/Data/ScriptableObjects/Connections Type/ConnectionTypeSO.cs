using UnityEngine;

// Esta línea es vital: permite crear los archivos desde el menú de Unity
[CreateAssetMenu(fileName = "NewConnectionType", menuName = "Simulador/Connection Type")]
public class ConnectionTypeSO : ScriptableObject
{
    public string labelName; // Nombre visible (ej: "Socket CPU")
    public Color debugColor = Color.white; // Color para identificarlo visualmente
}