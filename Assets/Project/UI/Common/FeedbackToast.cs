using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Tipo de mensaje del toast. Determina icono, texto y color de refuerzo.</summary>
public enum ToastKind { Info, Success, Error, Warning }

/// <summary>
/// Toast de feedback reutilizable: icono + texto + motivo opcional.
/// Implementa la regla de accesibilidad AUDITORIA §7.1 / §8: el estado NUNCA se
/// comunica solo por color; siempre hay icono + texto, y el color solo refuerza.
/// Lo usa el ensamblaje para el acierto/rechazo (el "motivo" sale de
/// <see cref="CompatResult.Reason"/>).
///
/// Si no se pasa un Sprite, el icono se DIBUJA por vector (Painter2D) según el tipo
/// (✓ ✕ ! i) — nítido a cualquier escala, como <see cref="DiagonalStripes"/>. Su color
/// sale del USS (<c>.toast--success .toast__icon { color: … }</c>), no de C#.
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
    private ToastKind _kind;
    private bool _vectorIcon = true;

    public FeedbackToast()
    {
        AddToClassList("toast");
        AddToClassList("is-hidden");
        pickingMode = PickingMode.Ignore;   // no bloquea la interacción con la escena

        _icon = new VisualElement();
        _icon.AddToClassList("toast__icon");
        _icon.pickingMode = PickingMode.Ignore;
        _icon.generateVisualContent += DrawIcon;

        var body = new VisualElement();
        body.AddToClassList("toast__body");
        body.pickingMode = PickingMode.Ignore;

        _text = new Label();
        _text.AddToClassList("toast__text");
        _text.pickingMode = PickingMode.Ignore;

        _reason = new Label();
        _reason.AddToClassList("toast__reason");
        _reason.pickingMode = PickingMode.Ignore;

        body.Add(_text);
        body.Add(_reason);
        Add(_icon);
        Add(body);
    }

    /// <summary>
    /// Muestra el toast. <paramref name="icon"/> es opcional: sin él se dibuja el icono
    /// vectorial del tipo (icono + texto SIEMPRE). Con <paramref name="durationMs"/> &gt; 0
    /// se auto-oculta pasado ese tiempo.
    /// </summary>
    public void Show(string message, ToastKind kind = ToastKind.Info,
                     string reason = null, Sprite icon = null, int durationMs = 3000)
    {
        RemoveFromClassList("toast--info");
        RemoveFromClassList("toast--success");
        RemoveFromClassList("toast--error");
        RemoveFromClassList("toast--warning");
        AddToClassList(KindClass(kind));
        _kind = kind;

        _text.text = message ?? string.Empty;

        bool hasReason = !string.IsNullOrEmpty(reason);
        _reason.text = hasReason ? reason : string.Empty;
        _reason.style.display = hasReason ? DisplayStyle.Flex : DisplayStyle.None;

        _vectorIcon = icon == null;
        _icon.style.backgroundImage = _vectorIcon ? new StyleBackground(StyleKeyword.None) : new StyleBackground(icon);
        _icon.MarkDirtyRepaint();

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

    private void DrawIcon(MeshGenerationContext mgc)
    {
        if (!_vectorIcon) return;
        Rect r = _icon.contentRect;
        float size = Mathf.Min(r.width, r.height);
        if (size <= 1f) return;

        var p = mgc.painter2D;
        Vector2 c = r.center;
        float rad = size * 0.5f;

        // Disco de color (color del USS) + glifo blanco
        p.fillColor = _icon.resolvedStyle.color;
        p.BeginPath();
        p.Arc(c, rad, Angle.Degrees(0f), Angle.Degrees(360f));
        p.ClosePath();
        p.Fill();

        p.strokeColor = Color.white;
        p.fillColor = Color.white;
        p.lineWidth = size * 0.12f;
        p.lineCap = LineCap.Round;
        p.lineJoin = LineJoin.Round;

        switch (_kind)
        {
            case ToastKind.Success:
                p.BeginPath();
                p.MoveTo(c + new Vector2(-0.42f, 0.02f) * rad);
                p.LineTo(c + new Vector2(-0.12f, 0.32f) * rad);
                p.LineTo(c + new Vector2(0.45f, -0.30f) * rad);
                p.Stroke();
                break;
            case ToastKind.Error:
                p.BeginPath();
                p.MoveTo(c + new Vector2(-0.34f, -0.34f) * rad);
                p.LineTo(c + new Vector2(0.34f, 0.34f) * rad);
                p.MoveTo(c + new Vector2(0.34f, -0.34f) * rad);
                p.LineTo(c + new Vector2(-0.34f, 0.34f) * rad);
                p.Stroke();
                break;
            case ToastKind.Warning:
                p.BeginPath();
                p.MoveTo(c + new Vector2(0f, -0.48f) * rad);
                p.LineTo(c + new Vector2(0f, 0.12f) * rad);
                p.Stroke();
                Dot(p, c + new Vector2(0f, 0.44f) * rad, size * 0.075f);
                break;
            default:
                Dot(p, c + new Vector2(0f, -0.44f) * rad, size * 0.075f);
                p.BeginPath();
                p.MoveTo(c + new Vector2(0f, -0.12f) * rad);
                p.LineTo(c + new Vector2(0f, 0.48f) * rad);
                p.Stroke();
                break;
        }
    }

    private static void Dot(Painter2D p, Vector2 at, float radius)
    {
        p.BeginPath();
        p.Arc(at, radius, Angle.Degrees(0f), Angle.Degrees(360f));
        p.ClosePath();
        p.Fill();
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
