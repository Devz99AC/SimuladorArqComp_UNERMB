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
    return partName + " -> " + socketName + ": " + (tgt ? tgt.name : "null") + " ok=" + cr.Ok + (cr.Ok ? "" : " [" + cr.Reason + "]") + reach;
};
