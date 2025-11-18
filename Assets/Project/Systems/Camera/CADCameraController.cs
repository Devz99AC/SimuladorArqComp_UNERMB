using UnityEngine;
using UnityEngine.InputSystem;

public class CADCameraController : MonoBehaviour
{
    [Header("Configuración Inicial")]
    public Transform targetInicial; // La Motherboard

    [Header("Sensibilidad")]
    public float rotateSpeed = 5f;
    public float panSpeed = 0.01f;
    public float zoomStep = 2f;
    public float smoothing = 5f; 

    [Header("Límites")]
    public Vector2 zoomLimits = new Vector2(2f, 50f);
    public Vector2 verticalAngleLimit = new Vector2(5f, 89f);

    [Header("Automatización")]
    public float idleTimeBeforeReset = 10f; // Segundos de inactividad para resetear
    public float wideShotDistance = 15f;    // Distancia de la vista "Home"

    // --- ESTADO INTERNO ---
    private SimulationControls _controls;
    
    private Vector3 _targetPivotPosition; 
    private Vector3 _currentPivotPosition;

    private float _targetYaw;
    private float _targetPitch;
    private float _currentYaw;
    private float _currentPitch;

    private float _targetDistance;
    private float _currentDistance;

    private float _lastInputTime;
    private Vector3 _initialPivotPos; 

    private void Awake()
    {
        _controls = new SimulationControls();
    }

    private void Start()
    {
        if (targetInicial != null) 
            _targetPivotPosition = targetInicial.position;
        else 
            _targetPivotPosition = Vector3.zero;

        _initialPivotPos = _targetPivotPosition;

        Vector3 angles = transform.eulerAngles;
        _targetYaw = angles.y;
        _targetPitch = angles.x;
        _targetDistance = wideShotDistance; 

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
        bool receivedInput = false;

        // --- 0. RESET MANUAL (Barra Espaciadora) ---
        // Verifica que hayas creado la acción "ResetView" en el Input Map
        if (_controls.Player.ResetView.WasPressedThisFrame())
        {
            GoToWideView();
            receivedInput = true;
        }

        // 1. PANEO
        if (_controls.Player.Pan.IsPressed())
        {
            Vector2 delta = _controls.Player.Pan.ReadValue<Vector2>();
            if (delta.sqrMagnitude > 0.1f)
            {
                Vector3 right = transform.right * -delta.x * panSpeed;
                Vector3 up = transform.up * -delta.y * panSpeed;
                _targetPivotPosition += right + up;
                receivedInput = true;
            }
        }

        // 2. ORBITA
        if (_controls.Player.Inspect.IsPressed())
        {
            Vector2 delta = _controls.Player.Delta.ReadValue<Vector2>();
            if (delta.sqrMagnitude > 0.1f)
            {
                _targetYaw += delta.x * rotateSpeed * 0.1f;
                _targetPitch -= delta.y * rotateSpeed * 0.1f;
                _targetPitch = Mathf.Clamp(_targetPitch, verticalAngleLimit.x, verticalAngleLimit.y);
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

    // --- LÓGICA DE RETORNO ---
    private void CheckIdle(bool hasInput)
    {
        if (hasInput)
        {
            _lastInputTime = Time.time; 
        }
        else
        {
            if (Time.time - _lastInputTime > idleTimeBeforeReset)
            {
                GoToWideView();
                _lastInputTime = Time.time; 
            }
        }
    }

    // --- MÉTODOS PÚBLICOS ---

    public void GoToWideView()
    {
        _targetPivotPosition = _initialPivotPos; 
        _targetDistance = wideShotDistance;      
        
        // Reseteamos rotación a una vista isométrica agradable (45, 45)
        // Opcional: Si prefieres mantener la rotación actual, comenta estas 2 líneas
        _targetPitch = 45f; 
        _targetYaw = 45f;
    }

    public void SetTopDownView(Transform target, float height)
    {
        _targetPivotPosition = target.position;
        _targetPitch = 89f; // Casi cenital
        _targetYaw = 0f; 
        _targetDistance = height;
        _lastInputTime = Time.time;
    }

    // Esta función se mantiene por compatibilidad con el InteractionManager anterior
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
}