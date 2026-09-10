using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Reemplazo UITK de UISoundController (que dependía de IPointerEnter/Click de
/// uGUI, AUDITORIA §2.4). Cablea sonidos de hover/click de UI Toolkit a
/// AudioManager. Los clips se configuran una vez por vista y se comparten;
/// así se evita un componente por botón.
///
/// Uso típico en el controlador de una vista:
///   UiSound.Configure(hoverClip, clickClip);
///   UiSound.AttachAll(root);            // o UiSound.Attach(miBoton);
/// </summary>
public static class UiSound
{
    private static AudioClip _hover;
    private static AudioClip _click;
    private static float _hoverVol = 0.5f;
    private static float _clickVol = 1.0f;

    /// <summary>Define los clips compartidos de UI. Llamar una vez por vista.</summary>
    public static void Configure(AudioClip hover, AudioClip click, float hoverVol = 0.5f, float clickVol = 1.0f)
    {
        _hover = hover;
        _click = click;
        _hoverVol = hoverVol;
        _clickVol = clickVol;
    }

    /// <summary>Cablea un elemento (típicamente un Button) a los sonidos de UI.</summary>
    public static void Attach(VisualElement element)
    {
        if (element == null) return;
        element.RegisterCallback<MouseEnterEvent>(_ => Play(_hover, _hoverVol));
        if (element is Button btn) btn.clicked += () => Play(_click, _clickVol);
        else element.RegisterCallback<ClickEvent>(_ => Play(_click, _clickVol));
    }

    /// <summary>Cablea todos los Button descendientes de un root.</summary>
    public static void AttachAll(VisualElement root)
    {
        if (root == null) return;
        root.Query<Button>().ForEach(Attach);
    }

    private static void Play(AudioClip clip, float volume)
    {
        if (clip != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySound(clip, volume);
    }
}
