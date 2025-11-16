using UnityEngine;
using UnityEngine.InputSystem;

public class InteractionManager : MonoBehaviour
{
    [Header("Configuración")]
    public LayerMask interactableLayer; // Recuerda asignar el Layer "Interactable" aquí
    public float dragHeight = 0.5f;     // Altura a la que flota la pieza al arrastrar

    // Referencias
    private SimulationControls _controls;
    private Camera _cam;
    private CADCameraController _camController; // <--- AHORA USAMOS EL NUEVO SCRIPT CAD
    
    // Estado del Drag & Drop
    private PCPart _currentPart; 
    private Plane _dragPlane; 

    private void Awake()
    {
        _controls = new SimulationControls();
        _cam = Camera.main;
        
        // Buscamos el nuevo script de cámara estilo CAD
        if (_cam != null)
            _camController = _cam.GetComponent<CADCameraController>();
    }
    
    private void OnEnable() 
    { 
        _controls.Enable(); 
        _controls.Player.Select.performed += OnClick; 
        _controls.Player.Select.canceled += OnRelease; 
    }
    
    private void OnDisable() 
    { 
        _controls.Player.Select.performed -= OnClick; 
        _controls.Player.Select.canceled -= OnRelease; 
        _controls.Disable(); 
    }

    private void Update()
    {
        if (_currentPart != null) 
        {
            MovePart();
        }
    }

    private void OnClick(InputAction.CallbackContext ctx)
    {
        Vector2 mousePos = _controls.Player.Point.ReadValue<Vector2>();
        if (_cam == null) return;

        Ray ray = _cam.ScreenPointToRay(mousePos);

        // Lanzamos Raycast
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
        {
            // Buscamos el script PCPart (usando InParent por seguridad)
            PCPart part = hit.collider.GetComponentInParent<PCPart>();
            
            if (part != null)
            {
                // 1. ORDEN A LA CÁMARA: "Cambia tu pivote a esta pieza"
                if (_camController != null) 
                {
                    _camController.FocusOnObject(part.transform);
                }

                // 2. LOGICA DE ARRASTRE
                if (!part.IsInstalled)
                {
                    _currentPart = part;
                    // Creamos plano matemático en la posición del impacto
                    _dragPlane = new Plane(Vector3.up, hit.point);
                }
            }
        }
        else
        {
            // SI HACEMOS CLIC EN EL VACÍO:
            // Volvemos a enfocar el objetivo inicial (la Motherboard) para no perdernos
            if (_camController != null && _camController.targetInicial != null) 
            {
                _camController.FocusOnObject(_camController.targetInicial);
            }
        }
    }

    private void OnRelease(InputAction.CallbackContext ctx)
    {
        if (_currentPart != null)
        {
            _currentPart.TryToSnap(); 
            _currentPart = null;      
        }
    }

    private void MovePart()
    {
        Vector2 mousePos = _controls.Player.Point.ReadValue<Vector2>();
        Ray ray = _cam.ScreenPointToRay(mousePos);

        if (_dragPlane.Raycast(ray, out float distance))
        {
            // Mover la pieza a donde apunta el mouse en el plano invisible
            Vector3 targetPos = ray.GetPoint(distance);
            // Le sumamos un poquito de altura para que no atraviese la mesa al arrastrar
            targetPos.y += dragHeight; 
            
            _currentPart.transform.position = targetPos;
        }
    }
}