using UnityEngine;
using UnityEngine.InputSystem;

public class CADCameraController : MonoBehaviour
{
    [Header("Configuración Inicial")]
    public Transform targetInicial; // La Motherboard o el centro de la mesa

    [Header("Sensibilidad")]
    public float rotateSpeed = 5f;
    public float panSpeed = 0.01f;
    public float zoomStep = 2f;
    public float smoothing = 5f; // Un poco más suave para transiciones elegantes

    [Header("Límites")]
    public Vector2 zoomLimits = new Vector2(2f, 50f);
    public Vector2 verticalAngleLimit = new Vector2(5f, 89f);

    [Header("Automatización")]
    public float idleTimeBeforeReset = 10f; // Segundos sin tocar nada para volver al inicio
    public float wideShotDistance = 15f;    // Distancia de la vista general

    // --- ESTADO INTERNO ---
    private SimulationControls _controls;
    
    // Pivote y posiciones objetivo
    private Vector3 _targetPivotPosition; 
    private Vector3 _currentPivotPosition;

    private float _targetYaw;
    private float _targetPitch;
    private float _currentYaw;
    private float _currentPitch;

    private float _targetDistance;
    private float _currentDistance;

    // Variables para el Timer de Inactividad
    private float _lastInputTime;
    private Vector3 _initialPivotPos; // Para recordar dónde es el "Home"

    private void Awake()
    {
        _controls = new SimulationControls();
    }

    private void Start()
    {
        // 1. Configurar posición inicial
        if (targetInicial != null) 
            _targetPivotPosition = targetInicial.position;
        else 
            _targetPivotPosition = Vector3.zero;

        // Guardamos esta posición como el "Home"
        _initialPivotPos = _targetPivotPosition;

        Vector3 angles = transform.eulerAngles;
        _targetYaw = angles.y;
        _targetPitch = angles.x;
        _targetDistance = wideShotDistance; // Empezamos lejos

        // Inicializar suavizado
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

        // 0. RESET MANUAL (Barra Espaciadora)
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

    // --- LÓGICA DE INACTIVIDAD ---
    private void CheckIdle(bool hasInput)
    {
        if (hasInput)
        {
            _lastInputTime = Time.time; // Resetear reloj
        }
        else
        {
            // Si ha pasado el tiempo límite, volvemos a casa
            if (Time.time - _lastInputTime > idleTimeBeforeReset)
            {
                GoToWideView();
                _lastInputTime = Time.time; // Para que no lo llame cada frame
            }
        }
    }

    // --- MÉTODOS PÚBLICOS DE NAVEGACIÓN ---

    // 1. Volver al escritorio completo
    public void GoToWideView()
    {
        _targetPivotPosition = _initialPivotPos; // Centro de la mesa
        _targetDistance = wideShotDistance;      // Lejos
    }

    // 2. Enfocar para trabajar (Modo Ensamble)
    public void FocusForAssembly(Transform destination)
    {
        _targetPivotPosition = destination.position;
        _targetDistance = 5f; // Distancia cercana para trabajar (ajusta este valor)
        _lastInputTime = Time.time; // Evitar que el idle salte mientras trabajas
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

    public void SetTopDownView(Transform target, float height)
    {
        // 1. Centrar el pivote en la Motherboard
        _targetPivotPosition = target.position;
        
        // 2. Forzar la rotación a mirar hacia abajo (89 grados es casi perpendicular)
        _targetPitch = 89f; 
        
        // 3. Alinear la rotación horizontal (Opcional: 0 para que quede recta)
        _targetYaw = 0f; 

        // 4. Ajustar la altura (Zoom) para ver toda la placa
        _targetDistance = height;
        
        // Reiniciar el timer de inactividad para que no se mueva sola mientras trabajas
        _lastInputTime = Time.time;
    }
}