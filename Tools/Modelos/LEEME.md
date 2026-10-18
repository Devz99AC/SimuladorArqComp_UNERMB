# Tubería de modelos 3D (Sketchfab → Blender → Unity)

Scripts con los que se descargaron y prepararon los modelos de `Assets/Project/Content/Models/Lab_ATX/`. Sirven para repetir el proceso o añadir modelos nuevos. Blender: `C:\Program Files\Blender Foundation\Blender 4.5\blender.exe` (no está en el PATH).

1. **Descargar:** `python -I sketchfab_descargar.py check`, y después `python -I sketchfab_descargar.py get <carpeta>`.
   - Lee el token de la API de Sketchfab desde `%USERPROFILE%\.sketchfab_token\sketchfab.txt`. **Nunca pongas el token en el repositorio.**
   - Descarga el glTF de cada modelo de la lista `MODELS` a `<carpeta>/<slug>/gltf`, con su `licencia.json`.
   - Los originales viven fuera del repo: `C:\Users\devin\SimuladorArqComp_ArtSource\Sketchfab\`.
2. **Inspeccionar:** `blender -b --factory-startup --disable-autoexec --python blender_inspeccionar.py -- <modelo.gltf> <informe.txt>`. Muestra la jerarquía, los triángulos, el tamaño en metros y los materiales.
3. **Preparar y exportar:** `blender -b --factory-startup --disable-autoexec --python blender_preparar.py -- <slug> <modelo.gltf> <carpeta_salida> <informe.txt>`.
   - Cada `slug` tiene su receta: escala a tamaño real, agrupa en piezas con nombre, simplifica lo pesado, separa el panel lateral, da grosor a las paredes y pone los pivotes.
   - Exporta FBX con ejes de Unity. En Unity: Blender (x, y, z) → Unity (−x, z, −y).
4. **Reducir texturas:** `blender ... --python blender_reducir_texturas.py -- <carpeta>`. Reduce el color a 2048 px como máximo y normal/emisión/metal a 1024 px.

`--disable-autoexec` es obligatorio: los archivos descargados son de terceros y no deben ejecutar scripts.

Al añadir un modelo de terceros, regístralo en `Docs/Creditos_Modelos.md` (licencia CC BY: hay que citar al autor).
