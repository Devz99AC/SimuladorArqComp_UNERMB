using UnityEngine;

/// <summary>
/// Lista ordenada de laboratorios que ofrece el selector (ModeSelectionView). Añadir o
/// reordenar laboratorios = editar este asset; la pantalla se construye sola.
/// </summary>
[CreateAssetMenu(fileName = "LabCatalog", menuName = "Simulador/Catálogo de laboratorios")]
public class LabCatalogSO : ScriptableObject
{
    public LabDefinitionSO[] labs;
}
