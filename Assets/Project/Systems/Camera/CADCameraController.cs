using UnityEngine;
using UnityEngine.InputSystem;

public class CADCameraController : MonoBehaviour
{
    [Header("Configuración Inicial")]
    public Transform targetInicial; // La Motherboard (para empezar mirando algo)

    [Header("Sensibilidad (Ajusta a tu gusto)")]
    public float rotateSpeed = 5f;     // Velocidad de rotación (Click Derecho)
    public float panSpeed = 0.01f;     // Velocidad de paneo (Rueda presionada)
    public float zoomStep = 2f;        // Fuerza del Zoom (Rueda)
    public float smoothing = 10f;      // Suavizado (Más alto = más rápido/rígido)

    [Header("Límites")]
    public Vector2 zoomLimits = new Vector2(2f, 50f); // Nunca acercarse a menos de 2m
    public Vector2 verticalAngleLimit = new Vector2(5f, 89f); // No pasar por debajo del suelo

    // --- ESTADO INTERNO ---
    private SimulationControls _controls;
    
    // El "Pivote": El punto invisible en el espacio que estamos mirando
    private Vector3 _targetPivotPosition; 
    private Vector3 _currentPivotPosition; // Para suavizado

    private float _targetYaw;
    private float _targetPitch;
    private float _currentYaw;
    private float _currentPitch;

    private float _targetDistance;
    private float _currentDistance;

    private void Awake()
    {
        _controls = new SimulationControls();
    }

    private void Start()
    {
        // Configuración inicial
        if (targetInicial != null) 
            _targetPivotPosition = targetInicial.position;
        else 
            _targetPivotPosition = Vector3.zero;

        // Copiamos la posición actual de la cámara para que no haya "saltos" al play
        Vector3 angles = transform.eulerAngles;
        _targetYaw = angles.y;
        _targetPitch = angles.x;
        _targetDistance = 15f; // Distancia inicial estándar

        // Inicializamos los valores actuales
        _currentPivotPosition = _targetPivotPosition;
        _currentYaw = _targetYaw;
        _currentPitch = _targetPitch;
        _currentDistance = _targetDistance;
    }

    private void OnEnable() => _controls.Enable();
    private void OnDisable() => _controls.Disable();

    private void LateUpdate()
    {
        HandleInput();
        ApplyMovement();
    }

    // --- INPUT (Lo que tú haces con el mouse) ---
    private void HandleInput()
    {
        // 1. PANEO (Mover el pivote lateralmente) - Click Central
        if (_controls.Player.Pan.IsPressed())
        {
            Vector2 delta = _controls.Player.Pan.ReadValue<Vector2>();
            
            // Movemos el pivote relativo a la cámara (Izquierda/Derecha, Arriba/Abajo)
            Vector3 right = transform.right * -delta.x * panSpeed;
            Vector3 up = transform.up * -delta.y * panSpeed;
            
            _targetPivotPosition += right + up;
        }

        // 2. ORBITA (Rotar alrededor del pivote) - Click Derecho
        if (_controls.Player.Inspect.IsPressed())
        {
            Vector2 delta = _controls.Player.Delta.ReadValue<Vector2>();

            _targetYaw += delta.x * rotateSpeed * 0.1f;
            _targetPitch -= delta.y * rotateSpeed * 0.1f;
            
            // Restringir ángulo vertical (para no dar la vuelta completa)
            _targetPitch = Mathf.Clamp(_targetPitch, verticalAngleLimit.x, verticalAngleLimit.y);
        }

        // 3. ZOOM (Acercarse al pivote) - Rueda
        Vector2 scroll = _controls.Player.Zoom.ReadValue<Vector2>();
        if (Mathf.Abs(scroll.y) > 0.1f)
        {
            // Invertimos dirección si es necesario (-Mathf.Sign)
            float zoomDir = -Mathf.Sign(scroll.y); 
            _targetDistance += zoomDir * zoomStep;
            
            // Clamp IMPORTANTE: Evita que el zoom llegue a 0 o negativo (atravesar objetos)
            _targetDistance = Mathf.Clamp(_targetDistance, zoomLimits.x, zoomLimits.y);
        }
    }

    // --- MOVIMIENTO (Matemática suave) ---
    private void ApplyMovement()
    {
        float dt = Time.deltaTime * smoothing;

        // Interpolación (Lerp) para que todo sea suave como mantequilla
        _currentPivotPosition = Vector3.Lerp(_currentPivotPosition, _targetPivotPosition, dt);
        _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, dt);
        _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, dt);
        _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, dt);

        // CALCULAR POSICIÓN FINAL
        Quaternion rotation = Quaternion.Euler(_currentPitch, _currentYaw, 0);
        
        // La fórmula mágica de la órbita: Pivote - (Dirección * Distancia)
        Vector3 finalPosition = _currentPivotPosition - (rotation * Vector3.forward * _currentDistance);

        transform.position = finalPosition;
        transform.rotation = rotation;
    }

    // --- PÚBLICO: PARA CUANDO HACES CLICK EN UNA PIEZA ---
    public void FocusOnObject(Transform newTarget)
    {
        // Solo movemos el pivote al centro del objeto
        _targetPivotPosition = newTarget.position;
        
        // Opcional: Ajustamos la distancia a algo cómodo, PERO NO CERO
        // Si estamos muy lejos, nos acercamos. Si ya estamos cerca, no tocamos el zoom.
        if (_targetDistance > 10f)
        {
            _targetDistance = 8f;
        }
    }

    // Dibuja una bolita amarilla en la escena para saber qué estás mirando
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_targetPivotPosition, 0.5f);
    }
}