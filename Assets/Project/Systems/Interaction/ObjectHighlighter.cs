using UnityEngine;
using System.Collections.Generic;

public class ObjectHighlighter : MonoBehaviour
{
    // Cache de renderers y materiales
    private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();
    private Renderer[] _renderers;
    private Material _activeMaterial;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
    }

    public void SetHighlight(Material highlightMat)
    {
        if (highlightMat == null) return;
        if (_activeMaterial == highlightMat) return; // ya está puesto: no rehacer arrays cada frame
        if (_activeMaterial != null) RemoveHighlight(); // Limpiar si ya había uno

        foreach (var renderer in _renderers)
        {
            if (renderer == null) continue;

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

            // 5. Aplicamos con sharedMaterials: .materials clonaba TODOS los materiales en
            //    cada hover (fuga + rompía el batching, AUDITORIA §9.1).
            renderer.sharedMaterials = newMats;
        }
        _activeMaterial = highlightMat;
    }

    public void RemoveHighlight()
    {
        if (_activeMaterial == null) return;

        foreach (var renderer in _renderers)
        {
            if (renderer == null) continue;
            if (_originalMaterials.TryGetValue(renderer, out Material[] originals))
            {
                // Restauramos los originales
                renderer.sharedMaterials = originals;
            }
        }
        _activeMaterial = null;
    }
}
