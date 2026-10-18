using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cable procedural: un tubo suave que va de <see cref="startAnchor"/> a <see cref="endAnchor"/>
/// pasando por <see cref="waypoints"/> (bridas, pasacables). Reemplaza a los cables de
/// segmentos rígidos con ChainIK (codos visibles, sin caída, extremo fijo).
///
/// Cómo se construye la curva (spline de Bézier cúbica por tramos):
///  - Sale de cada conector en línea recta: el eje Z+ (azul) de cada ancla apunta hacia
///    donde SALE el cable. Así el cable entra "de frente" en el conector, como uno real.
///  - En los puntos de paso la tangente es la de Catmull-Rom (pasa suave por ellos).
///  - La holgura (longitud que sobra respecto al recorrido) se reparte entre los tramos y
///    los comba hacia <see cref="slackDirection"/> (abajo = gravedad).
///  - Con <see cref="restSurface"/> el cable no atraviesa esa superficie: se apoya en ella.
///  - Marcos de transporte paralelo + corrección de giro: el perfil plano (SATA, cintas de
///    laptop) no se retuerce de forma rara y llega bien orientado al conector final.
///
/// La malla se regenera solo cuando algo se mueve. Funciona también en el editor
/// (<c>ExecuteAlways</c>) para poder trazar el recorrido moviendo los puntos de paso.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class CableSpline : MonoBehaviour
{
    public enum Profile { Round, Flat }

    [Header("Recorrido (el eje Z+ de cada ancla apunta hacia donde SALE el cable)")]
    public Transform startAnchor;
    public Transform endAnchor;
    [Tooltip("Puntos de paso intermedios (bridas, pasacables), en orden. Opcional.")]
    public List<Transform> waypoints = new List<Transform>();

    [Header("Perfil")]
    public Profile profile = Profile.Round;
    [Tooltip("Radio (Round) o medio ancho (Flat), en unidades de mundo")]
    public float radius = 0.02f;
    [Tooltip("Medio grosor del perfil plano (Flat)")]
    public float flatHalfThickness = 0.005f;
    [Range(3, 24)] public int radialSegments = 10;
    [Tooltip("Muestras por unidad de longitud (más = curvas más suaves)")]
    public float samplesPerUnit = 40f;
    [Tooltip("Longitud del tramo recto con que el cable sale de cada conector")]
    public float stiffness = 0.06f;

    [Header("Longitud y caída")]
    [Tooltip("Longitud total del cable (unidades de mundo). <= 0: la del recorrido inicial × autoSlack")]
    public float length = 0f;
    [Range(1f, 2f)] public float autoSlack = 1.15f;
    [Tooltip("Hacia dónde se comba la longitud que sobra (mundo). Abajo = gravedad; arriba = cable rígido que se arquea por encima de la placa.")]
    public Vector3 slackDirection = Vector3.down;
    [Range(0f, 1f)] public float slackAmount = 1f;
    [Tooltip("Opcional: superficie (su eje Y+ es la normal) que el cable no atraviesa: se apoya encima")]
    public Transform restSurface;

    [Header("Interacción")]
    [Tooltip("Actualiza el MeshCollider (si hay) cuando el cable deja de moverse, para poder señalarlo/inspeccionarlo")]
    public bool updateCollider = true;

    /// <summary>Longitud efectiva (la del inspector o la automática).</summary>
    public float Length => length > 0f ? length : _autoLength;

    private Mesh _mesh;
    private float _autoLength;
    private bool _colliderDirty;
    private int _lastRebuildFrame = -1;
    private readonly List<Matrix4x4> _lastPoses = new List<Matrix4x4>();

    // Buffers reutilizados (sin basura por frame mientras se arrastra)
    private readonly List<Vector3> _points = new List<Vector3>();
    private readonly List<Vector3> _bezier = new List<Vector3>();
    private readonly List<Vector3> _samples = new List<Vector3>();
    private readonly List<Vector3> _vertices = new List<Vector3>();
    private readonly List<Vector3> _normals = new List<Vector3>();
    private readonly List<Vector2> _uvs = new List<Vector2>();
    private readonly List<int> _triangles = new List<int>();

    private void OnEnable()
    {
        if (_mesh == null)
        {
            _mesh = new Mesh { name = "Cable (procedural)", hideFlags = HideFlags.DontSave };
            _mesh.MarkDynamic();
        }
        GetComponent<MeshFilter>().sharedMesh = _mesh;
        _autoLength = 0f;
        _lastPoses.Clear();
        Rebuild();
    }

    private void OnDestroy()
    {
        if (_mesh == null) return;
        if (Application.isPlaying) Destroy(_mesh);
        else DestroyImmediate(_mesh);
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.0001f, radius);
        flatHalfThickness = Mathf.Max(0.0001f, flatHalfThickness);
        samplesPerUnit = Mathf.Max(1f, samplesPerUnit);
        _lastPoses.Clear(); // fuerza reconstrucción con los nuevos parámetros
    }

    private void LateUpdate()
    {
        if (PosesChanged()) Rebuild();
        else if (_colliderDirty && Time.frameCount > _lastRebuildFrame + 1) RefreshCollider();
    }

    /// <summary>Cambia hacia dónde se comba la holgura (p. ej. al conectar/desconectar) y fuerza la reconstrucción.</summary>
    public void SetSlackDirection(Vector3 direction)
    {
        if ((direction - slackDirection).sqrMagnitude < 1e-8f) return;
        slackDirection = direction;
        _lastPoses.Clear();
    }

    /// <summary>Distancia ya "gastada" hasta el último punto fijo y desde dónde puede alejarse el extremo.</summary>
    public Vector3 ReachOrigin(out float remaining)
    {
        Vector3 origin = startAnchor != null ? startAnchor.position : transform.position;
        float used = 0f;
        foreach (var w in waypoints)
        {
            if (w == null) continue;
            used += Vector3.Distance(origin, w.position);
            origin = w.position;
        }
        remaining = Mathf.Max(0f, Length - used);
        return origin;
    }

    /// <summary>Limita una posición del extremo libre a lo que alcanza el cable (lo usa <see cref="CablePlug"/>).</summary>
    public Vector3 ClampToReach(Vector3 desired)
    {
        if (Length <= 0f) return desired;
        Vector3 origin = ReachOrigin(out float remaining);
        Vector3 offset = desired - origin;
        return offset.magnitude <= remaining ? desired : origin + offset.normalized * remaining;
    }

    // ===================================================================== construcción

    private bool PosesChanged()
    {
        int count = 3 + waypoints.Count + (restSurface != null ? 1 : 0);
        bool changed = _lastPoses.Count != count;
        if (changed)
        {
            _lastPoses.Clear();
            for (int i = 0; i < count; i++) _lastPoses.Add(Matrix4x4.zero);
        }

        int k = 0;
        changed |= Track(transform, k++);
        changed |= Track(startAnchor, k++);
        changed |= Track(endAnchor, k++);
        foreach (var w in waypoints) changed |= Track(w, k++);
        if (restSurface != null) changed |= Track(restSurface, k);
        return changed;
    }

    private bool Track(Transform t, int index)
    {
        Matrix4x4 m = t != null ? t.localToWorldMatrix : Matrix4x4.zero;
        if (m == _lastPoses[index]) return false;
        _lastPoses[index] = m;
        return true;
    }

    /// <summary>Regenera la malla del cable. Se llama sola cuando algo se mueve.</summary>
    public void Rebuild()
    {
        if (_mesh == null || startAnchor == null || endAnchor == null) return;

        BuildPoints();
        if (_autoLength <= 0f || !Application.isPlaying) _autoLength = PathLength() * autoSlack;
        BuildBezier();
        SampleCurve();
        if (restSurface != null) RestOnSurface();
        BuildTube();

        _lastRebuildFrame = Time.frameCount;
        _colliderDirty = updateCollider;
    }

    private void BuildPoints()
    {
        _points.Clear();
        _points.Add(startAnchor.position);
        foreach (var w in waypoints) if (w != null) _points.Add(w.position);
        _points.Add(endAnchor.position);
    }

    private float PathLength()
    {
        float sum = 0f;
        for (int i = 0; i < _points.Count - 1; i++) sum += Vector3.Distance(_points[i], _points[i + 1]);
        return sum;
    }

    /// <summary>Puntos de control: 3 por tramo + 1 (P0, H0, H1, P1, H, H, P2, ...).</summary>
    private void BuildBezier()
    {
        _bezier.Clear();
        int n = _points.Count;
        float chordTotal = PathLength();
        float slack = Mathf.Max(0f, Length - chordTotal) * slackAmount;
        Vector3 sagDir = slackDirection.sqrMagnitude > 0f ? slackDirection.normalized : Vector3.down;

        for (int i = 0; i < n - 1; i++)
        {
            Vector3 a = _points[i], b = _points[i + 1];
            float chord = Vector3.Distance(a, b);

            // Asas: recta desde los conectores, Catmull-Rom en los puntos de paso.
            Vector3 outDir = i == 0 ? startAnchor.forward : CatmullDir(i);
            Vector3 inDir = i + 1 == n - 1 ? -endAnchor.forward : CatmullDir(i + 1);
            float hOut = i == 0 ? ConnectorHandle(chord) : chord * 0.33f;
            float hIn = i + 1 == n - 1 ? ConnectorHandle(chord) : chord * 0.33f;
            Vector3 h0 = a + outDir * hOut;
            Vector3 h1 = b - inDir * hIn;

            // Caída: la holgura del tramo lo comba perpendicular a la cuerda.
            // Parábola h ≈ √(3·c·s/8) con poca holgura; tiende a s/2 (dos ramales colgando) con mucha.
            if (slack > 0f && chord > 1e-5f && chordTotal > 1e-5f)
            {
                float s = slack * chord / chordTotal;
                float drop = Mathf.Sqrt(3f * chord * s / 8f + 0.25f * s * s);
                Vector3 axis = (b - a) / chord;
                Vector3 perp = sagDir - Vector3.Dot(sagDir, axis) * axis;
                Vector3 offset = perp * (drop / 0.75f); // mover ambas asas d baja el centro 0.75·d
                h0 += offset;
                h1 += offset;
            }

            if (i == 0) _bezier.Add(a);
            _bezier.Add(h0);
            _bezier.Add(h1);
            _bezier.Add(b);
        }
    }

    private float ConnectorHandle(float chord) => Mathf.Min(Mathf.Max(stiffness, chord * 0.3f), chord * 0.6f);

    private Vector3 CatmullDir(int i)
    {
        Vector3 d = _points[i + 1] - _points[i - 1];
        return d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.forward;
    }

    private void SampleCurve()
    {
        _samples.Clear();
        int segments = (_bezier.Count - 1) / 3;
        for (int s = 0; s < segments; s++)
        {
            Vector3 p0 = _bezier[s * 3], p1 = _bezier[s * 3 + 1], p2 = _bezier[s * 3 + 2], p3 = _bezier[s * 3 + 3];
            float approx = Vector3.Distance(p0, p1) + Vector3.Distance(p1, p2) + Vector3.Distance(p2, p3);
            int steps = Mathf.Clamp(Mathf.CeilToInt(approx * samplesPerUnit), 6, 160);
            for (int k = s == 0 ? 0 : 1; k <= steps; k++)
                _samples.Add(Bezier(p0, p1, p2, p3, k / (float)steps));
        }
    }

    private static Vector3 Bezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
    }

    /// <summary>Ningún punto por debajo de la superficie (más el radio): el cable se apoya en ella.</summary>
    private void RestOnSurface()
    {
        Vector3 up = restSurface.up;
        Vector3 origin = restSurface.position;
        float clearance = profile == Profile.Flat ? flatHalfThickness : radius;
        for (int i = 1; i < _samples.Count - 1; i++) // los extremos los fijan los conectores
        {
            float h = Vector3.Dot(_samples[i] - origin, up);
            if (h < clearance) _samples[i] += up * (clearance - h);
        }
    }

    private void BuildTube()
    {
        _vertices.Clear();
        _normals.Clear();
        _uvs.Clear();
        _triangles.Clear();

        int count = _samples.Count;
        if (count < 2)
        {
            _mesh.Clear();
            return;
        }

        int ring = radialSegments + 1; // +1: costura duplicada para el UV
        float a = radius;
        float b = profile == Profile.Flat ? flatHalfThickness : radius;
        float vScale = 1f / (2f * Mathf.PI * radius);

        // Marco inicial: la "arriba" del conector de salida.
        Vector3 tangent = Tangent(0);
        Vector3 normal = Vector3.ProjectOnPlane(startAnchor.up, tangent);
        if (normal.sqrMagnitude < 1e-8f) normal = Vector3.ProjectOnPlane(Vector3.up, tangent);
        if (normal.sqrMagnitude < 1e-8f) normal = Vector3.ProjectOnPlane(Vector3.right, tangent);
        normal.Normalize();

        // Primera pasada: transporte paralelo para saber cuánto giro sobra al llegar al final.
        Vector3 n = normal, t = tangent;
        for (int i = 1; i < count; i++)
        {
            Vector3 t2 = Tangent(i);
            n = Quaternion.FromToRotation(t, t2) * n;
            t = t2;
        }
        Vector3 endUp = Vector3.ProjectOnPlane(endAnchor.up, t);
        float twist = endUp.sqrMagnitude > 1e-8f ? Vector3.SignedAngle(n, endUp, t) : 0f;
        if (profile == Profile.Round) twist = 0f; // un tubo redondo no necesita llegar orientado

        // Segunda pasada: anillos, repartiendo la corrección de giro a lo largo del cable.
        Matrix4x4 toLocal = transform.worldToLocalMatrix;
        float travelled = 0f;
        n = normal;
        t = tangent;
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                Vector3 t2 = Tangent(i);
                n = Quaternion.FromToRotation(t, t2) * n;
                t = t2;
                travelled += Vector3.Distance(_samples[i - 1], _samples[i]);
            }

            Vector3 frameN = Quaternion.AngleAxis(twist * i / (count - 1), t) * n;
            Vector3 frameB = Vector3.Cross(t, frameN);
            Vector3 center = _samples[i];

            for (int j = 0; j < ring; j++)
            {
                // Grosor (b) sobre la "arriba" del conector y ancho (a) hacia su costado: la cinta
                // plana queda tumbada como un SATA real. El anillo recorre N→B (caras hacia fuera).
                float theta = 2f * Mathf.PI * j / radialSegments;
                float c = Mathf.Cos(theta), s = Mathf.Sin(theta);
                Vector3 offset = frameN * (c * b) + frameB * (s * a);
                Vector3 nrm = (frameN * (c / b) + frameB * (s / a)).normalized; // normal de la elipse

                _vertices.Add(toLocal.MultiplyPoint3x4(center + offset));
                _normals.Add(toLocal.MultiplyVector(nrm).normalized);
                _uvs.Add(new Vector2(j / (float)radialSegments, travelled * vScale));
            }
        }

        // Caras hacia fuera (orden horario de Unity visto desde fuera del tubo).
        for (int i = 0; i < count - 1; i++)
        {
            for (int j = 0; j < radialSegments; j++)
            {
                int v0 = i * ring + j, v1 = v0 + 1, v2 = v0 + ring, v3 = v2 + 1;
                _triangles.Add(v0); _triangles.Add(v1); _triangles.Add(v2);
                _triangles.Add(v1); _triangles.Add(v3); _triangles.Add(v2);
            }
        }

        _mesh.Clear();
        _mesh.indexFormat = _vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        _mesh.SetVertices(_vertices);
        _mesh.SetNormals(_normals);
        _mesh.SetUVs(0, _uvs);
        _mesh.SetTriangles(_triangles, 0);
        _mesh.RecalculateBounds();
    }

    private Vector3 Tangent(int i)
    {
        int last = _samples.Count - 1;
        Vector3 d = _samples[Mathf.Min(i + 1, last)] - _samples[Mathf.Max(i - 1, 0)];
        if (d.sqrMagnitude > 1e-12f) return d.normalized;
        return i == 0 ? startAnchor.forward : -endAnchor.forward;
    }

    private void RefreshCollider()
    {
        _colliderDirty = false;
        if (!Application.isPlaying) return;
        var col = GetComponent<MeshCollider>();
        if (col == null) return;
        col.sharedMesh = null; // fuerza a recocinar la malla
        col.sharedMesh = _mesh;
    }

    private void OnDrawGizmosSelected()
    {
        if (startAnchor == null || endAnchor == null) return;
        BuildPoints();
        Gizmos.color = new Color(1f, 0.55f, 0f, 0.9f);
        for (int i = 0; i < _points.Count - 1; i++) Gizmos.DrawLine(_points[i], _points[i + 1]);
        foreach (var p in _points) Gizmos.DrawWireSphere(p, radius * 1.5f);

        // Ejes de salida de los conectores (deben apuntar hacia el cable)
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(startAnchor.position, startAnchor.forward * stiffness * 2f);
        Gizmos.DrawRay(endAnchor.position, endAnchor.forward * stiffness * 2f);
    }
}
