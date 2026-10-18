# Prompt: laboratorio 2, PC compacta de oficina (SFF)

> Pega este documento entero al empezar la sesión. Está escrito para Claude Code trabajando en este repositorio con el editor de Unity abierto.

---

## Tu tarea

Construir el **laboratorio 2 de ensamblaje: una PC pequeña de oficina (SFF)**, del tipo Dell OptiPlex SFF / HP ProDesk SFF / Lenovo ThinkCentre SFF. El estudiante tiene que poder armarla de principio a fin y luego entrar desde el selector (tarjeta «LABORATORIO 2»). Mismo nivel de acabado que el lab 1 (ATX), que ya está hecho y es tu referencia.

## Antes de tocar nada

1. Lee `CLAUDE.md` (sobre todo §2, §4, §8, §9 y §12) y `Docs/Plan_Laboratorios_Practicos.md` (§4.2, §5, §6 y §7).
2. Comprueba la rama: el trabajo va en `labs-practicos` (o en la que diga el usuario). Si estás en `main`, el editor no tendrá `com.unity.pipeline`. Corre `unity status --project-path "C:\Users\devin\SimuladorArqComp_UNERMB"`: tiene que dar `ready`.
3. Mira cómo está hecho el lab ATX, porque vas a copiar su patrón:
   - `Assets/Project/Systems/Assembly/Editor/LabAtxBuilder.cs`: el constructor que genera toda la escena. Es la fuente de verdad de las posiciones.
   - `Assets/Project/Content/Models/Editor/EstiloMateriales.cs`: pasa los materiales al shader estilizado del proyecto.
   - Componentes: `CablePlug` (con `sourceSocket` / `homeWhenSourceInstalled`), `CablePortMarker` y `ShadowDistanceOverride`.
   - Herramientas: `Tools/Unity/capturar.sh` (capturas sin crear assets) y `Tools/Unity/arnes_ensamblaje.cs` (probar el montaje en Play sin ratón).

## Decisiones ya tomadas (no las vuelvas a preguntar)

- **Tipo de equipo:** PC pequeña de oficina SFF, **no** Mini-ITX gamer.
- **Arte:** no hay en Sketchfab un gabinete SFF con interior utilizable. El chasis se **modela** (es una caja de chapa simple) y se reutilizan piezas del lab ATX donde encajen (CPU, RAM, SSD M.2, ventilador, disco). Hay exteriores candidatos en el plan §5.1: HP EliteDesk SFF (7k tris) y Slim ThinkCentre (22k).
- **Escala real** (1 u = 1 m), escalas uniformes.
- **Materiales en el estilo del proyecto:** shader Attenuation2 vía `EstiloMateriales`.
- **Evaluación guiada:** fantasmas visibles y contador de errores, sin nota.
- **Montaje sin orden obligatorio:** `requires` solo para dependencias físicas.
- **Commits:** sin líneas de co-autoría ni de atribución (regla del usuario, prevalece sobre cualquier otra guía).
- **Sin pendiente:** sonidos y color space Linear siguen aplazados.

## Pregunta al usuario al empezar (una sola tanda, con AskUserQuestion)

1. **Equipo de referencia.** Recomienda el Dell OptiPlex 7070/7080 SFF: es coherente con la laptop Dell del lab 3 y tiene un manual de servicio público. Alternativas: HP ProDesk 600 G5 SFF o Lenovo ThinkCentre M720s.
2. **Exterior.** Usar uno de Sketchfab (HP EliteDesk SFF o ThinkCentre) más un interior modelado, o un chasis modelado completo y sencillo.
3. **Pasos del montaje.** Propón la lista de la sección siguiente y deja que quite o añada.

## Qué enseña este lab (lo que lo distingue del ATX)

Comprueba cada dato con el **manual de servicio oficial** del equipo elegido (Dell lo publica en dell.com/support; léelo con WebFetch). Las cifras de abajo son orientativas.

- **Tapa sin herramientas.** Un pestillo o botón libera la cubierta, que se desliza o se levanta. Es el **primer paso** y, al final, el último: cerrarla.
- **Poco espacio, así que el orden sí importa.** La jaula de discos va con **bisagra** y hay que levantarla para llegar a la RAM o a la placa. Con la jaula bajada, esas piezas no se pueden instalar ni retirar.
- **Tarjetas de perfil bajo.** El bracket es corto. Una tarjeta de perfil completo no cabe: es un rechazo con motivo, ideal para el contador de errores.
- **Fuente propietaria**, más pequeña que una ATX, con conectores propios del fabricante. Explica en las fichas por qué no se puede poner cualquier fuente.
- **Disipador pasivo con conducto (shroud)** y el ventilador del sistema empujando el aire a través de él.
- **2 o 4 ranuras DIMM, M.2 y una bahía de 2,5"/3,5"** en la jaula.
- **Panel frontal integrado:** un solo conector a la placa.

Pasos posibles (ajústalos al manual): quitar la tapa → levantar la jaula → CPU → disipador y conducto (requiere CPU) → RAM → M.2 → bajar la jaula → disco en la jaula → tarjeta de perfil bajo → fuente → cables (alimentación de placa y CPU, SATA, panel frontal y ventilador) → cerrar la tapa (requiere todo lo demás).

## Mecánica nueva necesaria: estados abierto/cerrado y la regla «antes hay que quitar X»

El sistema actual solo sabe «este lugar está ocupado» (`SocketSystem.IsComplete`) y «este exige que otro esté ocupado» (`requires`). Te falta:

1. **«Exige que otro esté VACÍO».** Por ejemplo, nada se instala dentro con la tapa puesta. Propuesta: añadir a `SocketSystem` una lista `requiresEmpty`, comprobada en `Evaluate`, con el mensaje «Antes hay que quitar X.».
   - Con eso, la tapa es una pieza normal: empieza instalada en su lugar (`required = true`), el estudiante la arrastra fuera al empezar y la vuelve a poner al final.
   - Su lugar `requires` todo lo de dentro, así que no se puede cerrar antes de tiempo, y el objetivo de victoria incluye haberla cerrado.
   - Ojo con `EvaluateRemoval`: hoy solo mira `requires`. Decide si `requiresEmpty` también bloquea retiradas.
2. **Piezas con bisagra (la jaula).** Opción A: tratarla como una pieza que se arrastra entre dos lugares, «bajada» (`required`) y «levantada». Opción B: un componente nuevo `HingedPart` que alterna abierto/cerrado con un clic animado, y una condición para `SocketSystem` del tipo `requiresOpen`. Elige la más simple que se entienda bien. La B es más natural para el estudiante, pero hay que integrarla en `InteractionManager` (clic sin arrastre) y en el contador del objetivo.

Esta mecánica la va a necesitar también el lab 3 (laptop: batería desconectada primero, pestillos ZIF). Diséñala pensando en los dos y documéntala en `CLAUDE.md` §4.

## Cómo construirlo (patrón del lab ATX)

1. **Modelos.**
   - Busca exteriores y piezas con `Tools/Modelos/sketchfab_descargar.py`. El token está en `%USERPROFILE%\.sketchfab_token\sketchfab.txt`: **nunca** lo muestres ni lo copies al repo.
   - Licencias: solo CC0 o CC BY. Si solo hay NC, pregunta.
   - Prepara cada modelo en Blender por script (`Tools/Modelos/blender_preparar.py`, Blender 4.5 en `C:\Program Files\Blender Foundation\Blender 4.5\blender.exe`). Siempre con `-b --factory-startup --disable-autoexec`: son archivos de terceros.
   - El chasis se modela por script en Blender o con primitivas en el constructor: chapa de 1 a 1,5 mm, bandeja, jaula, hueco de la fuente y ranuras de perfil bajo. Mídelo a escala real con el manual.
   - Destino: `Assets/Project/Content/Models/Lab_SFF/`. Pasa los materiales con `EstiloMateriales.ConvertFolder`. Créditos en `Docs/Creditos_Modelos.md` **y** en el modal de Créditos (`UI/MainMenu/MainMenu.uxml`).
2. **Constructor.** Antes de escribir `LabSffBuilder`, **extrae a una clase común** (p. ej. `LabBuilderKit`) los ayudantes de `LabAtxBuilder`: `Spawn`, `RestOnDesk`, `LocalBounds`, `BoundsAt`, `MakePart`, `MakeSocket`, `SocketFromChild`, los de cables (`Cable`, `CaseCable`, `BoardPort`, `Spline`, `Anchor`, `Home`, `AddPortMarker`) y `ConfigureScene`. Así el lab 3 no los copia una tercera vez. `LabAtxBuilder` tiene que seguir generando exactamente lo mismo: reconstruye `Lab_ATX` y compara.
3. **Escena `Lab_SFF`.**
   - Cópiala de `Lab_ATX` (mantiene mesa, luces, cámara, HUD, managers y `ShadowDistanceOverride`) y quita `PC (Lab ATX)`. Si para entonces existe el prefab `Lab_Systems` (Fase A), úsalo.
   - El equipo va **tumbado** con la tapa arriba, como se trabaja en la vida real. Es menos alto que el ATX, así que las paredes taparán menos el interior.
   - Encuadre de cámara: ajusta `wideShotDistance`, `zoomLimits` y los límites de paneo al tamaño del equipo.
4. **Lugares y cables.**
   - Mide los puertos de la placa con una captura cenital ortográfica: `capturar.sh` con `ortho`, y la cámara 0,0001 m por delante del punto que mira.
   - Pon un `CablePortMarker` en cada puerto.
   - La holgura de los cables se comba perpendicular a la cuerda: lee la nota de `CLAUDE.md` §12 antes de colocar las salidas cerca de una pared.
5. **Selector.**
   - `Data/ScriptableObjects/Labs/Lab_02_SFF.asset`: `sceneName = "Lab_SFF"`, `available = true`. Revisa título, descripción y «qué aprende», y pon una imagen de tarjeta renderizada con `capturar.sh` (1440×600) en `Content/Pictures/Labs/`.
   - Añade la escena a Build Settings.

## Cómo verificar (antes de decir que está hecho)

- **En Play, con `Tools/Unity/arnes_ensamblaje.cs`:**
  - Recorrido completo hasta N/N y modal de victoria.
  - Cada rechazo con su motivo: tapa puesta, jaula bajada, tarjeta de perfil completo, cable en un puerto equivocado.
  - Los bloqueos al retirar.
  - El alcance de los cables.
- **Capturas** con `capturar.sh`: desde la vista general, de cerca y montado completo. Míralas tú: nada atravesando paredes, cables dentro del equipo, señales de puerto visibles.
- **Consola limpia** (`unity command console --level warning`).
- **Recorrido real:** selector ▸ ENTRAR carga la escena.
- **Lo que no puedes probar:** el gesto con el ratón (`simulate_pointer` no llega al juego). Díselo al usuario y pídele que lo pruebe.

## Al terminar

- Actualiza `CLAUDE.md` (§2, §3, §4, §8, §12), `Docs/Plan_Laboratorios_Practicos.md` (§6 para probar el lab 2, §7.4 con el estado) y `HANDOFF.md`.
- Actualiza también la memoria `labs-practicos` y su línea en `MEMORY.md`.
- Commits por hito (modelos → mecánica nueva → escena → cables → selector y documentación), en español y **sin co-autoría**. No subas a origin sin preguntar.
- No incluyas los cambios ajenos que ya estaban en el árbol: materiales de `Assets/3D/SceneItems/Texturas`, `Shader Graphs_alpha.mat`, `Assets/_Recovery`, las fuentes TTHoves y `URPProjectSettings.asset`.
