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

    [Header("Estado")]
    public bool IsInstalled { get; private set; } = false;

    /// <summary>Socket en el que está instalada (null si está en la mesa).</summary>
    public SocketSystem CurrentSocket { get; private set; }

    /// <summary>True mientras anima (encaje o regreso a la mesa): no se puede agarrar.</summary>
    public bool IsMoving => _moveRoutine != null;

    /// <summary>Tamaño aproximado (mayor lado de su caja visual), en unidades de mundo.</summary>
    public float Size { get; private set; } = 0.25f;

    /// <summary>Centro visual actual (centro de sus renderers, no el pivote del modelo).</summary>
    public Vector3 VisualCenter => transform.TransformPoint(_localVisualCenter);

    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Transform _initialParent;
    private Vector3 _localVisualCenter;
    private Coroutine _moveRoutine;

    private void Start()
    {
        _initialParent = transform.parent;
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        CacheVisualBounds();

        // TRUCO: Si la pieza no se puede arrastrar (como la Motherboard),
        // le decimos al sistema que ya está "Instalada" para que no intente moverla.
        if (!isDraggable) IsInstalled = true;
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
        transform.SetParent(_initialParent, true);
    }

    /// <summary>
    /// Encaja la pieza en <paramref name="socket"/>: se acerca por encima (a <paramref name="approachHeight"/>),
    /// baja hasta su sitio y solo entonces queda instalada (la victoria llega cuando la última pieza asienta).
    /// </summary>
    public void InstallInto(SocketSystem socket, float duration, float approachHeight)
    {
        StopMove();
        socket.Reserve(this);
        CurrentSocket = socket;

        Vector3 seat = socket.SnapPosition;
        Quaternion rot = socket.SnapRotation;
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
        transform.SetParent(_initialParent, true);
        IsInstalled = false;

        if (duration <= 0f)
        {
            transform.position = _initialPosition;
            transform.rotation = _initialRotation;
            return;
        }
        _moveRoutine = StartCoroutine(MoveRoutine(_initialPosition, _initialRotation, duration, () => _moveRoutine = null));
    }

    // Compatibilidad con el nombre anterior.
    public void ResetToTable() => ReturnToTable();

    private void StopMove()
    {
        if (_moveRoutine == null) return;
        StopCoroutine(_moveRoutine);
        _moveRoutine = null;
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
