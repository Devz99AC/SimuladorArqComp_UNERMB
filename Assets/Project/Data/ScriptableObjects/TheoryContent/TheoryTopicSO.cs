using UnityEngine;

/// <summary>
/// Un TEMA del módulo teórico (el nivel más fino: lo que el estudiante lee en
/// una sola pantalla). Los temas se agrupan en unidades curriculares mediante
/// <see cref="TheoryUnitSO"/>.
///
/// <para><b>contentText</b> ya no es un bloque plano: admite un marcado ligero
/// que <c>TheoryMarkup</c> convierte en elementos con jerarquía visual real
/// (títulos de sección, párrafos, viñetas, tablas de término/definición y
/// recuadros de dato clave). Sintaxis, una marca por línea:</para>
/// <code>
/// ## Título de sección
/// Un párrafo normal.
/// - Elemento de lista con viñeta
/// 1. Elemento de lista numerada
/// : Término | Definición breve del término
/// > Dato clave que se muestra en un recuadro destacado
/// **negrita** dentro de cualquier línea
/// </code>
/// </summary>
[CreateAssetMenu(fileName = "NewTheoryTopic", menuName = "Simulador/Theory Topic")]
public class TheoryTopicSO : ScriptableObject
{
    [Header("Identificación")]
    [Tooltip("Título del tema, tal y como aparece en el índice y en la cabecera del lector.")]
    public string title;

    [Tooltip("Una frase que resume el tema. Se muestra bajo el título y en el índice lateral.")]
    [TextArea(2, 3)]
    public string summary;

    [Header("Contenido (admite marcado ligero: ## - 1. : > **)")]
    [TextArea(15, 40)]
    public string contentText;

    [Header("Recursos Visuales")]
    public Sprite topicImage;

    [Tooltip("Pie de figura. Se muestra bajo la imagen; si está vacío no se dibuja.")]
    public string imageCaption;
}
