using UnityEngine;

/// <summary>
/// Un laboratorio de ensamblaje (ATX, PC compacta, laptop…): lo que muestra su tarjeta en el
/// selector y la escena que abre. Mientras esté en construcción, <see cref="available"/> = false
/// y la tarjeta aparece como «Próximamente».
/// </summary>
[CreateAssetMenu(fileName = "Lab_", menuName = "Simulador/Laboratorio")]
public class LabDefinitionSO : ScriptableObject
{
    [Header("Tarjeta")]
    [Tooltip("Etiqueta corta sobre el título. Ej: \"LABORATORIO 1\"")]
    public string tag = "LABORATORIO";
    public string title = "Laboratorio";
    [TextArea(2, 5)]
    public string description;
    [Tooltip("Qué se aprende: un punto por línea (2-4 puntos cortos)")]
    public string[] learns;
    [Tooltip("Imagen opcional de la tarjeta. Sin imagen se usa el fondo de rayas con el número.")]
    public Sprite preview;

    [Header("Escena")]
    [Tooltip("Nombre de la escena (debe estar en Build Settings)")]
    public string sceneName;
    [Tooltip("Desmárcalo mientras el laboratorio esté en construcción: la tarjeta se muestra como «Próximamente»")]
    public bool available = true;
}
