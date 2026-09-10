using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Detecta si el puntero está sobre un elemento "pickable" de UI Toolkit
/// (paneles, botones, velos modales), NO sobre las zonas transparentes que
/// dejan pasar el clic al 3D.
///
/// Reemplaza a <c>EventSystem.current.IsPointerOverGameObject()</c>, que
/// **no ve** la UI Toolkit (esta no pasa por el EventSystem/GraphicRaycaster
/// de uGUI). Sin esto, al migrar el HUD a UITK los clics sobre los paneles se
/// "colarían" al raycast de ensamblaje. La vista del HUD se registra en
/// <see cref="Register"/>; los managers de interacción/cámara consultan
/// <see cref="IsPointerOverUI"/> cada frame.
///
/// Regla de layout que lo hace funcionar: el root y los contenedores van con
/// <c>picking-mode="Ignore"</c> (transparentes al puntero); solo los paneles
/// reales y sus botones quedan "pickables". Un velo modal a pantalla completa
/// (ModalPanel) tapa todo → congela la simulación mientras está abierto.
/// </summary>
public static class UIBlocker
{
    private static IPanel _panel;

    /// <summary>
    /// True mientras hay un modal abierto (opciones/victoria): congela TODA la
    /// interacción, independientemente de dónde esté el puntero. Es la red de
    /// seguridad de "congelar la sim con ModalPanel" (§7.2): la vista lo activa
    /// vía <see cref="ModalPanel.OnOpenChanged"/>.
    /// </summary>
    public static bool ModalOpen { get; set; }

    /// <summary>Registra el panel activo del HUD (llamar en Start de la vista).</summary>
    public static void Register(VisualElement root)
    {
        _panel = root != null ? root.panel : null;
    }

    /// <summary>Libera el registro (llamar en OnDisable/OnDestroy de la vista).</summary>
    public static void Clear()
    {
        _panel = null;
        ModalOpen = false;
    }

    /// <summary>
    /// True si <paramref name="screenPos"/> (pixeles de pantalla, origen
    /// abajo-izquierda como el Input System) cae sobre un elemento pickable,
    /// o si hay un modal abierto (congelación total).
    /// </summary>
    public static bool IsPointerOverUI(Vector2 screenPos)
    {
        if (ModalOpen) return true;
        if (_panel == null) return false;
        // El Input System usa origen abajo-izq; el panel UITK usa arriba-izq.
        Vector2 flipped = new Vector2(screenPos.x, Screen.height - screenPos.y);
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(_panel, flipped);
        return _panel.Pick(panelPos) != null;
    }
}
