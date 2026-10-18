using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using static LabBuilderKit;

/// <summary>
/// Construye el contenido del laboratorio 2 (escena Lab_SFF): una PC compacta de oficina, modelada sobre el
/// Dell OptiPlex 7070 SFF (Service Manual y Setup and Specifications de Dell). Es la fuente de verdad de las
/// posiciones: volver a ejecutarlo rehace "PC (Lab SFF)" desde cero.
///
/// Escala real (1 unidad = 1 m), mesa a 0,849 m, el estudiante mira desde -Z. El equipo va acostado con la tapa
/// arriba: su alto de pie (290 mm) es el ancho (X), su ancho (92,6 mm) la altura (Y) y su fondo (292 mm) va en Z.
/// El chasis, la placa, el disipador, la fuente y las tarjetas se hacen con primitivas (chapa de 1,2 mm). La CPU,
/// la RAM, el SSD M.2, el disco y el ventilador se reutilizan del lab ATX.
///
/// Lo que enseña (y el manual exige): la tapa empieza puesta; el módulo de discos tapa la memoria, el procesador y
/// el SSD y solo se levanta sin el disco; el SSD y la fuente van antes que el disipador; las tarjetas son de perfil
/// bajo y se sujetan con un pestillo; la fuente es propia de Dell (dos cables) y los discos se alimentan desde la
/// placa. El objetivo incluye volver a cerrar la tapa.
/// </summary>
public static class LabSffBuilder
{
    const string AtxModels = "Assets/Project/Content/Models/Lab_ATX/";
    const string Dir = "Assets/Project/Content/Models/Lab_SFF/";
    const string MatDir = Dir + "Materiales/";
    const string ColorDir = Dir + "Colores";
    const string RootName = "PC (Lab SFF)";

    // ---- Chasis (coordenadas locales: origen en la esquina inferior izquierda delantera, X a la derecha, Z hacia atrás)
    const float W = 0.290f, H = 0.0926f, D = 0.292f; // OptiPlex 7070 SFF: 290 × 92,6 × 292 mm
    const float Sheet = 0.0012f;
    const float FeetH = 0.003f;
    const float FloorTop = FeetH + Sheet;
    const float InnerTop = H - Sheet;            // cara de abajo de la tapa
    const float BezelDepth = 0.016f;             // frontal de plástico
    const float FrontInner = BezelDepth + Sheet; // cara interior de la pared frontal
    const float RearInner = D - Sheet;
    const float BoardBottom = FloorTop + 0.005f; // separadores de 5 mm
    const float BoardTop = BoardBottom + 0.0016f;
    static readonly Vector2 ChassisCenter = new Vector2(0.12f, 0.04f); // en la mesa (x, z)

    // ---- Placa
    static readonly Vector2 Cpu = new Vector2(0.172f, 0.180f);
    static readonly float[] DimmZ = { 0.044f, 0.054f, 0.064f, 0.074f };
    const float DimmX0 = 0.012f, DimmX1 = 0.150f;
    static readonly Vector2 M2Center = new Vector2(0.1555f, 0.139f);
    const float SlotX16 = 0.245f, SlotX4 = 0.2653f;
    const float SlotZ0 = 0.188f, SlotZ1 = 0.277f;

    // ---- Módulo de discos (sobre la memoria, delante a la izquierda) y fuente (detrás a la izquierda)
    const float ModX0 = 0.003f, ModX1 = 0.153f, ModZ0 = 0.019f, ModZ1 = 0.125f, ModFloor = 0.058f, ModHinge = 0.088f;
    static readonly Vector3 PsuSize = new Vector3(0.082f, 0.080f, 0.150f);
    static readonly Vector2 PsuCenter = new Vector2(0.045f, 0.215f);

    // ---- Conectores de cable: ancho (X), largo (Y), alto (Z, por donde sale el cable)
    static readonly Vector3 PlugSys = new Vector3(0.009f, 0.013f, 0.012f);
    static readonly Vector3 PlugCpu = new Vector3(0.009f, 0.009f, 0.012f);
    static readonly Vector3 PlugSataData = new Vector3(0.006f, 0.011f, 0.010f);
    static readonly Vector3 PlugSataPower = new Vector3(0.006f, 0.022f, 0.010f);
    static readonly Vector3 PlugFan = new Vector3(0.005f, 0.011f, 0.009f);
    static readonly Vector3 PlugButton = new Vector3(0.005f, 0.011f, 0.009f);

    static Vector3 _o;
    static Material _paint, _inner, _bezel, _slit, _silver, _blue, _pcb, _black, _white, _gold, _copper, _alu, _brass,
                    _choke, _psuMat, _label, _rubber, _cardPcb, _usbBlue, _dark, _red, _cableRed, _cableGray, _cableBlack, _hddMetal;

    static Vector3 P(float x, float y, float z) => _o + new Vector3(x, y, z);

    static Transform S(Transform parent, string name, float x0, float y0, float z0, float x1, float y1, float z1, Material m) =>
        Solid(parent, name, P(x0, y0, z0), P(x1, y1, z1), m);

    [MenuItem("Tools/Simulador/Reconstruir laboratorio SFF (PC compacta)")]
    static void BuildMenu() => Debug.Log(Build());

    public static string Build()
    {
        BeginLog();
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        Transform root = new GameObject(RootName).transform;
        _o = new Vector3(ChassisCenter.x - W * 0.5f, DeskY, ChassisCenter.y - D * 0.5f);
        MakeMaterials();
        EnsureTypes();

        // ---------------- Gabinete (fijo) ----------------
        Transform chassis = NewChild("Gabinete SFF", root);
        BuildChassis(chassis);
        MakePart(chassis, "Gabinete", null, "Gabinete SFF (factor de forma pequeño)",
            "Modelado sobre el Dell OptiPlex 7070 SFF: 290 × 92,6 × 292 mm y 7,8 litros, menos de la mitad que una torre. " +
            "Todo va muy junto, así que el orden importa: el módulo de discos tapa la memoria y el procesador, y el disipador " +
            "tapa el SSD y la fuente. Aquí está acostado, con la tapa arriba. Solo admite tarjetas de perfil bajo y una fuente " +
            "propia del fabricante.",
            draggable: false, fitCollider: false);

        // ---------------- Tapa (empieza puesta) ----------------
        Transform cover = BuildCover(root);
        var coverSocket = MakeSocket("Lugar tapa", chassis, cover.position, cover.rotation, "Type_SFFCover");
        coverSocket.fillAction = "poner la tapa";
        // La automática sería ~12 cm (40 % de la tapa): al quitarla para empezar y soltarla cerca, avisaría de lo que falta dentro.
        coverSocket.snapRadius = 0.06f;
        Transform coverRest = NewChild("Reposo de la tapa (mesa)", root);
        coverRest.SetPositionAndRotation(new Vector3(0.46f, DeskY + (InnerTop - 0.076f), -0.03f), Quaternion.identity);
        var coverPart = MakePart(cover, "Tapa", "Type_SFFCover", "Tapa del gabinete",
            "Se quita sin herramientas: se desliza el pestillo de la parte trasera hasta que hace clic y la tapa se desliza y se " +
            "levanta. Con la tapa puesta no se puede tocar nada de dentro. Se vuelve a poner al final, con todo instalado: se " +
            "apoya, se desliza hasta que encaja y el pestillo la bloquea solo.");
        coverPart.explainWrongPlace = false; // tapa casi todo el equipo: soltarla cerca no es un error de lugar
        coverPart.startInstalledIn = coverSocket;
        coverPart.restPoint = coverRest;

        // ---------------- Placa base (fija) ----------------
        Transform board = NewChild("Placa base", root);
        BuildBoard(board);
        MakePart(board, "PlacaBase", null, "Placa base del OptiPlex 7070 SFF",
            "Placa propia de Dell para este gabinete: socket LGA 1151 para procesadores Intel, cuatro ranuras DDR4 UDIMM " +
            "(hasta 64 GB), dos ranuras M.2 (SSD y Wi-Fi), una PCIe x16 y una PCIe x4 abierta. La fuente solo le da energía con " +
            "dos cables, y es la placa la que alimenta a los discos.",
            draggable: false, fitCollider: false);

        // ---------------- Módulo de discos (bisagra) y pestillo de las tarjetas ----------------
        Transform module = BuildModule(root);
        var moduleState = MakeSocket("Estado: módulo de discos bajado", chassis, P(0.078f, ModFloor, 0.072f), Quaternion.identity,
            "Type_DriveModule", required: false);
        moduleState.requiresEmpty.Add(coverSocket);
        moduleState.fillAction = "bajar el módulo de discos";
        moduleState.emptyAction = "levantar el módulo de discos (clic sobre él)";
        MakePart(module, "Módulo de discos", "Type_DriveModule", "Módulo de discos y unidad óptica",
            "Sujeta el disco duro y la unidad óptica delgada (opcional; aquí no está). Tapa la memoria, el procesador y el SSD: para " +
            "llegar a ellos hay que levantarlo (clic sobre él), y antes hay que quitar el disco, que va encima. Para bajarlo, sus " +
            "pestañas entran en las ranuras a unos 30° y se presiona hasta que queda sujeto.",
            draggable: false);
        AddHinge(module, moduleState, P(ModX0 + 0.001f, ModHinge, (ModZ0 + ModZ1) * 0.5f), Vector3.forward, 95f,
            "levantar el módulo de discos", "bajar el módulo de discos", "Módulo de discos levantado", "Módulo de discos bajado", root);

        Transform latch = BuildLatch(root);
        var latchState = MakeSocket("Estado: pestillo de las tarjetas cerrado", chassis, P(0.260f, 0.085f, 0.283f), Quaternion.identity,
            "Type_CardLatch", required: false);
        latchState.requiresEmpty.Add(coverSocket);
        latchState.fillAction = "cerrar el pestillo de las tarjetas";
        latchState.emptyAction = "abrir el pestillo de las tarjetas (clic sobre él)";
        MakePart(latch, "Pestillo de las tarjetas", "Type_CardLatch", "Pestillo de las tarjetas de expansión",
            "Sujeta los brackets de las tarjetas contra la parte trasera. Se abre tirando de su lengüeta metálica (clic sobre él) " +
            "para meter o sacar una tarjeta, y se cierra antes de poner la tapa.",
            draggable: false);
        AddHinge(latch, latchState, P(0.260f, 0.0870f, 0.2895f), Vector3.right, 100f,
            "abrir el pestillo de las tarjetas", "cerrar el pestillo de las tarjetas", "Pestillo abierto", "Pestillo cerrado", root);

        SocketSystem[] underModule = { coverSocket, moduleState };

        // ---------------- CPU, RAM y SSD M.2 (del modelo de la placa ATX) ----------------
        Transform atxBoard = Spawn(AtxModels + "PlacaBase/PlacaBase.fbx", "tmp placa ATX", root);
        atxBoard.rotation = Quaternion.Euler(0f, -90f, 0f) * atxBoard.rotation; // la pose en la que se analizaron sus piezas
        Transform cpu = atxBoard.Find("CPU");
        Transform ram1 = atxBoard.Find("RAM_1");
        Transform ram2 = atxBoard.Find("RAM_2");
        Transform ssd = atxBoard.Find("SSD_M2");
        Quaternion cpuRot = cpu.rotation;
        Quaternion ramRot = ram1.rotation;
        Quaternion ssdRot = Quaternion.Euler(0f, 90f, 0f) * ssd.rotation; // a lo largo de X
        float cpuTop = BoardTop + 0.003f + BoundsAt(cpu, cpuRot).size.y;

        var cpuSocket = MakeSocket("Lugar CPU (socket LGA 1151)", board, SeatAt(cpu, cpuRot, P(Cpu.x, BoardTop + 0.003f, Cpu.y)), cpuRot, "Type_CPU");
        cpuSocket.requiresEmpty.AddRange(underModule);

        string notaRam = "Funciona, pero con dos módulos se ocupan primero DIMM1 y DIMM2 (las que la BIOS de este equipo muestra como " +
                         "principales). Para completar el laboratorio, usa esas dos.";
        var dimms = new SocketSystem[4];
        for (int i = 0; i < 4; i++)
        {
            Vector3 seat = P((DimmX0 + DimmX1) * 0.5f, BoardTop + 0.001f, DimmZ[i]);
            dimms[i] = MakeSocket("Lugar RAM DIMM" + (i + 1), board, SeatAt(ram1, ramRot, seat), ramRot, "Type_RAM",
                required: i < 2, note: i < 2 ? null : notaRam);
            dimms[i].requiresEmpty.AddRange(underModule);
            if (i < 2) dimms[i].fillAction = "poner un módulo de memoria en DIMM" + (i + 1);
        }

        var m2Socket = MakeSocket("Lugar SSD M.2", board, SeatAt(ssd, ssdRot, P(M2Center.x, BoardTop + 0.0015f, M2Center.y)), ssdRot, "Type_M2");

        cpu = Detach(cpu, root, "CPU", new Vector2(0.00f, -0.20f), Quaternion.identity);
        ssd = Detach(ssd, root, "SSD M.2", new Vector2(0.075f, -0.20f), Quaternion.Euler(0f, 90f, 0f));
        Quaternion lying = Quaternion.AngleAxis(90f, Vector3.right);
        ram1 = Detach(ram1, root, "RAM 1", new Vector2(0.20f, -0.165f), lying);
        ram2 = Detach(ram2, root, "RAM 2", new Vector2(0.20f, -0.215f), lying);
        Object.DestroyImmediate(atxBoard.gameObject);

        // ---------------- Disipador con conducto, y su ventilador ----------------
        Transform cooler = BuildCooler(root, P(Cpu.x, cpuTop, Cpu.y));
        var coolerSocket = MakeSocket("Lugar disipador", board, cooler.position, cooler.rotation, "Type_Cooler");
        coolerSocket.requires.Add(cpuSocket);
        coolerSocket.requiresEmpty.AddRange(underModule);
        RestOnDesk(cooler, new Vector2(-0.20f, 0.07f), WorldBounds(cooler));

        // El SSD queda bajo el disipador: va antes que él (manual: para quitar el SSD hay que quitar antes el disipador).
        m2Socket.requiresEmpty.AddRange(underModule);
        m2Socket.requiresEmpty.Add(coolerSocket);

        Transform fan = Spawn(AtxModels + "Ventilador120/Ventilador120.fbx", "Ventilador del disipador", root);
        fan.localScale = Vector3.one * 0.68f; // de 120 a 80 mm
        Quaternion fanRot = fan.rotation;
        var fanSocket = MakeSocket("Lugar ventilador del disipador", board, SeatAt(fan, fanRot, P(Cpu.x, cpuTop + 0.038f, Cpu.y)), fanRot, "Type_HeatsinkFan");
        fanSocket.requires.Add(coolerSocket);
        fanSocket.requiresEmpty.AddRange(underModule);
        RestOnDesk(fan, new Vector2(-0.20f, 0.20f), WorldBounds(fan));

        // ---------------- Fuente propia (detrás a la izquierda) ----------------
        Transform psu = BuildPsu(root, out Transform outSys, out Transform outCpu);
        var psuSocket = MakeSocket("Lugar fuente (detrás a la izquierda)", chassis, psu.position, psu.rotation, "Type_PSU_SFF");
        psuSocket.requiresEmpty.AddRange(underModule);
        psuSocket.requiresEmpty.Add(coolerSocket); // manual: para quitar la fuente hay que quitar antes el disipador
        RestOnDesk(psu, new Vector2(-0.40f, 0.17f), WorldBounds(psu));

        // ---------------- Disco (sobre el módulo) ----------------
        Transform hdd = Spawn("Assets/3D/Componentes/hdd.fbx", "Disco duro", root);
        hdd.GetComponent<MeshRenderer>().sharedMaterial = _hddMetal;
        RestOnDesk(hdd, new Vector2(-0.17f, -0.20f), WorldBounds(hdd));
        Bounds hddDesk = WorldBounds(hdd);
        AddHddBracket(hdd, hddDesk);
        Quaternion hddRot = Quaternion.Euler(0f, 180f, 0f) * hdd.rotation; // conectores hacia la placa (+X)
        var hddSocket = MakeSocket("Lugar disco (sobre el módulo)", module, SeatAt(hdd, hddRot, P(0.078f, ModFloor + Sheet, 0.072f)), hddRot, "Type_HDD");
        hddSocket.requires.Add(moduleState);
        hddSocket.requiresEmpty.Add(coverSocket);

        // ---------------- Tarjetas ----------------
        Transform card = BuildLowProfileCard(root, P(SlotX16, BoardTop + 0.003f, (SlotZ0 + SlotZ1) * 0.5f));
        var cardSocket = MakeSocket("Lugar tarjeta (PCIe x16, perfil bajo)", board, card.position, card.rotation, "Type_PCIeLowProfile");
        cardSocket.requiresEmpty.Add(coverSocket);
        cardSocket.requiresEmpty.Add(latchState);
        card.rotation = Quaternion.Euler(0f, 0f, -90f) * card.rotation; // acostada en la mesa, el disipador hacia arriba
        RestOnDesk(card, new Vector2(-0.45f, -0.15f), WorldBounds(card));

        // ---------------- Botón de encendido (fijo, en el frente) ----------------
        Transform button = BuildPowerButton(root, out Transform buttonOut);
        MakePart(button, "Botón de encendido", null, "Botón de encendido",
            "Módulo con el botón y su luz, sujeto al frente con lengüetas. Su cable va al conector PWR_SW de la placa base.",
            draggable: false);

        // ---------------- Fichas de las piezas ----------------
        MakePart(cpu, "CPU", "Type_CPU", "Procesador (CPU) Intel LGA 1151",
            "Ejecuta las instrucciones de los programas. Se apoya en el socket sin hacer fuerza: el triángulo de una esquina marca el " +
            "pin 1 y tiene que coincidir con el del socket. Después se bajan la tapa metálica y la palanca.");
        foreach (var ram in new[] { ram1, ram2 })
            MakePart(ram, "RAM", "Type_RAM", "Memoria RAM DDR4 (UDIMM)",
                "Módulo de memoria de escritorio (UDIMM, sin ECC). Esta placa tiene cuatro ranuras y admite hasta 64 GB, 16 GB por " +
                "ranura. Se abren los clips de los dos lados, se alinea la muesca y se presiona hasta que los clips cierran.");
        MakePart(ssd, "SSD M.2", "Type_M2", "SSD M.2 (PCIe NVMe)",
            "Almacenamiento rápido y sin cables. Va en la ranura M.2 junto al procesador, se fija con un tornillo y lleva debajo una " +
            "almohadilla térmica. En este equipo queda bajo el disipador: se instala antes que él.");
        MakePart(cooler, "Disipador", "Type_Cooler", "Disipador del procesador (con conducto)",
            "Bloque de aletas de aluminio con base de cobre. Se atornilla sobre la CPU con 4 tornillos cautivos, que se aprietan en el " +
            "orden 1-2-3-4 marcado en la placa. El conducto de plástico guía el aire del ventilador a través de las aletas. " +
            "Tapa el SSD M.2 y el acceso a la fuente: instálalos antes.");
        MakePart(fan, "Ventilador", "Type_HeatsinkFan", "Ventilador del disipador",
            "Es el único ventilador del equipo. Va atornillado encima del disipador (3 tornillos) y empuja el aire a través de sus " +
            "aletas. Su cable va al cabezal del ventilador de la CPU, y la placa regula su velocidad según la temperatura.");
        MakePart(psu, "Fuente", "Type_PSU_SFF", "Fuente de alimentación de 200 W (propia de Dell)",
            "Fuente pequeña hecha para este gabinete: 200 W, 80 PLUS Bronze o Platinum. Tiene la forma y los conectores del " +
            "fabricante, así que no se puede cambiar por una ATX: no cabría y sus conectores no coinciden. Solo tiene dos cables, " +
            "alimentación del sistema y del procesador. Se sujeta con 3 tornillos y una lengüeta azul. En este equipo, para meterla " +
            "o sacarla hay que quitar antes el disipador.");
        MakePart(hdd, "HDD", "Type_HDD", "Disco duro de 3,5\" en su soporte",
            "Almacenamiento magnético de gran capacidad. El soporte azul lo sujeta con pines, sin tornillos. Va encima del módulo de " +
            "discos, con los conectores hacia la placa. Necesita dos cables, y los dos salen de la placa: datos SATA y alimentación.");
        MakePart(card, "Tarjeta perfil bajo", "Type_PCIeLowProfile", "Tarjeta de video de perfil bajo",
            "Tarjeta PCIe con bracket corto (perfil bajo, 79,2 mm): la única que cabe en una PC compacta. Se abre el pestillo de las " +
            "tarjetas, se mete en la ranura PCIe x16 con el bracket en la abertura trasera y se vuelve a cerrar el pestillo.");

        // ---------------- Cables ----------------
        BuildCables(new SffCables
        {
            root = root, board = board, inside = coverSocket,
            psuSocket = psuSocket, fanSocket = fanSocket, hddSocket = hddSocket,
            psu = psu, fan = fan, hdd = hdd, hddDesk = hddDesk,
            outSys = outSys, outCpu = outCpu, buttonOut = buttonOut,
        }, out SocketSystem[] ports);

        // ---------------- La tapa exige todo lo de dentro, con el módulo bajado y el pestillo cerrado ----------------
        coverSocket.requires.Add(moduleState);
        coverSocket.requires.Add(latchState);
        coverSocket.requires.AddRange(new[] { cpuSocket, coolerSocket, fanSocket, dimms[0], dimms[1], m2Socket, psuSocket, hddSocket, cardSocket });
        coverSocket.requires.AddRange(ports);

        // ---------------- Escena ----------------
        ConfigureScene(root, new SceneFrame
        {
            center = new Vector3(0.12f, 0.875f, 0.0f),
            wideShotDistance = 1.2f,
            wideShotPitch = 55f, // el equipo mide 9 cm de alto: desde 55° se ve su interior entero
            zoomLimits = new Vector2(0.10f, 2.0f),
            panLimitX = new Vector2(-0.7f, 0.8f),
            panLimitY = new Vector2(0.80f, 1.30f),
            panLimitZ = new Vector2(-0.4f, 0.4f),
            verticalAngleLimit = new Vector2(5f, 89f),
            horizontalAngleLimit = new Vector2(-90f, 90f),
            liftHeight = 0.04f,
            hoverClearance = 0.006f,
        });
        SetHudInstructions(
            "Primero quita la tapa: arrástrala a la mesa. Al terminar, vuelve a ponerla.",
            "Clic (sin arrastrar) en el módulo de discos o en el pestillo de las tarjetas: los abre o los cierra.",
            "Si algo no entra, el aviso dice qué hay que quitar o poner antes.");

        int required = 0;
        foreach (var s in root.GetComponentsInChildren<SocketSystem>()) if (s.required) required++;
        Log($"Lugares obligatorios: {required} · cara superior de la CPU a {cpuTop * 1000f:F1} mm del fondo del gabinete");

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        AssetDatabase.SaveAssets();
        return EndLog();
    }

    // ===================================================================== materiales y tipos

    static Material M(string name, Color c, bool metal) => EstiloMateriales.SolidMaterial(MatDir + name + ".mat", ColorDir, c, metal);

    static void MakeMaterials()
    {
        _paint = M("Chapa_Negra", new Color(0.10f, 0.10f, 0.11f), false);
        _inner = M("Chapa_Interior", new Color(0.66f, 0.67f, 0.69f), true);
        _bezel = M("Frontal_Plastico", new Color(0.15f, 0.15f, 0.16f), false);
        _slit = M("Rejilla", new Color(0f, 0f, 0f), false);
        _silver = M("Plateado", new Color(0.78f, 0.79f, 0.81f), true);
        _blue = M("Azul_Lengueta", new Color(0.12f, 0.42f, 0.85f), false);
        _pcb = M("PCB_Verde", new Color(0.07f, 0.30f, 0.17f), false);
        _black = M("Plastico_Negro", new Color(0.06f, 0.06f, 0.07f), false);
        _white = M("Plastico_Blanco", new Color(0.90f, 0.89f, 0.84f), false);
        _gold = M("Dorado", new Color(0.82f, 0.66f, 0.30f), true);
        _copper = M("Cobre", new Color(0.78f, 0.46f, 0.26f), true);
        _alu = M("Aluminio", new Color(0.80f, 0.81f, 0.83f), true);
        _brass = M("Laton", new Color(0.78f, 0.62f, 0.25f), true);
        _choke = M("Metal_Gris", new Color(0.40f, 0.41f, 0.43f), true);
        _psuMat = M("Fuente_Carcasa", new Color(0.30f, 0.31f, 0.33f), true);
        _label = M("Etiqueta", new Color(0.60f, 0.61f, 0.60f), false);
        _rubber = M("Goma", new Color(0.03f, 0.03f, 0.03f), false);
        _cardPcb = M("PCB_Negro", new Color(0.08f, 0.08f, 0.09f), false);
        _usbBlue = M("USB_Azul", new Color(0.10f, 0.25f, 0.75f), false);
        _dark = M("Conector_Negro", new Color(0.12f, 0.12f, 0.13f), false);
        _red = M("Conector_Rojo", new Color(0.78f, 0.12f, 0.10f), false);
        _cableRed = M("Cable_Rojo", new Color(0.70f, 0.08f, 0.08f), false);
        _cableGray = M("Cable_Gris", new Color(0.45f, 0.45f, 0.48f), false);
        _hddMetal = M("Disco_Metal", new Color(0.55f, 0.57f, 0.60f), true);
        _cableBlack = Resources.Load<Material>("Mat_Cable");
    }

    /// <summary>Tipos de conexión propios de este lab (los crea si faltan y fija sus textos: este constructor es su fuente).</summary>
    static void EnsureTypes()
    {
        EnsureType("Type_SFFCover", "Tapa SFF", "la tapa",
            "La tapa se pone al final, con todo instalado: se apoya sobre el equipo y se desliza hasta que el pestillo trasero hace clic.");
        EnsureType("Type_DriveModule", "Módulo de discos", "el módulo de discos", "");
        EnsureType("Type_CardLatch", "Pestillo de tarjetas", "el pestillo de las tarjetas", "");
        EnsureType("Type_PCIeLowProfile", "Tarjeta PCIe de perfil bajo", "la tarjeta de perfil bajo",
            "Va en la ranura PCIe x16 de la placa, con su bracket corto (79,2 mm) en la abertura trasera. Antes hay que abrir el pestillo de las tarjetas.");
        EnsureType("Type_PSU_SFF", "Fuente SFF (Dell)", "la fuente de alimentación (propia de Dell)",
            "Va detrás a la izquierda, con el enchufe hacia afuera. Es propia del fabricante: una fuente ATX no cabe ni tiene sus conectores.");
        EnsureType("Type_DellSysPower", "Alimentación del sistema (Dell)", "el conector de alimentación del sistema",
            "Es el cable de la fuente que alimenta la placa (en este equipo reemplaza al ATX de 24 pines). Va al conector de la placa que " +
            "queda junto a la fuente, delante.");
        EnsureType("Type_DellCpuPower", "Alimentación del procesador (Dell)", "el conector de alimentación del procesador",
            "Alimenta el procesador. Va al conector ATX_CPU de la placa, junto a la fuente y cerca del panel trasero.");
        EnsureType("Type_PowerButton", "Botón de encendido (PWR_SW)", "el cable del botón de encendido",
            "El cable del botón de encendido va al conector PWR_SW de la placa, cerca del frente.");
        EnsureType("Type_HeatsinkFan", "Ventilador del disipador", "el ventilador del disipador",
            "Se atornilla encima del disipador, que tiene que estar instalado antes.");
    }

    static void EnsureType(string asset, string label, string display, string hint)
    {
        string path = TypesDir + asset + ".asset";
        var t = AssetDatabase.LoadAssetAtPath<ConnectionTypeSO>(path);
        if (t == null)
        {
            t = ScriptableObject.CreateInstance<ConnectionTypeSO>();
            AssetDatabase.CreateAsset(t, path);
        }
        t.labelName = label;
        t.displayName = display;
        t.placementHint = hint;
        EditorUtility.SetDirty(t);
    }

    // ===================================================================== ayudantes

    /// <summary>Posición del pivote para que, con rotación <paramref name="rot"/>, la pieza apoye su base centrada en <paramref name="bottomCenter"/>.</summary>
    static Vector3 SeatAt(Transform t, Quaternion rot, Vector3 bottomCenter)
    {
        Bounds b = BoundsAt(t, rot);
        return bottomCenter - new Vector3(b.center.x, b.min.y, b.center.z);
    }

    /// <summary>Cilindro (sin colisionador) con su eje a lo largo de <paramref name="axis"/>.</summary>
    static Transform Disc(Transform parent, string name, Vector3 center, float radius, float thickness, Vector3 axis, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(center, Quaternion.FromToRotation(Vector3.up, axis));
        go.transform.localScale = new Vector3(radius * 2f, thickness * 0.5f, radius * 2f);
        go.GetComponent<MeshRenderer>().sharedMaterial = m;
        return go.transform;
    }

    static Transform PartRoot(string name, Transform root, Vector3 pos)
    {
        Transform t = NewChild(name, root);
        t.SetPositionAndRotation(pos, Quaternion.identity);
        return t;
    }

    static void AddHinge(Transform part, SocketSystem state, Vector3 pivot, Vector3 axis, float angle,
                         string openVerb, string closeVerb, string opened, string closed, Transform root)
    {
        Transform hinge = NewChild("Bisagra: " + part.name, root);
        hinge.SetPositionAndRotation(pivot, Quaternion.LookRotation(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.forward : Vector3.up));
        var h = part.gameObject.AddComponent<HingedPart>();
        h.closedState = state;
        h.hinge = hinge;
        h.openAngle = angle;
        h.openVerb = openVerb;
        h.closeVerb = closeVerb;
        h.openedText = opened;
        h.closedText = closed;
    }

    static void SetHudInstructions(params string[] lines)
    {
        var hud = Object.FindAnyObjectByType<AssemblyHudView>();
        if (hud == null) { Log("⚠ No hay AssemblyHudView en la escena"); return; }
        var so = new SerializedObject(hud);
        var arr = so.FindProperty("extraInstructions");
        arr.arraySize = lines.Length;
        for (int i = 0; i < lines.Length; i++) arr.GetArrayElementAtIndex(i).stringValue = lines[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ===================================================================== gabinete

    static void BuildChassis(Transform ch)
    {
        // Patas de goma
        foreach (var f in new[] { new Vector2(0.025f, 0.035f), new Vector2(W - 0.025f, 0.035f), new Vector2(0.025f, D - 0.025f), new Vector2(W - 0.025f, D - 0.025f) })
            Disc(ch, "Pata", P(f.x, FeetH * 0.5f, f.y), 0.008f, FeetH, Vector3.up, _rubber);

        S(ch, "Suelo", 0f, FeetH, BezelDepth, W, FloorTop, D, _inner);
        S(ch, "Lateral izquierdo", 0f, FeetH, BezelDepth, Sheet, InnerTop, D, _paint);
        S(ch, "Lateral derecho", W - Sheet, FeetH, BezelDepth, W, InnerTop, D, _paint);
        S(ch, "Pared frontal", Sheet, FloorTop, BezelDepth, W - Sheet, InnerTop, FrontInner, _inner);

        // Trasera: chapa con las aberturas de la fuente, el panel de E/S y las ranuras de expansión.
        Transform rear = NewChild("Trasera", ch);
        float z0 = RearInner, z1 = D;
        S(rear, "Borde izquierdo", 0f, FeetH, z0, 0.004f, InnerTop, z1, _paint);
        S(rear, "Bajo la fuente", 0.004f, FeetH, z0, 0.086f, 0.006f, z1, _paint);
        S(rear, "Sobre la fuente", 0.004f, 0.0835f, z0, 0.086f, InnerTop, z1, _paint);
        S(rear, "Entre fuente y E/S", 0.086f, FeetH, z0, 0.098f, InnerTop, z1, _paint);
        S(rear, "Bajo la E/S", 0.098f, FeetH, z0, 0.218f, BoardTop, z1, _paint);
        S(rear, "Sobre la E/S", 0.098f, 0.050f, z0, 0.218f, InnerTop, z1, _paint);
        for (int i = 0; i < 9; i++)
        {
            float x = 0.106f + i * 0.0115f;
            S(rear, "Rejilla trasera", x, 0.058f, z1, x + 0.006f, 0.084f, z1 + 0.0004f, _slit);
        }
        S(rear, "Entre E/S y ranuras", 0.218f, FeetH, z0, 0.2358f, InnerTop, z1, _paint);
        S(rear, "Bajo las ranuras", 0.2358f, FeetH, z0, 0.2745f, 0.0095f, z1, _paint);
        S(rear, "Sobre las ranuras", 0.2358f, 0.0870f, z0, 0.2745f, InnerTop, z1, _paint);
        S(rear, "Entre ranuras", 0.2542f, 0.0095f, z0, 0.2561f, 0.0870f, z1, _paint);
        S(rear, "Tapa de la ranura x4", 0.2561f, 0.0095f, z0 - 0.0006f, 0.2745f, 0.0870f, z1, _inner);
        S(rear, "Borde derecho", 0.2745f, FeetH, z0, W, InnerTop, z1, _paint);

        // Frontal de plástico
        Transform front = NewChild("Frontal", ch);
        S(front, "Frontal", 0f, FeetH, 0f, W, H, BezelDepth, _bezel);
        S(front, "Bahía de la unidad óptica (vacía)", 0.012f, 0.071f, -0.0006f, 0.140f, 0.083f, 0f, _black);
        S(front, "Franja plateada", 0.012f, 0.0655f, -0.0004f, 0.140f, 0.0665f, 0f, _silver);
        for (int i = 0; i < 9; i++)
        {
            float x = 0.152f + i * 0.0105f;
            S(front, "Rejilla frontal", x, 0.034f, -0.0004f, x + 0.0035f, 0.082f, 0f, _slit);
        }
        for (int i = 0; i < 4; i++)
        {
            float x = 0.152f + i * 0.0175f;
            S(front, "Puerto USB frontal", x, 0.016f, -0.0006f, x + 0.0125f, 0.0205f, 0f, i < 2 ? _usbBlue : _black);
        }
        Disc(front, "Toma de audio", P(0.232f, 0.0183f, -0.0003f), 0.0028f, 0.0006f, Vector3.back, _black);
        S(front, "Luz del disco", 0.264f, 0.046f, -0.0004f, 0.272f, 0.048f, 0f, _white);

        // Interior: soportes de la bisagra del módulo.
        Transform inner = NewChild("Interior", ch);
        foreach (float z in new[] { ModZ0 + 0.006f, ModZ1 - 0.010f })
            S(inner, "Soporte de la bisagra", Sheet, ModHinge - 0.006f, z, 0.007f, ModHinge + 0.003f, z + 0.004f, _inner);

        // Colisión: cajas (clic, y apoyo de las piezas al pasar por encima).
        Transform col = NewChild("Colisión", ch);
        Box(col, "Suelo", P(0f, 0f, BezelDepth), P(W, FloorTop, D));
        Box(col, "Lateral izquierdo", P(0f, 0f, BezelDepth), P(Sheet, InnerTop, D));
        Box(col, "Lateral derecho", P(W - Sheet, 0f, BezelDepth), P(W, InnerTop, D));
        Box(col, "Trasera", P(0f, 0f, RearInner), P(W, InnerTop, D));
        Box(col, "Frente", P(0f, 0f, 0f), P(W, H, FrontInner));
    }

    static Transform BuildCover(Transform root)
    {
        Transform t = PartRoot("Tapa", root, P(W * 0.5f, InnerTop, (BezelDepth + D) * 0.5f));
        S(t, "Chapa superior", 0f, InnerTop, BezelDepth, W, H, D, _paint);
        S(t, "Pestaña izquierda", -Sheet, 0.076f, BezelDepth + 0.004f, 0f, H, D, _paint);
        S(t, "Pestaña derecha", W, 0.076f, BezelDepth + 0.004f, W + Sheet, H, D, _paint);
        S(t, "Pestaña trasera", 0f, 0.080f, D, W, H, D + Sheet, _paint);
        S(t, "Pestillo de la tapa", 0.195f, 0.0835f, D + Sheet, 0.225f, 0.0905f, D + Sheet + 0.006f, _blue);
        for (int i = 0; i < 8; i++)
        {
            float z = 0.150f + i * 0.009f;
            S(t, "Rejilla de la tapa", 0.125f, H, z, 0.220f, H + 0.0003f, z + 0.004f, _slit);
        }
        return t;
    }

    // ===================================================================== placa base

    static void BuildBoard(Transform b)
    {
        float y0 = BoardBottom, y1 = BoardTop;
        S(b, "PCB", 0.092f, y0, 0.022f, 0.282f, y1, 0.284f, _pcb);
        S(b, "PCB (bajo el módulo)", 0.008f, y0, 0.022f, 0.092f, y1, 0.128f, _pcb);
        Box(b, "Colisión PCB", P(0.092f, y0, 0.022f), P(0.282f, y1, 0.284f));
        Box(b, "Colisión PCB (bajo el módulo)", P(0.008f, y0, 0.022f), P(0.092f, y1, 0.128f));
        foreach (var s in new[] { new Vector2(0.015f, 0.030f), new Vector2(0.100f, 0.030f), new Vector2(0.275f, 0.030f), new Vector2(0.100f, 0.275f), new Vector2(0.275f, 0.275f) })
            Disc(b, "Separador", P(s.x, (FloorTop + BoardBottom) * 0.5f, s.y), 0.0025f, BoardBottom - FloorTop, Vector3.up, _brass);

        // Socket LGA 1151 con su tapa metálica y la palanca.
        float cx = Cpu.x, cz = Cpu.y;
        S(b, "Socket LGA 1151", cx - 0.024f, y1, cz - 0.026f, cx + 0.024f, y1 + 0.0025f, cz + 0.026f, _black);
        S(b, "Marco del socket (izq.)", cx - 0.0225f, y1 + 0.0025f, cz - 0.0225f, cx - 0.0195f, y1 + 0.0036f, cz + 0.0225f, _silver);
        S(b, "Marco del socket (der.)", cx + 0.0195f, y1 + 0.0025f, cz - 0.0225f, cx + 0.0225f, y1 + 0.0036f, cz + 0.0225f, _silver);
        S(b, "Marco del socket (del.)", cx - 0.0195f, y1 + 0.0025f, cz - 0.0225f, cx + 0.0195f, y1 + 0.0036f, cz - 0.0195f, _silver);
        S(b, "Marco del socket (tras.)", cx - 0.0195f, y1 + 0.0025f, cz + 0.0195f, cx + 0.0195f, y1 + 0.0036f, cz + 0.0225f, _silver);
        S(b, "Palanca del socket", cx + 0.026f, y1, cz - 0.024f, cx + 0.0275f, y1 + 0.003f, cz + 0.022f, _silver);
        S(b, "Gancho de la palanca", cx + 0.0275f, y1, cz + 0.020f, cx + 0.031f, y1 + 0.003f, cz + 0.022f, _silver);

        // Cuatro ranuras DIMM (bajo el módulo de discos), con sus clips.
        for (int i = 0; i < 4; i++)
        {
            float z = DimmZ[i];
            S(b, "Ranura DIMM" + (i + 1), DimmX0, y1, z - 0.0034f, DimmX1, y1 + 0.0058f, z + 0.0034f, _black);
            S(b, "Clip DIMM" + (i + 1) + " (izq.)", DimmX0 - 0.004f, y1, z - 0.0034f, DimmX0, y1 + 0.009f, z + 0.0034f, _white);
            S(b, "Clip DIMM" + (i + 1) + " (der.)", DimmX1, y1, z - 0.0034f, DimmX1 + 0.004f, y1 + 0.009f, z + 0.0034f, _white);
        }

        // Ranura M.2 (bajo el borde del disipador) con su separador.
        S(b, "Ranura M.2", 0.111f, y1, M2Center.y - 0.011f, 0.117f, y1 + 0.0045f, M2Center.y + 0.011f, _black);
        S(b, "Almohadilla térmica del M.2", 0.120f, y1, M2Center.y - 0.009f, 0.190f, y1 + 0.0008f, M2Center.y + 0.009f, _choke);
        Disc(b, "Separador del M.2", P(0.196f, y1 + 0.0015f, M2Center.y), 0.0025f, 0.003f, Vector3.up, _brass);

        // Ranuras PCIe x16 (perfil bajo) y x4 abierta.
        S(b, "Ranura PCIe x16", SlotX16 - 0.0038f, y1, SlotZ0, SlotX16 + 0.0038f, y1 + 0.011f, SlotZ1, _black);
        S(b, "Traba de la ranura x16", SlotX16 - 0.0038f, y1, SlotZ0 - 0.004f, SlotX16 + 0.0038f, y1 + 0.012f, SlotZ0, _white);
        S(b, "Ranura PCIe x4", SlotX4 - 0.0038f, y1, 0.238f, SlotX4 + 0.0038f, y1 + 0.011f, SlotZ1, _black);
        Disc(b, "Pila de botón", P(0.236f, y1 + 0.0016f, 0.165f), 0.010f, 0.0032f, Vector3.up, _silver);

        // Conectores SATA (datos ×3) y de alimentación de los discos (en la placa: en este Dell la fuente no alimenta los discos).
        foreach (float x in new[] { 0.166f, 0.178f, 0.190f })
            S(b, "Conector SATA", x - 0.0055f, y1, 0.029f, x + 0.0055f, y1 + 0.009f, 0.035f, _black);
        S(b, "Conector de alimentación de los discos", 0.1995f, y1, 0.029f, 0.2125f, y1 + 0.008f, 0.035f, _white);

        // Cabezales: alimentación del sistema y del procesador, ventilador de la CPU y botón de encendido.
        S(b, "Conector alimentación del sistema", 0.0955f, y1, 0.1435f, 0.1045f, y1 + 0.009f, 0.1565f, _white);
        S(b, "Conector ATX_CPU", 0.1015f, y1, 0.2535f, 0.1105f, y1 + 0.009f, 0.2625f, _white);
        S(b, "Cabezal ventilador CPU", 0.2265f, y1, 0.1175f, 0.2375f, y1 + 0.006f, 0.1225f, _white);
        S(b, "Cabezal PWR_SW", 0.2655f, y1, 0.0845f, 0.2705f, y1 + 0.006f, 0.0955f, _black);

        // Chipset, reguladores y condensadores.
        S(b, "Disipador del chipset", 0.225f, y1, 0.049f, 0.247f, y1 + 0.007f, 0.071f, _alu);
        foreach (float z in new[] { 0.168f, 0.180f, 0.192f })
            S(b, "Bobina del regulador", 0.1115f, y1, z - 0.0045f, 0.1205f, y1 + 0.006f, z + 0.0045f, _choke);
        foreach (var c in new[] { new Vector2(0.125f, 0.240f), new Vector2(0.125f, 0.249f), new Vector2(0.134f, 0.240f), new Vector2(0.134f, 0.249f) })
            Disc(b, "Condensador", P(c.x, y1 + 0.004f, c.y), 0.003f, 0.008f, Vector3.up, _choke);

        // Panel trasero de E/S (asoma por la abertura de la trasera).
        float zi = 0.268f, zo = D - 0.0004f;
        S(b, "USB trasero 1", 0.104f, y1, zi, 0.118f, y1 + 0.0145f, zo, _silver);
        S(b, "USB trasero 2", 0.122f, y1, zi, 0.136f, y1 + 0.0145f, zo, _silver);
        S(b, "Interior USB 1", 0.106f, y1 + 0.002f, zo, 0.116f, y1 + 0.006f, zo + 0.0003f, _usbBlue);
        S(b, "Interior USB 2", 0.124f, y1 + 0.002f, zo, 0.134f, y1 + 0.006f, zo + 0.0003f, _usbBlue);
        S(b, "Red (RJ45)", 0.140f, y1, zi, 0.156f, y1 + 0.0135f, zo, _choke);
        S(b, "DisplayPort 1", 0.160f, y1, zi, 0.178f, y1 + 0.006f, zo, _black);
        S(b, "DisplayPort 2", 0.160f, y1 + 0.008f, zi, 0.178f, y1 + 0.014f, zo, _black);
        S(b, "Audio y serie", 0.182f, y1, zi, 0.212f, y1 + 0.010f, zo, _black);
    }

    // ===================================================================== bisagras

    static Transform BuildModule(Transform root)
    {
        Transform t = PartRoot("Módulo de discos", root, P((ModX0 + ModX1) * 0.5f, ModFloor, (ModZ0 + ModZ1) * 0.5f));
        S(t, "Bandeja", ModX0, ModFloor, ModZ0, ModX1, ModFloor + Sheet, ModZ1, _inner);
        S(t, "Pared de la bisagra", ModX0, ModFloor, ModZ0, ModX0 + Sheet, ModHinge, ModZ1, _inner);
        S(t, "Eje de la bisagra", ModX0 - 0.001f, ModHinge - 0.002f, ModZ0 + 0.004f, ModX0 + 0.003f, ModHinge + 0.002f, ModZ1 - 0.004f, _choke);
        S(t, "Borde derecho", ModX1 - Sheet, ModFloor, ModZ0, ModX1, ModFloor + 0.004f, ModZ1, _inner);
        S(t, "Tope trasero", ModX0, ModFloor, ModZ1 - Sheet, ModX1, ModFloor + 0.006f, ModZ1, _inner);
        S(t, "Lengüeta de liberación", ModX1, ModFloor - 0.002f, 0.030f, ModX1 + 0.006f, ModFloor + 0.010f, 0.050f, _blue);
        return t;
    }

    static Transform BuildLatch(Transform root)
    {
        Transform t = PartRoot("Pestillo de las tarjetas", root, P(0.260f, 0.0852f, 0.283f));
        S(t, "Pestillo", 0.233f, 0.0835f, 0.276f, 0.287f, 0.0870f, 0.2895f, _blue);
        S(t, "Lengüeta metálica", 0.254f, 0.0820f, 0.2735f, 0.266f, 0.0880f, 0.2765f, _silver);
        return t;
    }

    // ===================================================================== piezas hechas con primitivas

    /// <summary>Disipador de bajo perfil: base de cobre, aletas, 4 tornillos cautivos y conducto. Pivote: centro de su base.</summary>
    static Transform BuildCooler(Transform root, Vector3 baseCenter)
    {
        Transform t = PartRoot("Disipador", root, baseCenter);
        Vector3 c = baseCenter;
        Transform Q(string n, float x0, float y0, float z0, float x1, float y1, float z1, Material m) =>
            Solid(t, n, c + new Vector3(x0, y0, z0), c + new Vector3(x1, y1, z1), m);
        Q("Base de cobre", -0.020f, 0f, -0.020f, 0.020f, 0.005f, 0.020f, _copper);
        Q("Placa de las aletas", -0.046f, 0.005f, -0.046f, 0.046f, 0.008f, 0.046f, _alu);
        for (int i = 0; i < 17; i++)
        {
            float x = -0.044f + i * 0.0055f;
            Q("Aleta", x - 0.0004f, 0.008f, -0.044f, x + 0.0004f, 0.036f, 0.044f, _alu);
        }
        foreach (var s in new[] { new Vector2(-0.040f, -0.040f), new Vector2(0.040f, -0.040f), new Vector2(-0.040f, 0.040f), new Vector2(0.040f, 0.040f) })
        {
            Disc(t, "Tornillo cautivo", c + new Vector3(s.x, 0.022f, s.y), 0.0028f, 0.028f, Vector3.up, _silver);
            Disc(t, "Cabeza del tornillo", c + new Vector3(s.x, 0.0375f, s.y), 0.0042f, 0.003f, Vector3.up, _blue);
        }
        Q("Conducto (izq.)", -0.0475f, 0.030f, -0.0475f, -0.0445f, 0.040f, 0.0475f, _black);
        Q("Conducto (der.)", 0.0445f, 0.030f, -0.0475f, 0.0475f, 0.040f, 0.0475f, _black);
        Q("Conducto (del.)", -0.0445f, 0.030f, -0.0475f, 0.0445f, 0.040f, -0.0445f, _black);
        Q("Conducto (tras.)", -0.0445f, 0.030f, 0.0445f, 0.0445f, 0.040f, 0.0475f, _black);
        return t;
    }

    /// <summary>Fuente de 200 W propia de Dell. Pivote: centro de su base. Sus dos cables salen por la cara que mira a la placa (+X).</summary>
    static Transform BuildPsu(Transform root, out Transform outSys, out Transform outCpu)
    {
        Vector3 c = P(PsuCenter.x, FloorTop, PsuCenter.y);
        Transform t = PartRoot("Fuente de alimentación", root, c);
        float hx = PsuSize.x * 0.5f, hz = PsuSize.z * 0.5f, h = PsuSize.y;
        Transform Q(string n, float x0, float y0, float z0, float x1, float y1, float z1, Material m) =>
            Solid(t, n, c + new Vector3(x0, y0, z0), c + new Vector3(x1, y1, z1), m);
        Q("Carcasa", -hx, 0f, -hz, hx, h, hz, _psuMat);
        Q("Etiqueta", -hx + 0.010f, h, -hz + 0.020f, hx - 0.010f, h + 0.0003f, -hz + 0.080f, _label);
        Disc(t, "Rejilla del ventilador", c + new Vector3(hx + 0.0002f, h * 0.5f, 0.015f), 0.031f, 0.0006f, Vector3.right, _slit);
        Disc(t, "Aro del ventilador", c + new Vector3(hx + 0.0004f, h * 0.5f, 0.015f), 0.008f, 0.0006f, Vector3.right, _choke);
        Q("Toma de corriente", -0.026f, 0.028f, hz, -0.006f, 0.052f, hz + 0.0012f, _black);
        Q("Luz de diagnóstico", 0.006f, 0.050f, hz, 0.010f, 0.054f, hz + 0.0012f, _white);
        Q("Lengüeta azul", 0.012f, 0.070f, hz, 0.032f, 0.0795f, hz + 0.006f, _blue);
        Q("Pasacables del sistema", hx, 0.030f, -0.051f, hx + 0.005f, 0.040f, -0.039f, _black);
        Q("Pasacables del procesador", hx, 0.030f, 0.014f, hx + 0.005f, 0.040f, 0.026f, _black);
        outSys = Anchor("Salida alimentación del sistema", t, c + new Vector3(hx + 0.005f, 0.035f, -0.045f), Vector3.right);
        outCpu = Anchor("Salida alimentación del procesador", t, c + new Vector3(hx + 0.005f, 0.035f, 0.020f), Vector3.right);
        return t;
    }

    /// <summary>Tarjeta de video de perfil bajo, de pie en su ranura. Pivote: borde inferior de los contactos, en el centro.</summary>
    static Transform BuildLowProfileCard(Transform root, Vector3 pivot)
    {
        Transform t = PartRoot("Tarjeta de perfil bajo", root, pivot);
        Vector3 c = pivot;
        Transform Q(string n, float x0, float y0, float z0, float x1, float y1, float z1, Material m) =>
            Solid(t, n, c + new Vector3(x0, y0, z0), c + new Vector3(x1, y1, z1), m);
        Q("Placa (PCB)", -0.0008f, 0.0075f, -0.090f, 0.0008f, 0.064f, 0.0545f, _cardPcb);
        Q("Contactos PCIe x16", -0.0009f, 0f, -0.0445f, 0.0009f, 0.0075f, 0.0445f, _gold);
        Q("Bracket de perfil bajo", -0.0092f, -0.0095f, 0.0545f, 0.0092f, 0.0692f, 0.0555f, _silver);
        Q("Pestaña del bracket", -0.0092f, 0.0682f, 0.0555f, 0.0092f, 0.0692f, 0.0595f, _silver);
        Q("HDMI", -0.0065f, 0.018f, 0.0555f, 0.0065f, 0.0225f, 0.0575f, _black);
        Q("DisplayPort", -0.0065f, 0.036f, 0.0555f, 0.0065f, 0.0405f, 0.0575f, _black);
        Q("Disipador de la tarjeta", -0.012f, 0.014f, -0.070f, -0.0008f, 0.058f, 0.020f, _black);
        Disc(t, "Ventilador de la tarjeta", c + new Vector3(-0.0122f, 0.036f, -0.025f), 0.019f, 0.0008f, Vector3.left, _choke);
        Q("Memoria 1", 0.0008f, 0.030f, -0.060f, 0.0018f, 0.044f, -0.046f, _black);
        Q("Memoria 2", 0.0008f, 0.030f, -0.040f, 0.0018f, 0.044f, -0.026f, _black);
        return t;
    }

    /// <summary>Soporte azul del disco (rieles con pines a los lados), en la pose de la mesa.</summary>
    static void AddHddBracket(Transform hdd, Bounds b)
    {
        foreach (float side in new[] { -1f, 1f })
        {
            float z0 = side < 0 ? b.min.z - 0.0015f : b.max.z;
            var rail = Solid(hdd.parent, "Soporte del disco", new Vector3(b.min.x + 0.010f, b.min.y + 0.004f, z0),
                             new Vector3(b.max.x - 0.010f, b.min.y + 0.016f, z0 + 0.0015f), _blue);
            rail.SetParent(hdd, true);
        }
    }

    static Transform BuildPowerButton(Transform root, out Transform cableOut)
    {
        Transform t = PartRoot("Botón de encendido", root, P(0.268f, 0.062f, FrontInner));
        S(t, "Módulo del botón", 0.258f, 0.052f, FrontInner, 0.278f, 0.072f, FrontInner + 0.010f, _black);
        Disc(t, "Aro del botón", P(0.268f, 0.062f, -0.0004f), 0.0065f, 0.0008f, Vector3.back, _silver);
        Disc(t, "Botón", P(0.268f, 0.062f, -0.0009f), 0.0052f, 0.0008f, Vector3.back, _black);
        cableOut = Anchor("Salida del cable del botón", t, P(0.268f, 0.058f, FrontInner + 0.010f), Vector3.forward);
        return t;
    }

    // ===================================================================== cables

    class SffCables : CableRig
    {
        public Transform root, board, psu, fan, hdd, outSys, outCpu, buttonOut;
        public SocketSystem inside, psuSocket, fanSocket, hddSocket;
        public Bounds hddDesk;
    }

    static SocketSystem BoardPort(SffCables c, string name, string type, float x, float z, bool longAlongX, Vector3 size, float headerH,
                                  params SocketSystem[] requires)
    {
        Vector3 longAxis = longAlongX ? Vector3.right : Vector3.forward;
        var s = Port(name, c.board, P(x, BoardTop + headerH + size.z * 0.5f - 0.003f, z), Quaternion.LookRotation(Vector3.up, longAxis), type, size, requires);
        s.requiresEmpty.Add(c.inside);
        return s;
    }

    /// <summary>
    /// Seis cables. De la fuente: alimentación del sistema y del procesador (sus conectores reposan en la mesa y, con la
    /// fuente instalada, sobre la placa junto a ella). De la placa al disco: datos SATA y alimentación (en este Dell la
    /// alimentación de los discos sale de la placa). Del botón de encendido a PWR_SW, y del ventilador al cabezal de la CPU.
    /// Los que reposan dentro del equipo se arquean hacia arriba en vez de combarse: así no atraviesan la placa.
    /// </summary>
    static void BuildCables(SffCables c, out SocketSystem[] ports)
    {
        c.cables = NewChild("Cables", c.root);
        Transform deskRest = NewChild("Superficie de apoyo (mesa)", c.root);
        deskRest.position = new Vector3(0f, DeskY, 0f);
        Transform boardRest = NewChild("Superficie de apoyo (placa)", c.root);
        boardRest.position = new Vector3(0f, DeskY + BoardTop, 0f);
        c.fixedParent = c.board;
        c.stiffness = 0.015f; // cables cortos: tramo recto corto a la salida de cada conector
        Vector3 arch = Vector3.up;

        // --- Fuente ---
        c.rest = deskRest;
        Vector3 psuDesk = WorldBounds(c.psu).center;
        var sysPort = BoardPort(c, "Puerto alimentación del sistema", "Type_DellSysPower", 0.100f, 0.150f, false, PlugSys, 0.009f, c.psuSocket);
        Cable(c, "Cable de alimentación del sistema", c.outSys, "Type_DellSysPower", PlugSys, _dark, _cableBlack, CableSpline.Profile.Round, 0.0045f, 0.10f,
            new Vector3(psuDesk.x + 0.080f, 0f, psuDesk.z - 0.060f), Vector3.left, c.psuSocket,
            Home("Reposo alimentación del sistema", c.psuSocket.transform, P(0.104f, BoardTop + PlugSys.x * 0.5f, 0.205f), Vector3.left),
            "Cable de alimentación del sistema",
            "Lleva la energía de la fuente a la placa base. Es un conector propio de Dell, más pequeño que el ATX de 24 pines de una PC " +
            "normal: por eso no sirve una fuente cualquiera.",
            looseSlack: arch);

        var cpuPort = BoardPort(c, "Puerto ATX_CPU", "Type_DellCpuPower", 0.106f, 0.258f, false, PlugCpu, 0.009f, c.psuSocket);
        Cable(c, "Cable de alimentación del procesador", c.outCpu, "Type_DellCpuPower", PlugCpu, _dark, _cableBlack, CableSpline.Profile.Round, 0.004f, 0.10f,
            new Vector3(psuDesk.x + 0.080f, 0f, psuDesk.z + 0.030f), Vector3.left, c.psuSocket,
            Home("Reposo alimentación del procesador", c.psuSocket.transform, P(0.104f, BoardTop + PlugCpu.x * 0.5f, 0.232f), Vector3.left),
            "Cable de alimentación del procesador",
            "Alimenta el procesador con 12 V. Va al conector ATX_CPU, junto a la fuente. Tiene menos pines que el del sistema y una " +
            "forma distinta, para que no se puedan confundir.",
            looseSlack: arch);

        // --- Ventilador del disipador ---
        Bounds fanB = WorldBounds(c.fan);
        Transform fanOut = Anchor("Salida del cable del ventilador", c.fan, new Vector3(fanB.max.x - 0.006f, fanB.center.y, fanB.min.z + 0.008f), Vector3.right);
        var fanPort = BoardPort(c, "Puerto ventilador CPU (FAN_CPU)", "Type_CPUFan", 0.232f, 0.120f, true, PlugFan, 0.006f, c.fanSocket);
        Cable(c, "Cable del ventilador del disipador", fanOut, "Type_CPUFan", PlugFan, _dark, _cableBlack, CableSpline.Profile.Round, 0.0018f, 0.10f,
            new Vector3(fanB.max.x + 0.035f, 0f, fanB.min.z + 0.004f), Vector3.left, c.fanSocket,
            Home("Reposo cable del ventilador", c.fanSocket.transform, P(0.215f, BoardTop + PlugFan.x * 0.5f, 0.108f), Vector3.forward),
            "Cable del ventilador del disipador",
            "Va al cabezal del ventilador de la CPU. La placa vigila que gire y, si se detiene, avisa o apaga el equipo para proteger " +
            "el procesador.",
            looseSlack: arch);

        // --- De la placa al disco ---
        c.rest = boardRest;
        Bounds hb = c.hddDesk; // pose de la mesa: los conectores quedan en el extremo -X
        Quaternion hddPortRot = Quaternion.LookRotation(Vector3.left, Vector3.forward);
        var sataPower = MakeSocket("Puerto alimentación SATA (disco)", c.hdd,
            new Vector3(hb.min.x - PlugSataPower.z * 0.5f + 0.003f, hb.min.y + 0.007f, hb.center.z + 0.012f), hddPortRot, "Type_SATAPower");
        sataPower.requires.Add(c.hddSocket);
        sataPower.requiresEmpty.Add(c.inside);
        AddPortMarker(sataPower, PlugSataPower);
        var sataData = MakeSocket("Puerto datos SATA (disco)", c.hdd,
            new Vector3(hb.min.x - PlugSataData.z * 0.5f + 0.003f, hb.min.y + 0.007f, hb.center.z - 0.020f), hddPortRot, "Type_SATAData");
        sataData.requires.Add(c.hddSocket);
        sataData.requiresEmpty.Add(c.inside);
        AddPortMarker(sataData, PlugSataData);

        Quaternion upAlongX = Quaternion.LookRotation(Vector3.up, Vector3.right);
        StaticBlock("Conector datos SATA (lado de la placa)", c.board, P(0.166f, BoardTop + 0.009f + 0.002f, 0.032f), upAlongX, PlugSataData, _red);
        CaseCable(c, "Cable de datos SATA", Anchor("Salida datos SATA (placa)", c.board, P(0.166f, BoardTop + 0.016f, 0.032f), Vector3.up),
            "Type_SATAData", PlugSataData, _red, _cableRed, 0.0045f, 0.10f,
            Home("Reposo datos SATA", c.board, P(0.168f, BoardTop + PlugSataData.x * 0.5f, 0.080f), Vector3.back),
            "Cable de datos SATA",
            "Transmite los datos entre el disco y la placa base (hasta 6 Gb/s). Sale del conector SATA 0 de la placa y va al disco.",
            looseSlack: arch, profile: CableSpline.Profile.Flat, flatHalfThickness: 0.001f);

        StaticBlock("Conector alimentación de los discos (lado de la placa)", c.board, P(0.206f, BoardTop + 0.008f + 0.002f, 0.032f), upAlongX,
            new Vector3(0.006f, 0.013f, 0.010f), _dark);
        CaseCable(c, "Cable de alimentación de los discos", Anchor("Salida alimentación de los discos (placa)", c.board, P(0.206f, BoardTop + 0.015f, 0.032f), Vector3.up),
            "Type_SATAPower", PlugSataPower, _dark, _cableBlack, 0.007f, 0.10f,
            Home("Reposo alimentación SATA", c.board, P(0.200f, BoardTop + PlugSataPower.x * 0.5f, 0.080f), Vector3.back),
            "Cable de alimentación de los discos",
            "Sale de la placa base, no de la fuente: en este equipo Dell la placa reparte la energía a las unidades de almacenamiento. " +
            "En el disco entra en el conector de alimentación SATA, el ancho en forma de L.",
            looseSlack: arch, profile: CableSpline.Profile.Flat, flatHalfThickness: 0.0015f);

        // --- Botón de encendido ---
        var buttonPort = BoardPort(c, "Puerto PWR_SW", "Type_PowerButton", 0.268f, 0.090f, false, PlugButton, 0.006f);
        CaseCable(c, "Cable del botón de encendido", c.buttonOut, "Type_PowerButton", PlugButton, _dark, _cableGray, 0.002f, 0.10f,
            Home("Reposo cable del botón", c.board, P(0.262f, BoardTop + PlugButton.x * 0.5f, 0.055f), Vector3.back),
            "Cable del botón de encendido",
            "Une el botón de encendido del frente con el conector PWR_SW de la placa. Sin él, el equipo no se puede encender.",
            looseSlack: arch);

        RebuildCables(c);
        ports = new[] { sysPort, cpuPort, fanPort, sataPower, sataData, buttonPort };
        Log("Cables: 6");
    }
}
