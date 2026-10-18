using System;
using System.Collections;
using UnityEngine;

public class PCPart : MonoBehaviour
{
    [Header("Configuración Física")]
    [Tooltip("Desmárcalo para objetos fijos como la Motherboard base")]
    public bool isDraggable = true;

    [Header("Identidad Técnica")]
    public string partName = "Componente Genérico";
    public ConnectionTypeSO connectionType;

    [Header("Información Educativa (UI)")]
    public string title = "Título del Componente";
    [TextArea(4, 10)]
    public string description = "Descripción...";

    [Tooltip("Si se suelta sobre un lugar de otro tipo, explicar qué va ahí. Desmárcalo en piezas grandes que tapan muchos " +
             "lugares (la tapa del equipo): soltarla cerca del equipo no es un error de lugar, y su tolerancia (∝ tamaño) alcanzaría " +
             "puertos pequeños.")]
    public bool explainWrongPlace = true;

    [Header("Inicio (opcional)")]
    [Tooltip("La pieza empieza instalada en este lugar (p. ej. la tapa de un equipo cerrado). Vacío = empieza suelta.")]
    public SocketSystem startInstalledIn;
    [Tooltip("Si empieza instalada: dónde se deja al retirarla y soltarla en vacío (en la mesa). Vacío = donde está en la escena.")]
    public Transform restPoint;

    [Header("Estado")]
    public bool IsInstalled { get; private set; } = false;

    /// <summary>Socket en el que está instalada (null si está en la mesa).</summary>
    public SocketSystem CurrentSocket { get; private set; }

    /// <summary>True mientras anima (encaje o regreso a la mesa): no se puede agarrar.</summary>
    public bool IsMoving => _moveRoutine != null;

    /// <summary>True mientras el estudiante la sostiene (desde que la agarra hasta que la suelta).</summary>
    public bool IsHeld { get; private set; }

    /// <summary>Tamaño aproximado (mayor lado de su caja visual), en unidades de mundo.</summary>
    public float Size { get; private set; } = 0.25f;

    /// <summary>Centro visual actual (centro de sus renderers, no el pivote del modelo).</summary>
    public Vector3 VisualCenter => transform.TransformPoint(_localVisualCenter);

    // Sitio en la mesa, relativo al padre inicial: si ese padre se mueve (un conector que cuelga de la
    // fuente, y la fuente ya está dentro del gabinete), "volver a la mesa" sigue al padre.
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Transform _initialParent;
    private Vector3 _localVisualCenter;
    private Coroutine _moveRoutine;

    private void Start()
    {
        _initialParent = transform.parent;
        _initialPosition = transform.localPosition;
        _initialRotation = transform.localRotation;
        CacheVisualBounds();

        // TRUCO: Si la pieza no se puede arrastrar (como la Motherboard),
        // le decimos al sistema que ya está "Instalada" para que no intente moverla.
        if (!isDraggable) IsInstalled = true;

        if (startInstalledIn != null)
        {
            if (restPoint != null) SetHome(restPoint.parent, restPoint.position, restPoint.rotation);
            PlaceInstantly(startInstalledIn);
        }
    }

    /// <summary>Instalada ya, sin animación (estado inicial de la escena).</summary>
    public void PlaceInstantly(SocketSystem socket)
    {
        StopMove();
        IsHeld = false;
        socket.Reserve(this);
        CurrentSocket = socket;
        IsInstalled = true;
        socket.InstallPart(this);
    }

    /// <summary>Dónde quedaría el centro visual si la pieza estuviera en esa pose (misma escala de mundo).</summary>
    public Vector3 VisualCenterAt(Vector3 position, Quaternion rotation)
    {
        return position + rotation * Vector3.Scale(_localVisualCenter, transform.lossyScale);
    }

    /// <summary>La agarran: si estaba instalada, libera su socket (desinstalar = arrastrar fuera).</summary>
    public void BeginDrag()
    {
        StopMove();
        if (CurrentSocket != null)
        {
            CurrentSocket.RemovePart(this);
            CurrentSocket = null;
        }
        IsInstalled = false;
        IsHeld = true;
        transform.SetParent(_initialParent, true);
    }

    /// <summary>
    /// Encaja la pieza en <paramref name="socket"/>: se acerca por encima (a <paramref name="approachHeight"/>),
    /// baja hasta su sitio y solo entonces queda instalada (la victoria llega cuando la última pieza asienta).
    /// Si el lugar pide inserción en ángulo (<see cref="SocketSystem.insertTilt"/>), entra inclinada y luego baja.
    /// </summary>
    public void InstallInto(SocketSystem socket, float duration, float approachHeight)
    {
        StopMove();
        IsHeld = false;
        socket.Reserve(this);
        CurrentSocket = socket;

        Vector3 seat = socket.SnapPosition;
        Quaternion rot = socket.SnapRotation;
        if (socket.insertTilt > 0f && socket.insertHinge != null)
        {
            _moveRoutine = StartCoroutine(AngledInsertRoutine(socket, seat, rot, duration, approachHeight));
            return;
        }
        Vector3 above = seat + Vector3.up * approachHeight;

        _moveRoutine = StartCoroutine(MoveRoutine(above, rot, duration * 0.6f, () =>
            _moveRoutine = StartCoroutine(MoveRoutine(seat, rot, duration * 0.4f, () =>
            {
                _moveRoutine = null;
                IsInstalled = true; // antes de InstallPart: GameManager cuenta las piezas ya asentadas
                socket.InstallPart(this);
            }))));
    }

    /// <summary>Vuelve a su sitio inicial en la mesa (animado si <paramref name="duration"/> &gt; 0).</summary>
    public void ReturnToTable(float duration = 0f)
    {
        StopMove();
        IsHeld = false;
        transform.SetParent(_initialParent, true);
        IsInstalled = false;

        Vector3 homePos = _initialParent != null ? _initialParent.TransformPoint(_initialPosition) : _initialPosition;
        Quaternion homeRot = _initialParent != null ? _initialParent.rotation * _initialRotation : _initialRotation;

        if (duration <= 0f)
        {
            transform.SetPositionAndRotation(homePos, homeRot);
            return;
        }
        _moveRoutine = StartCoroutine(MoveRoutine(homePos, homeRot, duration, () => _moveRoutine = null));
    }

    /// <summary>
    /// Cambia su sitio de reposo (a donde vuelve al soltarla en vacío). Lo usa el conector de un cable
    /// cuando la pieza de la que sale se instala: deja de reposar en la mesa y pasa a dentro del gabinete.
    /// </summary>
    public void SetHome(Transform parent, Vector3 position, Quaternion rotation)
    {
        _initialParent = parent;
        _initialPosition = parent != null ? parent.InverseTransformPoint(position) : position;
        _initialRotation = parent != null ? Quaternion.Inverse(parent.rotation) * rotation : rotation;
    }

    // Compatibilidad con el nombre anterior.
    public void ResetToTable() => ReturnToTable();

    private void StopMove()
    {
        if (_moveRoutine == null) return;
        StopCoroutine(_moveRoutine);
        _moveRoutine = null;
    }

    /// <summary>
    /// Inserción en ángulo, como la SO-DIMM o una tarjeta M.2: la pieza llega inclinada (<see cref="SocketSystem.insertTilt"/>)
    /// a la boca del conector, se desliza dentro y gira sobre ese borde (<see cref="SocketSystem.insertHinge"/>) hasta su
    /// sitio, donde las pestañas o el tornillo la sujetan. Dura más que un encaje normal para que se vea el gesto.
    /// </summary>
    private IEnumerator AngledInsertRoutine(SocketSystem socket, Vector3 seat, Quaternion rot, float duration, float approachHeight)
    {
        Vector3 pivot = socket.insertHinge.position;
        Vector3 axis = socket.insertHinge.forward;
        Quaternion tilt = Quaternion.AngleAxis(socket.insertTilt, axis);
        Vector3 tiltedPos = pivot + tilt * (seat - pivot);
        Quaternion tiltedRot = tilt * rot;

        // Hacia dónde entra: del centro de la pieza inclinada al borde del conector, en el plano del giro.
        Vector3 into = Vector3.ProjectOnPlane(pivot - VisualCenterAt(tiltedPos, tiltedRot), axis).normalized;
        Vector3 mouth = tiltedPos - into * socket.insertSlide;
        Vector3 above = mouth + Vector3.up * approachHeight * 0.5f;

        float d = duration * 3f;
        yield return Tween(above, tiltedRot, d * 0.30f);
        yield return Tween(mouth, tiltedRot, d * 0.15f);
        yield return Tween(tiltedPos, tiltedRot, d * 0.20f);

        // Bajar girando sobre el borde del conector (el borde no se mueve).
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(d * 0.35f, 0.0001f);
            Quaternion r = Quaternion.AngleAxis(socket.insertTilt * (1f - Mathf.SmoothStep(0f, 1f, t)), axis);
            transform.SetPositionAndRotation(pivot + r * (seat - pivot), r * rot);
            yield return null;
        }
        transform.SetPositionAndRotation(seat, rot);
        _moveRoutine = null;
        IsInstalled = true;
        socket.InstallPart(this);
    }

    private IEnumerator Tween(Vector3 targetPos, Quaternion targetRot, float duration)
    {
        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
            float k = Mathf.SmoothStep(0f, 1f, t);
            transform.SetPositionAndRotation(Vector3.Lerp(fromPos, targetPos, k), Quaternion.Slerp(fromRot, targetRot, k));
            yield return null;
        }
        transform.SetPositionAndRotation(targetPos, targetRot);
    }

    private IEnumerator MoveRoutine(Vector3 targetPos, Quaternion targetRot, float duration, Action onDone)
    {
        Vector3 fromPos = transform.position;
        Quaternion fromRot = transform.rotation;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(duration, 0.0001f);
            float k = Mathf.SmoothStep(0f, 1f, t);
            transform.SetPositionAndRotation(Vector3.Lerp(fromPos, targetPos, k), Quaternion.Slerp(fromRot, targetRot, k));
            yield return null;
        }
        transform.SetPositionAndRotation(targetPos, targetRot);
        onDone?.Invoke();
    }

    private void CacheVisualBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            _localVisualCenter = Vector3.zero;
            return;
        }

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

        _localVisualCenter = transform.InverseTransformPoint(b.center);
        Size = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
    }
}
