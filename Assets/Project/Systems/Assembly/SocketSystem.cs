using System.Collections.Generic;
using UnityEngine;

public class SocketSystem : MonoBehaviour
{
    // Registro de sockets activos. Sustituye al OverlapSphere(0.5f) sin máscara de PCPart
    // (AUDITORIA §6.3) y permite que GameManager cuente el objetivo del laboratorio solo.
    public static readonly List<SocketSystem> All = new List<SocketSystem>();

    [Header("Configuración")]
    [Tooltip("El tipo de pieza que este socket acepta (ej: Type_CPU)")]
    public ConnectionTypeSO allowedType;

    [Tooltip("El punto exacto donde la pieza se anclará (Hijo vacío)")]
    public Transform snapPoint;

    [Tooltip("Cuenta para completar el laboratorio. Desmárcalo en lugares opcionales (p. ej. un 2.º par de ranuras de RAM).")]
    public bool required = true;

    [Tooltip("Tolerancia de encaje (unidades de mundo, en horizontal). <= 0: automática, proporcional al tamaño de la pieza.")]
    public float snapRadius = 0f;

    [Tooltip("Nota que se muestra al instalar aquí: el encaje es válido, pero hay algo que aprender " +
             "(p. ej. \"con dos módulos, mejor A2 y B2\"). Vacío = mensaje de éxito normal.")]
    [TextArea(2, 4)]
    public string installNote;

    [Tooltip("Orden de montaje: estos lugares deben estar completos antes (p. ej. el disipador exige la CPU). " +
             "Mientras este lugar esté ocupado, tampoco se pueden vaciar.")]
    public List<SocketSystem> requires = new List<SocketSystem>();

    [Header("Estado")]
    public bool isOccupied = false;

    /// <summary>Pieza instalada (o reservada mientras termina su animación de encaje).</summary>
    public PCPart Occupant { get; private set; }

    public Vector3 SnapPosition => snapPoint != null ? snapPoint.position : transform.position;
    public Quaternion SnapRotation => snapPoint != null ? snapPoint.rotation : transform.rotation;

    private void OnEnable()
    {
        if (!All.Contains(this)) All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Diferido: al añadir el componente por código/menú, OnValidate llega antes de asignar los campos.
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            if (allowedType == null) Debug.LogWarning($"⚠️ SocketSystem '{name}' no tiene allowedType: no aceptará ninguna pieza.", this);
            if (snapPoint == null) Debug.LogWarning($"⚠️ SocketSystem '{name}' no tiene snapPoint: se usará su propio Transform.", this);
        };
    }
#endif

    // Función 1: Validación (¿Puedo entrar? y si no, ¿por qué?)
    public CompatResult Evaluate(PCPart part)
    {
        // Blindaje null == null (AUDITORIA §5.2): sin tipo a ambos lados no hay encaje.
        if (part == null || allowedType == null || part.connectionType == null)
            return CompatResult.Reject("Esta pieza no se instala aquí");

        // Si ya hay algo puesto, rechazar.
        if (isOccupied && Occupant != part)
            return CompatResult.Reject("Este lugar ya está ocupado",
                "Retira primero el componente instalado (arrástralo fuera) o usa otro lugar libre.");

        // Si el tipo de conexión no coincide (Rojo con Azul), rechazar explicando qué va aquí.
        if (part.connectionType != allowedType)
        {
            string here = "Aquí va " + allowedType.DisplayName + ".";
            string hint = part.connectionType.placementHint;
            return CompatResult.Reject(
                CompatResult.Capitalize(part.connectionType.DisplayName) + " no va aquí",
                string.IsNullOrEmpty(hint) ? here : here + " " + hint);
        }

        // Orden de montaje: lo que tiene que estar antes.
        foreach (var req in requires)
        {
            if (req == null || req.IsComplete) continue;
            string what = req.allowedType != null ? req.allowedType.DisplayName : "otro componente";
            return CompatResult.Reject("Todavía no se puede instalar aquí",
                "Antes hay que instalar " + what + ".");
        }

        // Si todo coincide, aprobar.
        return CompatResult.Accept();
    }

    public bool CanAccept(PCPart part) => Evaluate(part).Ok;

    /// <summary>Ocupado por una pieza ya asentada (no solo reservada durante su animación).</summary>
    public bool IsComplete => isOccupied && Occupant != null && Occupant.IsInstalled;

    /// <summary>¿Se puede retirar la pieza de aquí? No, si otro lugar ocupado depende de este.</summary>
    public CompatResult EvaluateRemoval()
    {
        foreach (var other in All)
        {
            if (other == null || other == this || !other.isOccupied || !other.requires.Contains(this)) continue;
            string blocker = other.allowedType != null ? other.allowedType.DisplayName : "otro componente";
            return CompatResult.Reject("Todavía no se puede retirar",
                "Primero retira " + blocker + ": depende de esta pieza (va encima o conectado a ella).");
        }
        return CompatResult.Accept();
    }

    /// <summary>Reserva el socket mientras la pieza anima hacia él (nadie más puede ocuparlo).</summary>
    public void Reserve(PCPart part)
    {
        isOccupied = true;
        Occupant = part;
    }

    // Función 2: Instalación (¡Pasa!)
    public void InstallPart(PCPart part)
    {
        Reserve(part);

        Transform anchor = snapPoint != null ? snapPoint : transform;
        part.transform.SetParent(anchor);
        part.transform.localPosition = Vector3.zero;
        part.transform.localRotation = Quaternion.identity;

        if (GameManager.Instance != null) GameManager.Instance.RegisterInstallation(part.partName);

        Debug.Log($"✅ ÉXITO: {part.partName} conectado correctamente.");
    }

    // Función 3: Desinstalación (el ensayo-error necesita poder deshacer — AUDITORIA §6.2)
    public void RemovePart(PCPart part)
    {
        if (Occupant != part) return;

        isOccupied = false;
        Occupant = null;

        if (GameManager.Instance != null) GameManager.Instance.UnregisterInstallation(part.partName);
    }

    /// <summary>Tolerancia efectiva para esta pieza: la del inspector o ~40% del tamaño de la pieza.</summary>
    public float RadiusFor(PCPart part)
    {
        if (snapRadius > 0f) return snapRadius;
        return Mathf.Max(part.Size * 0.4f, 0.02f);
    }

    /// <summary>
    /// Busca el socket "al que apunta" la pieza: el que tenga más cerca (en horizontal) el
    /// lugar donde quedaría su centro visual una vez instalada, normalizado por su tolerancia.
    /// Devuelve null si ninguno está a tiro. <paramref name="result"/> dice si encajaría y,
    /// si no, por qué — así soltar sobre el lugar equivocado enseña en vez de fallar en silencio.
    /// </summary>
    public static SocketSystem FindTarget(PCPart part, out CompatResult result)
    {
        result = default;
        SocketSystem best = null;
        float bestScore = 1f;
        Vector3 current = part.VisualCenter;

        foreach (var socket in All)
        {
            // Los lugares que lleva la propia pieza (p. ej. los de la placa base) viajan con ella: no son destino.
            if (socket == null || socket.transform.IsChildOf(part.transform)) continue;
            Vector3 installed = part.VisualCenterAt(socket.SnapPosition, socket.SnapRotation);
            Vector3 delta = installed - current;
            delta.y = 0f; // el arrastre es sobre un plano horizontal: la altura no cuenta

            float score = delta.magnitude / socket.RadiusFor(part);
            if (score <= bestScore)
            {
                bestScore = score;
                best = socket;
            }
        }

        if (best != null) result = best.Evaluate(part);
        return best;
    }
}
