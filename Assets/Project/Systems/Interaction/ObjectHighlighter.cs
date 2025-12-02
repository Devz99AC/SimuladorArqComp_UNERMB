using UnityEngine;
using System.Collections.Generic;

public class ObjectHighlighter : MonoBehaviour
{
    // Cache de renderers y materiales
    private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();
    private Renderer[] _renderers;
    private bool _isActive = false;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
    }

    public void SetHighlight(Material highlightMat)
    {
        if (highlightMat == null) return;
        if (_isActive) RemoveHighlight(); // Limpiar si ya había uno

        foreach (var renderer in _renderers)
        {
            // 1. Si es la primera vez, guardamos los materiales originales
            if (!_originalMaterials.ContainsKey(renderer))
            {
                _originalMaterials.Add(renderer, renderer.sharedMaterials);
            }

            // 2. Preparamos el nuevo array (Originales + 1 hueco para el Outline)
            Material[] originals = _originalMaterials[renderer];
            Material[] newMats = new Material[originals.Length + 1];

            // 3. Copiamos los originales
            for (int i = 0; i < originals.Length; i++)
            {
                newMats[i] = originals[i];
            }

            // 4. Ponemos el Material Fresnel al final
            newMats[newMats.Length - 1] = highlightMat;

            // 5. Aplicamos
            renderer.materials = newMats;
        }
        _isActive = true;
    }

    public void RemoveHighlight()
    {
        if (!_isActive) return;

        foreach (var renderer in _renderers)
        {
            if (_originalMaterials.ContainsKey(renderer))
            {
                // Restauramos los originales
                renderer.materials = _originalMaterials[renderer];
            }
        }
        _isActive = false;
    }
}