using UnityEngine;

/// <summary>
/// El PROGRAMA completo del módulo teórico: la lista ordenada de unidades más
/// los datos de la unidad curricular y la bibliografía.
///
/// Es la única referencia que necesita <c>TheoryView</c>: cambiar el contenido
/// del módulo es editar este asset, sin tocar la escena ni el código
/// (mismo patrón data-driven que ya usaba el índice de temas).
/// </summary>
[CreateAssetMenu(fileName = "TheorySyllabus", menuName = "Simulador/Theory Syllabus")]
public class TheorySyllabusSO : ScriptableObject
{
    [Header("Unidad curricular")]
    public string courseName = "Arquitectura del Computador";
    public string courseCode = "ACT139";
    public string courseTrajectory = "Trayecto I";
    public string courseCredits = "9 unidades crédito";

    [Header("Contenido")]
    public TheoryUnitSO[] units;

    [Header("Bibliografía")]
    [Tooltip("Una referencia por línea, en formato APA.")]
    [TextArea(6, 20)]
    public string references;
}
