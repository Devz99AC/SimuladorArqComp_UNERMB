using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.EventSystems; // Necesario para bloquear UI

public class InteractionManager : MonoBehaviour
{
    [Header("Configuración de Raycast")]
    public LayerMask interactableLayer;

    [Header("Configuración Visual (Colores)")]
    [Tooltip("Material Naranja Transparente (Hover/Inspección)")]
    public Material hoverMaterial; 
    [Tooltip("Material Verde Transparente (Arrastre)")]
    public Material dragMaterial;  

    [Header("Configuración de Arrastre")]
    public float dragHeight = 1.5f;
    
    [Header("Límites de la Mesa")]
    public float tableLimitSize = 4f;
    public float maxRayDistance = 30f;

    [Header("Configuración de Cámara")]
    public Transform assemblyZone;
    public float topDownHeight = 12f;

    // Referencias internas
    private SimulationControls _controls;
    private Camera _cam;
    private CADCameraController _camController;

    private PCPart _currentPart;
    private Plane _dragPlane;

    // Estado del Highlight
    private ObjectHighlighter _currentHighlighter;

    private void Awake()
    {
        _controls = new SimulationControls();
        _cam = Camera.main;
        if (_cam != null)
            _camController = _cam.GetComponent<CADCameraController>();
    }

    private void OnEnable() => _controls.Enable();
    private void OnDisable() => _controls.Disable();

    private void Update()
    {
        // 1. Manejo del Highlight (Hover constante)
        HandleHover();

        // 2. Inputs de Mouse
        if (_controls.Player.Select.WasPressedThisFrame()) HandleLeftClick();
        if (_controls.Player.Select.WasReleasedThisFrame()) HandleRelease();
        if (_controls.Player.Inspect.WasPressedThisFrame()) HandleRightClick();

        // 3. Movimiento de pieza
        if (_currentPart != null) MovePart();
    }

    // --- LÓGICA VISUAL (HIGHLIGHT) ---
    private void HandleHover()
    {
        // Si estamos arrastrando, el objeto ya está verde, no calculamos hover
        if (_currentPart != null) return;

        // Si tocamos UI, limpiamos todo
        if (EventSystem.current.IsPointerOverGameObject()) 
        {
            ClearHighlight();
            return;
        }

        Vector2 mousePos = _controls.Player.Point.ReadValue<Vector2>();
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

    // --- LÓGICA DE INTERACCIÓN ---

    private void HandleLeftClick()
    {
        if (EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 mousePos = _controls.Player.Point.ReadValue<Vector2>();
        Ray ray = _cam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
        {
            PCPart part = hit.collider.GetComponentInParent<PCPart>();

            if (part != null)
            {
                // A. Cámara
                if (_camController != null && assemblyZone != null && !part.IsInstalled)
                    _camController.SetTopDownView(assemblyZone, topDownHeight);
                else if (_camController != null)
                    _camController.FocusOnObject(part.transform);

                // B. Arrastre
                if (!part.IsInstalled && part.isDraggable)
                {
                    _currentPart = part;
                    _dragPlane = new Plane(Vector3.up, hit.point);

                    // FEEDBACK VISUAL: CAMBIAR A VERDE
                    ObjectHighlighter hl = part.GetComponent<ObjectHighlighter>();
                    if (hl != null)
                    {
                        ClearHighlight(); // Limpiar hover naranja
                        _currentHighlighter = hl;
                        // Usamos material VERDE porque lo tenemos agarrado
                        _currentHighlighter.SetHighlight(dragMaterial != null ? dragMaterial : hoverMaterial); 
                    }
                }
            }
        }
        else if (_camController != null) 
        {
            _camController.GoToWideView();
        }
    }

    private void HandleRelease()
    {
        if (_currentPart != null)
        {
            _currentPart.TryToSnap();
            
            // Apagamos el brillo verde al soltar
            ClearHighlight(); 
            
            _currentPart = null;
            StartCoroutine(ReturnToWideViewAfterDelay(1.0f));
        }
    }

    private void HandleRightClick()
    {
        if (EventSystem.current.IsPointerOverGameObject()) return;
        if (_currentPart != null) return;

        Vector2 mousePos = _controls.Player.Point.ReadValue<Vector2>();
        Ray ray = _cam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
        {
            PCPart part = hit.collider.GetComponentInParent<PCPart>();
            if (part != null && UIManager.Instance != null)
            {
                UIManager.Instance.ShowPartInfo(part.title, part.description);
                
                // Opcional: Mantener el highlight naranja mientras lees la ficha
                ObjectHighlighter hl = part.GetComponent<ObjectHighlighter>();
                if (hl != null) 
                {
                    ClearHighlight();
                    _currentHighlighter = hl;
                    hl.SetHighlight(hoverMaterial);
                }
            }
        }
    }

    // Corrutina para volver a la vista panorámica
    private IEnumerator ReturnToWideViewAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_currentPart == null && _camController != null) _camController.GoToWideView();
    }

    private void MovePart()
    {
        Vector2 mousePos = _controls.Player.Point.ReadValue<Vector2>();
        Ray ray = _cam.ScreenPointToRay(mousePos);

        if (_dragPlane.Raycast(ray, out float distance))
        {
            if (distance > maxRayDistance) return;
            Vector3 worldPos = ray.GetPoint(distance);

            if (assemblyZone != null)
            {
                float centerX = assemblyZone.position.x;
                float centerZ = assemblyZone.position.z;
                worldPos.x = Mathf.Clamp(worldPos.x, centerX - tableLimitSize, centerX + tableLimitSize);
                worldPos.z = Mathf.Clamp(worldPos.z, centerZ - tableLimitSize, centerZ + tableLimitSize);
                worldPos.y = assemblyZone.position.y + dragHeight;
            }
            _currentPart.transform.position = worldPos;
        }
    }

    // Dibujar límites en el editor
    private void OnDrawGizmos()
    {
        if (assemblyZone != null)
        {
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.8f);
            Vector3 center = assemblyZone.position;
            center.y += dragHeight;
            Vector3 size = new Vector3(tableLimitSize * 2, 0.1f, tableLimitSize * 2);
            Gizmos.DrawWireCube(center, size);
        }
    }
}