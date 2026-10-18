# Prepara un modelo de Sketchfab (glTF) para el lab ATX y lo exporta a FBX para Unity.
# blender -b --factory-startup --disable-autoexec --python blender_prep.py -- <modelo> <ruta.gltf> <carpeta_salida> <informe.txt>
#
# Qué hace (según el modelo): hornea transformaciones, agrupa mallas en piezas con nombre,
# simplifica lo que sobra, escala a tamaño real (1 u = 1 m), separa piezas móviles (panel lateral),
# da grosor a paredes de una sola cara, pone el pivote en el centro de cada pieza y exporta FBX
# con los ejes de Unity (Y arriba) y las texturas copiadas junto al FBX.
import math, os, sys, bpy, bmesh
from mathutils import Vector, Matrix

args = sys.argv[sys.argv.index("--") + 1:]
model, src, out_dir, report_path = args
os.makedirs(out_dir, exist_ok=True)
report = []

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)


# ----------------------------------------------------------------- utilidades
def tris(o):
    o.data.calc_loop_triangles()
    return len(o.data.loop_triangles)


def meshes_under(root):
    res, stack = [], [root]
    while stack:
        n = stack.pop()
        if n.type == "MESH":
            res.append(n)
        stack.extend(n.children)
    return res


def bake_all():
    """Deja todas las mallas en la raíz con su transformación aplicada; borra los vacíos."""
    for o in [o for o in bpy.data.objects if o.type == "MESH"]:
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
        if o.data.users > 1:
            o.data = o.data.copy()
        o.data.transform(o.matrix_world)
        o.matrix_world = Matrix.Identity(4)
    for o in [o for o in bpy.data.objects if o.type != "MESH"]:
        bpy.data.objects.remove(o, do_unlink=True)


def decimate(o, ratio):
    if ratio >= 1.0:
        return
    m = o.modifiers.new("dec", "DECIMATE")
    m.ratio = ratio
    m.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier=m.name)


def join(objs, name):
    objs = [o for o in objs if o and o.name in bpy.data.objects]
    if not objs:
        return None
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    res = bpy.context.view_layer.objects.active
    res.name = name
    res.data.name = name
    return res


def scale_all(factor, pivot=Vector((0, 0, 0))):
    m = Matrix.Translation(pivot) @ Matrix.Scale(factor, 4) @ Matrix.Translation(-pivot)
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.data.transform(m)


def rotate_all(axis, degrees):
    m = Matrix.Rotation(math.radians(degrees), 4, axis)
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.data.transform(m)


def bounds(objs):
    mn = Vector((1e9, 1e9, 1e9)); mx = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for v in o.data.vertices:
            w = o.matrix_world @ v.co
            mn = Vector(map(min, mn, w)); mx = Vector(map(max, mx, w))
    return mn, mx


def recenter_world(objs, anchor="center"):
    """Lleva el conjunto para que su centro (o su base) quede en el origen del mundo."""
    mn, mx = bounds(objs)
    c = (mn + mx) / 2
    if anchor == "base":
        c.z = mn.z
    for o in objs:
        o.data.transform(Matrix.Translation(-c))


def origin_to_center(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS")


def solidify(o, thickness):
    m = o.modifiers.new("grosor", "SOLIDIFY")
    m.thickness = thickness
    m.offset = -1.0
    m.use_even_offset = True
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier=m.name)


def separate_faces(o, predicate, new_name):
    """Separa a un objeto nuevo las caras cuyo centro cumple predicate(centro, normal)."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    picked = [f for f in bm.faces if predicate(f.calc_center_median(), f.normal)]
    if not picked:
        bm.free()
        return None
    new_bm = bmesh.new()
    vmap = {}
    for f in picked:
        vs = []
        for v in f.verts:
            if v not in vmap:
                vmap[v] = new_bm.verts.new(v.co)
            vs.append(vmap[v])
        nf = new_bm.faces.new(vs)
        nf.material_index = f.material_index
    uv_src = bm.loops.layers.uv.active
    if uv_src:
        uv_dst = new_bm.loops.layers.uv.new("UVMap")
        for f, nf in zip(picked, new_bm.faces):
            for l, nl in zip(f.loops, nf.loops):
                nl[uv_dst].uv = l[uv_src].uv
    bmesh.ops.delete(bm, geom=picked, context="FACES")
    bm.to_mesh(o.data); bm.free()
    me = bpy.data.meshes.new(new_name)
    new_bm.to_mesh(me); new_bm.free()
    for mat in o.data.materials:
        me.materials.append(mat)
    no = bpy.data.objects.new(new_name, me)
    bpy.context.scene.collection.objects.link(no)
    return no


def export(filename, objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    path = os.path.join(out_dir, filename)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", bake_space_transform=True,
        axis_forward="-Z", axis_up="Y", use_mesh_modifiers=True,
        mesh_smooth_type="FACE", path_mode="COPY", embed_textures=False,
        bake_anim=False, add_leaf_bones=False)
    total = sum(tris(o) for o in objs)
    report.append(f"EXPORTADO {filename}: {len(objs)} piezas, {total} tris")
    for o in objs:
        mn, mx = bounds([o])
        s = mx - mn
        report.append(f"   {o.name:22s} tris={tris(o):6d} tamaño(mm)=({s.x*1000:.0f}, {s.y*1000:.0f}, {s.z*1000:.0f}) "
                      f"centro(mm)=({o.location.x*1000:.1f}, {o.location.y*1000:.1f}, {o.location.z*1000:.1f}) mats={len(o.data.materials)}")


# ----------------------------------------------------------------- por modelo
if model == "pc_case":
    # Ya viene en metros. Frente = +X, trasera = -X (recorte E/S), panel removible = lado -Y.
    groups = {o.name: o for o in bpy.data.objects}
    fan_a = meshes_under(groups["SM_fan_blade_5"]) + meshes_under(groups["SM_fan_rim_4"])
    fan_b = meshes_under(groups["SM_fan_blade.002_10"]) + meshes_under(groups["SM_fan_rim.002_9"])
    feet = [m for k in ("SM_foot_3", "SM_foot.001_6", "SM_foot.002_7", "SM_foot.003_8") for m in meshes_under(groups[k])]
    shell = meshes_under(groups["SM_PC_case_0"])
    bake_all()
    for o in fan_a + fan_b:
        if tris(o) > 2000:
            decimate(o, 0.08)
    chasis = join(shell + feet, "Chasis")
    # Panel lateral: el panel enmarcado del lado -Y (cara + sus cantos), dentro de su marco.
    panel = separate_faces(chasis, lambda c, n: c.y <= -0.1885 and -0.1845 <= c.x <= 0.1195 and 0.0085 <= c.z <= 0.3925, "PanelLateral")
    solidify(chasis, 0.0015)
    if panel:
        solidify(panel, 0.0015)
    f1 = join(fan_a, "VentiladorGabinete_1")
    f2 = join(fan_b, "VentiladorGabinete_2")
    parts = [p for p in (chasis, panel, f1, f2) if p]
    recenter_world(parts, anchor="base")
    for p in parts:
        origin_to_center(p)
    export("Gabinete.fbx", parts)

elif model == "motherboard_components":
    groups = {o.name: o for o in bpy.data.objects}
    sets = {
        "PlacaBase": meshes_under(groups["MotherBoard"]),
        "CPU": meshes_under(groups["CPU"]),
        "SSD_M2": meshes_under(groups["M2"]),
        "RAM_1": meshes_under(groups["RAM"]),
        "RAM_2": meshes_under(groups["RAM1"]),
        "RAM_3": meshes_under(groups["RAM2"]),
        "RAM_4": meshes_under(groups["RAM3"]),
    }
    bake_all()
    for o in sets["PlacaBase"]:
        if tris(o) > 800:
            decimate(o, 0.2)          # condensadores (1.860 tris c/u) y pestillo de la CPU (5.148)
    parts = [join(objs, name) for name, objs in sets.items()]
    # PCB: 9,207 × 7,322 → ATX 305 × 244 mm
    scale_all(0.0332)
    # La placa estaba de pie (componentes hacia -Y): tumbarla con los componentes hacia +Z.
    rotate_all(Vector((1, 0, 0)), -90)
    pcb_parts = [parts[0]]
    mn, mx = bounds(pcb_parts)
    # Origen del conjunto: centro de la placa, a la altura de su cara inferior
    shift = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
    for p in parts:
        p.data.transform(Matrix.Translation(-shift))
    for p in parts:
        origin_to_center(p)
    export("PlacaBase.fbx", parts)

elif model == "gpu_rtx3060ti":
    bake_all()
    gpu = join([o for o in bpy.data.objects if o.type == "MESH"], "GPU")
    scale_all(0.0504)                 # cubierta 4,8 → 242 mm (RTX 3060 Ti FE)
    recenter_world([gpu])
    origin_to_center(gpu)
    export("GPU.fbx", [gpu])

elif model == "cpu_cooler":
    bake_all()
    cooler = join([o for o in bpy.data.objects if o.type == "MESH"], "Disipador")
    scale_all(0.005913)               # torre de 158 mm
    recenter_world([cooler], anchor="base")
    origin_to_center(cooler)
    export("Disipador.fbx", [cooler])

elif model == "ssd_m2_980pro":
    bake_all()
    ssd = join([o for o in bpy.data.objects if o.type == "MESH"], "SSD_M2_980Pro")
    scale_all(0.019984)               # M.2 2280 = 80 mm
    recenter_world([ssd])
    origin_to_center(ssd)
    export("SSD_980Pro.fbx", [ssd])

elif model == "fan_sl120":
    bake_all()
    fan = join([o for o in bpy.data.objects if o.type == "MESH"], "Ventilador120")
    scale_all(0.13266)                # 120 mm
    recenter_world([fan])
    origin_to_center(fan)
    export("Ventilador120.fbx", [fan])

open(report_path, "w", encoding="utf-8").write("\n".join(report) + "\n")
