using System.Collections;
using UnityEngine;

/// <summary>
/// Pieza fija con dos posiciones que se alternan con un clic (sin arrastrar): el módulo de discos que se levanta
/// sobre su bisagra, el pestillo que sujeta las tarjetas, el pestillo ZIF de una cinta…
///
/// Su posición «cerrada» es un lugar de ESTADO (<see cref="closedState"/>, ocupado = cerrado), así que usa las mismas
/// reglas que cualquier lugar:
///  - otros lugares la exigen cerrada (<see cref="SocketSystem.requires"/>: el disco va sobre el módulo bajado) o
///    abierta (<see cref="SocketSystem.requiresEmpty"/>: la memoria solo se toca con el módulo levantado);
///  - no se abre si algo depende de que esté cerrada (el disco montado encima) ni se mueve con lo que estorba
///    puesto (la tapa), con el motivo en español;
///  - si su lugar es <c>required</c>, cuenta para el objetivo estando cerrada.
/// Gira alrededor de <see cref="hinge"/> (su eje Z+, regla de la mano derecha). Lo que lleva montado (sus hijos,
/// p. ej. el lugar del disco) gira con ella. La pieza no se arrastra (<see cref="PCPart.isDraggable"/> = false):
/// conserva su ficha con clic derecho.
/// </summary>
[RequireComponent(typeof(PCPart))]
public class HingedPart : MonoBehaviour
{
    [Tooltip("Lugar de estado: ocupado = cerrado. Su allowedType debe ser el connectionType de esta pieza.")]
    public SocketSystem closedState;
    [Tooltip("Eje de giro: su posición y su Z+ (hermano de esta pieza, no hijo: no debe girar con ella)")]
    public Transform hinge;
    [Tooltip("Grados que gira al abrirse (positivo = regla de la mano derecha sobre el Z+ de la bisagra)")]
    public float openAngle = 95f;
    public float duration = 0.45f;
    public bool startOpen = false;

    [Header("Textos")]
    [Tooltip("Ej: \"levantar el módulo de discos\"")]
    public string openVerb = "abrir";
    [Tooltip("Ej: \"bajar el módulo de discos\"")]
    public string closeVerb = "cerrar";
    [Tooltip("Aviso al abrir. Ej: \"Módulo de discos levantado\"")]
    public string openedText = "Abierto";
    [Tooltip("Aviso al cerrar. Ej: \"Módulo de discos bajado\"")]
    public string closedText = "Cerrado";

    private PCPart _part;
    private Vector3 _closedPos;
    private Quaternion _closedRot;
    private Vector3 _hingePos;
    private Vector3 _hingeAxis;
    private float _angle;
    private Coroutine _routine;

    public bool IsClosed => closedState != null && closedState.isOccupied;
    public bool IsMoving => _routine != null;

    private void Awake()
    {
        _part = GetComponent<PCPart>();
        // Pose cerrada (la de la escena) y bisagra, en el espacio del padre: el equipo no se mueve.
        _closedPos = transform.localPosition;
        _closedRot = transform.localRotation;
        Transform parent = transform.parent;
        _hingePos = parent != null ? parent.InverseTransformPoint(hinge.position) : hinge.position;
        _hingeAxis = parent != null ? parent.InverseTransformDirection(hinge.forward) : hinge.forward;
        if (closedState != null) closedState.StateOwner = this;
    }

    private void Start()
    {
        if (closedState == null) { enabled = false; return; }
        if (startOpen) SetAngle(openAngle);
        else closedState.FillState(_part);
    }

    /// <summary>Abre o cierra si las reglas lo permiten. El resultado trae el aviso (o el motivo del rechazo).</summary>
    public CompatResult Toggle()
    {
        if (closedState == null || IsMoving) return CompatResult.Accept();

        if (IsClosed)
        {
            CompatResult removal = closedState.EvaluateRemoval();
            if (!removal.Ok) return CompatResult.Reject("Todavía no se puede " + openVerb, removal.Reason);
            closedState.RemovePart(_part);
            Animate(openAngle, null);
            return CompatResult.Accept(openedText);
        }

        CompatResult fill = closedState.Evaluate(_part);
        if (!fill.Ok) return CompatResult.Reject("Todavía no se puede " + closeVerb, fill.Reason);
        closedState.Reserve(_part); // ocupado mientras baja: nada se instala debajo a mitad de camino
        Animate(0f, () => closedState.FillState(_part));
        return CompatResult.Accept(closedText);
    }

    private void Animate(float target, System.Action onDone)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(AnimateRoutine(target, onDone));
    }

    private IEnumerator AnimateRoutine(float target, System.Action onDone)
    {
        float from = _angle;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
            SetAngle(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t)));
            yield return null;
        }
        SetAngle(target);
        _routine = null;
        onDone?.Invoke();
    }

    private void SetAngle(float angle)
    {
        _angle = angle;
        Quaternion r = Quaternion.AngleAxis(angle, _hingeAxis);
        transform.localPosition = _hingePos + r * (_closedPos - _hingePos);
        transform.localRotation = r * _closedRot;
    }
}
