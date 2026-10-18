# Reduce texturas grandes en su sitio: color ≤ 2048 px; normal/emisión/metal-rugosidad ≤ 1024 px.
# blender -b --factory-startup --disable-autoexec --python blender_downscale.py -- <carpeta_raiz>
import os, sys, bpy

root = sys.argv[sys.argv.index("--") + 1]
for dp, _, files in os.walk(root):
    for f in files:
        if not f.lower().endswith((".png", ".jpg", ".jpeg")):
            continue
        path = os.path.join(dp, f)
        low = f.lower()
        limit = 1024 if any(k in low for k in ("normal", "emissive", "metallic", "roughness")) else 2048
        img = bpy.data.images.load(path)
        w, h = img.size
        if max(w, h) > limit:
            k = limit / max(w, h)
            before = os.path.getsize(path)
            img.scale(int(w * k), int(h * k))
            img.filepath_raw = path
            img.save()
            print(f"REDUCIDA {f}: {w}x{h} → {int(w*k)}x{int(h*k)}  {before/1048576:.1f} → {os.path.getsize(path)/1048576:.1f} MB")
        bpy.data.images.remove(img)
