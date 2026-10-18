// Vuelca la jerarquía de un laboratorio generado (todos sus objetos, transforms y campos serializados de cada componente)
// a un archivo de texto, para comparar antes y después de tocar LabBuilderKit o un constructor: si el volcado es idéntico,
// el cambio no alteró la escena. Las referencias a objetos salen por su ruta, no por su id (cambia al reconstruir).
// Uso: copiar a Temp/, cambiar RAIZ y SALIDA, y ejecutar con
//   unity command eval_file --file Temp/volcar.cs --result-only --project-path "C:\Users\devin\SimuladorArqComp_UNERMB"
// Después: diff de los dos archivos. (EditorJsonUtility no sirve en Unity 6.6: saca las referencias de escena como 0.)
string RAIZ = "PC (Lab SFF)";
string SALIDA = "Temp/volcado.txt";
var root = UnityEngine.GameObject.Find(RAIZ);
if (root == null) return "No está " + RAIZ + " en la escena abierta";
System.Func<UnityEngine.Object, string> Ruta = o => {
    if (o == null) return "null";
    var t = o is UnityEngine.Component c ? c.transform : o is UnityEngine.GameObject g ? g.transform : null;
    if (t == null) return UnityEditor.AssetDatabase.GetAssetPath(o) + ":" + o.name;
    var p = t.name; while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
    return (o is UnityEngine.Component ? o.GetType().Name + "@" : "") + p;
};
var sb = new System.Text.StringBuilder();
foreach (var t in root.GetComponentsInChildren<UnityEngine.Transform>(true)) {
    sb.AppendLine(Ruta(t.gameObject) + " capa=" + t.gameObject.layer + " pos=" + t.localPosition.ToString("F5") + " rot=" + t.localRotation.ToString("F5") + " esc=" + t.localScale.ToString("F5"));
    foreach (var c in t.GetComponents<UnityEngine.Component>()) {
        if (c is UnityEngine.Transform) continue;
        var line = new System.Text.StringBuilder("   " + c.GetType().Name + ":");
        var so = new UnityEditor.SerializedObject(c);
        var it = so.GetIterator();
        bool enter = true;
        while (it.Next(enter)) {
            enter = it.propertyType == UnityEditor.SerializedPropertyType.Generic;
            if (it.name == "m_Script" || it.name == "m_EditorClassIdentifier") continue;
            string v;
            switch (it.propertyType) {
                case UnityEditor.SerializedPropertyType.ObjectReference: v = Ruta(it.objectReferenceValue); break;
                case UnityEditor.SerializedPropertyType.Float: v = it.floatValue.ToString("G6"); break;
                case UnityEditor.SerializedPropertyType.Integer: v = it.intValue.ToString(); break;
                case UnityEditor.SerializedPropertyType.Boolean: v = it.boolValue.ToString(); break;
                case UnityEditor.SerializedPropertyType.String: v = it.stringValue; break;
                case UnityEditor.SerializedPropertyType.Enum: v = it.enumValueIndex.ToString(); break;
                case UnityEditor.SerializedPropertyType.Vector3: v = it.vector3Value.ToString("F5"); break;
                case UnityEditor.SerializedPropertyType.Vector2: v = it.vector2Value.ToString("F5"); break;
                case UnityEditor.SerializedPropertyType.Quaternion: v = it.quaternionValue.ToString("F5"); break;
                case UnityEditor.SerializedPropertyType.Color: v = it.colorValue.ToString(); break;
                case UnityEditor.SerializedPropertyType.Generic: continue;
                default: v = it.propertyType.ToString(); break;
            }
            line.Append(" " + it.propertyPath + "=" + v);
        }
        sb.AppendLine(line.ToString());
    }
}
System.IO.File.WriteAllText(SALIDA, sb.ToString());
return RAIZ + ": " + root.GetComponentsInChildren<UnityEngine.Transform>(true).Length + " objetos → " + SALIDA;
