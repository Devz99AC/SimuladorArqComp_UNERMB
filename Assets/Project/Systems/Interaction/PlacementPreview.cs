using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// "Fantasmas" translúcidos de la pieza que se arrastra, colocados en cada lugar libre y
/// compatible (AUDITORIA §6.4: descubribilidad). El lugar al que apunta ahora mismo se ve
/// más intenso: el estudiante sabe dónde va a quedar ANTES de soltar.
///
/// El fantasma es un clon de la pieza reducido a mallas (sin colliders ni scripts) con
/// <c>Mat_GhostPreview</c>. Se construye dentro de un padre inactivo para que ningún
/// Awake/OnEnable del clon llegue a ejecutarse.
/// </summary>
public class PlacementPreview
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private readonly Material _material;
    private readonly Color _idle;
    private readonly Color _active;
    private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
    private readonly List<Ghost> _ghosts = new List<Ghost>();

    private struct Ghost
    {
        public SocketSystem socket;
        public GameObject root;
        public Renderer[] renderers;
    }

    public PlacementPreview(Material material, Color idle, Color active)
    {
        _material = material;
        _idle = idle;
        _active = active;
    }

    /// <summary>Crea un fantasma en cada socket libre que aceptaría <paramref name="part"/>.</summary>
    public void Show(PCPart part)
    {
        Clear();
        if (_material == null || part == null) return;

        foreach (var socket in SocketSystem.All)
        {
            if (socket == null || !socket.Evaluate(part).Ok) continue;

            GameObject ghost = BuildGhost(part);
            ghost.transform.SetPositionAndRotation(socket.SnapPosition, socket.SnapRotation);
            _ghosts.Add(new Ghost { socket = socket, root = ghost, renderers = ghost.GetComponentsInChildren<Renderer>() });
        }
        Highlight(null);
    }

    /// <summary>Intensifica el fantasma de <paramref name="target"/> (null = todos en reposo).</summary>
    public void Highlight(SocketSystem target)
    {
        foreach (var g in _ghosts)
        {
            _block.SetColor(BaseColorId, g.socket == target ? _active : _idle);
            foreach (var r in g.renderers) r.SetPropertyBlock(_block);
        }
    }

    public void Clear()
    {
        foreach (var g in _ghosts)
            if (g.root != null) Object.Destroy(g.root);
        _ghosts.Clear();
    }

    private GameObject BuildGhost(PCPart part)
    {
        var holder = new GameObject("GhostHolder");
        holder.SetActive(false);

        GameObject clone = Object.Instantiate(part.gameObject, holder.transform, false);
        clone.name = part.name + " (vista previa)";
        StripToVisuals(clone);

        clone.transform.SetParent(null, false);
        clone.transform.localScale = part.transform.lossyScale;
        Object.Destroy(holder);

        foreach (var t in clone.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = 2; // Ignore Raycast

        foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
        {
            int slots = SubMeshCount(r);
            var mats = new Material[slots];
            for (int i = 0; i < slots; i++) mats[i] = _material;
            r.sharedMaterials = mats; // también descarta el outline que pudiera tener la pieza
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        return clone;
    }

    private static int SubMeshCount(Renderer r)
    {
        Mesh mesh = null;
        if (r is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
        else
        {
            var filter = r.GetComponent<MeshFilter>();
            if (filter != null) mesh = filter.sharedMesh;
        }
        return mesh != null ? Mathf.Max(1, mesh.subMeshCount) : Mathf.Max(1, r.sharedMaterials.Length);
    }

    /// <summary>
    /// Deja solo Transform + mallas. Primero los scripts, luego el resto, y en cada objeto del ÚLTIMO al
    /// primero: un componente con [RequireComponent] siempre se añade después del que exige
    /// (p. ej. CablePlug después de PCPart), así que nunca se intenta quitar uno del que otro depende.
    /// </summary>
    private static void StripToVisuals(GameObject root)
    {
        var objects = root.GetComponentsInChildren<Transform>(true);
        for (int pass = 0; pass < 2; pass++)
        {
            foreach (var t in objects)
            {
                Component[] components = t.GetComponents<Component>();
                for (int i = components.Length - 1; i >= 0; i--)
                {
                    Component c = components[i];
                    if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer) continue;
                    bool isScript = c is MonoBehaviour;
                    if (pass == 0 ? isScript : !isScript) Object.DestroyImmediate(c);
                }
            }
        }
    }
}
