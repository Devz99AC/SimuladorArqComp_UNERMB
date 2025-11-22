using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections; 
using UnityEngine.EventSystems; // NECESARIO para el bloqueo de UI

public class InteractionManager : MonoBehaviour
{
    [Header("Configuración de Raycast")]
    public LayerMask interactableLayer; 

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

    private void Awake()
    {
        _controls = new SimulationControls();
        _cam = Camera.main;
        
        if (_cam != null)
            _camController = _cam.GetComponent<CADCameraController>();
    }
    
    private void OnEnable() 
    { 
        _controls.Enable(); 
        _controls.Player.Select.performed += OnClick; 
        _controls.Player.Select.canceled += OnRelease; 
        _controls.Player.Inspect.performed += OnInspect;
        _controls.Player.ToggleInstructions.performed += OnToggleInstructions;
    }
    
    private void OnDisable() 
    { 
        _controls.Player.Select.performed -= OnClick; 
        _controls.Player.Select.canceled -= OnRelease; 
        _controls.Player.Inspect.performed -= OnInspect;
        _controls.Player.ToggleInstructions.performed -= OnToggleInstructions;
        _controls.Disable(); 
    }

    private void Update()
    {
        if (_currentPart != null) 
        {
            MovePart();
        }
    }

    // --- 1. ARRASTRE Y CÁMARA (CLIC IZQUIERDO) ---
    private void OnClick(InputAction.CallbackContext ctx)
    {
        // BLOQUEO UI: Si tocamos un botón, no hacemos nada
        if (EventSystem.current.IsPointerOverGameObject()) return;

        Vector2 mousePos = _controls.Player.Point.ReadValue<Vector2>();
        if (_cam == null) return;

        Ray ray = _cam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
        {
            PCPart part = hit.collider.GetComponentInParent<PCPart>();
            
            if (part != null)
            {
                // A. LOGICA DE CÁMARA:
                // Si la pieza NO está instalada (está en la mesa), ponemos vista Top-Down para ayudar
                if (_camController != null && assemblyZone != null && !part.IsInstalled) 
                {
                    _camController.SetTopDownView(assemblyZone, topDownHeight);
                }
                // Si ya está instalada (o es la Motherboard fija), podríamos simplemente enfocarla
                else if (_camController != null)
                {
                    _camController.FocusOnObject(part.transform);
                }

                // B. LOGICA DE ARRASTRE:
                // Solo arrastramos si NO está instalada Y si ES ARRASTRABLE
                if (!part.IsInstalled && part.isDraggable)
                {
                    _currentPart = part;
                    _dragPlane = new Plane(Vector3.up, hit.point);
                    
                    // --- NUEVO: OUTLINE AL ARRASTRAR ---
                    var highlighter = _currentPart.GetComponent<ObjectHighlighter>();
                    if (highlighter != null) highlighter.EnableHighlight();
                }
            }
        }
        else
        {
            // Clic en el vacío: Volver a vista panorámica
            if (_camController != null) 
            {
                _camController.GoToWideView();
            }
        }
    }

    private void OnRelease(InputAction.CallbackContext ctx)
    {
        if (_currentPart != null)
        {
            // --- NUEVO: QUITAR OUTLINE AL SOLTAR ---
            var highlighter = _currentPart.GetComponent<ObjectHighlighter>();
            if (highlighter != null) highlighter.DisableHighlight();

            _currentPart.TryToSnap(); 
            _currentPart = null;      

            StartCoroutine(ReturnToWideViewAfterDelay(1.0f));
        }
    }

    // --- 2. INSPECCIÓN UI (CLIC DERECHO) ---
    private void OnInspect(InputAction.CallbackContext ctx)
    {
        if (EventSystem.current.IsPointerOverGameObject()) return;
        if (_currentPart != null) return; // No inspeccionar si estamos arrastrando

        Vector2 mousePos = _controls.Player.Point.ReadValue<Vector2>();
        if (_cam == null) return;
        Ray ray = _cam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
        {
            PCPart part = hit.collider.GetComponentInParent<PCPart>();
            
            if (part != null)
            {
                // --- NUEVO: OUTLINE MOMENTANEO AL INSPECCIONAR ---
                var highlighter = part.GetComponent<ObjectHighlighter>();
                if (highlighter != null) 
                {
                    highlighter.EnableHighlight();
                    StartCoroutine(DisableHighlightDelay(highlighter, 2.0f));
                }

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowPartInfo(part.title, part.description);
                }
            }
        }
    }

    private void OnToggleInstructions(InputAction.CallbackContext ctx)
    {
        if (UIManager.Instance != null) UIManager.Instance.ToggleInstructions();
    }

    private IEnumerator DisableHighlightDelay(ObjectHighlighter highlighter, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (highlighter != null) highlighter.DisableHighlight();
    }

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