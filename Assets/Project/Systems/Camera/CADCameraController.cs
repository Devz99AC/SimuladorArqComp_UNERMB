using UnityEngine;
using UnityEngine.InputSystem;
// Bloqueo puntero-sobre-UI vía UIBlocker (UI Toolkit), no EventSystem de uGUI.

public class CADCameraController : MonoBehaviour
{
    [Header("Objetivos")]
    public Transform targetInicial;

    [Header("Sensibilidad")]
    public float rotateSpeed = 5f;
    public float panSpeed = 0.01f;
    [Tooltip("Fracción de la distancia que avanza cada paso de rueda (zoom proporcional: fino de cerca, rápido de lejos)")]
    [Range(0.02f, 0.5f)] public float zoomPercent = 0.12f;
    public float smoothing = 5f;

    [Header("Límites Verticales (Cuello)")]
    public Vector2 verticalAngleLimit = new Vector2(5f, 89f);

    [Header("Límites Horizontales (Giro)")]
    public bool limitHorizontalRotation = true;
    public Vector2 horizontalAngleLimit = new Vector2(-45f, 45f);

    [Header("Límites de Zoom")]
    public Vector2 zoomLimits = new Vector2(2f, 50f);

    [Header("Límites de Paneo (La Caja 3D)")]
    public bool enablePanLimits = true;
    [Tooltip("Ancho: Izquierda / Derecha")]
    public Vector2 panLimitX = new Vector2(-5f, 5f);
    [Tooltip("Altura: Suelo / Techo (Evita ir bajo la mesa)")]
    public Vector2 panLimitY = new Vector2(0f, 3f);
    [Tooltip("Profundidad: Atrás / Fondo")]
    public Vector2 panLimitZ = new Vector2(-5f, 5f);

    [Header("Gestos")]
    [Tooltip("Píxeles de arrastre con clic derecho antes de empezar a orbitar (un clic corto abre la ficha)")]
    public float orbitStartPixels = 6f;

    [Header("Automatización")]
    [Tooltip("Volver sola a la vista general tras un rato sin tocar nada. Desactivado: el encuadre lo decide el estudiante (Espacio reencuadra).")]
    public bool autoResetWhenIdle = false;
    public float idleTimeBeforeReset = 10f;
    public float wideShotDistance = 15f;

    private SimulationControls _controls;
    private Vector3 _targetPivotPosition;
    private Vector3 _currentPivotPosition;
    private float _targetYaw, _targetPitch, _currentYaw, _currentPitch;
    private float _targetDistance, _currentDistance;
    private float _lastInputTime;
    private Vector3 _initialPivotPos;

    // Gestos en curso (solo pueden EMPEZAR fuera de la UI; una vez empezados, siguen aunque el puntero pase sobre un panel)
    private bool _orbitArmed, _orbiting, _panning;
    private Vector2 _orbitPressPos;

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

    // Los controles se crean aquí (no en Awake): si Unity recompila en mitad del Play, los campos no
    // serializables vuelven a null y solo OnEnable se vuelve a llamar.
    private void OnEnable()
    {
        if (_controls == null) _controls = new SimulationControls();
        _controls.Enable();
    }
    private void OnDisable() => _controls?.Disable();
    private void OnDestroy() => _controls?.Dispose();

    private void LateUpdate()
    {
        bool hasInput = HandleInput();
        CheckIdle(hasInput);
        ApplyMovement();
    }

    private bool HandleInput()
    {
        if (UIBlocker.ModalOpen)
        {
            _orbitArmed = _orbiting = _panning = false;
            return false;
        }

        Vector2 pointer = _controls.Player.Point.ReadValue<Vector2>();
        bool overUI = UIBlocker.IsPointerOverUI(pointer);
        bool receivedInput = false;

        if (_controls.Player.ResetView.WasPressedThisFrame())
        {
            GoToWideView();
            receivedInput = true;
        }

        // 1. PANEO (botón central)
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.middleButton.wasPressedThisFrame) _panning = !overUI;
            if (!mouse.middleButton.isPressed) _panning = false;
        }
        if (_panning)
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

        // 2. ÓRBITA (clic derecho + arrastre). Un clic derecho corto NO orbita: es la ficha técnica.
        if (_controls.Player.Inspect.WasPressedThisFrame())
        {
            _orbitArmed = !overUI;
            _orbiting = false;
            _orbitPressPos = pointer;
        }
        if (!_controls.Player.Inspect.IsPressed())
        {
            _orbitArmed = false;
            _orbiting = false;
        }
        if (_orbitArmed && !_orbiting && (pointer - _orbitPressPos).sqrMagnitude > orbitStartPixels * orbitStartPixels)
        {
            _orbiting = true;
        }
        if (_orbiting)
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

        // 3. ZOOM (no si la rueda está sobre un panel: ahí desplaza su texto)
        Vector2 scroll = _controls.Player.Zoom.ReadValue<Vector2>();
        if (!overUI && Mathf.Abs(scroll.y) > 0.1f)
        {
            float factor = scroll.y > 0f ? 1f - zoomPercent : 1f / (1f - zoomPercent);
            _targetDistance = Mathf.Clamp(_targetDistance * factor, zoomLimits.x, zoomLimits.y);
            receivedInput = true;
        }

        return receivedInput;
    }

    private void CheckIdle(bool hasInput)
    {
        if (hasInput) _lastInputTime = Time.time;
        else if (autoResetWhenIdle && Time.time - _lastInputTime > idleTimeBeforeReset) GoToWideView();
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

    /// <summary>Centra la órbita en el objeto y se acerca un poco (doble clic sobre una pieza).</summary>
    public void FocusOnObject(Transform newTarget)
    {
        _targetPivotPosition = newTarget.position;
        _targetDistance = Mathf.Clamp(_targetDistance * 0.6f, zoomLimits.x, zoomLimits.y);
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

    // GIZMOS: la caja 3D de paneo
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
