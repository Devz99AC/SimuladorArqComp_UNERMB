using UnityEngine;

/// <summary>Estado del contorno de la pieza resaltada (cada uno con su color en <see cref="SelectionOutlineFeature"/>).</summary>
public enum OutlineStyle { Hover, Drag, Reject }

/// <summary>
/// Registro de la pieza resaltada que lee <see cref="SelectionOutlineFeature"/>. Solo hay una a la vez.
/// La pieza se marca con el bit «Resaltado» del <c>renderingLayerMask</c> de sus renderers (lo hace
/// <see cref="ObjectHighlighter"/>); aquí solo queda quién es y en qué estado está.
/// </summary>
public static class SelectionOutline
{
    /// <summary>Nombre del rendering layer (Project Settings ▸ Tags and Layers ▸ Rendering Layers).</summary>
    public const string LayerName = "Resaltado";

    private static uint s_Layer;

    /// <summary>Bit del rendering layer «Resaltado». Si el nombre no existe, el último bit (no lo usa nada más).</summary>
    public static uint RenderingLayer
    {
        get
        {
            if (s_Layer == 0)
            {
                int index = RenderingLayerMask.NameToRenderingLayer(LayerName);
                s_Layer = 1u << (index > 0 ? index : 31);
            }
            return s_Layer;
        }
    }

    public static ObjectHighlighter Current { get; private set; }
    public static OutlineStyle Style { get; private set; }

    /// <summary>¿Hay algo resaltado? Si no, la feature no encola ningún pase (coste cero en reposo).</summary>
    public static bool IsActive => Current != null; // también falso si la pieza se destruyó

    internal static void Set(ObjectHighlighter owner, OutlineStyle style)
    {
        Current = owner;
        Style = style;
    }

    internal static void Release(ObjectHighlighter owner)
    {
        if (Current == owner) Current = null;
    }
}
