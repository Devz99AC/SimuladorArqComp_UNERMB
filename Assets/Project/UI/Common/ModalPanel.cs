using System;
using UnityEngine.UIElements;

/// <summary>
/// Panel modal reutilizable. Es un velo a pantalla completa que ABSORBE el
/// puntero (pickingMode = Position), de modo que no se pueda interactuar con la
/// escena por debajo mientras está abierto. Base de la corrección AUDITORIA §7.2
/// (en pausa/victoria hoy se puede seguir arrastrando piezas).
///
/// El velo NO congela la lógica 3D por sí solo: el controlador de AssemblyLab
/// debe suscribirse a <see cref="OnOpenChanged"/> para deshabilitar
/// InteractionManager/CADCameraController al abrir y rehabilitarlos al cerrar.
///
/// Usable desde UXML como &lt;ModalPanel&gt;…contenido…&lt;/ModalPanel&gt;.
/// </summary>
[UxmlElement]
public partial class ModalPanel : VisualElement
{
    public const string UssOverlay = "modal-overlay";

    /// <summary>Se dispara con true al abrir y false al cerrar.</summary>
    public event Action<bool> OnOpenChanged;

    /// <summary>Si es true, un clic en el velo (fuera del contenido) cierra el panel.</summary>
    [UxmlAttribute] public bool closeOnBackdrop { get; set; }

    public bool IsOpen { get; private set; }

    public ModalPanel()
    {
        AddToClassList(UssOverlay);
        pickingMode = PickingMode.Position;     // absorbe el puntero (bloquea el 3D debajo)
        style.display = DisplayStyle.None;       // oculto por defecto
        focusable = true;
        RegisterCallback<PointerDownEvent>(OnBackdropPointerDown);
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        style.display = DisplayStyle.Flex;
        BringToFront();
        Focus();
        OnOpenChanged?.Invoke(true);
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        style.display = DisplayStyle.None;
        OnOpenChanged?.Invoke(false);
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    private void OnBackdropPointerDown(PointerDownEvent evt)
    {
        // Cierra solo si el clic cae en el propio velo, no en el contenido interior.
        if (closeOnBackdrop && ReferenceEquals(evt.target, this))
            Close();
    }
}
