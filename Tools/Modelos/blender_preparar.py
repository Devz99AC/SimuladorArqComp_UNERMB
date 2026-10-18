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


def solidify(o, thickness, even=True):
    m = o.modifiers.new("grosor", "SOLIDIFY")
    m.thickness = thickness
    m.offset = -1.0
    m.use_even_offset = even  # False en mallas con caras casi degeneradas: el grosor "parejo" se dispara en ellas
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.modifier_apply(modifier=m.name)


def recalc_normals(o):
    """Normales coherentes y hacia fuera (hay modelos con media malla invertida). En una malla abierta,
    recalc_face_normals las deja coherentes pero puede elegir "hacia dentro": se comprueba con el centro."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    center = sum((v.co for v in bm.verts), Vector()) / max(1, len(bm.verts))
    outward = sum((f.calc_center_median() - center).dot(f.normal) * f.calc_area() for f in bm.faces)
    if outward < 0:
        bmesh.ops.reverse_faces(bm, faces=bm.faces)
    report.append(f"normales de {o.name}: {'invertidas' if outward < 0 else 'ya hacia fuera'} (índice {outward:.3f})")
    bm.to_mesh(o.data); bm.free()


def face_normals_towards(o, direction):
    """Da la vuelta a las caras cuya normal no mira hacia direction (una placa casi plana: la tapa inferior)."""
    bm = bmesh.new()
    bm.from_mesh(o.data)
    bad = [f for f in bm.faces if f.normal.dot(direction) < 0]
    if bad:
        bmesh.ops.reverse_faces(bm, faces=bad)
    bm.to_mesh(o.data); bm.free()


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

# ----------------------------------------------------------------- lab 3 (laptop)
elif model == "dell_latitude_5400":
    # Yolala3D, abierta a ~105°. Ancho = Y (2,92), fondo = X (frente -0,79 → bisagra 1,229), arriba = Z;
    # la base va de z 0,339 (fondo) a 0,438 (teclado). Se usa CERRADA y BOCA ABAJO: se borra lo que queda
    # dentro (teclado, textos, bisel y panel), se cierra la pantalla, se voltea y se separa la tapa inferior
    # (las caras de abajo de la base) de la carcasa. Se exporta con los ejes del laboratorio: ancho = X de Unity,
    # bisagra hacia +Z de Unity (al fondo, como en las figuras del manual de Dell), arriba = Y.
    keep_base = ["Object_14", "Object_20"]
    keep_ports = ["Object_10", "Object_18", "Object_24", "Object_8", "Object_454",
                  "Object_380", "Object_384", "Object_412", "Object_414", "Object_442", "Object_444"]
    keep_lid = ["Object_363", "Object_362"]
    keep = set(keep_base + keep_ports + keep_lid)
    for o in [o for o in bpy.data.objects if o.type == "MESH" and o.name not in keep]:
        bpy.data.objects.remove(o, do_unlink=True)
    bake_all()
    for o in [o for o in bpy.data.objects if o.type == "MESH"]:
        a, b = bounds([o])
        report.append(f"   {o.name:12s} min=({a.x:.3f}, {a.y:.3f}, {a.z:.3f}) max=({b.x:.3f}, {b.y:.3f}, {b.z:.3f})")
    get = lambda names: [bpy.data.objects[n] for n in names if n in bpy.data.objects]
    base, ports, lid = get(keep_base), get(keep_ports), get(keep_lid)

    # 1) Cerrar la pantalla: su eje largo (de la bisagra al borde superior) pasa a apuntar hacia el frente (-X),
    #    y se apoya sobre el teclado con el borde de la bisagra en la trasera de la base.
    back = bpy.data.objects["Object_363"]
    vs = [back.matrix_world @ v.co for v in back.data.vertices]
    zmin = min(v.z for v in vs); zmax = max(v.z for v in vs)
    lo = [v for v in vs if v.z < zmin + 0.02]; hi = [v for v in vs if v.z > zmax - 0.02]
    d = sum(hi, Vector()) / len(hi) - sum(lo, Vector()) / len(lo)
    ang = math.atan2(d.z, d.x) + math.pi
    rot = Matrix.Rotation(ang, 4, "Y")
    for o in lid:
        o.data.transform(rot)
    bmn, bmx = bounds(base)
    lmn, lmx = bounds(lid)
    shift = Vector((bmx.x - lmx.x, 0.0, bmx.z - lmn.z + 0.001))
    for o in lid:
        o.data.transform(Matrix.Translation(shift))
    report.append(f"pantalla girada {math.degrees(ang):.1f}°; base z {bmn.z:.3f}–{bmx.z:.3f}")

    # 2) Tapa inferior: las caras de abajo de la base (fondo plano y chaflanes, ~3 mm a escala real). La mitad del
    #    fondo trae las normales invertidas: se recalculan antes de elegir.
    shell = join(base, "Carcasa")
    recalc_normals(shell)
    cover = separate_faces(shell, lambda c, n: abs(n.z) > 0.3 and c.z < bmn.z + 0.022, "TapaInferior")
    face_normals_towards(cover, Vector((0, 0, -1)))
    solidify(shell, 0.009, even=False)   # ~1 mm a escala real: la carcasa ya no está cerrada por abajo
    solidify(cover, 0.012, even=False)   # ~1,3 mm, hacia dentro
    shell = join([shell] + ports + lid, "Carcasa")

    # 3) Boca abajo y con los ejes del laboratorio: (x, y, z) → (-y, -x, -z) (giro propio, sin espejo).
    flip = Matrix(((0, -1, 0, 0), (-1, 0, 0, 0), (0, 0, -1, 0), (0, 0, 0, 1)))
    for o in (shell, cover):
        o.data.transform(flip)

    # 4) Tamaño real del manual de Dell (Setup and Specifications): 323,05 × 216 mm; alto 19,6 (frente) a 20,85 (atrás).
    mn, mx = bounds([shell, cover])
    size = mx - mn
    sx, sy = 0.32305 / size.x, 0.216 / size.y
    sz = 0.0202 / size.z
    sm = Matrix.Diagonal((sx, sy, sz, 1.0))
    for o in (shell, cover):
        o.data.transform(sm)
    report.append(f"escala x={sx:.5f} y={sy:.5f} z={sz:.5f} (sin ajustar el alto sería {size.z * (sx + sy) / 2 * 1000:.1f} mm)")
    recenter_world([shell, cover], anchor="base")

    # 5) El hueco del touchpad sobresale hacia el interior (llega a 14,6 mm) y atravesaría la batería: se quita. Son caras de
    #    la cara de arriba del reposamanos, que con la laptop cerrada nunca se ven. Solo en su zona (delante, en el centro; en
    #    Unity x de -0,03 a 0,06 y z de -0,095 a -0,03): la pared de la bisagra queda a la vista y no se toca.
    inside = lambda c: -0.06 < c.x < 0.03 and 0.03 < c.y < 0.095 and 0.0095 < c.z < 0.0190
    bm = bmesh.new(); bm.from_mesh(shell.data)
    kill = [f for f in bm.faces if inside(f.calc_center_median())]
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bm.to_mesh(shell.data); bm.free()
    report.append(f"caras del interior quitadas: {len(kill)}")
    for p in (shell, cover):
        origin_to_center(p)
    export("Laptop.fbx", [shell, cover])

elif model == "sodimm_ddr4_8gb":
    # Viene de canto: grosor = X, largo = Y (2,223 → 69,6 mm), ancho = Z (contactos abajo). Se deja acostada:
    # largo = X, ancho = Y (contactos hacia -Y de Blender = +Z de Unity), grosor = Z.
    bake_all()
    ram = join([o for o in bpy.data.objects if o.type == "MESH"], "SODIMM")
    scale_all(0.6960 / 22.23)
    ram.data.transform(Matrix(((0, 1, 0, 0), (0, 0, 1, 0), (1, 0, 0, 0), (0, 0, 0, 1))))
    recenter_world([ram])
    origin_to_center(ram)
    export("SODIMM.fbx", [ram])

elif model == "laptop_fan":
    # Dos ventiladores de tipo blower; se usa el que no tiene cable (".004", 77 × 97 mm) llevado a ~62 mm
    # (tamaño aproximado del de una laptop de 14"; el manual de Dell no lo da) y simplificado.
    names = [o.name for o in bpy.data.objects if o.type == "MESH" and ".004" in o.name]
    for o in [o for o in bpy.data.objects if o.type == "MESH" and o.name not in names]:
        bpy.data.objects.remove(o, do_unlink=True)
    bake_all()
    for o in [o for o in bpy.data.objects if o.type == "MESH"]:
        decimate(o, 0.12)
    fan = join([o for o in bpy.data.objects if o.type == "MESH"], "VentiladorLaptop")
    mn, mx = bounds([fan])
    scale_all(0.062 / max(mx.x - mn.x, mx.y - mn.y))
    recenter_world([fan], anchor="base")
    origin_to_center(fan)
    export("VentiladorLaptop.fbx", [fan])

open(report_path, "w", encoding="utf-8").write("\n".join(report) + "\n")
