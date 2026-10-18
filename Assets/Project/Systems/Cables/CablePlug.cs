using UnityEngine;

/// <summary>
/// Extremo libre de un cable (conector). Es una pieza más (<see cref="PCPart"/> con su
/// <c>connectionType</c>, p. ej. Type_ATX24) que se instala en un puerto (<see cref="SocketSystem"/>
/// con el mismo tipo): reutiliza todo el flujo de arrastre, vista previa, rechazo con motivo,
/// progreso y desinstalación. Lo propio del cable es esto:
///  - No se puede llevar más lejos de lo que mide el cable (<see cref="CableSpline.ClampToReach"/>).
///  - Mientras se arrastra, el conector mira "hacia delante" con el cable siguiéndolo por detrás,
///    en vez de conservar la pose que tenía sobre la mesa.
///  - Suelto, la holgura cae por gravedad (y se apoya en la mesa); conectado, se arquea hacia donde
///    sale el cable del puerto, como un cable rígido, en vez de atravesar la placa.
///  - Si el cable sale de una pieza que también se instala (la fuente, el disipador…), el conector
///    suelto cambia de sitio de reposo con ella: en la mesa mientras la pieza está en la mesa, y dentro
///    del gabinete (<see cref="homeWhenSourceInstalled"/>) cuando se instala.
/// El tubo del cable sigue al conector solo: el <see cref="CableSpline.endAnchor"/> es este Transform.
/// </summary>
[RequireComponent(typeof(PCPart))]
public class CablePlug : MonoBehaviour, IDragConstraint
{
    [Tooltip("Cable al que pertenece este conector (su endAnchor debe ser este Transform)")]
    public CableSpline cable;

    [Tooltip("Qué tan rápido gira el conector para alinearse con el cable al arrastrarlo")]
    [SerializeField] private float turnSharpness = 12f;

    [Tooltip("Hacia dónde cae la holgura con el conector suelto (gravedad)")]
    [SerializeField] private Vector3 looseSlack = Vector3.down;

    [Tooltip("Conectado: la holgura se arquea hacia donde sale el cable del puerto (Z+ del conector)")]
    [SerializeField] private bool archWhenConnected = true;

    [Tooltip("Qué tan rápido cambia la caída al conectar/desconectar (transición suave)")]
    [SerializeField] private float slackBlendSharpness = 8f;

    [Header("Origen del cable (opcional)")]
    [Tooltip("Lugar donde se instala la pieza de la que sale el cable (p. ej. la fuente). Vacío = el origen no se mueve.")]
    public SocketSystem sourceSocket;
    [Tooltip("Dónde reposa este conector, suelto, cuando esa pieza ya está instalada")]
    public Transform homeWhenSourceInstalled;

    private PCPart _part;
    private Transform _deskParent;
    private Vector3 _deskLocalPos;
    private Quaternion _deskLocalRot;
    private bool _sourceWasInstalled;

    private void Awake()
    {
        _part = GetComponent<PCPart>();
    }

    private void Start()
    {
        _deskParent = transform.parent;
        _deskLocalPos = transform.localPosition;
        _deskLocalRot = transform.localRotation;
        _sourceWasInstalled = SourceInstalled;
    }

    private bool SourceInstalled => sourceSocket != null && sourceSocket.IsComplete;

    private void Update()
    {
        if (_part == null) return;
        FollowSource();
        if (cable == null) return;

        // CurrentSocket se asigna al empezar la animación de encaje: la caída cambia mientras baja.
        Vector3 target = archWhenConnected && _part.CurrentSocket != null ? transform.forward : looseSlack.normalized;
        Vector3 current = cable.slackDirection.sqrMagnitude > 0f ? cable.slackDirection.normalized : target;
        Vector3 next = Vector3.Angle(current, target) < 0.5f
            ? target
            : Vector3.Slerp(current, target, 1f - Mathf.Exp(-slackBlendSharpness * Time.deltaTime));
        cable.SetSlackDirection(next);
    }

    /// <summary>Si la pieza de origen se instaló o se retiró, el conector suelto se va a su nuevo sitio de reposo.</summary>
    private void FollowSource()
    {
        if (homeWhenSourceInstalled == null) return;
        bool installed = SourceInstalled;
        if (installed == _sourceWasInstalled) return;
        _sourceWasInstalled = installed;

        if (installed)
            _part.SetHome(homeWhenSourceInstalled, homeWhenSourceInstalled.position, homeWhenSourceInstalled.rotation);
        else if (_deskParent != null)
            _part.SetHome(_deskParent, _deskParent.TransformPoint(_deskLocalPos), _deskParent.rotation * _deskLocalRot);

        if (_part.CurrentSocket == null && !_part.IsHeld) _part.ReturnToTable(0.35f);
    }

    public Vector3 ConstrainDragPosition(Vector3 desired)
    {
        return cable != null ? cable.ClampToReach(desired) : desired;
    }

    public Quaternion DragRotation(Vector3 position, Quaternion current)
    {
        if (cable == null) return current;

        // Z+ del conector = hacia donde sale el cable → apunta de vuelta al origen (horizontal),
        // así la cara que encaja queda mirando en la dirección del arrastre.
        Vector3 origin = cable.ReachOrigin(out _);
        Vector3 back = origin - position;
        back.y = 0f;
        if (back.sqrMagnitude < 1e-8f) return current;

        Quaternion target = Quaternion.LookRotation(back.normalized, Vector3.up);
        return Quaternion.Slerp(current, target, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
    }

    private void Reset()
    {
        if (cable == null) cable = GetComponentInParent<CableSpline>();
    }
}
