using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class InteractionManager : MonoBehaviour
{
    [Header("Configuración de Raycast")]
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
    public Transform assemblyZone; // Arrastra aquí tu Motherboard
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
            // Buscamos el script PCPart en el objeto o sus padres
            PCPart part = hit.collider.GetComponentInParent<PCPart>();
            
            if (part != null)
            {
                // 1. Activar Modo Ingeniero (Top-Down)
                if (_camController != null && assemblyZone != null && !part.IsInstalled) 
                {
                    _camController.SetTopDownView(assemblyZone, topDownHeight);
                }

                // 2. Iniciar Arrastre
                if (!part.IsInstalled)
                {
                    _currentPart = part;
                    // Plano horizontal en el punto de impacto
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
            _currentPart.TryToSnap(); // Intentar conectar
            _currentPart = null;      // Soltar

            // Volver a vista panorámica tras 1 segundo
            StartCoroutine(ReturnToWideViewAfterDelay(1.0f));
        }
    }

    private IEnumerator ReturnToWideViewAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Solo volvemos si no ha agarrado otra pieza
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
            // FRENO DE HORIZONTE: Si está muy lejos, no movemos nada
            if (distance > maxRayDistance) return;

            Vector3 worldPos = ray.GetPoint(distance);

            // JAULA (CLAMPING): Restringir posición a los límites de la mesa
            if (assemblyZone != null)
            {
                float centerX = assemblyZone.position.x;
                float centerZ = assemblyZone.position.z;

                // La pieza no puede salir del cuadrado definido por tableLimitSize
                worldPos.x = Mathf.Clamp(worldPos.x, centerX - tableLimitSize, centerX + tableLimitSize);
                worldPos.z = Mathf.Clamp(worldPos.z, centerZ - tableLimitSize, centerZ + tableLimitSize);
                
                // Mantener altura fija
                worldPos.y = assemblyZone.position.y + dragHeight;
            }

            _currentPart.transform.position = worldPos;
        }
    }

    // --- VISUALIZACIÓN (GIZMOS) ---
    // Esto dibuja la jaula amarilla en la escena para que puedas calibrar el tamaño
    private void OnDrawGizmos()
    {
        if (assemblyZone != null)
        {
            // Color amarillo semitransparente para el borde
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.8f);

            Vector3 center = assemblyZone.position;
            center.y += dragHeight; // Dibujarlo a la altura de arrastre

            // El tamaño total es el doble del radio (tableLimitSize * 2)
            Vector3 size = new Vector3(tableLimitSize * 2, 0.1f, tableLimitSize * 2);

            Gizmos.DrawWireCube(center, size);
            
            // Relleno tenue
            Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.1f);
            Gizmos.DrawCube(center, size);
        }
    }
}