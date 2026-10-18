using UnityEngine;
// El bloqueo puntero-sobre-UI lo hace UIBlocker (UI Toolkit); EventSystem de
// uGUI ya no ve la UI migrada.

/// <summary>
/// Hover, arrastre y encaje de piezas en el laboratorio.
///
/// Gestos (separados para que no se pisen — AUDITORIA §6.1):
///  - Clic izq. + arrastrar: mueve la pieza. Soltar sobre un lugar compatible la instala;
///    sobre uno incompatible, vuelve a la mesa y el HUD explica por qué.
///  - Arrastrar una pieza instalada: la retira (deshacer — §6.2).
///  - Doble clic izq.: encuadra la pieza con la cámara.
///  - Clic izq. sobre una pieza con bisagra o pestillo (<see cref="HingedPart"/>): la abre o la cierra.
///  - Clic der. SIN mover el ratón: ficha técnica. Clic der. + arrastrar: órbita (la hace la cámara).
/// La cámara ya NO se mueve sola al agarrar/soltar: el estudiante controla el encuadre.
/// </summary>
public class InteractionManager : MonoBehaviour
{
    [Header("Configuración de Raycast")]
    public LayerMask interactableLayer;

    [Header("Configuración Visual (Colores)")]
    [Tooltip("Material Naranja Transparente (Hover/Inspección)")]
    public Material hoverMaterial;
    [Tooltip("Material Verde Transparente (Arrastre)")]
    public Material dragMaterial;
    [Tooltip("Material translúcido de la vista previa. Vacío = Resources/Mat_GhostPreview")]
    [SerializeField] private Material ghostMaterial;
    [Tooltip("Modo guiado: muestra fantasmas en los lugares donde la pieza encaja")]
    [SerializeField] private bool showPlacementHints = true;
    [SerializeField] private Color ghostIdleColor = new Color(0.35f, 0.85f, 1f, 0.16f);
    [SerializeField] private Color ghostTargetColor = new Color(0.35f, 1f, 0.6f, 0.5f);
    [Tooltip("Color del borde cuando la pieza está sobre un lugar donde NO encaja")]
    [SerializeField] private Color rejectOutlineColor = new Color(0.9f, 0.28f, 0.3f, 1f);

    [Header("Configuración de Arrastre")]
    [Tooltip("Cuánto se eleva la pieza al arrastrarla, para pasar por encima de la placa (unidades de mundo)")]
    [SerializeField] private float liftHeight = 0.12f;
    [Tooltip("Suavizado del seguimiento del cursor (más alto = más pegado)")]
    [SerializeField] private float followSharpness = 25f;
    [Tooltip("Duración de la animación de encaje / regreso a la mesa (s)")]
    [SerializeField] private float settleDuration = 0.3f;
    [Tooltip("Píxeles que hay que mover el ratón para que un clic se convierta en arrastre")]
    [SerializeField] private float dragStartPixels = 6f;
    [SerializeField] private float doubleClickSeconds = 0.3f;
    [Tooltip("Al arrastrar, la pieza pasa por encima de lo que tiene debajo (paredes del gabinete, otras piezas) con este margen")]
    [SerializeField] private float hoverClearance = 0.01f;
    [Tooltip("Capas en las que la pieza se apoya al pasar por encima")]
    [SerializeField] private LayerMask supportLayers = Physics.DefaultRaycastLayers;

    [Header("Límites de la Mesa")]
    public float tableLimitSize = 4f;
    public float maxRayDistance = 30f;
    [Tooltip("Opcional: centro de la zona de trabajo. Si existe, el arrastre se limita a un cuadrado de ±tableLimitSize")]
    public Transform assemblyZone;

    // Referencias internas
    private SimulationControls _controls;
    private Camera _cam;
    private CADCameraController _camController;
    private PlacementPreview _preview;
    private Material _rejectMaterial;

    // Clic izquierdo pendiente (aún no es arrastre)
    private PCPart _pressPart;
    private RaycastHit _pressHit;
    private Vector2 _pressPos;
    private PCPart _lastClickPart;
    private float _lastClickTime;

    // Arrastre en curso
    private PCPart _currentPart;
    private IDragConstraint _constraint;
    private Plane _dragPlane;
    private Vector3 _grabOffset;
    private Vector3 _targetPos;
    private SocketSystem _hoverSocket;
    private bool _hoverCompatible;

    // Huella de la pieza arrastrada (relativa a su pivote), para pasar por encima de lo que haya debajo
    private Vector3 _footCenter;
    private Vector3 _footExtents;
    private float _bottomOffset;
    private readonly RaycastHit[] _supportHits = new RaycastHit[32];

    // Clic derecho (ficha solo si no hubo órbita)
    private Vector2 _rightPressPos;
    private bool _rightPressValid;

    // Estado del Highlight
    private ObjectHighlighter _currentHighlighter;

    private void Awake() => Init();

    // También desde OnEnable: si Unity recompila en mitad del Play, los campos no serializables
    // vuelven a null y Awake no se repite.
    private void Init()
    {
        _controls = new SimulationControls();
        _cam = Camera.main;
        if (_cam != null)
            _camController = _cam.GetComponent<CADCameraController>();

        if (ghostMaterial == null) ghostMaterial = Resources.Load<Material>("Mat_GhostPreview");
        _preview = new PlacementPreview(ghostMaterial, ghostIdleColor, ghostTargetColor);

        if (dragMaterial != null)
        {
            _rejectMaterial = new Material(dragMaterial) { name = "Outline_Rechazo (runtime)" };
            _rejectMaterial.SetColor("_OutlineColor", rejectOutlineColor);
        }
    }

    private void OnEnable()
    {
        if (_controls == null) Init();
        _controls.Enable();
    }
    private void OnDisable() => _controls?.Disable();

    private void OnDestroy()
    {
        _preview?.Clear();
        if (_rejectMaterial != null) Destroy(_rejectMaterial);
        _controls?.Dispose();
    }

    private void Update()
    {
        // Modal abierto (opciones/victoria): todo congelado; si había algo en la mano, vuelve a la mesa.
        if (UIBlocker.ModalOpen)
        {
            if (_currentPart != null) CancelDrag();
            _pressPart = null;
            ClearHighlight();
            return;
        }

        // 1. Manejo del Highlight (Hover constante)
        HandleHover();

        // 2. Inputs de Mouse
        if (_controls.Player.Select.WasPressedThisFrame()) HandleLeftPress();
        if (_pressPart != null && _currentPart == null) CheckDragStart();
        if (_controls.Player.Select.WasReleasedThisFrame()) HandleLeftRelease();

        if (_controls.Player.Inspect.WasPressedThisFrame()) HandleRightPress();
        if (_controls.Player.Inspect.WasReleasedThisFrame()) HandleRightRelease();

        // 3. Movimiento de pieza
        if (_currentPart != null) DragUpdate();
    }

    private Vector2 PointerPos => _controls.Player.Point.ReadValue<Vector2>();

    private bool RaycastPart(Vector2 screenPos, out PCPart part, out RaycastHit hit)
    {
        part = null;
        hit = default;
        if (_cam == null) return false;

        Ray ray = _cam.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out hit, 100f, interactableLayer)) return false;

        part = hit.collider.GetComponentInParent<PCPart>();
        return part != null;
    }

    // --- LÓGICA VISUAL (HIGHLIGHT) ---
    private void HandleHover()
    {
        // Si estamos arrastrando, el objeto ya tiene su color de arrastre, no calculamos hover
        if (_currentPart != null) return;

        // Si el puntero está sobre la UI (UI Toolkit), limpiamos todo
        Vector2 mousePos = PointerPos;
        if (UIBlocker.IsPointerOverUI(mousePos))
        {
            ClearHighlight();
            return;
        }

        if (_cam == null) return;
        Ray ray = _cam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
        {
            // Buscamos el componente visual en el objeto o sus padres
            ObjectHighlighter highlighter = hit.collider.GetComponentInParent<ObjectHighlighter>();

            if (highlighter != null)
            {
                // Si es un objeto nuevo, cambiamos el brillo
                if (_currentHighlighter != highlighter)
                {
                    ClearHighlight(); // Apagar anterior
                    _currentHighlighter = highlighter;
                    _currentHighlighter.SetHighlight(hoverMaterial); // Encender nuevo (Naranja)
                }
            }
            else
            {
                ClearHighlight();
            }
        }
        else
        {
            ClearHighlight();
        }
    }

    private void ClearHighlight()
    {
        if (_currentHighlighter != null)
        {
            _currentHighlighter.RemoveHighlight();
            _currentHighlighter = null;
        }
    }

    private void SetHighlight(PCPart part, Material material)
    {
        ObjectHighlighter hl = part.GetComponent<ObjectHighlighter>();
        if (hl == null) return;
        if (_currentHighlighter != hl) ClearHighlight();
        _currentHighlighter = hl;
        hl.SetHighlight(material != null ? material : hoverMaterial);
    }

    // --- CLIC IZQUIERDO: clic, doble clic o inicio de arrastre ---

    private void HandleLeftPress()
    {
        _pressPart = null;
        Vector2 mousePos = PointerPos;
        if (UIBlocker.IsPointerOverUI(mousePos)) return;

        if (RaycastPart(mousePos, out PCPart part, out RaycastHit hit))
        {
            // Agarrar el tubo de un cable = tirar de su conector libre (como un cable real).
            if (!part.isDraggable)
            {
                var cable = part.GetComponent<CableSpline>();
                if (cable != null && cable.endAnchor != null && cable.endAnchor.TryGetComponent(out PCPart plug) && plug.isDraggable)
                    part = plug;
            }
            _pressPart = part;
            _pressHit = hit;
            _pressPos = mousePos;
        }
    }

    private void CheckDragStart()
    {
        if (!_pressPart.isDraggable || _pressPart.IsMoving) return;
        if ((PointerPos - _pressPos).sqrMagnitude < dragStartPixels * dragStartPixels) return;

        // Retirar una pieza de la que depende otra (p. ej. la CPU bajo el disipador) no se permite: se explica.
        if (_pressPart.CurrentSocket != null)
        {
            CompatResult removal = _pressPart.CurrentSocket.EvaluateRemoval();
            if (!removal.Ok)
            {
                if (AssemblyHudView.Instance != null)
                    AssemblyHudView.Instance.ShowFeedback(removal.Message, ToastKind.Warning, removal.Reason);
                _pressPart = null;
                return;
            }
        }

        BeginDrag(_pressPart, _pressHit);
    }

    private void HandleLeftRelease()
    {
        if (_currentPart != null)
        {
            EndDrag();
        }
        else if (_pressPart != null)
        {
            // Fue un clic (sin arrastre). Sobre una bisagra o pestillo: abrirlo o cerrarlo (si el ratón no se movió).
            if (_pressPart.TryGetComponent(out HingedPart hinge))
            {
                if ((PointerPos - _pressPos).sqrMagnitude <= dragStartPixels * dragStartPixels) ToggleHinge(hinge);
                _lastClickPart = null;
                _pressPart = null;
                return;
            }

            // Dos seguidos sobre la misma pieza = encuadrarla.
            bool isDouble = _pressPart == _lastClickPart && Time.unscaledTime - _lastClickTime <= doubleClickSeconds;
            if (isDouble && _camController != null) _camController.FocusOnObject(_pressPart.transform);

            _lastClickPart = isDouble ? null : _pressPart;
            _lastClickTime = Time.unscaledTime;
        }
        _pressPart = null;
    }

    private void ToggleHinge(HingedPart hinge)
    {
        CompatResult result = hinge.Toggle();
        var hud = AssemblyHudView.Instance;
        if (hud == null || string.IsNullOrEmpty(result.Message)) return;
        hud.ShowFeedback(result.Message, result.Ok ? ToastKind.Success : ToastKind.Warning, result.Reason);
    }

    // --- ARRASTRE ---

    private void BeginDrag(PCPart part, RaycastHit hit)
    {
        _currentPart = part;
        _pressPart = null;
        _constraint = part.GetComponent<IDragConstraint>();

        part.BeginDrag(); // si estaba instalada, libera su socket

        // El plano pasa por el punto agarrado, elevado: la pieza no "salta" al cursor
        // (se conserva el desfase de agarre) y pasa por encima de la placa.
        Vector3 lift = Vector3.up * liftHeight;
        _dragPlane = new Plane(Vector3.up, hit.point + lift);
        _grabOffset = part.transform.position - hit.point;
        _targetPos = part.transform.position + lift;

        Bounds b = WorldBounds(part);
        _footCenter = b.center - part.transform.position;
        _footExtents = new Vector3(b.extents.x, 0.001f, b.extents.z);
        _bottomOffset = b.min.y - part.transform.position.y;

        _hoverSocket = null;
        _hoverCompatible = false;

        ClearHighlight(); // fuera el hover naranja ANTES de clonar el fantasma
        if (showPlacementHints) _preview.Show(part);
        SetHighlight(part, dragMaterial);
    }

    private void DragUpdate()
    {
        Ray ray = _cam.ScreenPointToRay(PointerPos);
        if (_dragPlane.Raycast(ray, out float distance) && distance <= maxRayDistance)
        {
            Vector3 p = ray.GetPoint(distance) + _grabOffset;

            if (assemblyZone != null)
            {
                Vector3 c = assemblyZone.position;
                p.x = Mathf.Clamp(p.x, c.x - tableLimitSize, c.x + tableLimitSize);
                p.z = Mathf.Clamp(p.z, c.z - tableLimitSize, c.z + tableLimitSize);
            }
            if (_constraint != null) p = _constraint.ConstrainDragPosition(p);

            // Por encima de lo que tenga debajo: entra al gabinete por arriba en vez de atravesar sus paredes.
            float support = SupportHeight(p);
            if (!float.IsNegativeInfinity(support))
                p.y = Mathf.Max(p.y, support + hoverClearance - _bottomOffset);
            _targetPos = p;
        }

        Transform t = _currentPart.transform;
        float k = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
        t.position = Vector3.Lerp(t.position, _targetPos, k);
        if (_constraint != null) t.rotation = _constraint.DragRotation(t.position, t.rotation);

        // ¿A qué lugar apunta ahora? Fantasma intenso si encaja; borde rojo si no.
        SocketSystem target = SocketSystem.FindTarget(_currentPart, out CompatResult result);
        bool compatible = target != null && result.Ok;
        if (target != _hoverSocket || compatible != _hoverCompatible)
        {
            _hoverSocket = target;
            _hoverCompatible = compatible;
            _preview.Highlight(compatible ? target : null);
            bool rejecting = target != null && !compatible;
            SetHighlight(_currentPart, rejecting && _rejectMaterial != null ? _rejectMaterial : dragMaterial);
        }
    }

    /// <summary>Altura de lo más alto que queda bajo la huella de la pieza si su pivote estuviera en <paramref name="pivot"/>.</summary>
    private float SupportHeight(Vector3 pivot)
    {
        Vector3 c = pivot + _footCenter;
        // Desde un poco por encima del plano de arrastre (no desde el techo: la lámpara no cuenta).
        float planeHeight = -_dragPlane.distance;
        Vector3 origin = new Vector3(c.x, planeHeight + 0.5f, c.z);
        int n = Physics.BoxCastNonAlloc(origin, _footExtents, Vector3.down, _supportHits, Quaternion.identity,
                                        4f, supportLayers, QueryTriggerInteraction.Ignore);
        float top = float.NegativeInfinity;
        for (int i = 0; i < n; i++)
        {
            RaycastHit h = _supportHits[i];
            if (h.distance <= 0f) continue; // ya solapaba al empezar el barrido
            Transform t = h.collider.transform;
            if (t.IsChildOf(_currentPart.transform)) continue;
            if (h.collider.GetComponentInParent<CableSpline>() != null) continue; // un cable no sostiene nada
            if (h.point.y > top) top = h.point.y;
        }
        return top;
    }

    private static Bounds WorldBounds(PCPart part)
    {
        Renderer[] renderers = part.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(part.transform.position, Vector3.zero);
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private void EndDrag()
    {
        PCPart part = _currentPart;
        _currentPart = null;
        _constraint = null;
        _preview.Clear();
        ClearHighlight();

        SocketSystem target = SocketSystem.FindTarget(part, out CompatResult result);
        var hud = AssemblyHudView.Instance;

        if (target != null && result.Ok)
        {
            part.InstallInto(target, settleDuration, liftHeight);
            if (hud != null)
            {
                if (string.IsNullOrEmpty(target.installNote)) hud.ShowFeedback("Instalado: " + part.title, ToastKind.Success);
                else hud.ShowFeedback("Instalado: " + part.title, ToastKind.Warning, target.installNote);
            }
        }
        else
        {
            // Soltar sobre un lugar equivocado enseña el porqué; soltar en vacío solo la devuelve.
            if (target != null && hud != null) hud.ShowFeedback(result.Message, ToastKind.Error, result.Reason);
            part.ReturnToTable(settleDuration);
        }
    }

    private void CancelDrag()
    {
        PCPart part = _currentPart;
        _currentPart = null;
        _constraint = null;
        _preview.Clear();
        ClearHighlight();
        part.ReturnToTable(settleDuration);
    }

    // --- CLIC DERECHO: ficha técnica (solo si no se arrastró para orbitar) ---

    private void HandleRightPress()
    {
        _rightPressPos = PointerPos;
        _rightPressValid = _currentPart == null && !UIBlocker.IsPointerOverUI(_rightPressPos);
    }

    private void HandleRightRelease()
    {
        if (!_rightPressValid) return;
        _rightPressValid = false;

        Vector2 mousePos = PointerPos;
        if ((mousePos - _rightPressPos).sqrMagnitude > dragStartPixels * dragStartPixels) return; // fue órbita

        if (RaycastPart(mousePos, out PCPart part, out _) && AssemblyHudView.Instance != null)
        {
            AssemblyHudView.Instance.ShowPartInfo(part.title, part.description);

            // Mantener el highlight naranja mientras lees la ficha
            SetHighlight(part, hoverMaterial);
        }
    }

    // Dibujar límites en el editor
    private void OnDrawGizmos()
    {
        if (assemblyZone != null)
        {
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.8f);
            Vector3 center = assemblyZone.position;
            center.y += liftHeight;
            Vector3 size = new Vector3(tableLimitSize * 2, 0.1f, tableLimitSize * 2);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
