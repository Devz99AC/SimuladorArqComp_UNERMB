using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static LabBuilderKit;

/// <summary>
/// Construye el contenido del laboratorio 3 (escena Lab_Laptop): el mantenimiento interno de una laptop Dell Latitude 5400,
/// con el orden y las piezas del Owner's Manual de Dell (P98G) y las medidas de su Setup and Specifications. Es la fuente de
/// verdad de las posiciones: volver a ejecutarlo rehace "PC (Lab Laptop)" desde cero.
///
/// Escala real (1 unidad = 1 m). La laptop va cerrada y boca abajo sobre una alfombrilla antiestática, con la bisagra al
/// fondo (+Z) como en las figuras del manual. Coordenadas locales: origen en la esquina delantera izquierda, sobre la
/// alfombrilla; X a la derecha (323,05 mm), Z hacia la bisagra (216 mm), Y hacia arriba (20,2 mm con la tapa).
/// La carcasa es el modelo de Yolala3D (Laptop.fbx, cortado en carcasa + tapa inferior); el interior se hace con
/// primitivas. La SO-DIMM y el ventilador son modelos de Sketchfab; los SSD, los del lab ATX.
///
/// El trabajo (mantenimiento, decisión del usuario): llega cerrada con todo puesto. Hay que aflojar los 8 tornillos
/// cautivos y quitar la tapa, desconectar la batería, poner el 2.º módulo de memoria, cambiar el SSD (falla) por el nuevo,
/// reasentar la tarjeta Wi-Fi con sus antenas, conectar la cinta del touchpad (llegó suelta) con su pestillo ZIF, volver a
/// conectar la batería y cerrar. Las posiciones internas son aproximadas, tomadas de las figuras del manual.
/// </summary>
public static class LabLaptopBuilder
{
    const string Dir = "Assets/Project/Content/Models/Lab_Laptop/";
    const string AtxModels = "Assets/Project/Content/Models/Lab_ATX/";
    const string MatDir = Dir + "Materiales/";
    const string ColorDir = Dir + "Colores";
    const string RootName = "PC (Lab Laptop)";

    // ---- Laptop (Dell Setup and Specifications: 323,05 × 216 mm; 19,6 mm delante y 20,85 mm detrás)
    const float W = 0.32305f, D = 0.216f;
    const float MatT = 0.002f;                         // alfombrilla antiestática
    static readonly Vector2 LaptopCenter = new Vector2(0f, 0.02f); // en la mesa (x, z)
    const float FloorY = 0.0089f;                      // bandeja interior (cara de abajo del teclado y del reposamanos)
    const float BoardBottom = 0.0092f, BoardTop = 0.0102f;
    const float CoverTop = 0.0202f;                    // cara de afuera de la tapa inferior
    const float CoverDepth = 0.0079f;                  // de su cara de afuera al borde más bajo de su faldón
    const float BatTop = FloorY + 0.00705f;            // batería de 3 celdas: 181 × 95,9 × 7,05 mm

    // ---- Interior (aproximado, de las figuras del manual: boca abajo y con la bisagra al fondo)
    static readonly Vector4 Battery = new Vector4(0.128f, 0.005f, 0.309f, 0.101f);   // x0, z0, x1, z1
    static readonly float[] RamX = { 0.097f, 0.175f };                                 // centro de las ranuras A y B
    const float RamConnZ = 0.140f, RamY = BoardTop + 0.0035f;                           // borde de los contactos y altura del módulo
    const float SsdX = 0.106f, SsdConnZ = 0.100f, SsdBottom = 0.0110f;                 // M.2 2280, sobre su placa térmica
    const float ShieldY = SsdBottom + 0.0030f, ShieldTop = ShieldY + 0.0004f;          // el SSD que trae mide 2,8 mm con sus chips
    const float WifiX = 0.045f, WifiConnZ = 0.122f, WifiY = BoardTop + 0.0010f;        // M.2 2230 (22 × 30 mm)
    const float MainX = 0.041f, AuxX = 0.049f, TermZ = 0.096f;                         // terminales de las antenas
    const float BracketY = WifiY + 0.0008f + 0.0013f, BracketTop = BracketY + 0.0003f;
    static readonly Vector2 BatteryConn = new Vector2(0.226f, 0.111f);
    static readonly Vector2 Zif = new Vector2(0.263f, 0.112f);

    // ---- Conectores: ancho (X), alto (Y), largo (Z, por donde sale el cable)
    static readonly Vector3 PlugBattery = new Vector3(0.009f, 0.003f, 0.005f);
    static readonly Vector3 PlugAntenna = new Vector3(0.0022f, 0.0012f, 0.0022f);
    static readonly Vector3 PlugFfc = new Vector3(0.012f, 0.0003f, 0.004f);
    static readonly Vector3 HoleMarker = new Vector3(0.0045f, 0.0015f, 0.0045f);

    static Vector3 _o;
    static Material _cover, _vent, _rubber, _screwDark, _silver, _tray, _antistatic, _magnesium, _pcb, _pcbGreen, _black,
                    _white, _gold, _copper, _alu, _batteryMat, _label, _labelWhite, _cableWhite, _cableBlack, _ffc, _ffcBlue,
                    _zif, _thermal, _shield, _brass, _dark, _marker;

    static Vector3 P(float x, float y, float z) => _o + new Vector3(x, y, z);

    static Transform S(Transform parent, string name, float x0, float y0, float z0, float x1, float y1, float z1, Material m) =>
        Solid(parent, name, P(x0, y0, z0), P(x1, y1, z1), m);

    static Quaternion Back => Quaternion.LookRotation(Vector3.back, Vector3.up);

    [MenuItem("Tools/Simulador/Reconstruir laboratorio Laptop (Dell Latitude 5400)")]
    static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        BeginLog();
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        Transform root = new GameObject(RootName).transform;
        _o = new Vector3(LaptopCenter.x - W * 0.5f, DeskY + MatT, LaptopCenter.y - D * 0.5f);
        MakeMaterials();
        EnsureTypes();

        BuildWorkbench(root, out Transform[] trayRest);

        // ---------------- Carcasa (fija) y tapa inferior (empieza puesta) ----------------
        Transform laptop = Spawn(Dir + "Laptop/Laptop.fbx", "Laptop Dell Latitude 5400", root);
        laptop.position = P(W * 0.5f, 0f, D * 0.5f);
        Transform shellMesh = laptop.Find("Carcasa");
        Transform coverMesh = laptop.Find("TapaInferior");
        foreach (var m in shellMesh.GetComponent<MeshRenderer>().sharedMaterials) DoubleSided(m);
        BuildShellColliders(laptop);
        MakePart(laptop, "Laptop", null, "Dell Latitude 5400",
            "Laptop de empresa de 14 pulgadas (2019): 323,05 × 216 mm, de 19,6 mm de alto delante a 20,85 mm detrás, y 1,48 kg " +
            "(Setup and Specifications de Dell). Está cerrada y boca abajo, sobre una alfombrilla antiestática: así se trabaja " +
            "en ella. Antes de abrirla hay que guardar y cerrar todo, apagarla (no suspenderla), desconectar el cargador y todos " +
            "los cables y quitar la tarjeta microSD. En este servicio, el touchpad no responde, la Wi-Fi se corta y el SSD da " +
            "errores: hay que cambiarlo por el nuevo y, de paso, ampliar la memoria.",
            draggable: false, fitCollider: false);

        Transform cover = PartRoot("Tapa inferior", root, P(W * 0.5f, CoverTop, D * 0.5f));
        coverMesh.SetParent(cover, true);
        coverMesh.name = "Tapa (carcasa)";
        coverMesh.GetComponent<MeshRenderer>().sharedMaterial = _cover;
        BuildCoverDetails(cover);
        var coverSocket = MakeSocket("Lugar tapa inferior", laptop, cover.position, cover.rotation, "Type_LaptopCover");
        coverSocket.fillAction = "poner la tapa inferior";
        coverSocket.emptyAction = "quitar la tapa inferior";
        coverSocket.cycleRequired = true;
        coverSocket.cycleAction = "volver a poner la tapa inferior";
        coverSocket.snapRadius = 0.05f;
        coverSocket.onlyOwnType = true; // cubre todo el equipo: no explica «aquí va la tapa» a piezas pequeñas
        Transform coverRest = NewChild("Reposo de la tapa (alfombrilla)", root);
        coverRest.SetPositionAndRotation(P(W * 0.5f, CoverDepth + 0.0005f, D + 0.124f), Quaternion.identity);
        var coverPart = MakePart(cover, "Tapa inferior", "Type_LaptopCover", "Tapa inferior (base cover)",
            "Cubre todo el interior. Va sujeta con 8 tornillos cautivos y con pestañas a presión por los bordes. Manual de Dell: " +
            "se aflojan los 8 tornillos, se hace palanca con una púa de plástico desde la esquina superior izquierda y se sigue " +
            "por los lados, y se levanta. Para cerrarla: se alinea, se presionan los bordes hasta que encaja y se aprietan los " +
            "8 tornillos. Con herramientas de metal se raya la carcasa o se daña algo de dentro.");
        coverPart.explainWrongPlace = false; // tapa casi todo el equipo: soltarla cerca no es un error de lugar
        coverPart.startInstalledIn = coverSocket;
        coverPart.restPoint = coverRest;
        BuildCaptiveScrews(cover, coverSocket);

        // ---------------- Interior fijo: bandeja, placa, disipador, ventilador ----------------
        Transform inside = NewChild("Interior", root);
        Transform board = BuildBoard(inside);
        BuildCooling(inside);

        // ---------------- Batería (fija) y su cable ----------------
        Transform battery = BuildBattery(inside, out Transform batteryOut);
        MakePart(battery, "Batería", null, "Batería de 3 celdas (42 Wh)",
            "Iones de litio, 3 celdas y 42 Wh: 181 × 95,9 × 7,05 mm y 200 g (también hay de 51 Wh y de 68 Wh, de 4 celdas). Va " +
            "sujeta con un tornillo cautivo. Lo primero al abrir el equipo es desconectar su cable de la placa: mientras está " +
            "conectada, la placa tiene corriente aunque el equipo esté apagado. No se aprieta, no se dobla, no se perfora y no " +
            "se hace palanca sobre ella; si está hinchada no se usa. Para cambiarla, el manual pide aflojar su tornillo y " +
            "levantarla; en este servicio basta con desconectarla.",
            draggable: false);

        Transform boardRest = Plane("Superficie de apoyo (placa)", root, BoardTop);
        var rig = new CableRig
        {
            cables = NewChild("Cables", root),
            rest = boardRest,
            fixedParent = board,
            radialSegments = 8,
            samplesPerUnit = 900f, // cables de pocos centímetros: hacen falta muchas muestras por metro
            stiffness = 0.003f,
        };

        SocketSystem batteryPort = Port("Puerto de la batería (placa)", board, P(BatteryConn.x, BoardTop + 0.0025f, BatteryConn.y), Back,
            "Type_BateriaLaptop", PlugBattery);
        batteryPort.requiresEmpty.Add(coverSocket);
        batteryPort.fillAction = "volver a conectar la batería";
        batteryPort.emptyAction = "desconectar la batería (arrastra su conector fuera de la placa)";
        batteryPort.cycleRequired = true;
        batteryPort.cycleAction = "volver a conectar la batería";
        batteryPort.snapRadius = 0.010f;
        batteryPort.blockReason = "Con la batería conectada, la placa sigue teniendo corriente aunque el equipo esté apagado: un " +
                                  "tornillo o una pieza que toque sus contactos puede provocar un cortocircuito y dañarla.";
        S(board, "Conector de la batería (placa)", BatteryConn.x - 0.0055f, BoardTop, BatteryConn.y - 0.0030f,
          BatteryConn.x + 0.0055f, BoardTop + 0.0010f, BatteryConn.y + 0.0030f, _white);
        // Se apoya en la placa: su conector está más bajo que la batería. Corto, pero con alcance para sostenerlo a 2 cm.
        rig.rest = boardRest;
        PluggedCable(rig, "Cable de la batería", batteryOut, "Type_BateriaLaptop", PlugBattery, _white, _cableBlack,
            CableSpline.Profile.Round, 0.0012f, 0.0008f, 0.026f, batteryPort,
            RestPoint("Reposo del cable de la batería", root, P(BatteryConn.x, BatTop + PlugBattery.y * 0.5f, 0.093f), Back),
            "Cable de la batería",
            "Une la batería con la placa base. Al abrir el equipo, es lo primero que se desconecta (tirando del conector, nunca " +
            "del cable) y lo último que se vuelve a conectar, ya con todo lo demás en su sitio. Manual de Dell: además, para " +
            "descargar la electricidad residual de la placa, se mantiene pulsado el botón de encendido unos 20 segundos con el " +
            "equipo desconectado.");

        // ---------------- Memoria: ranura A (con el módulo que trae) y ranura B (vacía) ----------------
        SocketSystem[] lockedInside = { coverSocket, batteryPort };
        var ram = BuildMemory(root, board, lockedInside);

        // ---------------- SSD: cambiar el que trae por el nuevo ----------------
        var ssd = BuildSsd(root, board, lockedInside, trayRest);

        // ---------------- Tarjeta Wi-Fi con sus antenas ----------------
        var wifi = BuildWifi(root, board, lockedInside, rig, trayRest[2]);

        // ---------------- Cinta del touchpad con su pestillo ZIF ----------------
        var touch = BuildTouchpadRibbon(root, board, lockedInside, rig);

        RebuildCables(rig);

        // ---------------- Reglas finales ----------------
        // Volver a conectar la batería exige todo lo de dentro terminado; la tapa, la batería conectada (y así, todo).
        batteryPort.requires.AddRange(new[] { ram[0], ram[1] });
        batteryPort.requires.AddRange(ssd);
        batteryPort.requires.AddRange(wifi);
        batteryPort.requires.AddRange(touch);
        coverSocket.requires.Add(batteryPort);

        // ---------------- Escena ----------------
        ConfigureScene(root, new SceneFrame
        {
            center = new Vector3(LaptopCenter.x + 0.03f, DeskY + 0.015f, LaptopCenter.y + 0.03f),
            wideShotDistance = 0.65f,
            wideShotPitch = 55f,
            zoomLimits = new Vector2(0.05f, 1.4f),
            panLimitX = new Vector2(-0.40f, 0.45f),
            panLimitY = new Vector2(DeskY - 0.01f, DeskY + 0.45f),
            panLimitZ = new Vector2(-0.30f, 0.40f),
            verticalAngleLimit = new Vector2(5f, 89f),
            horizontalAngleLimit = new Vector2(-90f, 90f),
            liftHeight = 0.02f,      // a esta escala, 5 cm de elevación es mucho
            hoverClearance = 0.003f,
        });
        SetHudInstructions(
            "Servicio: el touchpad no responde, la Wi-Fi se corta y el SSD da errores. Hay que cambiar el SSD por el nuevo y ampliar la memoria a doble canal.",
            "Tornillos de la tapa (cautivos): clic sobre cada uno para aflojarlo o apretarlo. Los sueltos (M2x3) se arrastran a la bandeja.",
            "Lo primero al abrir: desconectar la batería. Lo último antes de cerrar: volver a conectarla.",
            "Clic en el pestillo de la cinta para levantarlo o bajarlo.");
        SetHudTexts("PASOS", "¡MANTENIMIENTO COMPLETADO!",
            "La laptop quedó cerrada y lista: memoria en doble canal, SSD nuevo, Wi-Fi y touchpad reconectados, y la batería " +
            "conectada al final, con todo en su sitio.");

        int required = 0;
        foreach (var s in root.GetComponentsInChildren<SocketSystem>()) if (s.required) required++;
        Log($"Lugares obligatorios: {required}");

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        return EndLog();
    }

    // ===================================================================== materiales y tipos

    static Material M(string name, Color c, bool metal) => EstiloMateriales.SolidMaterial(MatDir + name + ".mat", ColorDir, c, metal);

    static void MakeMaterials()
    {
        _cover = M("Tapa_Inferior", new Color(0.16f, 0.16f, 0.17f), false);
        _vent = M("Rejilla", new Color(0f, 0f, 0f), false);
        _rubber = M("Goma", new Color(0.03f, 0.03f, 0.03f), false);
        _screwDark = M("Tornillo_Oscuro", new Color(0.22f, 0.22f, 0.24f), true);
        _silver = M("Plateado", new Color(0.78f, 0.79f, 0.81f), true);
        _tray = M("Bandeja", new Color(0.20f, 0.21f, 0.23f), true);
        _antistatic = M("Alfombrilla_Antiestatica", new Color(0.22f, 0.36f, 0.52f), false);
        _magnesium = M("Chasis_Interior", new Color(0.30f, 0.31f, 0.33f), true);
        _pcb = M("PCB_Azul", new Color(0.12f, 0.36f, 0.62f), false);
        _pcbGreen = M("PCB_Verde", new Color(0.07f, 0.30f, 0.17f), false);
        _black = M("Plastico_Negro", new Color(0.06f, 0.06f, 0.07f), false);
        _white = M("Plastico_Blanco", new Color(0.90f, 0.89f, 0.84f), false);
        _gold = M("Dorado", new Color(0.82f, 0.66f, 0.30f), true);
        _copper = M("Cobre", new Color(0.78f, 0.46f, 0.26f), true);
        _alu = M("Aluminio", new Color(0.80f, 0.81f, 0.83f), true);
        _batteryMat = M("Bateria", new Color(0.09f, 0.09f, 0.10f), false);
        _label = M("Etiqueta", new Color(0.60f, 0.61f, 0.60f), false);
        _labelWhite = M("Etiqueta_Blanca", new Color(0.80f, 0.80f, 0.78f), false);
        _cableWhite = M("Cable_Blanco", new Color(0.92f, 0.92f, 0.92f), false);
        _ffc = M("Cinta_FFC", new Color(0.88f, 0.86f, 0.78f), false);
        _ffcBlue = M("Cinta_Refuerzo", new Color(0.15f, 0.40f, 0.85f), false);
        _zif = M("Pestillo_ZIF", new Color(0.30f, 0.20f, 0.12f), false);
        _thermal = M("Placa_Termica", new Color(0.34f, 0.35f, 0.37f), true);
        _shield = M("Soporte_SSD", new Color(0.10f, 0.10f, 0.11f), true);
        _brass = M("Laton", new Color(0.78f, 0.62f, 0.25f), true);
        _dark = M("Conector_Negro", new Color(0.12f, 0.12f, 0.13f), false);
        _marker = M("Marca_Negra", new Color(0f, 0f, 0f), false);
        _cableBlack = Resources.Load<Material>("Mat_Cable");
    }

    /// <summary>Material de la carcasa de doble cara (el modelo trae paredes de una sola cara y normales mezcladas).</summary>
    static void DoubleSided(Material m)
    {
        if (m == null || !m.HasProperty("_Cull")) return;
        m.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(m);
    }

    /// <summary>Tipos de conexión propios de este lab (los crea si faltan y fija sus textos: este constructor es su fuente).</summary>
    static void EnsureTypes()
    {
        EnsureType("Type_LaptopCover", "Tapa inferior (laptop)", "la tapa inferior",
            "Se pone al final, con la batería ya conectada: se alinea, se presiona por los bordes hasta que encaja y se aprietan sus 8 tornillos.");
        EnsureType("Type_TornilloCautivo", "Tornillo cautivo", "el tornillo cautivo", "");
        EnsureType("Type_TornilloM2x3", "Tornillo M2x3", "un tornillo M2x3",
            "Va en un agujero del soporte del SSD o del soporte de las antenas de la Wi-Fi.");
        EnsureType("Type_BateriaLaptop", "Cable de la batería", "el cable de la batería",
            "Va a su conector en la placa, delante del ventilador y junto a la memoria.");
        EnsureType("Type_SODIMM", "Memoria SO-DIMM", "la memoria SO-DIMM",
            "Va en una de las dos ranuras de memoria del centro de la placa: entra en ángulo y se baja hasta que las pestañas hacen clic.");
        EnsureType("Type_SSDLaptop", "SSD M.2 2280 (laptop)", "el SSD M.2",
            "Va en la ranura M.2 de la izquierda, delante de la memoria, sobre su placa térmica y bajo su soporte negro.");
        EnsureType("Type_SoporteSSD", "Soporte del SSD", "el soporte del SSD",
            "Es la chapa negra que cubre el SSD: va encima de él y se sujeta con 2 tornillos M2x3.");
        EnsureType("Type_WiFiM2", "Tarjeta Wi-Fi M.2 2230", "la tarjeta Wi-Fi",
            "Va en la ranura M.2 pequeña de la izquierda, junto a los puertos: entra en ángulo y se baja.");
        EnsureType("Type_SoporteWiFi", "Soporte de las antenas", "el soporte de las antenas",
            "Va sobre los conectores de las antenas, en el extremo de la tarjeta Wi-Fi, y lo sujeta un tornillo M2x3.");
        EnsureType("Type_AntenaMain", "Antena principal (MAIN)", "la antena principal (cable blanco)",
            "Va al terminal MAIN de la tarjeta, el del triángulo blanco. Al revés, la conexión puede ser más débil o inestable.");
        EnsureType("Type_AntenaAux", "Antena auxiliar (AUX)", "la antena auxiliar (cable negro)",
            "Va al terminal AUX de la tarjeta, el del triángulo negro. Al revés, la conexión puede ser más débil o inestable.");
        EnsureType("Type_CintaTouchpad", "Cinta del touchpad (FFC)", "la cinta del touchpad",
            "Va al conector con pestillo de la placa, a la derecha del cable de la batería, con el pestillo levantado.");
        EnsureType("Type_PestilloZIF", "Pestillo ZIF", "el pestillo de la cinta", "");
    }

    // ===================================================================== ayudantes

    static Transform Plane(string name, Transform root, float y)
    {
        Transform t = NewChild(name, root);
        t.position = new Vector3(0f, _o.y + y, 0f);
        return t;
    }

    /// <summary>Agranda el colisionador de una pieza diminuta (tornillo, pestillo) para que se pueda señalar.</summary>
    static void Grow(Component part, Vector3 extra)
    {
        var box = part.GetComponent<BoxCollider>();
        if (box != null) box.size += Vector3.Scale(extra, Inverse(part.transform.lossyScale));
    }

    /// <summary>Lugar fijo que exige la tapa quitada y la batería desconectada antes de tocarlo (todo lo de dentro).</summary>
    static SocketSystem Inside(SocketSystem s, SocketSystem[] locked)
    {
        s.requiresEmpty.AddRange(locked);
        return s;
    }

    /// <summary>Pieza que empieza instalada en <paramref name="socket"/> y, al retirarla, reposa en <paramref name="rest"/>.</summary>
    static void StartsIn(PCPart part, SocketSystem socket, Transform rest)
    {
        part.startInstalledIn = socket;
        part.restPoint = rest;
    }

    static Transform RestPoint(string name, Transform root, Vector3 pos, Quaternion rot)
    {
        Transform t = NewChild(name, root);
        t.SetPositionAndRotation(pos, rot);
        return t;
    }

    /// <summary>Tornillo M2x3 suelto (no cautivo): cabeza de 3,8 mm y caña de 3 mm. Pivote: bajo la cabeza, en su eje.</summary>
    static Transform BuildLooseScrew(string name, Transform root, Vector3 seat)
    {
        Transform t = PartRoot(name, root, seat);
        Disc(t, "Cabeza", seat + Vector3.up * 0.0004f, 0.0019f, 0.0008f, Vector3.up, _silver);
        Solid(t, "Ranura", seat + new Vector3(-0.0012f, 0.0008f, -0.00025f), seat + new Vector3(0.0012f, 0.00085f, 0.00025f), _dark);
        Solid(t, "Ranura", seat + new Vector3(-0.00025f, 0.0008f, -0.0012f), seat + new Vector3(0.00025f, 0.00085f, 0.0012f), _dark);
        Disc(t, "Caña", seat + Vector3.down * 0.0015f, 0.0010f, 0.0030f, Vector3.up, _silver);
        return t;
    }

    /// <summary>Agujero para un tornillo M2x3 (lugar normal con su señal), que exige la pieza que sujeta.</summary>
    static SocketSystem Hole(string name, Transform parent, Vector3 pos, SocketSystem holds, SocketSystem[] locked, string fill, string empty)
    {
        var s = Inside(MakeSocket(name, parent, pos, Quaternion.identity, "Type_TornilloM2x3"), locked);
        s.requires.Add(holds);
        s.snapRadius = 0.006f;
        s.onlyOwnType = true; // agujeros junto a terminales y conectores: solo hablan con tornillos
        s.cycleRequired = true;
        s.fillAction = fill;
        s.emptyAction = empty;
        s.cycleAction = fill;
        s.blockReason = "el soporte sigue atornillado.";
        AddPortMarker(s, HoleMarker);
        return s;
    }

    /// <summary>
    /// Cable cuyo conector empieza enchufado en <paramref name="port"/> y, desenchufado, reposa en <paramref name="home"/>
    /// (cerca del puerto: el cable es corto). El tubo sale de <paramref name="start"/>.
    /// </summary>
    static CablePlug PluggedCable(CableRig c, string name, Transform start, string type, Vector3 plugSize, Material plugMat, Material cableMat,
                                  CableSpline.Profile profile, float radius, float flatHalf, float length, SocketSystem port, Transform home,
                                  string title, string desc)
    {
        Transform plug = MakePlug("Conector: " + title, c.cables, port.SnapPosition, port.SnapRotation, plugSize, plugMat, type, title, desc);
        var spline = Spline(c, name, start, plug, cableMat, profile, radius, flatHalf, length, title, desc);
        var cp = plug.gameObject.AddComponent<CablePlug>();
        cp.cable = spline;
        LieFlat(cp);
        var part = plug.GetComponent<PCPart>();
        part.startInstalledIn = port;
        part.restPoint = home;
        return cp;
    }

    /// <summary>
    /// Dentro de una laptop no hay sitio para que un cable se arquee (atravesaría la tapa): el sobrante se comba siempre hacia
    /// abajo, sobre su superficie de apoyo (la placa, la batería), esté conectado o no.
    /// </summary>
    static void LieFlat(CablePlug plug)
    {
        SetLooseSlack(plug, Vector3.down);
        var so = new SerializedObject(plug);
        so.FindProperty("archWhenConnected").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetHudTexts(string noun, string title, string text)
    {
        var hud = Object.FindAnyObjectByType<AssemblyHudView>();
        if (hud == null) { Log("⚠ No hay AssemblyHudView en la escena"); return; }
        var so = new SerializedObject(hud);
        so.FindProperty("progressNoun").stringValue = noun;
        so.FindProperty("victoryTitle").stringValue = title;
        so.FindProperty("victoryText").stringValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ===================================================================== mesa de trabajo

    /// <summary>Alfombrilla antiestática y bandeja magnética para los tornillos sueltos (sus 3 sitios de reposo).</summary>
    static void BuildWorkbench(Transform root, out Transform[] trayRest)
    {
        Transform mat = NewChild("Alfombrilla antiestática", root);
        S(mat, "Alfombrilla", -0.178f, -MatT, -0.122f, 0.502f, 0f, 0.458f, _antistatic);
        Disc(mat, "Broche de la pulsera", P(-0.165f, 0.0006f, -0.110f), 0.005f, 0.0012f, Vector3.up, _silver);
        Box(mat, "Colisión", P(-0.178f, -MatT, -0.122f), P(0.502f, 0f, 0.458f));
        MakePart(mat, "Alfombrilla", null, "Alfombrilla antiestática (ESD)",
            "Superficie disipativa conectada a tierra. El manual de Dell pide trabajar sobre ella con una pulsera antiestática " +
            "con cable (las inalámbricas ya no se aceptan), enganchada a la alfombrilla y a metal sin pintar del equipo, y dejar " +
            "encima cada pieza que se saca. Una descarga que ni se siente puede dañar la memoria o la placa, a veces sin que se " +
            "note hasta semanas después.",
            draggable: false, fitCollider: false);

        Transform tray = NewChild("Bandeja magnética", root);
        float x0 = 0.355f, x1 = 0.445f, z0 = -0.075f, z1 = -0.015f, h = 0.010f, t = 0.0015f;
        S(tray, "Fondo", x0, 0f, z0, x1, 0.002f, z1, _tray);
        S(tray, "Imán", x0 + t, 0.002f, z0 + t, x1 - t, 0.0024f, z1 - t, _black);
        S(tray, "Pared delantera", x0, 0f, z0, x1, h, z0 + t, _tray);
        S(tray, "Pared trasera", x0, 0f, z1 - t, x1, h, z1, _tray);
        S(tray, "Pared izquierda", x0, 0f, z0, x0 + t, h, z1, _tray);
        S(tray, "Pared derecha", x1 - t, 0f, z0, x1, h, z1, _tray);
        Box(tray, "Colisión", P(x0, 0f, z0), P(x1, 0.0024f, z1));
        MakePart(tray, "Bandeja", null, "Bandeja magnética para tornillos",
            "Aquí van los tornillos sueltos (los M2x3 de los soportes) mientras se trabaja: el imán los sujeta. El manual de Dell " +
            "advierte que un tornillo perdido dentro del equipo puede perforar la batería o dañar otras piezas. Los de la tapa " +
            "no vienen aquí: son cautivos y no salen de ella.",
            draggable: false, fitCollider: false);

        trayRest = new Transform[3];
        for (int i = 0; i < 3; i++)
            trayRest[i] = RestPoint("Reposo tornillo " + (i + 1) + " (bandeja)", root, P(0.375f + i * 0.025f, 0.0024f + 0.003f, -0.045f), Quaternion.identity);
    }

    // ===================================================================== carcasa y tapa

    static void BuildShellColliders(Transform laptop)
    {
        Transform col = NewChild("Colisión", laptop);
        Box(col, "Pantalla y teclado", P(0f, 0f, 0f), P(W, FloorY, D));
        Box(col, "Pared izquierda", P(0f, 0f, 0f), P(0.006f, 0.014f, D));
        Box(col, "Pared derecha", P(W - 0.006f, 0f, 0f), P(W, 0.014f, D));
        Box(col, "Pared delantera", P(0f, 0f, 0f), P(W, 0.014f, 0.006f));
        Box(col, "Pared de la bisagra", P(0f, 0f, D - 0.006f), P(W, 0.014f, D));
    }

    /// <summary>Rejillas, patas de goma, etiqueta y rejillas de los altavoces de la tapa inferior (figuras del manual).</summary>
    static void BuildCoverDetails(Transform cover)
    {
        float y = CoverTop;
        // Rejilla grande a lo largo de la bisagra (entrada de aire del ventilador) y una pequeña delante a la izquierda.
        for (int col = 0; col < 4; col++)
        {
            float cx0 = 0.032f + col * 0.065f;
            for (int row = 0; row < 15; row++)
            {
                float z = 0.132f + row * 0.0042f;
                S(cover, "Rejilla", cx0, y, z, cx0 + 0.057f, y + 0.0002f, z + 0.0018f, _vent);
            }
        }
        for (int row = 0; row < 7; row++)
        {
            float z = 0.026f + row * 0.0042f;
            S(cover, "Rejilla delantera", 0.078f, y, z, 0.118f, y + 0.0002f, z + 0.0018f, _vent);
        }
        S(cover, "Etiqueta", 0.172f, y, 0.084f, 0.238f, y + 0.00015f, 0.104f, _label);
        foreach (var f in new[] { new Vector2(0.040f, 0.200f), new Vector2(0.283f, 0.200f), new Vector2(0.040f, 0.060f), new Vector2(0.283f, 0.040f) })
            Solid(cover, "Pata de goma", P(f.x - 0.008f, y, f.y - 0.0035f), P(f.x + 0.008f, y + 0.0015f, f.y + 0.0035f), _rubber, PrimitiveType.Cylinder);
        foreach (var g in new[] { new Vector2(0.040f, 0.016f), new Vector2(0.283f, 0.016f) })
            S(cover, "Rejilla del altavoz", g.x - 0.016f, y, g.y - 0.002f, g.x + 0.016f, y + 0.0002f, g.y + 0.002f, _vent);
    }

    /// <summary>
    /// Los 8 tornillos cautivos de la tapa (posiciones de la figura del manual: las 4 esquinas, el centro del borde de la
    /// bisagra, el centro de la tapa y uno en cada lado corto). Un clic los afloja o los aprieta (giran y suben 1,5 mm sin
    /// salir de la tapa). Su estado «apretado» es un lugar que exige la tapa puesta; la tapa no sale con uno apretado.
    /// </summary>
    static void BuildCaptiveScrews(Transform cover, SocketSystem coverSocket)
    {
        Vector2[] at =
        {
            new Vector2(0.008f, 0.208f), new Vector2(0.1615f, 0.209f), new Vector2(0.315f, 0.208f), new Vector2(0.315f, 0.091f),
            new Vector2(0.315f, 0.009f), new Vector2(0.1615f, 0.108f), new Vector2(0.008f, 0.073f), new Vector2(0.008f, 0.009f),
        };
        for (int i = 0; i < at.Length; i++)
        {
            float x = at[i].x, z = at[i].y, y = CoverTop;
            Transform s = PartRoot("Tornillo cautivo " + (i + 1), cover, P(x, y, z));
            Disc(s, "Cabeza", P(x, y + 0.0003f, z), 0.0025f, 0.0006f, Vector3.up, _screwDark);
            S(s, "Ranura", x - 0.0016f, y + 0.0006f, z - 0.0003f, x + 0.0016f, y + 0.00065f, z + 0.0003f, _black);
            S(s, "Ranura", x - 0.0003f, y + 0.0006f, z - 0.0016f, x + 0.0003f, y + 0.00065f, z + 0.0016f, _black);
            var part = MakePart(s, "Tornillo cautivo", "Type_TornilloCautivo", "Tornillo cautivo de la tapa",
                "Uno de los 8 que sujetan la tapa inferior. Es cautivo: al aflojarlo no sale, queda retenido en la tapa. Así no se " +
                "pierde ni cae dentro del equipo, donde podría perforar la batería o hacer un cortocircuito. Clic: aflojar o apretar.",
                draggable: false);
            Grow(part, new Vector3(0.002f, 0.002f, 0.002f));
            Disc(cover, "Alojamiento del tornillo " + (i + 1), P(x, y + 0.00005f, z), 0.0034f, 0.0001f, Vector3.up, _vent);

            var state = MakeSocket("Estado: tornillo cautivo " + (i + 1) + " apretado", cover, P(x, y, z), Quaternion.identity, "Type_TornilloCautivo");
            state.requires.Add(coverSocket);
            state.cycleRequired = true;
            state.fillAction = "apretar los tornillos de la tapa (clic en cada uno)";
            state.emptyAction = "aflojar los 8 tornillos de la tapa (clic en cada uno)";
            state.cycleAction = "apretar los tornillos de la tapa (clic en cada uno)";
            state.blockReason = "la tapa sigue atornillada, y si se hace palanca así se rompen sus pestañas de plástico.";
            var h = AddHinge(s, state, P(x, y, z), Vector3.up, -1080f, "aflojar el tornillo", "apretar el tornillo",
                "Tornillo aflojado", "Tornillo apretado", cover, lift: 0.0015f);
            h.duration = 0.6f;
        }
    }

    // ===================================================================== interior

    static Transform BuildBoard(Transform inside)
    {
        S(inside, "Bandeja interior", 0.006f, FloorY - 0.0006f, 0.006f, 0.317f, FloorY, 0.210f, _magnesium);
        S(inside, "Ranura de la cinta del touchpad", 0.256f, FloorY, 0.1015f, 0.270f, FloorY + 0.00005f, 0.1035f, _vent);
        S(inside, "Altavoz izquierdo", 0.012f, FloorY, 0.010f, 0.042f, FloorY + 0.0050f, 0.030f, _black);
        foreach (float x in new[] { 0.010f, 0.283f })
            S(inside, "Soporte de la bisagra", x, FloorY, 0.196f, x + 0.030f, FloorY + 0.004f, 0.212f, _magnesium);

        Transform b = NewChild("Placa base", inside);
        S(b, "PCB", 0.028f, BoardBottom, 0.104f, 0.300f, BoardTop, 0.204f, _pcb);
        S(b, "PCB (frente izquierdo)", 0.028f, BoardBottom, 0.075f, 0.128f, BoardTop, 0.104f, _pcb);
        Box(b, "Colisión PCB", P(0.028f, BoardBottom, 0.104f), P(0.300f, BoardTop, 0.204f));
        Box(b, "Colisión PCB (frente izquierdo)", P(0.028f, BoardBottom, 0.075f), P(0.128f, BoardTop, 0.104f));
        // Pila de botón (CR-2032), chipset y algo de electrónica, solo para que se lea como una placa.
        Disc(b, "Pila de botón (CR-2032)", P(0.050f, BoardTop + 0.0016f, 0.165f), 0.010f, 0.0032f, Vector3.up, _silver);
        S(b, "Envoltura de la pila", 0.040f, BoardTop, 0.155f, 0.060f, BoardTop + 0.0002f, 0.175f, _black);
        S(b, "Chipset", 0.205f, BoardTop, 0.170f, 0.225f, BoardTop + 0.0012f, 0.186f, _black);
        foreach (float z in new[] { 0.152f, 0.160f, 0.168f })
            S(b, "Bobina del regulador", 0.122f, BoardTop, z - 0.003f, 0.128f, BoardTop + 0.003f, z + 0.003f, _alu);
        S(b, "Chip de la BIOS", 0.230f, BoardTop, 0.140f, 0.236f, BoardTop + 0.0012f, 0.146f, _black);
        MakePart(b, "Placa base", null, "Placa base",
            "Placa propia de Dell para el Latitude 5400, con el procesador Intel de 8.ª generación soldado. Tiene 2 ranuras para " +
            "memoria SO-DIMM, una ranura M.2 2280 para el SSD, una M.2 2230 para la Wi-Fi y otra para una tarjeta de red móvil " +
            "(opcional), la pila de botón CR-2032 de la BIOS y los conectores de la batería y de las cintas. Las posiciones de " +
            "este modelo son aproximadas, tomadas de las figuras del manual de Dell.",
            draggable: false, fitCollider: false);
        return b;
    }

    /// <summary>Disipador con su tubo de calor de cobre, aletas a la salida del ventilador (en la pared derecha) y ventilador.</summary>
    static void BuildCooling(Transform inside)
    {
        Transform t = NewChild("Disipador y ventilador", inside);
        float cx = 0.159f, cz = 0.168f;
        S(t, "Placa de cobre (sobre el procesador)", cx - 0.015f, BoardTop + 0.0012f, cz - 0.015f, cx + 0.015f, BoardTop + 0.0022f, cz + 0.015f, _copper);
        S(t, "Soporte (izquierda-derecha)", cx - 0.025f, BoardTop + 0.0022f, cz - 0.004f, cx + 0.025f, BoardTop + 0.0028f, cz + 0.004f, _black);
        S(t, "Soporte (delante-detrás)", cx - 0.004f, BoardTop + 0.0022f, cz - 0.025f, cx + 0.004f, BoardTop + 0.0028f, cz + 0.025f, _black);
        foreach (var s in new[] { new Vector2(-0.022f, 0f), new Vector2(0.022f, 0f), new Vector2(0f, -0.022f), new Vector2(0f, 0.022f) })
            Disc(t, "Tornillo del disipador", P(cx + s.x, BoardTop + 0.0031f, cz + s.y), 0.0018f, 0.0006f, Vector3.up, _screwDark);
        S(t, "Tubo de calor (desde el procesador)", cx - 0.004f, BoardTop + 0.0022f, cz + 0.015f, cx + 0.004f, BoardTop + 0.0040f, 0.200f, _copper);
        S(t, "Tubo de calor (hasta las aletas)", cx - 0.004f, BoardTop + 0.0022f, 0.192f, 0.300f, BoardTop + 0.0040f, 0.200f, _copper);
        for (int i = 0; i < 18; i++)
        {
            float z = 0.132f + i * 0.0034f;
            S(t, "Aleta", 0.290f, BoardTop, z, 0.312f, BoardTop + 0.0055f, z + 0.0006f, _copper);
        }
        Transform fan = Spawn(Dir + "VentiladorLaptop/VentiladorLaptop.fbx", "Ventilador", t);
        fan.position = SeatAt(fan, fan.rotation, P(0.262f, BoardTop, 0.160f));
        // Colisión por partes: una caja que lo envolviera todo taparía la mitad de atrás de la memoria B.
        Bounds fb = WorldBounds(fan);
        Box(t, "Colisión ventilador", fb.min, fb.max);
        Box(t, "Colisión placa del disipador", P(cx - 0.025f, BoardTop, cz - 0.025f), P(cx + 0.025f, BoardTop + 0.0031f, cz + 0.025f));
        Box(t, "Colisión tubo de calor", P(cx - 0.004f, BoardTop, 0.183f), P(0.300f, BoardTop + 0.0040f, 0.200f));
        Box(t, "Colisión aletas", P(0.290f, BoardTop, 0.132f), P(0.312f, BoardTop + 0.0055f, 0.194f));
        MakePart(t, "Disipador", null, "Disipador y ventilador",
            "El procesador está soldado a la placa y lo enfría una placa de cobre con un tubo de calor que lleva el calor hasta " +
            "unas aletas, donde el ventilador empuja el aire hacia la salida lateral. No forma parte de este servicio: el manual " +
            "pide quitar antes el marco interior, y sus tornillos se aflojan y se aprietan en el orden marcado en el disipador. " +
            "Las rejillas de la tapa inferior son la entrada de aire: no se tapan.",
            draggable: false, fitCollider: false);
    }

    static Transform BuildBattery(Transform inside, out Transform cableOut)
    {
        Transform t = PartRoot("Batería", inside, P((Battery.x + Battery.z) * 0.5f, FloorY, (Battery.y + Battery.w) * 0.5f));
        S(t, "Carcasa", Battery.x, FloorY, Battery.y, Battery.z, BatTop, Battery.w, _batteryMat);
        S(t, "Etiqueta", 0.150f, BatTop, 0.020f, 0.250f, BatTop + 0.0001f, 0.080f, _label);
        S(t, "Aviso (desconectar antes de tocar nada)", 0.135f, BatTop, 0.087f, 0.300f, BatTop + 0.0001f, 0.095f, _labelWhite);
        Disc(t, "Tornillo cautivo de la batería", P(0.300f, BatTop + 0.0003f, 0.020f), 0.0022f, 0.0006f, Vector3.up, _screwDark);
        cableOut = Anchor("Salida del cable de la batería", t, P(BatteryConn.x, BatTop - 0.0025f, Battery.w + 0.0002f), Vector3.forward);
        return t;
    }

    // ===================================================================== memoria

    /// <summary>Dos ranuras SO-DIMM; la A trae su módulo, la B está vacía y el nuevo espera en la alfombrilla.</summary>
    static SocketSystem[] BuildMemory(Transform root, Transform board, SocketSystem[] locked)
    {
        var slots = new SocketSystem[2];
        var modules = new Transform[2];
        string[] names = { "A", "B" };
        for (int i = 0; i < 2; i++)
        {
            float cx = RamX[i];
            S(board, "Ranura de memoria " + names[i], cx - 0.036f, BoardTop, RamConnZ - 0.002f, cx + 0.036f, BoardTop + 0.0055f, RamConnZ + 0.005f, _black);
            S(board, "Pestaña " + names[i] + " (izq.)", cx - 0.0375f, BoardTop, RamConnZ - 0.031f, cx - 0.0355f, BoardTop + 0.0050f, RamConnZ - 0.023f, _silver);
            S(board, "Pestaña " + names[i] + " (der.)", cx + 0.0355f, BoardTop, RamConnZ - 0.031f, cx + 0.0375f, BoardTop + 0.0050f, RamConnZ - 0.023f, _silver);

            Transform module = Spawn(Dir + "SODIMM/SODIMM.fbx", i == 0 ? "RAM (la que trae)" : "RAM nueva", root);
            modules[i] = module;
            Vector3 seat = P(cx, RamY, RamConnZ - 0.015f);
            var s = Inside(MakeSocket("Lugar RAM ranura " + names[i], board, seat, Quaternion.identity, "Type_SODIMM", required: i == 1), locked);
            s.fillAction = i == 0 ? "poner un módulo de memoria en la ranura A" : "poner el segundo módulo de memoria (ranura B)";
            s.insertTilt = 30f; // manual: «Slide the memory module firmly into the slot at an angle»
            s.insertHinge = NewChild("Bisagra de inserción", s.transform);
            s.insertHinge.SetPositionAndRotation(P(cx, RamY, RamConnZ), Quaternion.LookRotation(Vector3.right, Vector3.up));
            s.insertSlide = 0.005f;
            slots[i] = s;
            module.position = seat; // pivote del modelo = su centro
        }
        // La que trae: instalada en A. La nueva: en la alfombrilla, a la izquierda.
        modules[1].position = P(-0.075f, 0.0007f, 0.000f);
        string desc = "Memoria de laptop (SO-DIMM, 69,6 × 30 mm) DDR4 de 8 GB. Este equipo tiene 2 ranuras y admite hasta 16 GB " +
                      "por ranura (32 GB en total): DDR4-2666 que, con procesadores Intel de 8.ª generación, trabaja a 2400 MHz. Con " +
                      "dos módulos iguales funciona en doble canal (más ancho de banda). Manual de Dell: se alinea la muesca, se " +
                      "introduce en ángulo y se presiona hacia abajo hasta que las pestañas hacen clic; si no hay clic, se saca y " +
                      "se vuelve a poner. Para sacarla se abren las pestañas y el módulo salta solo.";
        var ramA = MakePart(modules[0], "RAM", "Type_SODIMM", "Memoria SO-DIMM DDR4 8 GB (la que trae)", desc);
        StartsIn(ramA, slots[0], RestPoint("Reposo de la RAM que trae (alfombrilla)", root, P(-0.075f, 0.0007f, 0.190f), Quaternion.identity));
        MakePart(modules[1], "RAM", "Type_SODIMM", "Memoria SO-DIMM DDR4 8 GB (nueva)",
            desc + " Esta es la nueva: va en la ranura libre, la B.");
        return slots;
    }

    // ===================================================================== SSD

    /// <summary>
    /// Ranura M.2 2280 con su placa térmica, el SSD que trae (instalado) y el nuevo (en la alfombrilla), el soporte negro que
    /// lo cubre y sus 2 tornillos M2x3. Manual: tornillos → soporte → SSD; y al revés para ponerlo. El lugar del SSD solo
    /// cuenta con el nuevo.
    /// </summary>
    static SocketSystem[] BuildSsd(Transform root, Transform board, SocketSystem[] locked, Transform[] trayRest)
    {
        S(board, "Ranura M.2 2280 (SSD)", SsdX - 0.0125f, BoardTop, SsdConnZ, SsdX + 0.0125f, BoardTop + 0.0025f, SsdConnZ + 0.005f, _black);
        S(board, "Placa térmica del SSD", SsdX - 0.013f, FloorY, 0.018f, SsdX + 0.013f, SsdBottom, SsdConnZ, _thermal);
        Box(board, "Colisión placa térmica", P(SsdX - 0.013f, FloorY, 0.018f), P(SsdX + 0.013f, SsdBottom, SsdConnZ));

        // Pivote de los dos SSD: el centro del borde de los contactos, abajo (así encajan los dos en el mismo lugar).
        Vector3 seat = P(SsdX, SsdBottom, SsdConnZ);
        var slot = Inside(MakeSocket("Lugar SSD M.2", board, seat, Quaternion.identity, "Type_SSDLaptop"), locked);
        slot.insertTilt = 20f;
        slot.insertHinge = NewChild("Bisagra de inserción", slot.transform);
        slot.insertHinge.SetPositionAndRotation(seat, Quaternion.LookRotation(Vector3.right, Vector3.up));
        slot.insertSlide = 0.004f;
        slot.fillAction = "poner el SSD";
        slot.goalAction = "poner el SSD nuevo (el de 1 TB)";

        // El que trae: el SSD del modelo de la placa ATX, a lo largo de Z con los contactos hacia la ranura.
        Transform atxBoard = Spawn(AtxModels + "PlacaBase/PlacaBase.fbx", "tmp placa ATX", root);
        Transform oldMesh = atxBoard.Find("SSD_M2");
        oldMesh.SetParent(root, true);
        Object.DestroyImmediate(atxBoard.gameObject);
        Transform oldSsd = SsdRoot("SSD (el que trae)", root, oldMesh, Quaternion.Euler(0f, -90f, 0f), seat);
        // El nuevo: el Samsung 980 Pro del lab ATX, acostado con la etiqueta hacia arriba (sus contactos están en su -X).
        Transform newMesh = Spawn(AtxModels + "SSD_980Pro/SSD_980Pro.fbx", "SSD 980 Pro", root);
        Transform newSsd = SsdRoot("SSD nuevo", root, newMesh, Quaternion.LookRotation(Vector3.down, Vector3.left) * Quaternion.Euler(0f, 0f, 180f), P(-0.075f, 0f, 0.100f));

        string common = "Formato M.2 2280 (22 × 80 mm), PCIe NVMe. Este equipo admite M.2 2230 o 2280, PCIe NVMe o SATA, de hasta " +
                        "1 TB. Va en su ranura sobre una placa térmica y lo cubre un soporte negro con 2 tornillos M2x3. Entra en " +
                        "ángulo por el lado de los contactos y se baja.";
        var oldPart = MakePart(oldSsd, "SSD viejo", "Type_SSDLaptop", "SSD M.2 2280 (el que trae, con fallos)",
            "El SSD que traía el equipo: el diagnóstico avisa de errores de lectura, así que se cambia por uno nuevo. " + common +
            " Antes de cambiarlo hay que rescatar los datos o tener una copia de seguridad: el nuevo llega vacío.");
        StartsIn(oldPart, slot, RestPoint("Reposo del SSD que trae (alfombrilla)", root, P(-0.075f, 0f, 0.150f), oldSsd.rotation));
        var newPart = MakePart(newSsd, "SSD nuevo", "Type_SSDLaptop", "SSD M.2 2280 NVMe de 1 TB (nuevo)",
            "El SSD nuevo. " + common + " Este es PCIe 4.0 y, en este equipo (PCIe 3.0), funciona a la velocidad de PCIe 3.0.");
        slot.goalPart = newPart;

        // Soporte (la chapa negra) y sus 2 tornillos, en esquinas opuestas (figura del manual).
        float sx0 = SsdX - 0.015f, sx1 = SsdX + 0.015f, sz0 = 0.019f, sz1 = 0.103f;
        Transform shield = PartRoot("Soporte del SSD", root, P(SsdX, ShieldY, (sz0 + sz1) * 0.5f));
        S(shield, "Chapa", sx0, ShieldY, sz0, sx1, ShieldTop, sz1, _shield);
        S(shield, "Marca de orientación", sx0 + 0.003f, ShieldTop, sz1 - 0.008f, sx0 + 0.006f, ShieldTop + 0.0001f, sz1 - 0.004f, _label);
        var shieldSocket = Inside(MakeSocket("Lugar soporte del SSD", board, shield.position, shield.rotation, "Type_SoporteSSD"), locked);
        shieldSocket.requires.Add(slot);
        shieldSocket.cycleRequired = true;
        shieldSocket.fillAction = "poner el soporte del SSD";
        shieldSocket.emptyAction = "quitar el soporte del SSD (la chapa negra)";
        shieldSocket.cycleAction = "volver a poner el soporte del SSD";
        slot.requiresEmpty.Add(shieldSocket);
        var shieldPart = MakePart(shield, "Soporte del SSD", "Type_SoporteSSD", "Soporte del SSD",
            "Chapa negra que sujeta el SSD contra su placa térmica y le ayuda a disipar el calor. Se fija a la carcasa con 2 " +
            "tornillos M2x3. Manual de Dell: se quitan los 2 tornillos, se gira un poco el soporte y se saca; luego sale el SSD.");
        StartsIn(shieldPart, shieldSocket, RestPoint("Reposo del soporte del SSD (alfombrilla)", root, P(0.385f, 0.0002f, 0.060f), Quaternion.identity));

        Vector2[] screwAt = { new Vector2(sx1 - 0.004f, sz1 - 0.004f), new Vector2(sx0 + 0.004f, sz0 + 0.004f) };
        string[] where = { "atrás", "delante" };
        var holes = new SocketSystem[2];
        for (int i = 0; i < 2; i++)
        {
            Vector3 at = P(screwAt[i].x, ShieldTop, screwAt[i].y);
            Disc(board, "Separador del tornillo", P(screwAt[i].x, (FloorY + ShieldY) * 0.5f, screwAt[i].y), 0.0016f, ShieldY - FloorY, Vector3.up, _brass);
            holes[i] = Hole("Agujero: soporte del SSD (" + where[i] + ")", board, at, shieldSocket, locked,
                "atornillar el soporte del SSD (2 tornillos M2x3)", "quitar los 2 tornillos del soporte del SSD (arrástralos a la bandeja)");
            Transform screw = BuildLooseScrew("Tornillo M2x3 (SSD, " + where[i] + ")", root, at);
            var p = MakePart(screw, "Tornillo M2x3", "Type_TornilloM2x3", "Tornillo M2x3",
                "Tornillo suelto de 2 mm de diámetro y 3 mm de largo (M2x3). Los soportes del SSD (2) y de la Wi-Fi (1) llevan este. " +
                "Al quitarlo, va a la bandeja magnética: uno perdido dentro del equipo puede perforar la batería o hacer un cortocircuito.");
            Grow(p, new Vector3(0.002f, 0.001f, 0.002f));
            StartsIn(p, holes[i], trayRest[i]);
        }
        return new[] { slot, shieldSocket, holes[0], holes[1] };
    }

    /// <summary>Raíz de un SSD con el pivote en el centro del borde de sus contactos, abajo (el que mira a +Z).</summary>
    static Transform SsdRoot(string name, Transform root, Transform mesh, Quaternion rot, Vector3 pivot)
    {
        mesh.rotation = rot;
        Bounds b = BoundsAt(mesh, rot);
        Transform t = PartRoot(name, root, pivot);
        mesh.position = pivot - new Vector3(b.center.x, b.min.y, b.max.z);
        mesh.SetParent(t, true);
        return t;
    }

    // ===================================================================== Wi-Fi

    /// <summary>
    /// Tarjeta Wi-Fi M.2 2230 con sus dos terminales (MAIN y AUX), sus antenas, el soporte que las sujeta y su tornillo.
    /// Manual: tornillo → soporte → antenas → tarjeta; y al revés para ponerla. Hay que reasentarla (quitarla y volver a
    /// ponerla). Los terminales viajan con la tarjeta; las antenas llegan desde la pantalla por la bisagra izquierda.
    /// </summary>
    static SocketSystem[] BuildWifi(Transform root, Transform board, SocketSystem[] locked, CableRig rig, Transform screwRest)
    {
        S(board, "Ranura M.2 2230 (Wi-Fi, Key-E)", WifiX - 0.013f, BoardTop, WifiConnZ, WifiX + 0.013f, BoardTop + 0.0025f, WifiConnZ + 0.005f, _black);
        Disc(board, "Separador del tornillo de la Wi-Fi", P(WifiX, BoardTop + 0.0005f, 0.0905f), 0.0016f, 0.001f, Vector3.up, _brass);

        Vector3 seat = P(WifiX, WifiY, WifiConnZ);
        Transform card = PartRoot("Tarjeta Wi-Fi", root, seat);
        float y0 = WifiY, y1 = WifiY + 0.0008f;
        S(card, "PCB", WifiX - 0.011f, y0, WifiConnZ - 0.030f, WifiX + 0.011f, y1, WifiConnZ, _pcbGreen);
        S(card, "Contactos", WifiX - 0.010f, y1, WifiConnZ - 0.004f, WifiX + 0.010f, y1 + 0.00005f, WifiConnZ, _gold);
        S(card, "Blindaje", WifiX - 0.009f, y1, WifiConnZ - 0.025f, WifiX + 0.009f, y1 + 0.0013f, WifiConnZ - 0.006f, _silver);
        S(card, "Etiqueta", WifiX - 0.007f, y1 + 0.0013f, WifiConnZ - 0.022f, WifiX + 0.007f, y1 + 0.00135f, WifiConnZ - 0.010f, _labelWhite);
        foreach (var term in new[] { (MainX, _labelWhite, "MAIN"), (AuxX, _marker, "AUX") })
        {
            Disc(card, "Terminal " + term.Item3, P(term.Item1, y1 + 0.0003f, TermZ), 0.0010f, 0.0006f, Vector3.up, _gold);
            // Triángulo de la serigrafía (blanco = MAIN, negro = AUX), como un rombo pequeño junto al terminal.
            StaticBlock("Triángulo " + term.Item3, card, P(term.Item1, y1 + 0.00003f, TermZ - 0.0028f), Quaternion.Euler(0f, 45f, 0f),
                new Vector3(0.0016f, 0.00005f, 0.0016f), term.Item2);
        }

        var slot = Inside(MakeSocket("Lugar tarjeta Wi-Fi (M.2 2230)", board, seat, Quaternion.identity, "Type_WiFiM2"), locked);
        slot.insertTilt = 20f;
        slot.insertHinge = NewChild("Bisagra de inserción", slot.transform);
        slot.insertHinge.SetPositionAndRotation(seat, Quaternion.LookRotation(Vector3.right, Vector3.up));
        slot.insertSlide = 0.004f;
        slot.cycleRequired = true;
        slot.fillAction = "volver a poner la tarjeta Wi-Fi";
        slot.emptyAction = "sacar la tarjeta Wi-Fi";
        slot.cycleAction = "reasentar la tarjeta Wi-Fi (sacarla y volver a ponerla)";
        slot.snapRadius = 0.015f;
        var cardPart = MakePart(card, "Tarjeta Wi-Fi", "Type_WiFiM2", "Tarjeta Wi-Fi M.2 2230",
            "Tarjeta inalámbrica (Wi-Fi y Bluetooth) de 22 × 30 mm en la ranura M.2 2230 Key-E. Con este equipo venían la Intel " +
            "Wireless-AC 9560 o la Qualcomm QCA61x4A, y como opción la Intel Wi-Fi 6 AX200. Cuando la Wi-Fi se corta, reasentarla " +
            "(sacarla y volver a ponerla, con las antenas bien conectadas) es un arreglo típico. Manual de Dell: no se deja ningún " +
            "cable debajo de la tarjeta.");
        StartsIn(cardPart, slot, RestPoint("Reposo de la tarjeta Wi-Fi (alfombrilla)", root, P(-0.125f, 0.0001f, 0.075f), Quaternion.identity));

        // Soporte de las antenas: tapa sus conectores; lo sujeta el tornillo, que también fija la tarjeta.
        Transform bracket = PartRoot("Soporte de las antenas", root, P(WifiX, BracketY, 0.094f));
        S(bracket, "Chapa", WifiX - 0.008f, BracketY, 0.089f, WifiX + 0.008f, BracketTop, 0.098f, _silver);
        var bracketSocket = Inside(MakeSocket("Lugar soporte de las antenas", board, bracket.position, bracket.rotation, "Type_SoporteWiFi"), locked);
        bracketSocket.cycleRequired = true;
        bracketSocket.fillAction = "poner el soporte de las antenas";
        bracketSocket.emptyAction = "quitar el soporte de las antenas (la chapa sobre sus conectores)";
        bracketSocket.cycleAction = "volver a poner el soporte de las antenas";
        bracketSocket.snapRadius = 0.012f;
        bracketSocket.onlyOwnType = true; // está sobre los terminales: no explica «aquí va el soporte» a una antena
        bracketSocket.requires.Add(slot);
        slot.requiresEmpty.Add(bracketSocket);
        var bracketPart = MakePart(bracket, "Soporte de las antenas", "Type_SoporteWiFi", "Soporte de la tarjeta Wi-Fi",
            "Chapa pequeña que sujeta los conectores de las antenas sobre la tarjeta para que no se suelten. Un solo tornillo " +
            "M2x3 la fija a la placa y sujeta también la tarjeta.");
        StartsIn(bracketPart, bracketSocket, RestPoint("Reposo del soporte de las antenas (alfombrilla)", root, P(0.385f, 0.0002f, 0.120f), Quaternion.identity));

        // Terminales (viajan con la tarjeta) y antenas desde la bisagra izquierda.
        var ports = new SocketSystem[2];
        var types = new[] { "Type_AntenaMain", "Type_AntenaAux" };
        var titles = new[] { "Antena principal (cable blanco)", "Antena auxiliar (cable negro)" };
        var mats = new[] { _cableWhite, _cableBlack };
        var xs = new[] { MainX, AuxX };
        for (int i = 0; i < 2; i++)
        {
            Quaternion left = Quaternion.LookRotation(Vector3.left, Vector3.up);
            var port = Port(i == 0 ? "Terminal MAIN (principal)" : "Terminal AUX (auxiliar)", card,
                P(xs[i], y1 + 0.0006f + PlugAntenna.y * 0.5f, TermZ), left, types[i], PlugAntenna, slot);
            Inside(port, locked);
            port.requiresEmpty.Add(bracketSocket);
            port.cycleRequired = true;
            port.snapRadius = 0.0035f; // los dos terminales están a 8 mm: cada uno solo alcanza al suyo
            port.fillAction = i == 0 ? "conectar la antena principal (cable blanco) al terminal MAIN" : "conectar la antena auxiliar (cable negro) al terminal AUX";
            port.emptyAction = i == 0 ? "desconectar la antena principal (cable blanco)" : "desconectar la antena auxiliar (cable negro)";
            port.cycleAction = port.fillAction;
            bracketSocket.requires.Add(port);
            ports[i] = port;

            Transform start = Anchor("Salida de la " + titles[i] + " (bisagra)", root, P(0.022f + i * 0.004f, BoardTop + 0.0012f, 0.202f), Vector3.back);
            Transform home = RestPoint("Reposo de la " + titles[i], root, P(0.025f, BoardTop + PlugAntenna.y * 0.5f, TermZ + 0.006f - i * 0.008f), left);
            var plug = PluggedCable(rig, "Cable de la " + titles[i], start, types[i], PlugAntenna, mats[i], mats[i],
                CableSpline.Profile.Round, 0.0006f, 0.0005f, 0.140f, port, home, titles[i],
                "Cable coaxial fino (1,13 mm) que viene de la antena, en el marco de la pantalla, y pasa por la bisagra. La tarjeta " +
                "tiene dos terminales: MAIN (principal, triángulo blanco) y AUX (auxiliar, triángulo negro). Aquí el blanco va a " +
                "MAIN y el negro a AUX: es la convención de otros manuales de Dell (el del 5400 no indica los colores), así que en " +
                "un equipo real se comprueban los triángulos de la tarjeta. Se conecta presionando recto hasta el clic y se " +
                "desconecta tirando recto hacia arriba, sin palanca.");
            Grow(plug, new Vector3(0.002f, 0.0015f, 0.002f));
            var wp = new List<Transform>
            {
                Anchor("Paso de la " + titles[i] + " (1)", rig.cables, P(0.022f + i * 0.003f, BoardTop + 0.0012f, 0.150f), Vector3.back),
                Anchor("Paso de la " + titles[i] + " (2)", rig.cables, P(0.026f + i * 0.002f, BoardTop + 0.0012f, 0.110f), Vector3.back),
            };
            plug.cable.waypoints = wp;
        }

        Vector3 holeAt = P(WifiX, BracketTop, 0.0905f);
        var hole = Hole("Agujero: soporte de las antenas", board, holeAt, bracketSocket, locked,
            "atornillar el soporte de las antenas (1 tornillo M2x3)", "quitar el tornillo del soporte de las antenas (arrástralo a la bandeja)");
        Transform screw = BuildLooseScrew("Tornillo M2x3 (Wi-Fi)", root, holeAt);
        var sp = MakePart(screw, "Tornillo M2x3", "Type_TornilloM2x3", "Tornillo M2x3",
            "Tornillo suelto de 2 mm de diámetro y 3 mm de largo (M2x3): sujeta el soporte de las antenas y la tarjeta Wi-Fi. Al " +
            "quitarlo, va a la bandeja magnética.");
        Grow(sp, new Vector3(0.002f, 0.001f, 0.002f));
        StartsIn(sp, hole, screwRest);

        return new[] { slot, ports[0], ports[1], bracketSocket, hole };
    }

    // ===================================================================== cinta del touchpad

    /// <summary>
    /// Conector ZIF de la cinta del touchpad, con su pestillo (clic: levantar o bajar). La cinta llegó suelta (la avería):
    /// se levanta el pestillo, se mete la cinta y se baja. El pestillo bajado exige la cinta puesta.
    /// </summary>
    static SocketSystem[] BuildTouchpadRibbon(Transform root, Transform board, SocketSystem[] locked, CableRig rig)
    {
        float zx = Zif.x, zz = Zif.y;
        S(board, "Conector ZIF de la cinta", zx - 0.008f, BoardTop, zz - 0.003f, zx + 0.008f, BoardTop + 0.0012f, zz + 0.003f, _black);

        Transform latch = PartRoot("Pestillo de la cinta (ZIF)", root, P(zx, BoardTop + 0.0012f, zz));
        S(latch, "Pestillo", zx - 0.0085f, BoardTop + 0.0012f, zz - 0.003f, zx + 0.0085f, BoardTop + 0.0017f, zz + 0.0025f, _zif);
        var latchState = Inside(MakeSocket("Estado: pestillo de la cinta bajado", board, latch.position, Quaternion.identity, "Type_PestilloZIF"), locked);
        latchState.fillAction = "bajar el pestillo de la cinta (clic sobre él)";
        latchState.emptyAction = "levantar el pestillo de la cinta (clic sobre él)";
        latchState.blockReason = "Con el pestillo bajado la cinta no entra: si se empuja, se doblan o se rompen sus contactos.";
        var latchPart = MakePart(latch, "Pestillo ZIF", "Type_PestilloZIF", "Pestillo del conector de la cinta (ZIF)",
            "Conector ZIF (zero insertion force, «sin fuerza de inserción»): un pestillo que se levanta para meter o sacar la " +
            "cinta sin empujar y se baja para sujetarla. El manual de Dell lo pide así: «lift the latch». Clic: levantar o bajar.",
            draggable: false);
        Grow(latchPart, new Vector3(0.002f, 0.003f, 0.003f));
        AddHinge(latch, latchState, P(zx, BoardTop + 0.0015f, zz + 0.0025f), Vector3.right, 100f,
            "levantar el pestillo", "bajar el pestillo", "Pestillo levantado", "Pestillo bajado", root);

        var port = Port("Conector de la cinta del touchpad", board, P(zx, BoardTop + 0.0006f, zz - 0.001f), Back, "Type_CintaTouchpad", PlugFfc);
        Inside(port, locked);
        port.requiresEmpty.Add(latchState);
        port.snapRadius = 0.008f;
        port.fillAction = "conectar la cinta del touchpad";
        latchState.requires.Add(port);

        // La cinta sale del touchpad por una ranura de la bandeja, entre la batería y la placa. Empieza suelta.
        Transform start = Anchor("Salida de la cinta del touchpad", root, P(zx, FloorY + 0.0003f, 0.1025f), Vector3.up);
        Transform plug = MakePlug("Conector: cinta del touchpad", rig.cables, P(zx, BoardTop + 0.0004f, 0.1065f), Back, PlugFfc, _ffcBlue,
            "Type_CintaTouchpad", "Cinta del touchpad (FFC)",
            "Cable plano flexible (FFC) que une el touchpad con la placa. Esta llegó suelta: por eso el touchpad no responde. " +
            "Con el pestillo levantado se mete recta y a fondo, con los contactos hacia abajo y la pestaña azul hacia arriba, y " +
            "se baja el pestillo. Se dobla con facilidad: no se pliega ni se tira de ella.");
        var box = plug.GetComponent<BoxCollider>();
        if (box != null) box.size = new Vector3(1.2f, 8f, 1.5f);
        var spline = Spline(rig, "Cinta del touchpad", start, plug, _ffc, CableSpline.Profile.Flat, PlugFfc.x * 0.5f, 0.0001f, 0.016f,
            "Cinta del touchpad (FFC)", "Cable plano flexible del touchpad a la placa base.");
        spline.restSurface = Plane("Superficie de apoyo (bandeja)", root, FloorY); // sale de la bandeja, por debajo de la placa
        var cp = plug.gameObject.AddComponent<CablePlug>();
        cp.cable = spline;
        LieFlat(cp);

        return new[] { port, latchState };
    }
}
