# Inspección de un glTF en Blender (sin interfaz): jerarquía, triángulos, tamaño y materiales.
# blender -b --factory-startup --disable-autoexec --python blender_inspect.py -- <ruta.gltf> <salida.txt>
import sys, bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
src, out = args[0], args[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)

dg = bpy.context.evaluated_depsgraph_get()
lines = []
total = 0
gmin = Vector((1e9, 1e9, 1e9)); gmax = Vector((-1e9, -1e9, -1e9))


def depth(o):
    d = 0
    while o.parent:
        d += 1; o = o.parent
    return d


def walk(o):
    global total, gmin, gmax
    info = ""
    if o.type == "MESH":
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        me.calc_loop_triangles()
        tris = len(me.loop_triangles)
        total += tris
        corners = [o.matrix_world @ Vector(c) for c in o.bound_box]
        mn = Vector((min(c.x for c in corners), min(c.y for c in corners), min(c.z for c in corners)))
        mx = Vector((max(c.x for c in corners), max(c.y for c in corners), max(c.z for c in corners)))
        gmin = Vector(map(min, gmin, mn)); gmax = Vector(map(max, gmax, mx))
        size = mx - mn
        mats = ",".join(m.name for m in o.data.materials if m)
        info = f" MESH tris={tris} tamaño(m)=({size.x:.3f},{size.y:.3f},{size.z:.3f}) mats=[{mats}]"
        ev.to_mesh_clear()
    s = o.scale
    lines.append("  " * depth(o) + f"{o.name} [{o.type}] escala=({s.x:.3g},{s.y:.3g},{s.z:.3g}){info}")
    for c in sorted(o.children, key=lambda c: c.name):
        walk(c)


for o in sorted([o for o in bpy.data.objects if o.parent is None], key=lambda o: o.name):
    walk(o)

size = gmax - gmin
head = [f"ARCHIVO: {src}",
        f"TOTAL tris={total}  objetos={len(bpy.data.objects)}  mallas={sum(1 for o in bpy.data.objects if o.type == 'MESH')}",
        f"CAJA TOTAL (m, Blender X/Y/Z con Z arriba): ({size.x:.4f}, {size.y:.4f}, {size.z:.4f})  min={tuple(round(v,3) for v in gmin)}",
        "MATERIALES: " + ", ".join(m.name for m in bpy.data.materials),
        "IMÁGENES: " + ", ".join(f"{i.name} {i.size[0]}x{i.size[1]}" for i in bpy.data.images),
        "JERARQUÍA:"]
open(out, "w", encoding="utf-8").write("\n".join(head + lines) + "\n")
