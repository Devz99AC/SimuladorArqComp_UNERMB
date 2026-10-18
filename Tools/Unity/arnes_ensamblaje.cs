// Arnés para probar un laboratorio en Play SIN ratón (simulate_pointer no llega al juego).
// Hace lo mismo que InteractionManager: BeginDrag → mover la pieza encima del lugar → FindTarget → EndDrag.
// Uso: copiar este archivo a Temp/, añadir al final las llamadas y un return, y ejecutarlo con
//   unity command eval_file --file Temp/arnes.cs --result-only --project-path "C:\Users\devin\SimuladorArqComp_UNERMB"
// Ejemplo de final:
//   s.AppendLine(Try("CPU", "Lugar CPU (socket LGA 1151)"));
//   return s.ToString();
// ⚠️ Un intento por pieza y por llamada (o separados ~1 s): la instalación anima 0,3 s y un lugar que
// "requires" otro no lo ve completo hasta que termina. Encadenar arrastres en el mismo fotograma da errores
// que un estudiante nunca provocaría.
// Las líneas de resultado dicen a qué lugar apuntó la pieza, si se acepta (con el motivo si no) y si el
// cable no alcanza (ClampToReach recortó la posición).
// También: Toggle("Módulo de discos") = clic en una bisagra o un tornillo cautivo; Remove("Tapa") = retirarla a la mesa;
// Progress() y Errors() (contador de errores y el último motivo).
var s = new System.Text.StringBuilder();
var im = UnityEngine.Object.FindAnyObjectByType<InteractionManager>();
var T = typeof(InteractionManager);
var F = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
System.Func<string, UnityEngine.GameObject> Find = n => {
    foreach (var t in UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(UnityEngine.FindObjectsInactive.Exclude))
        if (t.name == n) return t.gameObject;
    return null;
};
System.Func<string, string, string> Try = (partName, socketName) => {
    var pgo = Find(partName); var sgo = Find(socketName);
    if (pgo == null || sgo == null) return "NO ENCONTRADO " + partName + " / " + socketName;
    var part = pgo.GetComponent<PCPart>(); var sock = sgo.GetComponent<SocketSystem>();
    var hit = new UnityEngine.RaycastHit(); hit.point = part.transform.position;
    T.GetMethod("BeginDrag", F).Invoke(im, new object[] { part, hit });
    var c = part.GetComponent<IDragConstraint>();
    var want = sock.SnapPosition + UnityEngine.Vector3.up * 0.03f;
    var got = c != null ? c.ConstrainDragPosition(want) : want;
    part.transform.SetPositionAndRotation(got, sock.SnapRotation);
    CompatResult cr; var tgt = SocketSystem.FindTarget(part, out cr);
    T.GetMethod("EndDrag", F).Invoke(im, null);
    string reach = (got - want).magnitude > 0.001f ? "  (no alcanza: falta " + (got - want).magnitude.ToString("F3") + " m)" : "";
    return partName + " -> " + socketName + ": " + (tgt ? tgt.name : "null") + " ok=" + cr.Ok + (cr.Ok ? "" : " [" + cr.Message + " · " + cr.Reason + "]") + reach;
};
System.Func<string> Progress = () => GameManager.Instance == null ? "sin GameManager"
    : "progreso " + GameManager.Instance.InstalledCount + "/" + GameManager.Instance.TotalRequired;
// Contador de errores (cada intento rechazado con motivo) y el último registrado.
System.Func<string> Errors = () => {
    if (GameManager.Instance == null) return "sin GameManager";
    var e = GameManager.Instance.Errors;
    return "errores " + e.Count + (e.Count > 0 ? " (último: " + e[e.Count - 1].part + " · " + e[e.Count - 1].message + " · " + e[e.Count - 1].reason + ")" : "");
};
System.Func<int> ErrorCount = () => GameManager.Instance == null ? 0 : GameManager.Instance.Errors.Count;
// Clic sobre una bisagra, un pestillo o un tornillo cautivo (HingedPart), por el mismo camino que InteractionManager
// (ToggleHinge: aviso y, si se rechaza, error al contador).
System.Func<string, string> Toggle = name => {
    var go = Find(name);
    if (go == null) return "NO ENCONTRADO " + name;
    var h = go.GetComponent<HingedPart>();
    if (h == null) return name + " no tiene HingedPart";
    bool wasClosed = h.IsClosed; int before = ErrorCount();
    T.GetMethod("ToggleHinge", F).Invoke(im, new object[] { h });
    if (ErrorCount() > before) return "clic " + name + ": rechazado [" + Errors() + "]";
    return "clic " + name + ": " + (wasClosed ? "abierto" : "cerrado");
};
// Agarrar una pieza instalada y soltarla en vacío (vuelve a su reposo), por el mismo camino que InteractionManager
// (CheckDragStart comprueba si se puede retirar: si no, aviso y error al contador).
System.Func<string, string> Remove = name => {
    var go = Find(name);
    if (go == null) return "NO ENCONTRADO " + name;
    var part = go.GetComponent<PCPart>();
    var hit = new UnityEngine.RaycastHit(); hit.point = part.transform.position;
    int before = ErrorCount();
    T.GetField("_pressPart", F).SetValue(im, part);
    T.GetField("_pressHit", F).SetValue(im, hit);
    T.GetField("_pressPos", F).SetValue(im, new UnityEngine.Vector2(-9999f, -9999f)); // el «ratón» ya se movió: es un arrastre
    T.GetMethod("CheckDragStart", F).Invoke(im, null);
    if (T.GetField("_currentPart", F).GetValue(im) == null)
        return "retirar " + name + ": bloqueado" + (ErrorCount() > before ? " [" + Errors() + "]" : "");
    part.transform.position += new UnityEngine.Vector3(0f, 0.3f, -0.6f); // lejos de cualquier lugar
    T.GetMethod("EndDrag", F).Invoke(im, null);
    return "retirar " + name + ": ok";
};
