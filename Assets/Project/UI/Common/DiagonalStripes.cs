using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Fondo de rayas diagonales dibujadas por CÓDIGO (vector, Painter2D), no como
/// textura. Así se ven perfectamente NÍTIDAS a cualquier grosor (incluso 2px)
/// y a cualquier resolución: sin moiré ni desenfoque por el escalado de la UI.
/// Se anima sola (desplaza el patrón) mientras está en pantalla.
///
/// Uso en UXML: &lt;DiagonalStripes stripe-width="2" gap="2" speed="12" /&gt;
/// </summary>
[UxmlElement]
public partial class DiagonalStripes : VisualElement
{
    [UxmlAttribute] public float stripeWidth { get; set; } = 2f;   // grosor de la raya (px)
    [UxmlAttribute] public float gap { get; set; } = 2f;           // separación entre rayas (px)
    [UxmlAttribute] public float speed { get; set; } = 12f;        // velocidad de scroll (px/seg)
    [UxmlAttribute] public Color stripeColor { get; set; } = new Color(200f / 255f, 200f / 255f, 201f / 255f, 1f);
    [UxmlAttribute] public Color backColor { get; set; } = new Color(252f / 255f, 252f / 255f, 252f / 255f, 1f);

    private float _offset;
    private IVisualElementScheduledItem _anim;

    public DiagonalStripes()
    {
        generateVisualContent += OnGenerate;
        RegisterCallback<AttachToPanelEvent>(_ => { _anim?.Pause(); _anim = schedule.Execute(Tick).Every(16); });
        RegisterCallback<DetachFromPanelEvent>(_ => _anim?.Pause());
    }

    private void Tick(TimerState ts)
    {
        _offset += speed * (ts.deltaTime / 1000f);
        MarkDirtyRepaint();
    }

    private void OnGenerate(MeshGenerationContext mgc)
    {
        var r = contentRect;
        float w = r.width, h = r.height;
        if (w <= 1f || h <= 1f) return;

        var p = mgc.painter2D;

        // Fondo
        p.fillColor = backColor;
        p.BeginPath();
        p.MoveTo(new Vector2(0f, 0f));
        p.LineTo(new Vector2(w, 0f));
        p.LineTo(new Vector2(w, h));
        p.LineTo(new Vector2(0f, h));
        p.ClosePath();
        p.Fill();

        // Rayas diagonales 45° (líneas x+y = c). El paso en x+y equivale al
        // periodo perpendicular multiplicado por raíz de 2.
        float step = (stripeWidth + gap) * 1.41421356f;
        if (step < 0.5f) return;
        float off = _offset % step;

        p.lineWidth = stripeWidth;
        p.strokeColor = stripeColor;
        p.lineCap = LineCap.Butt;
        p.BeginPath();
        for (float c = off - step; c <= w + h + step; c += step)
        {
            p.MoveTo(new Vector2(c, 0f));
            p.LineTo(new Vector2(c - h, h));
        }
        p.Stroke();
    }
}
