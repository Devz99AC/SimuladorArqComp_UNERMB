using UnityEngine;

[CreateAssetMenu(fileName = "NewTheoryTopic", menuName = "Simulador/Theory Topic")]
public class TheoryTopicSO : ScriptableObject
{
    [Header("Contenido de la Página")]
    public string title;

    [TextArea(10, 20)] 
    public string contentText; 

    [Header("Recursos Visuales")]
    public Sprite topicImage; 
}