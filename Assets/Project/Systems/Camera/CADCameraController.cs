using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // ✅ NECESARIO PARA EL BLOQUEO

public class CADCameraController : MonoBehaviour
{
    [Header("Objetivos")]
    public Transform targetInicial;

    [Header("Sensibilidad")]
    public float rotateSpeed = 5f;
    public float panSpeed = 0.01f;
    public float zoomStep = 2f;
    public float smoothing = 5f; 

    [Header("Límites")]
    public Vector2 zoomLimits = new Vector2(2f, 50f);
    public Vector2 verticalAngleLimit = new Vector2(5f, 89f);

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

    // --- AQUÍ ESTÁ LA MODIFICACIÓN CLAVE ---
    private bool HandleInput()
    {
        bool receivedInput = false;

        // 🛑 1. FRENO DE SEGURIDAD DE UI
        // Si el mouse está tocando UI, la cámara NO debe moverse.
        // (Retornamos false para que tampoco resetee el timer de inactividad)
        if (EventSystem.current.IsPointerOverGameObject()) 
        {
            return false; 
        }

        // ... Código normal de cámara ...

        if (_controls.Player.ResetView.WasPressedThisFrame())
        {
            GoToWideView();
            receivedInput = true;
        }

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
        _targetYaw = 45f;
        _lastInputTime = Time.time;
    }

    public void SetTopDownView(Transform target, float height)
    {
        _targetPivotPosition = target.position;
        _targetPitch = 89f; 
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
}