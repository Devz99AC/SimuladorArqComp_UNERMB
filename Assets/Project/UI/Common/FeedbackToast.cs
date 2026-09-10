using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Tipo de mensaje del toast. Determina icono, texto y color de refuerzo.</summary>
public enum ToastKind { Info, Success, Error, Warning }

/// <summary>
/// Toast de feedback reutilizable: icono + texto + motivo opcional.
/// Implementa la regla de accesibilidad AUDITORIA §7.1 / §8: el estado NUNCA se
/// comunica solo por color; siempre hay icono + texto, y el color solo refuerza.
/// Pensado para el feedback de acierto/rechazo del ensamblaje (el "motivo"
/// vendrá del futuro motor de reglas, hallazgo §5.1).
///
/// Usable desde UXML como &lt;FeedbackToast /&gt;; se dispara desde C# con Show(...).
/// </summary>
[UxmlElement]
public partial class FeedbackToast : VisualElement
{
    private readonly VisualElement _icon;
    private readonly Label _text;
    private readonly Label _reason;
    private IVisualElementScheduledItem _autoHide;

    public FeedbackToast()
    {
        AddToClassList("toast");
        AddToClassList("is-hidden");
        pickingMode = PickingMode.Ignore;   // no bloquea la interacción con la escena

        _icon = new VisualElement();
        _icon.AddToClassList("toast__icon");

        var body = new VisualElement();
        body.AddToClassList("toast__body");

        _text = new Label();
        _text.AddToClassList("toast__text");

        _reason = new Label();
        _reason.AddToClassList("toast__reason");

        body.Add(_text);
        body.Add(_reason);
        Add(_icon);
        Add(body);
    }

    /// <summary>
    /// Muestra el toast. <paramref name="icon"/> es opcional pero recomendado:
    /// icono + texto SIEMPRE. Con <paramref name="durationMs"/> &gt; 0 se
    /// auto-oculta pasado ese tiempo.
    /// </summary>
    public void Show(string message, ToastKind kind = ToastKind.Info,
                     string reason = null, Sprite icon = null, int durationMs = 3000)
    {
        RemoveFromClassList("toast--info");
        RemoveFromClassList("toast--success");
        RemoveFromClassList("toast--error");
        RemoveFromClassList("toast--warning");
        AddToClassList(KindClass(kind));

        _text.text = message ?? string.Empty;

        bool hasReason = !string.IsNullOrEmpty(reason);
        _reason.text = hasReason ? reason : string.Empty;
        _reason.style.display = hasReason ? DisplayStyle.Flex : DisplayStyle.None;

        bool hasIcon = icon != null;
        if (hasIcon) _icon.style.backgroundImage = new StyleBackground(icon);
        _icon.style.display = hasIcon ? DisplayStyle.Flex : DisplayStyle.None;

        RemoveFromClassList("is-hidden");

        _autoHide?.Pause();
        if (durationMs > 0)
            _autoHide = schedule.Execute(Hide).StartingIn(durationMs);
    }

    public void Hide()
    {
        _autoHide?.Pause();
        AddToClassList("is-hidden");
    }

    private static string KindClass(ToastKind kind)
    {
        switch (kind)
        {
            case ToastKind.Success: return "toast--success";
            case ToastKind.Error:   return "toast--error";
            case ToastKind.Warning: return "toast--warning";
            default:                return "toast--info";
        }
    }
}
