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
        var psuSocket = MakeSocket("Lugar fuente (abajo y al fondo)", caseT, psuTarget - psuB.center, psuRot, "Type_PSU");

        Transform hdd = Spawn("Assets/3D/Componentes/hdd.fbx", "Disco duro", root);
        hdd.GetComponent<MeshRenderer>().sharedMaterial = // el modelo ligero trae el material Lit por defecto
            EstiloMateriales.SolidMaterial(ExtrasDir + "Disco_Metal.mat", ColorDir, new Color(0.55f, 0.57f, 0.6f), true);
        RestOnDesk(hdd, new Vector2(-0.46f, -0.25f), WorldBounds(hdd));
        Quaternion hddRot = Quaternion.LookRotation(Vector3.up, Vector3.left) * hdd.rotation; // de canto en la bahía: conectores hacia el fondo (fuente y puertos SATA)
        Bounds hddB = BoundsAt(hdd, hddRot);
        var hddTarget = new Vector3(inn.bottom + 0.002f + hddB.extents.x, inn.floor + hddB.extents.y, inn.front + 0.004f + hddB.extents.z);
        var hddSocket = MakeSocket("Lugar disco (bahía de 3,5\")", caseT, hddTarget - hddB.center, hddRot, "Type_HDD");

        Transform fan = Spawn(Models + "Ventilador120/Ventilador120.fbx", "Ventilador 120 mm", root);
        RestOnDesk(fan, new Vector2(0.20f, -0.24f), WorldBounds(fan));
        Quaternion fanRot = Quaternion.Euler(90f, 0f, 0f) * fan.rotation; // contra la pared trasera
        Bounds fanB = BoundsAt(fan, fanRot);
        var fanTarget = new Vector3(inn.top - 0.075f, inn.floor + 0.12f, inn.rear - 0.0005f - fanB.extents.z);
        var fanSocket = MakeSocket("Lugar ventilador trasero", caseT, fanTarget - fanB.center, fanRot, "Type_CaseFan");

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

        // ---------------- Cables ----------------
        BuildCables(new CableContext
        {
            root = root, caseT = caseT, inn = inn, board = board, boardCollider = mc, pcb = pcb,
            boardSocket = boardSocket, psuSocket = psuSocket, hddSocket = hddSocket, gpuSocket = gpuSocket,
            coolerSocket = coolerSocket, fanSocket = fanSocket,
            psu = psu, hdd = hdd, gpu = gpu, cooler = cooler, fan = fan,
        });

        // ---------------- Escena: mesa, cámara, arrastre, sombras ----------------
        ConfigureScene(root);

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets(); // los materiales de los añadidos y los cables (guardar la escena no los guarda)
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

    // ===================================================================== cables

    class CableContext
    {
        public Transform root, caseT, board, psu, hdd, gpu, cooler, fan;
        public Interior inn;
        public Collider boardCollider;
        public Bounds pcb;
        public SocketSystem boardSocket, psuSocket, hddSocket, gpuSocket, coolerSocket, fanSocket;
        public Transform cables, rest;
        public Material dark, red, blue, cableBlack, cableRed, cableBlue, cableGray;
    }

    // Tamaños de los conectores: ancho (X), largo (Y) y alto (Z, por donde sale el cable), en metros.
    static readonly Vector3 PlugATX24 = new Vector3(0.010f, 0.052f, 0.014f);
    static readonly Vector3 PlugEPS8 = new Vector3(0.010f, 0.021f, 0.014f);
    static readonly Vector3 PlugPCIe = new Vector3(0.010f, 0.021f, 0.014f);
    static readonly Vector3 PlugSataPower = new Vector3(0.006f, 0.022f, 0.010f);
    static readonly Vector3 PlugSataData = new Vector3(0.006f, 0.011f, 0.010f);
    static readonly Vector3 PlugFPanel = new Vector3(0.005f, 0.020f, 0.010f);
    static readonly Vector3 PlugUSB3 = new Vector3(0.010f, 0.023f, 0.010f);
    static readonly Vector3 PlugAudio = new Vector3(0.005f, 0.013f, 0.010f);
    static readonly Vector3 PlugFan = new Vector3(0.005f, 0.011f, 0.009f);

    /// <summary>
    /// Los cables del laboratorio. Cada uno sale de una pieza (fuente, disco, frente del gabinete, ventiladores)
    /// y su conector libre se arrastra a un puerto. Los puertos de la placa se midieron sobre la captura cenital
    /// del modelo (Z370-E: ATX 24 y USB 3.0 en el borde frontal, EPS 8 arriba junto al panel de E/S,
    /// F_PANEL y HD Audio en el borde inferior, CPU_FAN junto a las DIMM).
    /// </summary>
    static void BuildCables(CableContext c)
    {
        c.cables = NewChild("Cables", c.root);
        c.rest = NewChild("Superficie de apoyo (mesa)", c.root);
        c.rest.position = new Vector3(0f, DeskY, 0f);
        c.dark = EstiloMateriales.SolidMaterial(ExtrasDir + "Conector_Negro.mat", ColorDir, new Color(0.12f, 0.12f, 0.13f), false);
        c.red = EstiloMateriales.SolidMaterial(ExtrasDir + "Conector_Rojo.mat", ColorDir, new Color(0.78f, 0.12f, 0.1f), false);
        c.blue = EstiloMateriales.SolidMaterial(ExtrasDir + "Conector_Azul.mat", ColorDir, new Color(0.12f, 0.3f, 0.8f), false);
        c.cableBlack = Resources.Load<Material>("Mat_Cable");
        c.cableRed = EstiloMateriales.SolidMaterial(ExtrasDir + "Cable_Rojo.mat", ColorDir, new Color(0.7f, 0.08f, 0.08f), false);
        c.cableBlue = EstiloMateriales.SolidMaterial(ExtrasDir + "Cable_Azul.mat", ColorDir, new Color(0.1f, 0.22f, 0.65f), false);
        c.cableGray = EstiloMateriales.SolidMaterial(ExtrasDir + "Cable_Gris.mat", ColorDir, new Color(0.45f, 0.45f, 0.48f), false);

        Interior i = c.inn;
        float floorY = i.floor;
        float pcbTop = c.pcb.min.y + 0.0016f;

        // --- Fuente: cuatro salidas en su cara modular (la que mira al frente) ---
        Bounds psuB = WorldBounds(c.psu);
        Transform modular = c.psu.Find("modular_ports");
        Vector3 portsCenter = modular != null ? modular.GetComponent<Renderer>().bounds.center : psuB.center;
        Transform PsuOut(string name, float dx) => Anchor(name, c.psu, new Vector3(portsCenter.x + dx, portsCenter.y, psuB.min.z - 0.001f), Vector3.back);
        Transform outATX = PsuOut("Salida ATX 24", -0.045f);
        Transform outEPS = PsuOut("Salida EPS 8", -0.015f);
        Transform outPCIe = PsuOut("Salida PCIe", 0.015f);
        Transform outSata = PsuOut("Salida SATA", 0.045f);
        // Dónde reposan sus conectores sueltos con la fuente ya instalada: en el suelo, delante de ella.
        Transform PsuHome(string name, float z, Vector3 size) =>
            Home(name, c.psuSocket.transform, new Vector3(i.bottom + 0.059f, floorY + size.x * 0.5f, z), Vector3.forward);

        BoardPort(c, "Puerto ATX 24 pines", "Type_ATX24", -0.1716f, -0.0809f, true, true, PlugATX24, c.boardSocket, c.psuSocket);
        Cable(c, "Cable ATX 24 pines", outATX, "Type_ATX24", PlugATX24, c.dark, c.cableBlack, CableSpline.Profile.Round, 0.007f, 0.40f,
            new Vector3(0.60f, 0f, -0.20f), Vector3.forward, c.psuSocket, PsuHome("Reposo ATX 24", 0.060f, PlugATX24),
            "Conector ATX de 24 pines",
            "Lleva la energía principal de la fuente a la placa base (12 V, 5 V, 3,3 V y la señal de encendido). Es el conector más grande; " +
            "su pestaña tiene que quedar trabada.");

        BoardPort(c, "Puerto EPS 8 pines", "Type_EPS8", -0.0747f, 0.1009f, true, false, PlugEPS8, c.boardSocket, c.psuSocket);
        Cable(c, "Cable EPS 8 pines (CPU)", outEPS, "Type_EPS8", PlugEPS8, c.dark, c.cableBlack, CableSpline.Profile.Round, 0.004f, 0.48f,
            new Vector3(0.655f, 0f, -0.215f), Vector3.forward, c.psuSocket, PsuHome("Reposo EPS 8", 0.035f, PlugEPS8),
            "Conector EPS de 8 pines (CPU)",
            "Alimenta el procesador con 12 V. Va en el borde superior de la placa, cerca del socket. No hay que confundirlo con el " +
            "PCIe de 6+2: se parecen, pero sus pines tienen formas distintas.");

        // PCIe 6+2: en el canto superior de la GPU (el contrario a los contactos), a mitad de su largo.
        Bounds gpuB = WorldBounds(c.gpu);
        var pcie = MakeSocket("Puerto PCIe 6+2 (GPU)", c.gpu,
            new Vector3(gpuB.center.x, gpuB.center.y, gpuB.min.z - PlugPCIe.z * 0.5f + 0.004f),
            Quaternion.LookRotation(Vector3.back, Vector3.right), "Type_PCIePower");
        pcie.requires.Add(c.gpuSocket);
        pcie.requires.Add(c.psuSocket);
        AddPortMarker(pcie, PlugPCIe);
        Cable(c, "Cable PCIe 6+2 (GPU)", outPCIe, "Type_PCIePower", PlugPCIe, c.dark, c.cableBlack, CableSpline.Profile.Round, 0.004f, 0.30f,
            new Vector3(0.705f, 0f, -0.19f), Vector3.forward, c.psuSocket, PsuHome("Reposo PCIe", 0.012f, PlugPCIe),
            "Conector PCIe de 6+2 pines",
            "Da a la tarjeta de video la energía que la ranura PCIe no alcanza a darle (la ranura entrega hasta 75 W). " +
            "Los 2 pines sueltos se suman a los 6 cuando la tarjeta pide un conector de 8.");

        // Disco: los conectores están en el extremo -X del modelo (en la pose de la mesa).
        Bounds hddB = WorldBounds(c.hdd);
        Quaternion hddPortRot = Quaternion.LookRotation(Vector3.left, Vector3.forward);
        var sataPower = MakeSocket("Puerto alimentación SATA (disco)", c.hdd,
            new Vector3(hddB.min.x - PlugSataPower.z * 0.5f + 0.003f, hddB.min.y + 0.007f, hddB.center.z + 0.012f), hddPortRot, "Type_SATAPower");
        sataPower.requires.Add(c.hddSocket);
        sataPower.requires.Add(c.psuSocket);
        AddPortMarker(sataPower, PlugSataPower);
        Cable(c, "Cable de alimentación SATA", outSata, "Type_SATAPower", PlugSataPower, c.dark, c.cableBlack, CableSpline.Profile.Flat, 0.007f, 0.26f,
            new Vector3(0.745f, 0f, -0.15f), Vector3.forward, c.psuSocket, PsuHome("Reposo SATA", -0.012f, PlugSataPower),
            "Conector de alimentación SATA",
            "Alimenta discos duros y SSD de 2,5\" con 12 V y 5 V. Tiene forma de L para que solo entre en una posición.",
            flatHalfThickness: 0.0015f);

        // Datos SATA: sale del disco (ese extremo ya viene enchufado) y su conector libre va a la placa.
        Vector3 dataPos = new Vector3(hddB.min.x - 0.003f, hddB.min.y + 0.007f, hddB.center.z - 0.02f);
        Transform outData = Anchor("Salida datos SATA", c.hdd, dataPos, Vector3.left);
        StaticBlock("Conector datos SATA (lado del disco)", c.hdd, dataPos + Vector3.right * 0.002f, hddPortRot, PlugSataData, c.red);
        BoardPort(c, "Puerto SATA 1", "Type_SATAData", -0.3314f, -0.0796f, false, true, PlugSataData, c.boardSocket, c.hddSocket);
        Cable(c, "Cable de datos SATA", outData, "Type_SATAData", PlugSataData, c.red, c.cableRed, CableSpline.Profile.Flat, 0.0045f, 0.26f,
            new Vector3(-0.548f, 0f, -0.312f), Vector3.right, c.hddSocket,
            Home("Reposo datos SATA", c.hddSocket.transform, new Vector3(i.bottom + 0.059f, floorY + PlugSataData.x * 0.5f, -0.040f), Vector3.left),
            "Cable de datos SATA",
            "Transmite los datos entre el disco y la placa base (hasta 6 Gb/s). Es plano y delgado, con un conector en L más pequeño " +
            "que el de alimentación.",
            flatHalfThickness: 0.001f);

        // --- Frente del gabinete: panel frontal, USB 3.0 y audio (salen de la pared frontal, abajo) ---
        Transform FrontOut(string name, float x) => Anchor(name, c.caseT, new Vector3(x, floorY + 0.012f, i.front + 0.008f), Vector3.forward);
        Transform CaseRest(string name, float x, Vector3 size) =>
            Home(name, c.caseT, new Vector3(x, floorY + size.x * 0.5f, i.front + 0.075f), Vector3.back);

        BoardPort(c, "Puerto F_PANEL", "Type_FPanel", -0.3696f, -0.0744f, true, false, PlugFPanel, c.boardSocket);
        CaseCable(c, "Cables del panel frontal", FrontOut("Salida panel frontal", 0.205f), "Type_FPanel", PlugFPanel, c.dark, c.cableGray, 0.0025f, 0.20f,
            CaseRest("Reposo F_PANEL", 0.205f, PlugFPanel),
            "Cables del panel frontal (F_PANEL)",
            "Conectan el botón de encendido, el de reinicio y las luces del frente del gabinete al cabezal F_PANEL. " +
            "El manual de la placa indica qué pin va en cada lugar.");

        BoardPort(c, "Puerto USB 3.0 frontal", "Type_USB3Header", -0.2757f, -0.0826f, false, true, PlugUSB3, c.boardSocket);
        CaseCable(c, "Cable USB 3.0 frontal", FrontOut("Salida USB 3.0", 0.26f), "Type_USB3Header", PlugUSB3, c.blue, c.cableBlue, 0.004f, 0.20f,
            CaseRest("Reposo USB 3.0", 0.26f, PlugUSB3),
            "Cable USB 3.0 frontal",
            "Lleva los puertos USB del frente del gabinete a la placa base. Su cabezal de 19 pines tiene una muesca para que solo " +
            "entre en una posición.");

        BoardPort(c, "Puerto HD Audio", "Type_HDAudio", -0.3700f, 0.1060f, true, false, PlugAudio, c.boardSocket);
        CaseCable(c, "Cable de audio frontal", FrontOut("Salida audio", 0.155f), "Type_HDAudio", PlugAudio, c.dark, c.cableBlack, 0.0025f, 0.38f,
            CaseRest("Reposo audio", 0.155f, PlugAudio),
            "Cable de audio frontal (HD Audio)",
            "Lleva las tomas de auriculares y micrófono del frente del gabinete al cabezal HD_AUDIO de la placa.");

        // --- Ventiladores ---
        BoardPort(c, "Puerto CHA_FAN1", "Type_FanHeader", -0.1013f, -0.0841f, true, true, PlugFan, c.boardSocket);
        Transform frontFan = Anchor("Salida ventiladores frontales", c.caseT, new Vector3(0.31f, 0.965f, i.front + 0.018f), Vector3.down);
        CaseCable(c, "Cable de los ventiladores frontales", frontFan, "Type_FanHeader", PlugFan, c.dark, c.cableBlack, 0.002f, 0.26f,
            CaseRest("Reposo ventiladores frontales", 0.40f, PlugFan),
            "Cable de los ventiladores frontales",
            "Los dos ventiladores del frente se unen con un divisor (cable en Y) y van a un cabezal CHA_FAN de la placa, que regula su " +
            "velocidad según la temperatura.");

        BoardPort(c, "Puerto CHA_FAN2", "Type_FanHeader", -0.3690f, 0.0203f, true, false, PlugFan, c.boardSocket, c.fanSocket);
        Bounds fanB = WorldBounds(c.fan);
        Transform rearOut = Anchor("Salida ventilador trasero", c.fan, new Vector3(fanB.min.x + 0.008f, fanB.center.y, fanB.min.z + 0.008f), Vector3.left);
        // Instalado, su conector cuelga justo debajo de la salida: con la cuerda casi vertical, la holgura no se
        // comba hacia los lados (ni atraviesa la pared trasera).
        Vector3 rearOutInstalled = c.fanSocket.transform.TransformPoint(Vector3.Scale(c.fan.InverseTransformPoint(rearOut.position), c.fan.localScale));
        Cable(c, "Cable del ventilador trasero", rearOut, "Type_FanHeader", PlugFan, c.dark, c.cableBlack, CableSpline.Profile.Round, 0.002f, 0.40f,
            new Vector3(0.285f, 0f, -0.28f), Vector3.left, c.fanSocket,
            Home("Reposo ventilador trasero", c.fanSocket.transform, rearOutInstalled + new Vector3(0f, -0.05f, -0.015f), Vector3.back),
            "Cable del ventilador trasero",
            "Va a un cabezal CHA_FAN de la placa base, que ajusta su velocidad según la temperatura.");

        BoardPort(c, "Puerto CPU_FAN", "Type_CPUFan", -0.0846f, -0.0176f, true, false, PlugFan, c.coolerSocket);
        Bounds coolB = WorldBounds(c.cooler);
        Transform coolOut = Anchor("Salida ventilador del disipador", c.cooler,
            new Vector3(coolB.max.x - 0.008f, coolB.min.y + 0.04f, coolB.center.z - 0.05f), Vector3.down);
        Cable(c, "Cable del ventilador del disipador", coolOut, "Type_CPUFan", PlugFan, c.dark, c.cableBlack, CableSpline.Profile.Round, 0.0018f, 0.18f,
            new Vector3(-0.545f, 0f, -0.07f), Vector3.left, c.coolerSocket,
            Home("Reposo ventilador del disipador", c.coolerSocket.transform, new Vector3(-0.13f, pcbTop + 0.012f, -0.071f), Vector3.back),
            "Cable del ventilador del disipador",
            "El ventilador del procesador va siempre al cabezal CPU_FAN: la placa vigila que gire y, si se detiene, avisa o apaga el " +
            "equipo para proteger el procesador.");

        // Al añadir CableSpline aún no tenía anclas: su malla se construye ahora (en Play se rehace sola al activarse).
        foreach (var spline in c.cables.GetComponentsInChildren<CableSpline>()) spline.Rebuild();
        Log($"Cables: 11 · superficie del PCB a {pcbTop:F4}");
    }

    static Transform Anchor(string name, Transform parent, Vector3 pos, Vector3 dir)
    {
        Transform t = NewChild(name, parent);
        t.SetPositionAndRotation(pos, Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > 0.9f ? Vector3.forward : Vector3.up));
        return t;
    }

    /// <summary>Pose de un conector suelto acostado: su Z+ (por donde sale el cable) mira hacia <paramref name="dir"/>.</summary>
    static Quaternion Lying(Vector3 dir) => Quaternion.LookRotation(dir, Vector3.Cross(dir, Vector3.up));

    static Transform Home(string name, Transform parent, Vector3 pos, Vector3 dir)
    {
        Transform t = NewChild(name, parent);
        t.SetPositionAndRotation(pos, Lying(dir));
        return t;
    }

    static void StaticBlock(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(pos, rot);
        go.transform.localScale = Vector3.Scale(size, Inverse(parent.lossyScale));
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Vector3 Inverse(Vector3 v) => new Vector3(1f / v.x, 1f / v.y, 1f / v.z);

    /// <summary>
    /// Puerto en la placa (medido en la mesa, coordenadas de la captura). Vertical: el conector entra desde arriba
    /// y el cable sale hacia arriba. En ángulo (SATA, USB 3.0): entra de frente por el borde y el cable sale hacia -Z.
    /// </summary>
    static SocketSystem BoardPort(CableContext c, string name, string type, float x, float z, bool vertical, bool longAlongX,
                                  Vector3 size, params SocketSystem[] requires)
    {
        Vector3 longAxis = longAlongX ? Vector3.right : Vector3.forward;
        Quaternion rot;
        Vector3 pos;
        if (vertical)
        {
            float top = TopNear(c.boardCollider, x, z, 0.004f, c.pcb.min.y + 0.0016f);
            rot = Quaternion.LookRotation(Vector3.up, longAxis);
            pos = new Vector3(x, top + size.z * 0.5f - 0.003f, z);
        }
        else
        {
            rot = Quaternion.LookRotation(Vector3.back, longAxis);
            pos = new Vector3(x, c.pcb.min.y + 0.0016f + 0.0075f, c.pcb.min.z - size.z * 0.5f + 0.003f);
        }
        var s = MakeSocket(name, c.board, pos, rot, type);
        foreach (var r in requires) s.requires.Add(r);
        AddPortMarker(s, size);
        return s;
    }

    /// <summary>Señal luminosa del puerto (los cabezales son pequeños y oscuros: el usuario pidió que se noten más).</summary>
    static void AddPortMarker(SocketSystem socket, Vector3 plugSize)
    {
        socket.gameObject.AddComponent<CablePortMarker>().size = plugSize;
    }

    static float TopNear(Collider col, float x, float z, float half, float fallback)
    {
        float top = float.MinValue;
        for (int ix = -1; ix <= 1; ix++)
            for (int iz = -1; iz <= 1; iz++)
                if (col.Raycast(new Ray(new Vector3(x + ix * half, DeskY + 0.5f, z + iz * half), Vector3.down), out RaycastHit h, 1f))
                    top = Mathf.Max(top, h.point.y);
        return top == float.MinValue ? fallback : top;
    }

    static Transform MakePlug(string name, Transform parent, Vector3 pos, Quaternion rot, Vector3 size, Material mat,
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

    /// <summary>Cable cuyo origen es una pieza que también se instala: su conector reposa en la mesa y luego dentro del gabinete.</summary>
    static void Cable(CableContext c, string name, Transform start, string type, Vector3 plugSize, Material plugMat, Material cableMat,
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
    }

    static void SetLooseSlack(CablePlug plug, Vector3 direction)
    {
        var so = new SerializedObject(plug);
        so.FindProperty("looseSlack").vector3Value = direction.normalized;
        so.ApplyModifiedPropertiesWithoutUndo();
        plug.cable.slackDirection = direction.normalized;
    }

    /// <summary>Cable que sale del gabinete (fijo): su conector reposa dentro, en el suelo del frente.</summary>
    static void CaseCable(CableContext c, string name, Transform start, string type, Vector3 plugSize, Material plugMat, Material cableMat,
                          float radius, float length, Transform home, string title, string desc)
    {
        Transform plug = MakePlug("Conector: " + title, c.caseT, home.position, home.rotation, plugSize, plugMat, type, title, desc);
        Object.DestroyImmediate(home.gameObject);
        var spline = Spline(c, name, start, plug, cableMat, CableSpline.Profile.Round, radius, 0.001f, length, title, desc);
        var cp = plug.gameObject.AddComponent<CablePlug>();
        cp.cable = spline;
        // La caída se aplica perpendicular a la cuerda: "abajo" a secas sacaría el sobrante por la pared frontal.
        SetLooseSlack(cp, new Vector3(0f, -0.4f, 1f));
    }

    static CableSpline Spline(CableContext c, string name, Transform start, Transform plug, Material mat, CableSpline.Profile profile,
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
        spline.radialSegments = 8;
        spline.samplesPerUnit = 160f;
        spline.stiffness = 0.025f;
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
