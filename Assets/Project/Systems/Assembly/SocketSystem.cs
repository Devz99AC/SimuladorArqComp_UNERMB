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

    [Tooltip("Estos lugares tienen que estar VACÍOS para instalar o retirar aquí (p. ej. nada de dentro se toca con la tapa " +
             "puesta, ni la memoria con el módulo de discos bajado). Mensaje: «Antes hay que» + su acción de vaciar.")]
    public List<SocketSystem> requiresEmpty = new List<SocketSystem>();

    [Header("Textos de las reglas (opcional)")]
    [Tooltip("Cómo se dice llenar este lugar en los mensajes. Ej: \"bajar el módulo de discos\". Vacío = \"instalar\" + el tipo.")]
    public string fillAction;
    [Tooltip("Cómo se dice vaciarlo. Ej: \"quitar la tapa\", \"levantar el módulo de discos (haz clic en él)\". Vacío = \"quitar\" + el tipo.")]
    public string emptyAction;

    [Header("Estado")]
    public bool isOccupied = false;

    /// <summary>Pieza instalada (o reservada mientras termina su animación de encaje).</summary>
    public PCPart Occupant { get; private set; }

    /// <summary>
    /// Si no es null, este lugar es un ESTADO (cerrado = ocupado) que alterna una pieza con bisagra o pestillo: no recibe
    /// piezas arrastradas, no tiene fantasma ni cuenta como destino al arrastrar. Lo asigna <see cref="HingedPart"/>.
    /// </summary>
    public HingedPart StateOwner { get; set; }

    string TypeName => allowedType != null ? allowedType.DisplayName : "otro componente";

    /// <summary>"instalar la CPU", "bajar el módulo de discos"…</summary>
    public string FillText => string.IsNullOrEmpty(fillAction) ? "instalar " + TypeName : fillAction;

    /// <summary>"quitar la tapa", "levantar el módulo de discos"…</summary>
    public string EmptyText => string.IsNullOrEmpty(emptyAction) ? "quitar " + TypeName : emptyAction;

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

        // Lo que estorba: la tapa puesta, el módulo bajado… (primero, porque es lo que se ve).
        SocketSystem inTheWay = FirstOccupied(requiresEmpty);
        if (inTheWay != null)
            return CompatResult.Reject("Todavía no se puede instalar aquí", "Antes hay que " + inTheWay.EmptyText + ".");

        // Orden de montaje: lo que tiene que estar antes.
        foreach (var req in requires)
        {
            if (req == null || req.IsComplete) continue;
            return CompatResult.Reject("Todavía no se puede instalar aquí",
                "Antes hay que " + req.FillText + ".");
        }

        // Si todo coincide, aprobar.
        return CompatResult.Accept();
    }

    public bool CanAccept(PCPart part) => Evaluate(part).Ok;

    /// <summary>Ocupado por una pieza ya asentada (no solo reservada durante su animación).</summary>
    public bool IsComplete => isOccupied && Occupant != null && Occupant.IsInstalled;

    /// <summary>
    /// Cuenta para el objetivo: completo y con todo lo que exige también cumplido. Para un lugar normal es lo mismo que
    /// <see cref="IsComplete"/> (sus <see cref="requires"/> se comprobaron al instalar y no se pueden vaciar mientras esté
    /// ocupado). La diferencia está en los que empiezan ocupados: la tapa del equipo cerrado exige todo lo de dentro,
    /// así que no cuenta hasta que se cierra al final (y el progreso no baja al quitarla para empezar).
    /// </summary>
    public bool IsSatisfied => Satisfied(0);

    private bool Satisfied(int depth)
    {
        if (!IsComplete) return false;
        if (depth > 16) return true; // ciclo en los requires: no debería pasar
        foreach (var req in requires)
            if (req != null && !req.Satisfied(depth + 1)) return false;
        return true;
    }

    /// <summary>¿Se puede llenar ya (lo exigido está y lo que estorba no)? Lo usa la señal de los puertos de cable.</summary>
    public bool PrerequisitesMet()
    {
        if (FirstOccupied(requiresEmpty) != null) return false;
        foreach (var req in requires)
            if (req != null && !req.IsComplete) return false;
        return true;
    }

    private SocketSystem FirstOccupied(List<SocketSystem> list)
    {
        foreach (var s in list)
            if (s != null && s != this && s.isOccupied) return s;
        return null;
    }

    /// <summary>
    /// ¿Se puede vaciar este lugar (retirar su pieza, abrir la bisagra)? No, si algo que debe estar vacío para tocarlo
    /// está puesto (la tapa), ni si otro lugar ocupado depende de este.
    /// </summary>
    public CompatResult EvaluateRemoval()
    {
        SocketSystem inTheWay = FirstOccupied(requiresEmpty);
        if (inTheWay != null)
            return CompatResult.Reject("Todavía no se puede retirar", "Antes hay que " + inTheWay.EmptyText + ".");

        foreach (var other in All)
        {
            if (other == null || other == this || !other.isOccupied || !other.requires.Contains(this)) continue;
            string first = string.IsNullOrEmpty(other.emptyAction) ? "Primero retira " + other.TypeName : "Primero hay que " + other.emptyAction;
            return CompatResult.Reject("Todavía no se puede retirar",
                first + ": depende de esta pieza (va encima o conectado a ella).");
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

    /// <summary>Lugar de estado (<see cref="StateOwner"/>): queda ocupado (cerrado) sin mover la pieza.</summary>
    public void FillState(PCPart owner)
    {
        Reserve(owner);
        if (GameManager.Instance != null) GameManager.Instance.RegisterInstallation(owner.partName);
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
    /// Si alguno de los que están a tiro la acepta, gana ese (el más cercano de ellos).
    /// Devuelve null si ninguno está a tiro. <paramref name="result"/> dice si encajaría y,
    /// si no, por qué — así soltar sobre el lugar equivocado enseña en vez de fallar en silencio.
    /// Si ninguno la acepta, el motivo sale de un lugar de su mismo tipo (si hay uno a tiro): con el disipador
    /// sobre la CPU que falta, «antes hay que instalar la CPU» enseña más que «aquí va el ventilador», que
    /// comparte centro con él.
    /// </summary>
    public static SocketSystem FindTarget(PCPart part, out CompatResult result)
    {
        result = default;
        SocketSystem best = null, bestValid = null, bestSameType = null;
        float bestScore = 1f, bestValidScore = 1f, bestSameTypeScore = 1f;
        Vector3 current = part.VisualCenter;

        foreach (var socket in All)
        {
            // Los lugares que lleva la propia pieza (p. ej. los de la placa base) viajan con ella: no son destino.
            // Los de estado (bisagras, pestillos) tampoco: se alternan con un clic.
            if (socket == null || socket.StateOwner != null || socket.transform.IsChildOf(part.transform)) continue;
            if (!part.explainWrongPlace && socket.allowedType != part.connectionType) continue;
            Vector3 installed = part.VisualCenterAt(socket.SnapPosition, socket.SnapRotation);
            Vector3 delta = installed - current;
            delta.y = 0f; // el arrastre es sobre un plano horizontal: la altura no cuenta

            float score = delta.magnitude / socket.RadiusFor(part);
            if (score > 1f) continue;
            if (score <= bestScore)
            {
                bestScore = score;
                best = socket;
            }
            // Un lugar donde SÍ encaja gana a uno incompatible que quede igual o más cerca (p. ej. el puerto
            // PCIe de la GPU justo encima de la ranura que ocupa la propia GPU).
            if (score <= bestValidScore && socket.Evaluate(part).Ok)
            {
                bestValidScore = score;
                bestValid = socket;
            }
            if (score <= bestSameTypeScore && socket.allowedType != null && socket.allowedType == part.connectionType)
            {
                bestSameTypeScore = score;
                bestSameType = socket;
            }
        }

        if (bestValid != null)
        {
            result = CompatResult.Accept();
            return bestValid;
        }
        if (bestSameType != null) best = bestSameType;
        if (best != null) result = best.Evaluate(part);
        return best;
    }
}
