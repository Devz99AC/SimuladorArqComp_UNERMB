using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Señal luminosa en un puerto de cable libre. Los cabezales de la placa son pequeños y oscuros: sin ayuda
/// cuesta encontrarlos. Mientras el puerto está libre se ve una caja translúcida del tamaño del conector:
///  - late con brillo cuando ya se puede conectar (todo lo que exige su <see cref="SocketSystem.requires"/> está puesto);
///  - queda tenue mientras falta algo (p. ej. la placa aún no está en el gabinete);
///  - desaparece al conectar.
/// La caja se crea al empezar (no se guarda en la escena), en la capa Ignore Raycast: no tapa clics ni sostiene piezas.
/// </summary>
[RequireComponent(typeof(SocketSystem))]
public class CablePortMarker : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Tooltip("Tamaño del conector que entra aquí: ancho (X), largo (Y) y alto (Z, por donde sale el cable)")]
    public Vector3 size = new Vector3(0.01f, 0.02f, 0.012f);
    [Tooltip("Cuánto más grande que el conector se dibuja la señal")]
    public float padding = 1.5f;
    public Color color = new Color(1f, 0.55f, 0.12f, 1f);
    [Tooltip("Pulsos por segundo cuando ya se puede conectar")]
    public float pulseSpeed = 1.2f;
    [Range(0f, 1f)] public float readyMin = 0.55f;
    [Range(0f, 1f)] public float readyMax = 0.9f;
    [Range(0f, 1f)] public float waitingAlpha = 0.2f;

    private SocketSystem _socket;
    private Renderer _renderer;
    private MaterialPropertyBlock _block;

    // En Start (no en Awake): ObjectHighlighter guarda los renderers de la pieza en Awake y así no se lleva la señal.
    private void Start()
    {
        _socket = GetComponent<SocketSystem>();
        var material = Resources.Load<Material>("Mat_PuertoCable");
        if (material == null) { enabled = false; return; }

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.name = "Señal del puerto";
        go.layer = 2; // Ignore Raycast
        go.transform.SetParent(transform, false);
        // size está en metros: se descuenta la escala heredada (el disco reutilizado viene ×100).
        Vector3 inherited = transform.lossyScale;
        go.transform.localScale = new Vector3(size.x / inherited.x, size.y / inherited.y, size.z / inherited.z) * padding;

        _renderer = go.GetComponent<MeshRenderer>();
        _renderer.sharedMaterial = material;
        _renderer.shadowCastingMode = ShadowCastingMode.Off;
        _renderer.receiveShadows = false;
        _block = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (_renderer == null) return;

        bool show = !_socket.isOccupied;
        if (_renderer.enabled != show) _renderer.enabled = show;
        if (!show) return;

        float alpha = waitingAlpha;
        if (Ready())
        {
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * 2f * Mathf.PI);
            alpha = Mathf.Lerp(readyMin, readyMax, wave);
        }
        _block.SetColor(BaseColorId, new Color(color.r, color.g, color.b, alpha));
        _renderer.SetPropertyBlock(_block);
    }

    private bool Ready()
    {
        foreach (var req in _socket.requires)
            if (req != null && !req.IsComplete) return false;
        return true;
    }
}
