using UnityEngine;

// Esta línea es vital: permite crear los archivos desde el menú de Unity
[CreateAssetMenu(fileName = "NewConnectionType", menuName = "Simulador/Connection Type")]
public class ConnectionTypeSO : ScriptableObject
{
    public string labelName; // Nombre visible (ej: "Socket CPU")
    public Color debugColor = Color.white; // Color para identificarlo visualmente

    [Header("Textos educativos (feedback de ensamblaje)")]
    [Tooltip("Qué es, con artículo y en minúscula. Ej: \"la memoria RAM\". Se usa en los mensajes de acierto/rechazo.")]
    public string displayName;

    [Tooltip("Dónde se instala, en una o dos frases. Se muestra como motivo cuando el estudiante se equivoca de lugar.")]
    [TextArea(2, 5)]
    public string placementHint;

    /// <summary>Nombre para mensajes; cae en <see cref="labelName"/> si no hay uno educativo.</summary>
    public string DisplayName => string.IsNullOrEmpty(displayName) ? labelName : displayName;
}
