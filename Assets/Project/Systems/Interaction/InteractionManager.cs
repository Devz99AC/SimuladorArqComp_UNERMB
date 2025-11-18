using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections; // Necesario para Corrutinas

public class InteractionManager : MonoBehaviour
{
    [Header("Configuración de Raycast")]
    [Tooltip("Capa de los objetos que se pueden tocar (Piezas, Motherboard)")]
    public LayerMask interactableLayer; 

    [Header("Configuración de Arrastre")]
    [Tooltip("Altura a la que flota la pieza al arrastrarla")]
    public float dragHeight = 1.5f; 
    
    [Header("Límites de la Mesa (La Jaula)")]
    [Tooltip("Distancia máxima desde el centro que la pieza puede alejarse")]
    public float tableLimitSize = 4f; 
    [Tooltip("Si el rayo golpea más lejos que esto, lo ignoramos (Freno de horizonte)")]
    public float maxRayDistance = 30f;

    [Header("Configuración de Cámara")]
    [Tooltip("El objeto central del ensamble (Motherboard)")]
    public Transform assemblyZone; 
    [Tooltip("Altura de la cámara en modo Ingeniero")]
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
        // Suscribir eventos de Clic Izquierdo (Arrastre)
        _controls.Player.Select.performed += OnClick; 
        _controls.Player.Select.canceled += OnRelease; 
        
        // Suscribir evento de Clic Derecho (Inspección UI)
        _controls.Player.Inspect.performed += OnInspect;
    }
    
    private void OnDisable() 
    { 
        _controls.Player.Select.performed -= OnClick; 
        _controls.Player.Select.canceled -= OnRelease; 
        _controls.Player.Inspect.performed -= OnInspect;
        _controls.Disable(); 
    }

    private void Update()
    {
        // Lógica de movimiento continua mientras se arrastra
        if (_currentPart != null) 
        {
            MovePart();
        }
    }

    // --- 1. ARRASTRE Y CÁMARA (CLIC IZQUIERDO) ---
    private void OnClick(InputAction.CallbackContext ctx)
    {
        Vector2 mousePos = _controls.Player.Point.ReadValue<Vector2>();
        if (_cam == null) return;

        Ray ray = _cam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
        {
            PCPart part = hit.collider.GetComponentInParent<PCPart>();
            
            if (part != null)
            {
                // A. Activar Modo Ingeniero (Top-Down) si no está instalada
                if (_camController != null && assemblyZone != null && !part.IsInstalled) 
                {
                    _camController.SetTopDownView(assemblyZone, topDownHeight);
                }

                // B. Iniciar Arrastre
                if (!part.IsInstalled)
                {
                    _currentPart = part;
                    _dragPlane = new Plane(Vector3.up, hit.point);
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
            _currentPart.TryToSnap(); 
            _currentPart = null;      

            StartCoroutine(ReturnToWideViewAfterDelay(1.0f));
        }
    }

    // --- 2. INSPECCIÓN UI (CLIC DERECHO) ---
    private void OnInspect(InputAction.CallbackContext ctx)
    {
        Debug.Log("🖱️ [Input] Clic Derecho detectado.");

        // Si estamos arrastrando algo, ignoramos la inspección
        if (_currentPart != null) 
        {
            Debug.Log("⚠️ [Bloqueo] No se puede inspeccionar mientras arrastras una pieza.");
            return;
        }

        Vector2 mousePos = _controls.Player.Point.ReadValue<Vector2>();
        if (_cam == null) 
        {
            Debug.LogError("❌ [Error] No se encontró la cámara principal.");
            return;
        }

        Ray ray = _cam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableLayer))
        {
            Debug.Log($"🔨 [Raycast] Golpeó: {hit.collider.gameObject.name}");

            PCPart part = hit.collider.GetComponentInParent<PCPart>();
            
            if (part != null)
            {
                Debug.Log($"🧩 [Componente] Se encontró PCPart: {part.title}");

                // Llamar al UIManager para mostrar la ficha técnica
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowPartInfo(part.title, part.description);
                    Debug.Log("✅ [UI] Orden enviada al UIManager.");
                }
                else
                {
                    Debug.LogError("❌ [Error] UIManager.Instance es NULL. ¿Pusiste el script UIManager en la escena?");
                }
            }
            else
            {
                Debug.LogWarning("⚠️ [Falta Script] El objeto tiene Layer correcto pero NO tiene script PCPart.");
            }
        }
        else
        {
            Debug.Log("💨 [Aire] El raycast no tocó nada en el Layer Interactable.");
        }
    }
    
    // --- UTILIDADES Y LÓGICA MATEMÁTICA ---

    private IEnumerator ReturnToWideViewAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_currentPart == null && _camController != null)
        {
            _camController.GoToWideView();
        }
    }

    private void MovePart()
    {
        Vector2 mousePos = _controls.Player.Point.ReadValue<Vector2>();
        Ray ray = _cam.ScreenPointToRay(mousePos);

        if (_dragPlane.Raycast(ray, out float distance))
        {
            // Freno de horizonte
            if (distance > maxRayDistance) return;

            Vector3 worldPos = ray.GetPoint(distance);

            // Jaula (Límites de la mesa)
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

    // Visualización en Editor
    private void OnDrawGizmos()
    {
        if (assemblyZone != null)
        {
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.8f);
            Vector3 center = assemblyZone.position;
            center.y += dragHeight;
            Vector3 size = new Vector3(tableLimitSize * 2, 0.1f, tableLimitSize * 2);
            Gizmos.DrawWireCube(center, size);
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.1f);
            Gizmos.DrawCube(center, size);
        }
    }
}