using UnityEngine;

/// <summary>
/// Marca la pieza para el contorno de selección: enciende el bit «Resaltado» del <c>renderingLayerMask</c>
/// de sus renderers y lo apaga al salir. La silueta la dibuja <see cref="SelectionOutlineFeature"/> en pantalla:
/// una sola por pieza aunque tenga cien hijos, del mismo grosor en píxeles para un tornillo que para la placa.
/// No toca materiales ni crea arrays (los renderers siguen en su lote del SRP Batcher).
/// </summary>
public class ObjectHighlighter : MonoBehaviour
{
    private Renderer[] _renderers;
    private bool _marked;

    // En Awake: las señales de puerto (CablePortMarker) se crean en Start y así no se llevan el contorno.
    private void Awake() => _renderers = GetComponentsInChildren<Renderer>();

    public void SetHighlight(OutlineStyle style)
    {
        ObjectHighlighter previous = SelectionOutline.Current;
        if (previous != null && previous != this) previous.RemoveHighlight(); // solo una pieza a la vez
        if (!_marked) Mark(true);
        SelectionOutline.Set(this, style);
    }

    public void RemoveHighlight()
    {
        if (!_marked) return;
        Mark(false);
        SelectionOutline.Release(this);
    }

    private void OnDisable() => RemoveHighlight();

    private void Mark(bool on)
    {
        if (_renderers == null) _renderers = GetComponentsInChildren<Renderer>();
        uint bit = SelectionOutline.RenderingLayer;
        foreach (var r in _renderers)
        {
            if (r == null) continue;
            r.renderingLayerMask = on ? r.renderingLayerMask | bit : r.renderingLayerMask & ~bit;
        }
        _marked = on;
    }
}
