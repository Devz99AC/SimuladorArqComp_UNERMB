using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Construye el contenido del laboratorio ATX (escena Lab_ATX) a partir de los modelos de
/// Content/Models/Lab_ATX: gabinete tumbado y abierto, placa base en la mesa, piezas sueltas y
/// todos los lugares de encaje. Es la fuente de verdad de las posiciones: volver a ejecutarlo
/// rehace "PC (Lab ATX)" desde cero (lo ajustado a mano dentro de ese objeto se pierde).
///
/// Escala real (1 unidad = 1 m). La mesa está a 0,849 m y el estudiante mira desde -Z.
/// El gabinete se mide al construir (rayos contra su malla), así que las posiciones internas
/// se adaptan si cambia el modelo. Lo que el modelo no trae se añade aquí: tapa del hueco
/// trasero sobrante, escudo de E/S, separadores de la placa y la jaula del disco.
/// Las rotaciones de instalación se aplican sobre la pose con la que aparece cada modelo (algunos
/// FBX traen la raíz girada), porque la orientación de cada pieza se analizó en esa pose.
/// </summary>
public static class LabAtxBuilder
{
    const string Models = "Assets/Project/Content/Models/Lab_ATX/";
    const string TypesDir = "Assets/Project/Data/ScriptableObjects/Connections Type/";
    const string ExtrasDir = Models + "Añadidos/";
    const string ColorDir = Models + "Colores";
    const string RootName = "PC (Lab ATX)";
    const float DeskY = 0.849f;
    const int InteractableLayer = 3;

    // Huecos del modelo de gabinete medidos con rayos (relativos a la esquina inferior del chasis
    // tumbado: x desde su cara "suelo del gabinete", y desde la mesa). El de arriba no corresponde
    // a nada de una torre ATX y se tapa.
    static readonly Vector2 HuecoSobranteX = new Vector2(0.141f, 0.331f);
    static readonly Vector2 HuecoSobranteY = new Vector2(0.123f, 0.182f);

    static StringBuilder _log;

    struct Interior
    {
        public float floor, rear, front, bottom, top;
        public Bounds outer;
    }

    [MenuItem("Tools/Simulador/Reconstruir laboratorio ATX")]
    static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        _log = new StringBuilder();
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        Transform root = new GameObject(RootName).transform;

        // ---------------- Gabinete (fijo, con ficha) ----------------
        Transform caseT = Spawn(Models + "Gabinete/Gabinete.fbx", "Gabinete", root);
        caseT.rotation = Quaternion.LookRotation(Vector3.up, Vector3.right) * caseT.rotation; // bandeja abajo, frente hacia el estudiante, techo a la derecha
        Transform chasis = caseT.Find("Chasis");
        RestOnDesk(caseT, new Vector2(0.30f, 0.05f), chasis.GetComponent<Renderer>().bounds);
        caseT.Find("PanelLateral").gameObject.SetActive(false); // abierto: el panel se quitó para trabajar
        Interior inn = MeasureInterior(chasis);
        Log($"Interior: suelo {inn.floor:F4}  fondo(trasera) {inn.rear:F4}  frente {inn.front:F4}  base(-X) {inn.bottom:F4}  techo(+X) {inn.top:F4}");
        Log($"Exterior: {inn.outer.min:F3} .. {inn.outer.max:F3}");
        BuildCaseColliders(caseT, inn);
        MakePart(caseT, "Gabinete", null, "Gabinete ATX (torre media)",
            "Es la carcasa que protege y sujeta todos los componentes. Aquí está tumbado y sin el panel lateral, como se hace al armar un equipo. " +
            "Por dentro tiene la bandeja de la placa base con sus separadores, el hueco de la fuente (abajo y al fondo), la bahía del disco y " +
            "los ventiladores, que meten aire fresco por el frente y lo sacan por detrás.",
            draggable: false, fitCollider: false);

        // ---------------- Placa base (en la mesa, se prepara fuera) ----------------
        Transform board = Spawn(Models + "PlacaBase/PlacaBase.fbx", "Placa base", root);
        board.rotation = Quaternion.Euler(0f, -90f, 0f) * board.rotation; // misma orientación que tendrá dentro del gabinete: meterla es solo trasladarla
        Transform boardMesh = board.Find("PlacaBase");
        RestOnDesk(board, new Vector2(-0.22f, 0.04f), boardMesh.GetComponent<Renderer>().bounds);
        Bounds pcb = boardMesh.GetComponent<Renderer>().bounds;

        // Lugares tomados de las piezas que el modelo trae ya montadas (pose exacta).
        SocketSystem cpuSocket = SocketFromChild(board, "CPU", "Lugar CPU (socket LGA 1151)", "Type_CPU");
        SocketSystem m2Socket = SocketFromChild(board, "SSD_M2", "Lugar SSD M.2", "Type_M2");
        string notaRam = "Funciona, pero con dos módulos el manual de la placa indica A2 y B2: así trabajan en doble canal (el doble de ancho de banda).";
        SocketFromChild(board, "RAM_1", "Lugar RAM DIMM_A1", "Type_RAM", required: false, note: notaRam);
        SocketFromChild(board, "RAM_2", "Lugar RAM DIMM_A2", "Type_RAM");
        SocketFromChild(board, "RAM_3", "Lugar RAM DIMM_B1", "Type_RAM", required: false, note: notaRam);
        SocketFromChild(board, "RAM_4", "Lugar RAM DIMM_B2", "Type_RAM");

        Bounds cpuInstalled = WorldBounds(board.Find("CPU"));

        // Las piezas se separan de la placa y quedan sueltas en la mesa.
        Transform cpu = Detach(board.Find("CPU"), root, "CPU", new Vector2(-0.31f, -0.19f), Quaternion.identity);
        Transform m2 = Detach(board.Find("SSD_M2"), root, "SSD M.2", new Vector2(-0.31f, -0.265f), Quaternion.identity);
        Quaternion lying = Quaternion.AngleAxis(90f, Vector3.right); // módulos de RAM acostados en la mesa
        Transform ram1 = Detach(board.Find("RAM_1"), root, "RAM 1", new Vector2(-0.16f, -0.16f), lying);
        Transform ram2 = Detach(board.Find("RAM_2"), root, "RAM 2", new Vector2(-0.16f, -0.225f), lying);
        Object.DestroyImmediate(board.Find("RAM_3").gameObject);
        Object.DestroyImmediate(board.Find("RAM_4").gameObject);

        // Disipador: su base sobre la cara superior de la CPU (con la CPU en su sitio).
        Transform cooler = Spawn(Models + "Disipador/Disipador.fbx", "Disipador", root);
        Quaternion coolerRot = Quaternion.Euler(0f, 90f, 0f) * cooler.rotation; // ventilador hacia el frente: empuja el aire hacia la salida trasera
        var cpuTop = new Vector3(cpuInstalled.center.x, cpuInstalled.max.y, cpuInstalled.center.z);
        Vector3 coolerBase = BaseCentroidLocal(cooler);
        var coolerSocket = MakeSocket("Lugar disipador", board, cpuTop - coolerRot * Vector3.Scale(coolerBase, cooler.lossyScale), coolerRot, "Type_Cooler");
        coolerSocket.requires.Add(cpuSocket);
        RestOnDesk(cooler, new Vector2(-0.62f, -0.06f), WorldBounds(cooler));

        // GPU: contactos dorados dentro de la ranura PCIe x16 más cercana al procesador.
        Transform gpu = Spawn(Models + "GPU/GPU.fbx", "Tarjeta de video", root);
        RestOnDesk(gpu, new Vector2(-0.70f, -0.24f), WorldBounds(gpu));
        Vector3 slot = MeasurePcieSlot(boardMesh, new Vector3(-0.2524f, 0f, 0.0602f), out float slotTop);
        Quaternion gpuRot = Quaternion.LookRotation(Vector3.down, Vector3.left) * gpu.rotation; // contactos abajo, bracket atrás, ventiladores hacia la fuente
        Vector3 fingers = GpuFingersLocal(gpu);
        Vector3 fingersTarget = new Vector3(slot.x, slotTop - 0.0075f, slot.z);
        var gpuSocket = MakeSocket("Lugar GPU (PCIe x16)", board, fingersTarget - gpuRot * Vector3.Scale(fingers, gpu.lossyScale), gpuRot, "Type_GPU");
        Log($"Ranura PCIe x16: centro {slot:F4}, borde superior {slotTop:F4}");

        // ---------------- La placa dentro del gabinete ----------------
        // Puertos traseros contra el escudo de E/S y la placa sobre separadores de 6,35 mm. El interior del modelo mide
        // 398 mm de alto: placa (311 con salientes) + fuente (82) solo caben con la placa pegada al techo.
        var boardDelta = new Vector3(
            (inn.top - 0.002f) - pcb.max.x,
            (inn.floor + 0.00635f) - pcb.min.y,
            (inn.rear - 0.002f) - pcb.max.z);
        var boardSocket = MakeSocket("Lugar placa base (bandeja)", caseT, board.position + boardDelta, board.rotation, "Type_Motherboard");
        gpuSocket.requires.Add(boardSocket);
        Log($"Placa instalada: desplazamiento {boardDelta:F4}; borde inferior en x={pcb.min.x + boardDelta.x:F4} (la fuente necesita hasta {inn.bottom + 0.088f:F4})");

        Transform extras = NewChild("Añadidos", caseT);
        extras.rotation = Quaternion.identity;
        BuildExtras(extras, inn, pcb, boardDelta);

        // ---------------- Fuente, disco y ventilador trasero ----------------
        Transform psu = Spawn("Assets/3D/Componentes/powersupply.fbx", "Fuente de alimentación", root);
        psu.localScale = Vector3.one * 0.045f; // el modelo viejo viene a escala ×22: queda en 144 × 82 × 134 mm (cabe bajo la placa)
        RestOnDesk(psu, new Vector2(0.66f, -0.03f), WorldBounds(psu));
        Quaternion psuRot = Quaternion.Euler(0f, 0f, 90f) * psu.rotation; // ventilador hacia la base del gabinete, enchufe hacia atrás
        Bounds psuB = BoundsAt(psu, psuRot);
        var psuTarget = new Vector3(inn.bottom + 0.001f + psuB.extents.x, inn.floor + psuB.extents.y, inn.rear - 0.001f - psuB.extents.z);
        MakeSocket("Lugar fuente (abajo y al fondo)", caseT, psuTarget - psuB.center, psuRot, "Type_PSU");

        Transform hdd = Spawn("Assets/3D/Componentes/hdd.fbx", "Disco duro", root);
        hdd.GetComponent<MeshRenderer>().sharedMaterial = // el modelo ligero trae el material Lit por defecto
            EstiloMateriales.SolidMaterial(ExtrasDir + "Disco_Metal.mat", ColorDir, new Color(0.55f, 0.57f, 0.6f), true);
        RestOnDesk(hdd, new Vector2(-0.46f, -0.25f), WorldBounds(hdd));
        Quaternion hddRot = Quaternion.LookRotation(Vector3.up, Vector3.left) * hdd.rotation; // de canto en la bahía: conectores hacia el fondo (fuente y puertos SATA)
        Bounds hddB = BoundsAt(hdd, hddRot);
        var hddTarget = new Vector3(inn.bottom + 0.002f + hddB.extents.x, inn.floor + hddB.extents.y, inn.front + 0.004f + hddB.extents.z);
        MakeSocket("Lugar disco (bahía de 3,5\")", caseT, hddTarget - hddB.center, hddRot, "Type_HDD");

        Transform fan = Spawn(Models + "Ventilador120/Ventilador120.fbx", "Ventilador 120 mm", root);
        RestOnDesk(fan, new Vector2(0.20f, -0.24f), WorldBounds(fan));
        Quaternion fanRot = Quaternion.Euler(90f, 0f, 0f) * fan.rotation; // contra la pared trasera
        Bounds fanB = BoundsAt(fan, fanRot);
        var fanTarget = new Vector3(inn.top - 0.075f, inn.floor + 0.12f, inn.rear - 0.0005f - fanB.extents.z);
        MakeSocket("Lugar ventilador trasero", caseT, fanTarget - fanB.center, fanRot, "Type_CaseFan");

        // ---------------- Piezas: componentes y fichas ----------------
        MakePart(board, "PlacaBase", "Type_Motherboard", "Placa base ATX (ASUS ROG Strix Z370-E)",
            "Es el circuito principal: conecta el procesador, la memoria, el almacenamiento y las tarjetas de expansión. " +
            "Formato ATX (30,5 × 24,4 cm), socket LGA 1151 para procesadores Intel, cuatro ranuras DDR4 y dos ranuras M.2. " +
            "Conviene instalarle el procesador, el disipador y la memoria fuera del gabinete, donde hay espacio.",
            fitCollider: false);
        var mc = boardMesh.gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = boardMesh.GetComponent<MeshFilter>().sharedMesh;

        MakePart(cpu, "CPU", "Type_CPU", "Procesador (CPU) Intel LGA 1151",
            "Ejecuta las instrucciones de los programas. Se apoya en el socket sin hacer fuerza: el triángulo dorado de una esquina indica " +
            "cómo va orientado. Nunca funciona sin disipador: se calentaría en segundos.");
        MakePart(cooler, "Disipador", "Type_Cooler", "Disipador de torre con ventilador",
            "Extrae el calor del procesador: la base de cobre toca la CPU (con pasta térmica en medio), los heatpipes llevan el calor a las " +
            "aletas y el ventilador lo empuja hacia la parte trasera del gabinete, donde sale.");
        MakePart(ram1, "RAM", "Type_RAM", "Memoria RAM DDR4 (módulo DIMM)",
            "Guarda los datos y programas en uso mientras el equipo está encendido; se borra al apagarlo. " +
            "Con dos módulos se usan las ranuras A2 y B2, para que trabajen en doble canal.");
        MakePart(ram2, "RAM", "Type_RAM", "Memoria RAM DDR4 (módulo DIMM)",
            "Guarda los datos y programas en uso mientras el equipo está encendido; se borra al apagarlo. " +
            "Con dos módulos se usan las ranuras A2 y B2, para que trabajen en doble canal.");
        MakePart(m2, "SSD M.2", "Type_M2", "Unidad SSD M.2 NVMe",
            "Almacenamiento sin partes móviles conectado directamente por PCI Express: varias veces más rápido que un disco duro. " +
            "Se inserta en la ranura M.2 de la placa y se fija con un tornillo; no lleva cables.");
        MakePart(gpu, "GPU", "Type_GPU", "Tarjeta de video (GPU)",
            "Procesa los gráficos y los envía al monitor. Va en la ranura PCIe x16 más cercana al procesador, con su placa metálica " +
            "(el bracket) atornillada a la parte trasera del gabinete. Necesita alimentación extra de la fuente por un conector PCIe.");
        MakePart(psu, "Fuente", "Type_PSU", "Fuente de alimentación ATX",
            "Convierte la corriente alterna del enchufe en las tensiones continuas que usan los componentes (12 V, 5 V y 3,3 V). " +
            "Va abajo y al fondo del gabinete, con su ventilador hacia la rejilla y el enchufe hacia afuera.");
        MakePart(hdd, "HDD", "Type_HDD", "Disco duro de 3,5\" (HDD)",
            "Almacenamiento magnético de gran capacidad y bajo costo, más lento que un SSD. Va en la bahía de discos y necesita dos " +
            "cables: datos SATA (a la placa) y alimentación SATA (a la fuente).");
        MakePart(fan, "Ventilador", "Type_CaseFan", "Ventilador de 120 mm",
            "Mueve el aire dentro del gabinete: los del frente meten aire fresco y el trasero saca el caliente. " +
            "Se conecta a un cabezal de ventilador de 4 pines de la placa base.");

        // ---------------- Escena: mesa, cámara, arrastre, sombras ----------------
        ConfigureScene(root);

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return _log.ToString();
    }

    // =====================================================================

    static void Log(string s) => _log.AppendLine(s);

    static Transform Spawn(string path, string name, Transform parent)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Transform NewChild(string name, Transform parent)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    static ConnectionTypeSO Type(string name) =>
        string.IsNullOrEmpty(name) ? null : AssetDatabase.LoadAssetAtPath<ConnectionTypeSO>(TypesDir + name + ".asset");

    /// <summary>Apoya el objeto en la mesa con el centro de <paramref name="b"/> en (x, z).</summary>
    static void RestOnDesk(Transform t, Vector2 xz, Bounds b)
    {
        t.position += new Vector3(xz.x - b.center.x, DeskY - b.min.y, xz.y - b.center.z);
    }

    static Bounds WorldBounds(Transform t)
    {
        var rs = t.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    /// <summary>Caja de las mallas en el espacio local de <paramref name="t"/>.</summary>
    static Bounds LocalBounds(Transform t)
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
    static Bounds BoundsAt(Transform t, Quaternion rot)
    {
        Vector3 p = t.position;
        Quaternion r = t.rotation;
        t.SetPositionAndRotation(Vector3.zero, rot);
        Bounds b = WorldBounds(t);
        t.SetPositionAndRotation(p, r);
        return b;
    }

    static void SetLayer(Transform t, int layer)
    {
        foreach (var c in t.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = layer;
    }

    static SocketSystem MakeSocket(string name, Transform parent, Vector3 pos, Quaternion rot, string type, bool required = true, string note = null)
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
    static SocketSystem SocketFromChild(Transform board, string child, string name, string type, bool required = true, string note = null)
    {
        Transform c = board.Find(child);
        return MakeSocket(name, board, c.position, c.rotation, type, required, note);
    }

    /// <summary>Separa una pieza de la placa y la deja en la mesa.</summary>
    static Transform Detach(Transform part, Transform root, string name, Vector2 xz, Quaternion extraRot)
    {
        part.name = name;
        part.SetParent(root, true);
        part.rotation = extraRot * part.rotation;
        RestOnDesk(part, xz, WorldBounds(part));
        return part;
    }

    static PCPart MakePart(Transform t, string id, string type, string title, string desc, bool draggable = true, bool fitCollider = true)
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

    /// <summary>Centro de la base (vértices más bajos) en el espacio local de la pieza.</summary>
    static Vector3 BaseCentroidLocal(Transform t)
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

    /// <summary>Punto medio del borde de los contactos PCIe (submalla del material dorado), en local.</summary>
    static Vector3 GpuFingersLocal(Transform gpu)
    {
        var mf = gpu.GetComponentInChildren<MeshFilter>();
        var mr = mf.GetComponent<MeshRenderer>();
        Mesh mesh = mf.sharedMesh;
        int sub = -1;
        for (int i = 0; i < mr.sharedMaterials.Length; i++)
            if (mr.sharedMaterials[i] != null && mr.sharedMaterials[i].name == "Material.012") sub = i;
        if (sub < 0) { Log("⚠ GPU: no encontré la submalla de los contactos (Material.012)"); return Vector3.zero; }
        var verts = mesh.vertices;
        var tris = mesh.GetTriangles(sub);
        Bounds b = new Bounds(verts[tris[0]], Vector3.zero);
        foreach (int i in tris) b.Encapsulate(verts[i]);
        Matrix4x4 m = gpu.worldToLocalMatrix * mf.transform.localToWorldMatrix;
        Vector3 c = m.MultiplyPoint3x4(new Vector3(b.center.x, b.center.y, b.max.z));
        Log($"GPU contactos (local): caja {b.size:F4}, borde {c:F4}");
        return c;
    }

    /// <summary>Busca la ranura PCIe cerca de <paramref name="guess"/>: centro (x, z) y altura de su borde.</summary>
    static Vector3 MeasurePcieSlot(Transform boardMesh, Vector3 guess, out float top)
    {
        var mc = boardMesh.gameObject.AddComponent<MeshCollider>();
        top = float.MinValue;
        float minX = float.MaxValue, maxX = float.MinValue;
        var heights = new List<KeyValuePair<float, float>>();
        for (float x = guess.x - 0.008f; x <= guess.x + 0.008f; x += 0.0004f)
            if (mc.Raycast(new Ray(new Vector3(x, DeskY + 0.3f, guess.z), Vector3.down), out RaycastHit h, 1f))
            {
                heights.Add(new KeyValuePair<float, float>(x, h.point.y));
                if (h.point.y > top) top = h.point.y;
            }
        foreach (var kv in heights)
            if (kv.Value > top - 0.003f) { minX = Mathf.Min(minX, kv.Key); maxX = Mathf.Max(maxX, kv.Key); }
        Object.DestroyImmediate(mc);
        return new Vector3((minX + maxX) * 0.5f, top, guess.z);
    }

    static Interior MeasureInterior(Transform chasis)
    {
        var mc = chasis.gameObject.AddComponent<MeshCollider>();
        bool backfaces = Physics.queriesHitBackfaces;
        Physics.queriesHitBackfaces = true;
        Bounds b = chasis.GetComponent<Renderer>().bounds;
        var p = new Vector3(b.center.x, b.min.y + 0.06f, b.center.z); // por debajo de los ventiladores frontales y del hueco sobrante
        var it = new Interior { outer = b };
        it.floor = Hit(mc, p, Vector3.down, b.min).y;
        it.rear = Hit(mc, p, Vector3.forward, b.max).z;
        it.front = Hit(mc, p, Vector3.back, b.min).z;
        it.top = Hit(mc, p, Vector3.right, b.max).x;
        it.bottom = Hit(mc, p, Vector3.left, b.min).x;
        Physics.queriesHitBackfaces = backfaces;
        Object.DestroyImmediate(mc);
        return it;
    }

    static Vector3 Hit(Collider c, Vector3 from, Vector3 dir, Vector3 fallback)
    {
        return c.Raycast(new Ray(from, dir), out RaycastHit h, 2f) ? h.point : fallback;
    }

    /// <summary>Paredes como cajas (clic y apoyo al arrastrar): más fiables que la malla, de una sola cara.</summary>
    static void BuildCaseColliders(Transform caseT, Interior i)
    {
        Transform group = NewChild("Colisión", caseT);
        group.rotation = Quaternion.identity;
        Bounds o = i.outer;
        Box(group, "Bandeja", new Vector3(i.bottom, o.min.y, i.front), new Vector3(i.top, i.floor, i.rear));
        Box(group, "Pared trasera", new Vector3(o.min.x, o.min.y, i.rear), o.max);
        Box(group, "Frente", o.min, new Vector3(o.max.x, o.max.y, i.front));
        Box(group, "Base", o.min, new Vector3(i.bottom, o.max.y, o.max.z));
        Box(group, "Techo", new Vector3(i.top, o.min.y, o.min.z), o.max);
    }

    static void Box(Transform parent, string name, Vector3 min, Vector3 max)
    {
        Transform t = NewChild(name, parent);
        t.SetPositionAndRotation((min + max) * 0.5f, Quaternion.identity);
        var box = t.gameObject.AddComponent<BoxCollider>();
        box.size = max - min;
    }

    static Transform Solid(Transform parent, string name, Vector3 min, Vector3 max, Material mat, PrimitiveType shape = PrimitiveType.Cube)
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

    static void BuildExtras(Transform extras, Interior i, Bounds pcb, Vector3 boardDelta)
    {
        Material caseMat = EstiloMateriales.SolidMaterial(ExtrasDir + "Gabinete_Liso.mat", ColorDir, new Color(0.01f, 0.01f, 0.015f), false);
        Material metal = EstiloMateriales.SolidMaterial(ExtrasDir + "Metal_Claro.mat", ColorDir, new Color(0.72f, 0.73f, 0.75f), true);
        Material brass = EstiloMateriales.SolidMaterial(ExtrasDir + "Laton.mat", ColorDir, new Color(0.78f, 0.62f, 0.25f), true);
        Bounds o = i.outer;

        // Tapa del hueco trasero que no corresponde a nada (por dentro y por fuera).
        Solid(extras, "Tapa del hueco trasero",
            new Vector3(o.min.x + HuecoSobranteX.x, DeskY + HuecoSobranteY.x, i.rear - 0.0008f),
            new Vector3(o.min.x + HuecoSobranteX.y, DeskY + HuecoSobranteY.y, o.max.z + 0.0008f), caseMat);

        // Escudo de E/S: 158,75 × 44,45 mm, contra la pared trasera, a la altura de los puertos de la placa.
        float ioTop = pcb.max.x + boardDelta.x - 0.010f;
        float pcbBottom = pcb.min.y + boardDelta.y;
        Solid(extras, "Escudo de E/S (I/O shield)",
            new Vector3(ioTop - 0.15875f, pcbBottom - 0.002f, i.rear - 0.0012f),
            new Vector3(ioTop, pcbBottom - 0.002f + 0.04445f, i.rear - 0.0002f), metal);

        // Separadores (standoffs) en los 9 agujeros ATX, medidos sobre la placa en la mesa.
        Transform standoffs = NewChild("Separadores", extras);
        float[] xs = { -0.3568f, -0.2332f, -0.0760f };
        float[] zs = { 0.1450f, -0.0091f, -0.0801f };
        int n = 0;
        foreach (float x in xs)
            foreach (float z in zs)
            {
                var c = new Vector3(x + boardDelta.x, 0f, z + boardDelta.z);
                Solid(standoffs, "Separador " + (++n),
                    new Vector3(c.x - 0.0025f, i.floor, c.z - 0.0025f),
                    new Vector3(c.x + 0.0025f, i.floor + 0.00635f, c.z + 0.0025f), brass, PrimitiveType.Cylinder);
            }

        // Jaula del disco: una chapa que lo sujeta de canto junto a la base del gabinete.
        Solid(extras, "Jaula del disco",
            new Vector3(i.bottom + 0.030f, i.floor, i.front + 0.002f),
            new Vector3(i.bottom + 0.032f, i.floor + 0.11f, i.front + 0.16f), metal);
    }

    static void ConfigureScene(Transform root)
    {
        // Mesa con colisionador: las piezas se apoyan en ella al arrastrar.
        var wood = GameObject.Find("Entorno/Desk/wood");
        if (wood != null && wood.GetComponent<Collider>() == null) wood.AddComponent<BoxCollider>();

        Transform center = NewChild("Centro de trabajo", root);
        center.position = new Vector3(0.0f, 0.88f, 0.0f);

        var cam = Camera.main;
        if (cam != null)
        {
            cam.nearClipPlane = 0.02f;
            cam.farClipPlane = 80f;
            var cad = cam.GetComponent<CADCameraController>();
            if (cad != null)
            {
                cad.targetInicial = center;
                cad.wideShotDistance = 1.3f;
                cad.wideShotPitch = 60f; // el gabinete tumbado tiene paredes de 29 cm: desde más bajo no se ve su interior
                cad.zoomLimits = new Vector2(0.15f, 2.2f);
                cad.panLimitX = new Vector2(-0.9f, 0.9f);
                cad.panLimitY = new Vector2(0.80f, 1.40f);
                cad.panLimitZ = new Vector2(-0.4f, 0.4f);
                cad.verticalAngleLimit = new Vector2(5f, 89f);
                cad.horizontalAngleLimit = new Vector2(-90f, 90f);
                EditorUtility.SetDirty(cad);
            }
            cam.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            cam.transform.position = center.position - cam.transform.forward * 1.3f;
        }

        var im = Object.FindAnyObjectByType<InteractionManager>();
        if (im != null)
        {
            var so = new SerializedObject(im);
            so.FindProperty("liftHeight").floatValue = 0.05f;
            so.FindProperty("hoverClearance").floatValue = 0.008f;
            so.FindProperty("maxRayDistance").floatValue = 10f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var sun = GameObject.Find("Directional Light");
        if (sun != null && sun.GetComponent<ShadowDistanceOverride>() == null) sun.AddComponent<ShadowDistanceOverride>();
    }
}
