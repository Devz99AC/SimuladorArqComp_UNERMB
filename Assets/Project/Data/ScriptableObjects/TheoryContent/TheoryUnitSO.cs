using UnityEngine;

/// <summary>
/// Una UNIDAD del contenido analítico de la unidad curricular "Arquitectura del
/// Computador" (ACT139, Trayecto I). Agrupa los temas que se leen dentro de
/// ella y da al índice lateral su estructura de dos niveles
/// (unidad → tema), en lugar de la lista plana de 20 elementos anterior.
/// </summary>
[CreateAssetMenu(fileName = "NewTheoryUnit", menuName = "Simulador/Theory Unit")]
public class TheoryUnitSO : ScriptableObject
{
    [Header("Identificación")]
    [Tooltip("Número de unidad del programa (1..11). Se muestra como 'Unidad N'.")]
    public int number = 1;

    public string title;

    [Tooltip("Qué se aprende en esta unidad. Se muestra en la portada de la unidad.")]
    [TextArea(2, 5)]
    public string description;

    [Header("Temas")]
    [Tooltip("Temas de la unidad, en el orden en que deben leerse.")]
    public TheoryTopicSO[] topics;
}
