using UnityEngine;
using System.Collections.Generic;

public class ObjectHighlighter : MonoBehaviour
{
    [Header("Configuración de Outline")]
    [Tooltip("Material que se usará para el borde/outline")]
    public Material outlineMaterial;

    private Renderer[] _renderers;
    private bool _isHighlighted = false;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
    }

    public void EnableHighlight()
    {
        if (_isHighlighted || outlineMaterial == null) return;

        foreach (var renderer in _renderers)
        {
            List<Material> materials = new List<Material>(renderer.sharedMaterials);
            materials.Add(outlineMaterial);
            renderer.materials = materials.ToArray();
        }
        _isHighlighted = true;
    }

    public void DisableHighlight()
    {
        if (!_isHighlighted || outlineMaterial == null) return;

        foreach (var renderer in _renderers)
        {
            List<Material> materials = new List<Material>(renderer.sharedMaterials);
            materials.RemoveAll(m => m.name.StartsWith(outlineMaterial.name)); // Remover por nombre para evitar problemas de instancia
            
            // Fallback por si el nombre cambió (instancia)
            if (materials.Count > renderer.sharedMaterials.Length - 1) 
            {
                 // Si no se borró por nombre, borramos el último (asumiendo que fue el que agregamos)
                 materials.RemoveAt(materials.Count - 1);
            }
            
            renderer.materials = materials.ToArray();
        }
        _isHighlighted = false;
    }
}
