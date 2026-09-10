using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.UIElements;

/// <summary>
/// Convierte el marcado ligero de <see cref="TheoryTopicSO.contentText"/> en
/// elementos de UI Toolkit con jerarquía visual real.
///
/// El módulo teórico antiguo volcaba TODO el tema en un único Label: un muro de
/// texto sin títulos ni listas, imposible de recorrer con la vista. Aquí cada
/// línea se convierte en el elemento que le corresponde, y el USS
/// (Theory.uss, bloque "Marcado") se encarga del ritmo tipográfico.
///
/// <para>Marcas reconocidas (una por línea, al principio de la línea):</para>
/// <list type="bullet">
///   <item><c>## Título</c> — título de sección</item>
///   <item><c>### Título</c> — subtítulo</item>
///   <item><c>- texto</c> — elemento de lista con viñeta</item>
///   <item><c>1. texto</c> — elemento de lista numerada (respeta el número escrito)</item>
///   <item><c>: Término | Definición</c> — fila de glosario</item>
///   <item><c>&gt; texto</c> — recuadro de dato clave</item>
///   <item>cualquier otra línea — párrafo</item>
///   <item><c>**negrita**</c> — funciona dentro de todas las anteriores</item>
/// </list>
/// </summary>
public static class TheoryMarkup
{
    // Clases USS (definidas en Theory.uss). Públicas para que la vista pueda
    // reutilizarlas en contenido que no venga del marcado (p. ej. el resumen).
    public const string UssHeading   = "md-h2";
    public const string UssSubhead   = "md-h3";
    public const string UssParagraph = "md-p";
    public const string UssList      = "md-list";
    public const string UssItem      = "md-item";
    public const string UssItemMark  = "md-item__mark";
    public const string UssItemText  = "md-item__text";
    public const string UssDef       = "md-def";
    public const string UssDefTerm   = "md-def__term";
    public const string UssDefBody   = "md-def__body";
    public const string UssCallout   = "md-callout";
    public const string UssCalloutTx = "md-callout__text";

    private static readonly Regex BoldPattern = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
    private static readonly Regex NumberedPattern = new Regex(@"^(\d+)\.\s+(.*)$", RegexOptions.Compiled);

    /// <summary>
    /// Vacía <paramref name="container"/> y lo vuelve a poblar con el marcado.
    /// Las listas consecutivas se agrupan para que el espaciado entre elementos
    /// de una misma lista sea menor que el que las separa de un párrafo.
    /// </summary>
    public static void Render(VisualElement container, string markup)
    {
        if (container == null) return;
        container.Clear();
        if (string.IsNullOrWhiteSpace(markup)) return;

        // Grupo de lista abierto: se cierra en cuanto llega una línea que no
        // sea un elemento de lista.
        VisualElement openList = null;

        foreach (string rawLine in SplitLines(markup))
        {
            string line = rawLine.Trim();

            if (line.Length == 0) { openList = null; continue; }

            if (line.StartsWith("## "))
            {
                openList = null;
                container.Add(MakeLabel(line.Substring(3), UssHeading));
            }
            else if (line.StartsWith("### "))
            {
                openList = null;
                container.Add(MakeLabel(line.Substring(4), UssSubhead));
            }
            else if (line.StartsWith("> "))
            {
                openList = null;
                container.Add(MakeCallout(line.Substring(2)));
            }
            else if (line.StartsWith(": "))
            {
                openList = null;
                container.Add(MakeDefinition(line.Substring(2)));
            }
            else if (line.StartsWith("- "))
            {
                openList = EnsureList(container, openList);
                openList.Add(MakeItem("•", line.Substring(2)));
            }
            else
            {
                Match numbered = NumberedPattern.Match(line);
                if (numbered.Success)
                {
                    openList = EnsureList(container, openList);
                    openList.Add(MakeItem(numbered.Groups[1].Value + ".", numbered.Groups[2].Value));
                }
                else
                {
                    openList = null;
                    container.Add(MakeLabel(line, UssParagraph));
                }
            }
        }
    }

    /// <summary>Aplica solo el formato en línea (**negrita**) a un texto suelto.</summary>
    public static string Inline(string text)
    {
        return string.IsNullOrEmpty(text) ? string.Empty : BoldPattern.Replace(text, "<b>$1</b>");
    }

    // --- Constructores de elementos ---

    private static IEnumerable<string> SplitLines(string markup)
    {
        return markup.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private static Label MakeLabel(string text, string ussClass)
    {
        var label = new Label(Inline(text));
        label.AddToClassList(ussClass);
        label.enableRichText = true;
        // Sin esto UI Toolkit no parte las líneas largas y el texto se sale.
        label.style.whiteSpace = WhiteSpace.Normal;
        return label;
    }

    private static VisualElement EnsureList(VisualElement container, VisualElement openList)
    {
        if (openList != null) return openList;
        var list = new VisualElement();
        list.AddToClassList(UssList);
        container.Add(list);
        return list;
    }

    /// <summary>Fila de lista: marca (viñeta o número) en columna propia y texto alineado.</summary>
    private static VisualElement MakeItem(string mark, string text)
    {
        var row = new VisualElement();
        row.AddToClassList(UssItem);

        var markLabel = new Label(mark);
        markLabel.AddToClassList(UssItemMark);

        var textLabel = MakeLabel(text, UssItemText);

        row.Add(markLabel);
        row.Add(textLabel);
        return row;
    }

    /// <summary>Fila de glosario "Término | Definición".</summary>
    private static VisualElement MakeDefinition(string text)
    {
        int separator = text.IndexOf('|');
        string term = separator >= 0 ? text.Substring(0, separator).Trim() : text.Trim();
        string body = separator >= 0 ? text.Substring(separator + 1).Trim() : string.Empty;

        var row = new VisualElement();
        row.AddToClassList(UssDef);
        row.Add(MakeLabel(term, UssDefTerm));
        if (body.Length > 0) row.Add(MakeLabel(body, UssDefBody));
        return row;
    }

    /// <summary>Recuadro de dato clave (barra naranja + fondo suave).</summary>
    private static VisualElement MakeCallout(string text)
    {
        var box = new VisualElement();
        box.AddToClassList(UssCallout);
        box.Add(MakeLabel(text, UssCalloutTx));
        return box;
    }
}
