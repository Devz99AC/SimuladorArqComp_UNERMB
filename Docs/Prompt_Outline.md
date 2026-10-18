# Prompt: contorno de selección claro y eficiente

> Pega este documento entero al empezar la sesión. Está escrito para Claude Code trabajando en este repositorio con el editor de Unity abierto (rama `labs-practicos`).

---

## Tu tarea

Rehacer el **contorno (outline) que marca la pieza** en los laboratorios de ensamblaje: al pasar el ratón, al arrastrar y al rechazar un encaje. Tiene que ser:

- **Claro:** del mismo grosor en pantalla para una pieza de 2 mm (un conector de antena de la laptop) que para una de 30 cm (la placa ATX), y una sola silueta por pieza, sin líneas internas.
- **Eficiente:** sin coste cuando no hay nada resaltado, sin crear basura (GC) en cada hover y sin cambiar materiales de los renderers.

Afecta a las cuatro escenas que usan `InteractionManager`: `Lab_ATX`, `Lab_SFF`, `Lab_Laptop` y `AssemblyLab`.

## Cómo funciona hoy (lo que hay que sustituir)

- `Systems/Interaction/ObjectHighlighter.cs` (en cada pieza) añade un material extra al final de `sharedMaterials` de cada renderer hijo y lo quita al salir.
- El material es un **casco invertido** (`Content/Shaders and Materials/URPOutline.shader`, `Cull Front`): infla la malla `_OutlineWidth = 0,005` (5 mm en el mundo) a lo largo de sus normales.
  - `Mat_Outline_Naranja` (#FF8000) es el hover; `Mat_Outline_Cyan` (#00FFE6), el arrastre.
  - `InteractionManager.Init` crea en tiempo de ejecución una copia roja del de arrastre para el rechazo (`_rejectMaterial`, `_OutlineColor`).
- Quién lo usa: `InteractionManager.HandleHover`, `SetHighlight` / `ClearHighlight`, `BeginDrag` (arrastre), `DragUpdate` (rechazo) y `HandleRightRelease` (la ficha mantiene el hover). Los materiales son campos serializados (`hoverMaterial`, `dragMaterial`) en las cuatro escenas.

**Problemas medidos:**
1. **Grosor fijo en el mundo:** 5 mm es más que un tornillo cautivo de la laptop (5 mm), un conector de antena (2,2 mm) o el pestillo ZIF (0,5 mm). En la vista general del lab ATX (a 1,3 m), en cambio, se queda en 2 o 3 píxeles.
2. **Primitivas rotas:** las piezas de los labs 2 y 3 son cubos de Unity, con normales por cara (24 vértices). El casco se abre en placas sueltas en las esquinas.
3. **Varios submeshes:** un material de más solo se dibuja sobre el último submesh. Las piezas FBX con varios materiales salen con el contorno incompleto: la carcasa de la laptop (7), el SSD (6), la SO-DIMM (2) y la placa ATX.
4. **Piezas compuestas:** cada renderer hijo lleva su propio casco. La tapa inferior de la laptop (más de 100 cajas: rejillas, patas, tornillos) y el chasis SFF salen llenos de líneas internas, con más de 100 draw calls extra por hover.
5. **Detrás de una pared no se ve** (`ZTest LEqual`): una pieza dentro del gabinete ATX, tapada por sus paredes, pierde el contorno.
6. **Colores que se confunden:**
   - El cian del arrastre se parece al cian de los fantasmas de colocación (`InteractionManager.ghostIdleColor` = 0,35/0,85/1).
   - El naranja del hover se parece al de las señales de puerto (`CablePortMarker`, naranja que late).
7. **Cambiar `sharedMaterials`** crea arrays en cada cambio y saca a los renderers del lote del SRP Batcher.

## Antes de tocar nada

1. Lee `CLAUDE.md` (§1, §4, §9 sobre el CLI y §12) y los archivos de arriba.
2. Comprueba la rama (`labs-practicos`) y que `unity status --project-path "C:\Users\devin\SimuladorArqComp_UNERMB"` dé `ready`.
3. El render pipeline:
   - **URP 17.6 en Unity 6000.6, solo Render Graph.**
   - Asset activo: `Assets/3D/URP/New Universal Render Pipeline Asset 1.asset`, con un único renderer `…Asset 1_Renderer.asset` (Forward). Hoy no tiene **ninguna** Renderer Feature, ni la textura de profundidad activada.
   - Ese renderer lo usan todas las escenas, también los menús.
4. Consulta la API de Render Graph de URP 17.6 (`RecordRenderGraph`, `UniversalResourceData`, listas de renderers con filtro por *rendering layer*, pase a pantalla completa) en la documentación o en los ejemplos del paquete. No la escribas de memoria.

## Pregunta al usuario al empezar (una sola tanda, con AskUserQuestion)

1. **Grosor.** ¿3 px (recomendado: se nota sin tapar las piezas de la laptop), 2 px (más fino) o 4 px? Siempre constante en pantalla, escalado con la resolución (referencia 1080p).
2. **A través de las paredes.** Cuando la pieza está tapada por otra (dentro del gabinete o bajo otra pieza), ¿se ve el contorno tenue (recomendado: unos 35 % de opacidad) o solo la parte visible?
3. **Colores por estado.** Propuesta recomendada:
   - hover en el naranja de la marca (#FF4500, el de `variables.uss`), más sólido que el de las señales de puerto;
   - arrastre en blanco cálido, que no se confunde con el cian ni el verde de los fantasmas;
   - rechazo en el rojo de error (#E5484D, `--state-error`).

   ¿O mantener los de hoy (naranja, cian y rojo)?

## Cómo resolverlo (recomendado; si encuentras un bloqueo, explícalo y propón el plan B)

**Contorno en espacio de pantalla con una Renderer Feature de URP (Render Graph):**

1. **Marcar sin tocar materiales.**
   - `ObjectHighlighter` deja de cambiar `sharedMaterials`: activa un bit de `renderer.renderingLayerMask` («Resaltado») en sus renderers, ya guardados en `Awake`, y lo quita al salir.
   - Guarda el estado (hover, arrastre o rechazo) en un registro estático que lee la feature. Solo hay una pieza resaltada a la vez.
   - Sin asignaciones por cambio de estado.
   - Hoy todos los renderers de los labs tienen `renderingLayerMask = 1` (499 en las tres escenas), así que hay bits libres. Ningún prefab usa los `Mat_Outline_*`: solo las cuatro escenas.
2. **Pase de máscara.**
   - Dibuja los renderers con ese bit en una textura R8 (todos sus submeshes) con un material de reemplazo unlit.
   - Si el usuario quiere verlo a través de las paredes, pide la profundidad (`ConfigureInput(Depth)`) solo cuando haga falta, y distingue la parte visible de la tapada en el pase de composición.
3. **Pase de contorno.**
   - A pantalla completa: dilata la máscara N px (anillo de muestras; con más de 4 px, *jump flooding*). Contorno = dilatada − máscara, con el color del estado, mezclado sobre el color de la cámara.
   - Una sola silueta por pieza aunque tenga 100 hijos.
4. **Coste cero en reposo.** Si no hay nada resaltado, la feature no encola ningún pase (los menús tampoco pagan nada).
5. **Integración.**
   - Añade la feature al renderer del URP asset.
   - En `InteractionManager`, cambia `hoverMaterial` / `dragMaterial` / `_rejectMaterial` por los estados (o colores), y actualiza las cuatro escenas.
   - Los constructores (`LabBuilderKit.MakePart` añade `ObjectHighlighter`) no deberían cambiar; si lo hacen, reconstruye y compara con `Tools/Unity/volcar_jerarquia.cs`.
6. **Limpieza** al final: retira `URPOutline.shader` y los dos `Mat_Outline_*` si ya nada los usa. Comprueba antes las escenas y los prefabs de `UI/_Legacy`.

**Plan B (más simple, menos completo):** casco invertido con **normales suavizadas** guardadas en un canal de UV (arregla las primitivas) y **ancho en espacio de clip** (constante en pantalla). No resuelve las líneas internas de las piezas compuestas ni los submeshes: úsalo solo si la feature no es viable.

**Cuidado con:**
- **Fantasmas** (`PlacementPreview`): clonan los renderers de la pieza al empezar el arrastre. El clon no debe heredar el bit de resaltado (hoy se limpia el hover antes de clonar).
- **`CablePortMarker`:** su caja se crea en `Start`, después de que `ObjectHighlighter` guarde sus renderers, en la capa Ignore Raycast. No debe recibir contorno.
- **Cables** (`CableSpline`): la malla es procedural y se regenera; el renderer es el mismo. Resaltar el tubo tiene que seguir funcionando.
- **`capturar.sh`:** usa una cámara temporal que copia la principal. Comprueba que la feature también actúa en ella, o la verificación no servirá.

## Cómo verificar (antes de decir que está hecho)

- **En Play, por código** (`simulate_pointer` no llega al juego): llama a `ObjectHighlighter.SetHighlight` (o a la API nueva) por `eval` y captura con `Tools/Unity/capturar.sh` estos casos, cada uno a dos distancias (vista general y de cerca), y míralos tú:
  - Laptop: un tornillo cautivo, un conector de antena y la tapa inferior entera (pieza compuesta: una sola silueta).
  - SFF: la tapa y el módulo de discos con bisagra.
  - ATX: la placa (FBX con varios submeshes) y una pieza dentro del gabinete tapada por una pared, si se eligió verlo a través.
  - Un cable, al pasar por su tubo.
  - Los tres estados: hover, arrastre (con fantasmas a la vista) y rechazo (borde rojo).
- **Grosor:** el mismo en píxeles en todas las capturas, a la misma resolución.
- **Coste:**
  - Con el Frame Debugger, o contando los pases: ningún pase extra sin nada resaltado, y uno o dos con una pieza resaltada, tenga los hijos que tenga.
  - Ninguna asignación por cambio de hover (por lectura del código o con el Profiler).
- **Sin regresiones:** el recorrido de los labs con `Tools/Unity/arnes_ensamblaje.cs` sigue llegando a 21/21, 16/16 y 22/22. Los menús se ven igual. La consola está limpia.
- **Lo que no puedes probar:** el hover real con el ratón. Pídele al usuario que lo pruebe en los tres labs, sobre todo con las piezas pequeñas de la laptop.

## Al terminar

- Actualiza `CLAUDE.md`: §3 (`ObjectHighlighter` y la feature nueva), §4 (la cadena de hover/arrastre) y §8 si retiras materiales.
- Actualiza `Docs/Plan_Laboratorios_Practicos.md` §7.4 y la memoria `labs-practicos`.
- Commits por hito (feature → integración → limpieza → documentación), en español y **sin líneas de co-autoría ni de atribución** (regla del usuario). No subas a origin sin preguntar.
- No incluyas los cambios ajenos que ya estaban en el árbol: los materiales de `Assets/3D/SceneItems/Texturas`, `Shader Graphs_alpha.mat`, `Assets/_Recovery`, las fuentes TTHoves, `URPProjectSettings.asset` y `SimuladorArqComp_UNERMB.slnx` (lo reordena Unity solo).
- Hay una skill para revisar la feature: `unity:validate-urp-render-graph-renderer-feature`. Úsala antes de darla por buena.
