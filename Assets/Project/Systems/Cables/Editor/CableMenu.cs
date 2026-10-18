using UnityEditor;
using UnityEngine;

/// <summary>
/// Menús de editor para montar cables sin cableado manual:
///  - GameObject ▸ Simulador ▸ Cable (spline): crea el conjunto completo (cable + inicio + conector
///    arrastrable) en la vista de escena o bajo el objeto seleccionado.
///  - GameObject ▸ Simulador ▸ Puerto de cable: crea un SocketSystem con su snapPoint, listo para
///    asignarle el tipo de conector que acepta.
/// Después solo hay que: colocar el Inicio sobre la fuente, asignar el connectionType del conector
/// y del puerto (p. ej. Type_ATX24), y orientar los ejes azules (Z+) hacia donde sale el cable.
/// </summary>
public static class CableMenu
{
    private const int InteractableLayer = 3;

    [MenuItem("GameObject/Simulador/Cable (spline)", false, 10)]
    private static void CreateCable(MenuCommand command)
    {
        var parent = command.context as GameObject;
        float size = HandleSize(parent);

        var root = new GameObject("Cable");
        Undo.RegisterCreatedObjectUndo(root, "Crear cable");
        Place(root, parent);
        root.layer = InteractableLayer;

        // Inicio fijo (p. ej. sobre la fuente). Z+ = hacia donde sale el cable.
        var start = new GameObject("Inicio").transform;
        start.SetParent(root.transform, false);
        start.localPosition = Vector3.zero;
        start.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);

        // Conector arrastrable: un bloque simple hasta tener el modelo real.
        var plug = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plug.name = "Conector";
        plug.layer = InteractableLayer;
        plug.transform.SetParent(root.transform, false);
        plug.transform.localPosition = new Vector3(size * 2f, 0f, 0f);
        plug.transform.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);
        plug.transform.localScale = new Vector3(size * 0.5f, size * 0.25f, size * 0.35f);

        var plugPart = plug.AddComponent<PCPart>();
        plugPart.isDraggable = true;
        plugPart.partName = "Conector";
        plugPart.title = "Conector";
        plug.AddComponent<ObjectHighlighter>();

        // El cable en sí (info al clic derecho sobre el tubo, no arrastrable).
        var cable = root.AddComponent<CableSpline>();
        cable.startAnchor = start;
        cable.endAnchor = plug.transform;
        cable.radius = size * 0.08f;
        cable.flatHalfThickness = size * 0.02f;
        cable.stiffness = size * 0.3f;
        cable.samplesPerUnit = 12f / size;

        var material = Resources.Load<Material>("Mat_Cable");
        if (material != null) root.GetComponent<MeshRenderer>().sharedMaterial = material;
        root.AddComponent<MeshCollider>();

        var info = root.AddComponent<PCPart>();
        info.isDraggable = false;
        info.partName = "Cable";
        info.title = "Cable";
        root.AddComponent<ObjectHighlighter>();

        var plugLogic = plug.AddComponent<CablePlug>();
        plugLogic.cable = cable;

        Selection.activeGameObject = root;
    }

    [MenuItem("GameObject/Simulador/Puerto de cable", false, 11)]
    private static void CreatePort(MenuCommand command)
    {
        var parent = command.context as GameObject;
        float size = HandleSize(parent);

        var port = new GameObject("Puerto");
        Undo.RegisterCreatedObjectUndo(port, "Crear puerto de cable");
        Place(port, parent);

        var box = port.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(size * 0.5f, size * 0.25f, size * 0.35f);

        var snap = new GameObject("SnapPoint").transform;
        snap.SetParent(port.transform, false);

        var socket = port.AddComponent<SocketSystem>();
        socket.snapPoint = snap;

        Selection.activeGameObject = port;
    }

    private static void Place(GameObject go, GameObject parent)
    {
        if (parent != null)
        {
            GameObjectUtility.SetParentAndAlign(go, parent);
        }
        else if (SceneView.lastActiveSceneView != null)
        {
            go.transform.position = SceneView.lastActiveSceneView.pivot;
        }
    }

    /// <summary>Tamaño de referencia según el zoom de la vista: el cable nace a una escala razonable en cualquier escena.</summary>
    private static float HandleSize(GameObject parent)
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null) return 0.1f;
        Vector3 at = parent != null ? parent.transform.position : view.pivot;
        float camDistance = Vector3.Distance(view.camera.transform.position, at);
        return Mathf.Max(0.01f, camDistance * 0.04f);
    }
}
