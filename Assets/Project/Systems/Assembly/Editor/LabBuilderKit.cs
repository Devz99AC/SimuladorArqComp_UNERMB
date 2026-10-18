using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ayudantes comunes de los constructores de laboratorio (<see cref="LabAtxBuilder"/>, <see cref="LabSffBuilder"/>…):
/// instanciar modelos, apoyarlos en la mesa, medir cajas, crear piezas, lugares, puertos y cables, y ajustar la
/// cámara y el arrastre de la escena. Todas las escenas de laboratorio comparten la mesa (a 0,849 m), la capa
/// Interactable y los tipos de conexión de Data/ScriptableObjects/Connections Type.
/// Los constructores la usan con <c>using static LabBuilderKit;</c>.
/// </summary>
public static class LabBuilderKit
{
    public const string TypesDir = "Assets/Project/Data/ScriptableObjects/Connections Type/";
    public const float DeskY = 0.849f;
    public const int InteractableLayer = 3;

    static StringBuilder _log;

    public static void BeginLog() => _log = new StringBuilder();
    public static void Log(string s) => _log?.AppendLine(s);
    public static string EndLog() => _log != null ? _log.ToString() : "";

    // ===================================================================== objetos y medidas

    public static Transform Spawn(string path, string name, Transform parent)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    public static Transform NewChild(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    public static ConnectionTypeSO Type(string name) =>
        string.IsNullOrEmpty(name) ? null : AssetDatabase.LoadAssetAtPath<ConnectionTypeSO>(TypesDir + name + ".asset");

    /// <summary>Apoya el objeto en la mesa con el centro de <paramref name="b"/> en (x, z).</summary>
    public static void RestOnDesk(Transform t, Vector2 xz, Bounds b)
    {
        t.position += new Vector3(xz.x - b.center.x, DeskY - b.min.y, xz.y - b.center.z);
    }

    public static Bounds WorldBounds(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    /// <summary>Caja de las mallas en el espacio local de <paramref name="t"/>.</summary>
    public static Bounds LocalBounds(Transform t)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            Matrix4x4 m = t.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z);
                Vector3 p = m.MultiplyPoint3x4(c);
                if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                else b.Encapsulate(p);
            }
        }
        return b;
    }

    /// <summary>Caja en mundo que tendría la pieza con rotación <paramref name="rot"/> y pivote en el origen.</summary>
    public static Bounds BoundsAt(Transform t, Quaternion rot)
    {
        Vector3 p = t.position;
        Quaternion r = t.rotation;
        t.SetPositionAndRotation(Vector3.zero, rot);
        Bounds b = WorldBounds(t);
        t.SetPositionAndRotation(p, r);
        return b;
    }

    public static void SetLayer(Transform t, int layer)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = layer;
    }

    /// <summary>Centro de la base (vértices más bajos) en el espacio local de la pieza.</summary>
    public static Vector3 BaseCentroidLocal(Transform t)
    {
        var pts = new List<Vector3>();
        float minY = float.MaxValue;
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
            foreach (var v in mf.sharedMesh.vertices)
            {
                Vector3 w = mf.transform.TransformPoint(v);
                pts.Add(w);
                if (w.y < minY) minY = w.y;
            }
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (var w in pts)
            if (w.y < minY + 0.002f) { sum += w; n++; }
        return t.InverseTransformPoint(sum / Mathf.Max(1, n));
    }

    public static Vector3 Hit(Collider c, Vector3 from, Vector3 dir, Vector3 fallback)
    {
        return c.Raycast(new Ray(from, dir), out RaycastHit h, 2f) ? h.point : fallback;
    }

    public static float TopNear(Collider col, float x, float z, float half, float fallback)
    {
        float top = float.MinValue;
        for (int ix = -1; ix <= 1; ix++)
            for (int iz = -1; iz <= 1; iz++)
                if (col.Raycast(new Ray(new Vector3(x + ix * half, DeskY + 0.5f, z + iz * half), Vector3.down), out RaycastHit h, 1f))
                    top = Mathf.Max(top, h.point.y);
        return top == float.MinValue ? fallback : top;
    }

    /// <summary>Colisionador de caja (sin malla) entre dos esquinas en mundo.</summary>
    public static Transform Box(Transform parent, string name, Vector3 min, Vector3 max)
    {
        Transform t = NewChild(name, parent);
        t.SetPositionAndRotation((min + max) * 0.5f, Quaternion.identity);
        var box = t.gameObject.AddComponent<BoxCollider>();
        box.size = max - min;
        return t;
    }

    /// <summary>Primitiva visible (sin colisionador) entre dos esquinas en mundo, alineada con los ejes.</summary>
    public static Transform Solid(Transform parent, string name, Vector3 min, Vector3 max, Material mat, PrimitiveType shape = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(shape);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation((min + max) * 0.5f, Quaternion.identity);
        Vector3 size = max - min;
        go.transform.localScale = shape == PrimitiveType.Cylinder ? new Vector3(size.x, size.y * 0.5f, size.z) : size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    public static void StaticBlock(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = Vector3.Scale(size, Inverse(parent.lossyScale));
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    public static Vector3 Inverse(Vector3 v) => new Vector3(1f / v.x, 1f / v.y, 1f / v.z);

    // ===================================================================== piezas y lugares

    public static SocketSystem MakeSocket(string name, Transform parent, Vector3 pos, Quaternion rot, string type, bool required = true, string note = null)
    {
        Transform t = NewChild(name, parent);
        t.SetPositionAndRotation(pos, rot);
        var s = t.gameObject.AddComponent<SocketSystem>();
        s.allowedType = Type(type);
        s.snapPoint = t;
        s.required = required;
        s.installNote = note;
        return s;
    }

    /// <summary>Lugar con la pose exacta de una pieza que el modelo trae ya montada.</summary>
    public static SocketSystem SocketFromChild(Transform board, string child, string name, string type, bool required = true, string note = null)
    {
        Transform c = board.Find(child);
        return MakeSocket(name, board, c.position, c.rotation, type, required, note);
    }

    /// <summary>Separa una pieza de la placa y la deja en la mesa.</summary>
    public static Transform Detach(Transform part, Transform root, string name, Vector2 xz, Quaternion extraRot)
    {
        part.name = name;
        part.SetParent(root, true);
        part.rotation = extraRot * part.rotation;
        RestOnDesk(part, xz, WorldBounds(part));
        return part;
    }

    public static PCPart MakePart(Transform t, string id, string type, string title, string desc, bool draggable = true, bool fitCollider = true)
    {
        SetLayer(t, InteractableLayer);
        if (fitCollider)
        {
            Bounds lb = LocalBounds(t);
            var box = t.gameObject.AddComponent<BoxCollider>();
            box.center = lb.center;
            box.size = lb.size;
        }
        var part = t.gameObject.AddComponent<PCPart>();
        part.isDraggable = draggable;
        part.partName = id;
        part.connectionType = Type(type);
        part.title = title;
        part.description = desc;
        t.gameObject.AddComponent<ObjectHighlighter>();
        return part;
    }

    // ===================================================================== cables

    /// <summary>Dónde cuelgan los cables de un laboratorio y cómo se construye su tubo.</summary>
    public class CableRig
    {
        public Transform cables;      // padre de los tubos y de los conectores que reposan en la mesa
        public Transform rest;        // superficie de apoyo de los cables (la mesa)
        public Transform fixedParent; // padre de los conectores de cables que salen de algo fijo (el gabinete)
        public int radialSegments = 8;
        public float samplesPerUnit = 160f;
        public float stiffness = 0.025f;
    }

    public static Transform Anchor(string name, Transform parent, Vector3 pos, Vector3 dir)
    {
        Transform t = NewChild(name, parent);
        t.SetPositionAndRotation(pos, Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.forward : Vector3.up));
        return t;
    }

    /// <summary>Pose de un conector suelto acostado: su Z+ (por donde sale el cable) mira hacia <paramref name="dir"/>.</summary>
    public static Quaternion Lying(Vector3 dir) => Quaternion.LookRotation(dir, Vector3.Cross(dir, Vector3.up));

    public static Transform Home(string name, Transform parent, Vector3 pos, Vector3 dir)
    {
        Transform t = NewChild(name, parent);
        t.SetPositionAndRotation(pos, Lying(dir));
        return t;
    }

    /// <summary>Puerto de cable: un lugar normal con el tipo del conector, su señal luminosa y lo que exige antes.</summary>
    public static SocketSystem Port(string name, Transform parent, Vector3 pos, Quaternion rot, string type, Vector3 plugSize, params SocketSystem[] requires)
    {
        var s = MakeSocket(name, parent, pos, rot, type);
        foreach (var r in requires) s.requires.Add(r);
        AddPortMarker(s, plugSize);
        return s;
    }

    /// <summary>Señal luminosa del puerto (los cabezales son pequeños y oscuros: el usuario pidió que se noten más).</summary>
    public static void AddPortMarker(SocketSystem socket, Vector3 plugSize)
    {
        socket.gameObject.AddComponent<CablePortMarker>().size = plugSize;
    }

    public static Transform MakePlug(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 size, Material mat,
                                     string type, string title, string desc)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        MakePart(go.transform, name, type, title, desc, fitCollider: false);
        return go.transform;
    }

    /// <summary>Cable cuyo origen es una pieza que también se instala: su conector reposa en la mesa y luego dentro del equipo.</summary>
    public static CablePlug Cable(CableRig c, string name, Transform start, string type, Vector3 plugSize, Material plugMat, Material cableMat,
                                  CableSpline.Profile profile, float radius, float length, Vector3 deskXZ, Vector3 deskDir,
                                  SocketSystem source, Transform homeInstalled, string title, string desc, float flatHalfThickness = 0.001f,
                                  Vector3? looseSlack = null)
    {
        var pos = new Vector3(deskXZ.x, DeskY + plugSize.x * 0.5f, deskXZ.z);
        Transform plug = MakePlug("Conector: " + title, c.cables, pos, Lying(deskDir), plugSize, plugMat, type, title, desc);
        var spline = Spline(c, name, start, plug, cableMat, profile, radius, flatHalfThickness, length, title, desc);
        var cp = plug.gameObject.AddComponent<CablePlug>();
        cp.cable = spline;
        cp.sourceSocket = source;
        cp.homeWhenSourceInstalled = homeInstalled;
        if (looseSlack.HasValue) SetLooseSlack(cp, looseSlack.Value);
        return cp;
    }

    public static void SetLooseSlack(CablePlug plug, Vector3 direction)
    {
        var so = new SerializedObject(plug);
        so.FindProperty("looseSlack").vector3Value = direction.normalized;
        so.ApplyModifiedPropertiesWithoutUndo();
        plug.cable.slackDirection = direction.normalized;
    }

    /// <summary>
    /// Cable que sale de algo fijo (<see cref="CableRig.fixedParent"/>): su conector reposa en <paramref name="home"/>.
    /// La caída se aplica perpendicular a la cuerda: "abajo" a secas puede sacar el sobrante por una pared, así que
    /// <paramref name="looseSlack"/> la inclina hacia el interior.
    /// </summary>
    public static CablePlug CaseCable(CableRig c, string name, Transform start, string type, Vector3 plugSize, Material plugMat, Material cableMat,
                                      float radius, float length, Transform home, string title, string desc, Vector3? looseSlack = null,
                                      CableSpline.Profile profile = CableSpline.Profile.Round, float flatHalfThickness = 0.001f)
    {
        Transform plug = MakePlug("Conector: " + title, c.fixedParent, home.position, home.rotation, plugSize, plugMat, type, title, desc);
        Object.DestroyImmediate(home.gameObject);
        var spline = Spline(c, name, start, plug, cableMat, profile, radius, flatHalfThickness, length, title, desc);
        var cp = plug.gameObject.AddComponent<CablePlug>();
        cp.cable = spline;
        SetLooseSlack(cp, looseSlack ?? new Vector3(0f, -0.4f, 1f));
        return cp;
    }

    public static CableSpline Spline(CableRig c, string name, Transform start, Transform plug, Material mat, CableSpline.Profile profile,
                                     float radius, float flatHalfThickness, float length, string title, string desc)
    {
        var go = new GameObject(name);
        go.transform.SetParent(c.cables, false);
        go.layer = InteractableLayer;
        var spline = go.AddComponent<CableSpline>();
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        spline.startAnchor = start;
        spline.endAnchor = plug;
        spline.profile = profile;
        spline.radius = radius;
        spline.flatHalfThickness = flatHalfThickness;
        spline.radialSegments = c.radialSegments;
        spline.samplesPerUnit = c.samplesPerUnit;
        spline.stiffness = c.stiffness;
        spline.length = length;
        spline.restSurface = c.rest;
        go.AddComponent<MeshCollider>();
        var info = go.AddComponent<PCPart>();
        info.isDraggable = false;
        info.partName = "Cable";
        info.title = title;
        info.description = desc;
        go.AddComponent<ObjectHighlighter>();
        return spline;
    }

    /// <summary>Construye ya la malla de todos los cables (al añadir CableSpline aún no tenía anclas; en Play se rehace sola).</summary>
    public static void RebuildCables(CableRig c)
    {
        foreach (var spline in c.cables.GetComponentsInChildren<CableSpline>()) spline.Rebuild();
    }

    // ===================================================================== escena

    /// <summary>Encuadre de la cámara y ajustes del arrastre de un laboratorio.</summary>
    public class SceneFrame
    {
        public Vector3 center = new Vector3(0f, 0.88f, 0f);
        public float wideShotDistance = 1.3f;
        public float wideShotPitch = 60f;
        public Vector2 zoomLimits = new Vector2(0.15f, 2.2f);
        public Vector2 panLimitX = new Vector2(-0.9f, 0.9f);
        public Vector2 panLimitY = new Vector2(0.80f, 1.40f);
        public Vector2 panLimitZ = new Vector2(-0.4f, 0.4f);
        public Vector2 verticalAngleLimit = new Vector2(5f, 89f);
        public Vector2 horizontalAngleLimit = new Vector2(-90f, 90f);
        public float liftHeight = 0.05f;
        public float hoverClearance = 0.008f;
    }

    public static void ConfigureScene(Transform root, SceneFrame f)
    {
        // Mesa con colisionador: las piezas se apoyan en ella al arrastrar.
        var wood = GameObject.Find("Entorno/Desk/wood");
        if (wood != null && wood.GetComponent<Collider>() == null) wood.AddComponent<BoxCollider>();

        Transform center = NewChild("Centro de trabajo", root);
        center.position = f.center;

        var cam = Camera.main;
        if (cam != null)
        {
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 80f;
            var cad = cam.GetComponent<CADCameraController>();
            if (cad != null)
            {
                cad.targetInicial = center;
                cad.wideShotDistance = f.wideShotDistance;
                cad.wideShotPitch = f.wideShotPitch;
                cad.zoomLimits = f.zoomLimits;
                cad.panLimitX = f.panLimitX;
                cad.panLimitY = f.panLimitY;
                cad.panLimitZ = f.panLimitZ;
                cad.verticalAngleLimit = f.verticalAngleLimit;
                cad.horizontalAngleLimit = f.horizontalAngleLimit;
                EditorUtility.SetDirty(cad);
            }
            cam.transform.rotation = Quaternion.Euler(f.wideShotPitch, 0f, 0f);
            cam.transform.position = center.position - cam.transform.forward * f.wideShotDistance;
        }

        var im = Object.FindAnyObjectByType<InteractionManager>();
        if (im != null)
        {
            var so = new SerializedObject(im);
            so.FindProperty("liftHeight").floatValue = f.liftHeight;
            so.FindProperty("hoverClearance").floatValue = f.hoverClearance;
            so.FindProperty("maxRayDistance").floatValue = 10f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var sun = GameObject.Find("Directional Light");
        if (sun != null && sun.GetComponent<ShadowDistanceOverride>() == null) sun.AddComponent<ShadowDistanceOverride>();
    }
}
