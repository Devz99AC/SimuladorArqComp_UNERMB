using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; 

public class CADCameraController : MonoBehaviour
{
    [Header("Objetivos")]
    public Transform targetInicial;

    [Header("Sensibilidad")]
    public float rotateSpeed = 5f;
    public float panSpeed = 0.01f;
    public float zoomStep = 2f;
    public float smoothing = 5f; 

    [Header("Límites Verticales (Cuello)")]
    public Vector2 verticalAngleLimit = new Vector2(5f, 89f);

    [Header("Límites Horizontales (Giro)")]
    public bool limitHorizontalRotation = true;
    public Vector2 horizontalAngleLimit = new Vector2(-45f, 45f);

    [Header("Límites de Zoom")]
    public Vector2 zoomLimits = new Vector2(2f, 50f);

    [Header("Límites de Paneo (La Caja 3D)")] // --- ACTUALIZADO ---
    public bool enablePanLimits = true;
    [Tooltip("Ancho: Izquierda / Derecha")]
    public Vector2 panLimitX = new Vector2(-5f, 5f);
    [Tooltip("Altura: Suelo / Techo (Evita ir bajo la mesa)")]
    public Vector2 panLimitY = new Vector2(0f, 3f); // <--- NUEVO
    [Tooltip("Profundidad: Atrás / Fondo")]
    public Vector2 panLimitZ = new Vector2(-5f, 5f);

    [Header("Automatización")]
    public float idleTimeBeforeReset = 10f;
    public float wideShotDistance = 15f;    

    private SimulationControls _controls;
    private Vector3 _targetPivotPosition; 
    private Vector3 _currentPivotPosition;
    private float _targetYaw, _targetPitch, _currentYaw, _currentPitch;
    private float _targetDistance, _currentDistance;
    private float _lastInputTime;
    private Vector3 _initialPivotPos; 

    private void Awake() => _controls = new SimulationControls();

    private void Start()
    {
        if (targetInicial != null) _targetPivotPosition = targetInicial.position;
        else _targetPivotPosition = Vector3.zero;

        _initialPivotPos = _targetPivotPosition;

        Vector3 angles = transform.eulerAngles;
        _targetYaw = angles.y;
        _targetPitch = angles.x;
        _targetDistance = wideShotDistance; 

        if (limitHorizontalRotation)
            _targetYaw = Mathf.Clamp(_targetYaw, horizontalAngleLimit.x, horizontalAngleLimit.y);

        _currentPivotPosition = _targetPivotPosition;
        _currentYaw = _targetYaw;
        _currentPitch = _targetPitch;
        _currentDistance = _targetDistance;
        
        _lastInputTime = Time.time;
    }

    private void OnEnable() => _controls.Enable();
    private void OnDisable() => _controls.Disable();

    private void LateUpdate()
    {
        bool hasInput = HandleInput();
        CheckIdle(hasInput);
        ApplyMovement();
    }

    private bool HandleInput()
    {
        if (EventSystem.current.IsPointerOverGameObject()) return false;

        bool receivedInput = false;

        if (_controls.Player.ResetView.WasPressedThisFrame())
        {
            GoToWideView();
            receivedInput = true;
        }

        // 1. PANEO (Con límite Y agregado)
        if (_controls.Player.Pan.IsPressed())
        {
            Vector2 delta = _controls.Player.Pan.ReadValue<Vector2>();
            if (delta.sqrMagnitude > 0.1f)
            {
                Vector3 right = transform.right * -delta.x * panSpeed;
                Vector3 up = transform.up * -delta.y * panSpeed;
                
                _targetPivotPosition += right + up;

                if (enablePanLimits)
                {
                    // Clamp X (Lados)
                    _targetPivotPosition.x = Mathf.Clamp(_targetPivotPosition.x, panLimitX.x, panLimitX.y);
                    
                    // Clamp Y (Altura) - ESTO EVITA IR AL INFINITO
                    _targetPivotPosition.y = Mathf.Clamp(_targetPivotPosition.y, panLimitY.x, panLimitY.y);
                    
                    // Clamp Z (Profundidad)
                    _targetPivotPosition.z = Mathf.Clamp(_targetPivotPosition.z, panLimitZ.x, panLimitZ.y);
                }
                receivedInput = true;
            }
        }

        // 2. ÓRBITA
        if (_controls.Player.Inspect.IsPressed())
        {
            Vector2 delta = _controls.Player.Delta.ReadValue<Vector2>();
            if (delta.sqrMagnitude > 0.1f)
            {
                _targetYaw += delta.x * rotateSpeed * 0.1f;
                _targetPitch -= delta.y * rotateSpeed * 0.1f;
                
                _targetPitch = Mathf.Clamp(_targetPitch, verticalAngleLimit.x, verticalAngleLimit.y);

                if (limitHorizontalRotation)
                {
                    _targetYaw = Mathf.Clamp(_targetYaw, horizontalAngleLimit.x, horizontalAngleLimit.y);
                }

                receivedInput = true;
            }
        }

        // 3. ZOOM
        Vector2 scroll = _controls.Player.Zoom.ReadValue<Vector2>();
        if (Mathf.Abs(scroll.y) > 0.1f)
        {
            float zoomDir = -Mathf.Sign(scroll.y); 
            _targetDistance += zoomDir * zoomStep;
            _targetDistance = Mathf.Clamp(_targetDistance, zoomLimits.x, zoomLimits.y);
            receivedInput = true;
        }

        return receivedInput;
    }

    private void CheckIdle(bool hasInput)
    {
        if (hasInput) _lastInputTime = Time.time; 
        else if (Time.time - _lastInputTime > idleTimeBeforeReset) GoToWideView();
    }

    public void GoToWideView()
    {
        _targetPivotPosition = _initialPivotPos; 
        _targetDistance = wideShotDistance;      
        _targetPitch = 45f; 
        
        if (limitHorizontalRotation)
            _targetYaw = (horizontalAngleLimit.x + horizontalAngleLimit.y) / 2;
        else
            _targetYaw = 45f;

        _lastInputTime = Time.time;
    }

    public void SetTopDownView(Transform target, float height)
    {
        _targetPivotPosition = target.position;
        _targetPitch = 89f; 
        
        if (limitHorizontalRotation)
            _targetYaw = (horizontalAngleLimit.x + horizontalAngleLimit.y) / 2;
        else
            _targetYaw = 0f;

        _targetDistance = height;
        _lastInputTime = Time.time;
    }

    public void FocusOnObject(Transform newTarget)
    {
         _targetPivotPosition = newTarget.position;
         if (_targetDistance > 10f) _targetDistance = 8f;
         _lastInputTime = Time.time;
    }

    private void ApplyMovement()
    {
        float dt = Time.deltaTime * smoothing;
        _currentPivotPosition = Vector3.Lerp(_currentPivotPosition, _targetPivotPosition, dt);
        _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, dt);
        _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, dt);
        _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, dt);

        Quaternion rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0);
        Vector3 finalPosition = _currentPivotPosition - (rotation * Vector3.forward * _currentDistance);

        transform.position = finalPosition;
        transform.rotation = rotation;
    }

    // GIZMOS ACTUALIZADOS: AHORA VES LA CAJA 3D
    private void OnDrawGizmosSelected()
    {
        if (enablePanLimits)
        {
            Gizmos.color = Color.cyan;
            
            float sizeX = panLimitX.y - panLimitX.x;
            float sizeY = panLimitY.y - panLimitY.x; // Altura
            float sizeZ = panLimitZ.y - panLimitZ.x;
            
            float centerX = (panLimitX.x + panLimitX.y) / 2;
            float centerY = (panLimitY.x + panLimitY.y) / 2; // Centro Y
            float centerZ = (panLimitZ.x + panLimitZ.y) / 2;

            Vector3 center = new Vector3(centerX, centerY, centerZ);
            Vector3 size = new Vector3(sizeX, sizeY, sizeZ);

            Gizmos.DrawWireCube(center, size);
        }
    }
}