using UnityEngine;

/// <summary>
/// Componente opcional en una pieza arrastrable que limita adónde puede llevarse.
/// Lo usa <see cref="CablePlug"/>: un conector no puede alejarse más que la longitud
/// de su cable. <see cref="InteractionManager"/> lo consulta cada frame de arrastre.
/// </summary>
public interface IDragConstraint
{
    /// <summary>Posición deseada (mundo) → posición permitida.</summary>
    Vector3 ConstrainDragPosition(Vector3 desired);

    /// <summary>Orientación que debe tomar la pieza mientras se arrastra en <paramref name="position"/>.</summary>
    Quaternion DragRotation(Vector3 position, Quaternion current);
}
