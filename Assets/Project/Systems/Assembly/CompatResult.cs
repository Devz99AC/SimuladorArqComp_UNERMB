/// <summary>
/// Resultado de evaluar si una pieza puede instalarse en un lugar. Además del sí/no
/// lleva el "porqué" educativo en español (AUDITORIA §5.1/§7.1): el momento del error
/// es el de máximo aprendizaje, así que el rechazo nunca es silencioso.
/// </summary>
public readonly struct CompatResult
{
    public readonly bool Ok;
    /// <summary>Titular corto (ej: "La memoria RAM no va aquí").</summary>
    public readonly string Message;
    /// <summary>Explicación opcional (qué va aquí y dónde va la pieza).</summary>
    public readonly string Reason;

    private CompatResult(bool ok, string message, string reason)
    {
        Ok = ok;
        Message = message;
        Reason = reason;
    }

    public static CompatResult Accept() => new CompatResult(true, null, null);

    public static CompatResult Reject(string message, string reason = null) => new CompatResult(false, message, reason);

    /// <summary>Pone en mayúscula la primera letra (los displayName van en minúscula con artículo).</summary>
    public static string Capitalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return char.ToUpper(text[0]) + text.Substring(1);
    }
}
