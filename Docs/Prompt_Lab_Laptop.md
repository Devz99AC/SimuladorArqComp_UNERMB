# Prompt: laboratorio 3, laptop (Dell Latitude 5400)

> Pega este documento entero al empezar la sesión. Está escrito para Claude Code trabajando en este repositorio con el editor de Unity abierto. Si el lab 2 (PC compacta) ya está hecho, parte de la mecánica nueva ya existirá: reutilízala.

---

## Tu tarea

Construir el **laboratorio 3 de ensamblaje: el mantenimiento interno de una laptop Dell Latitude 5400**. El estudiante:

1. Abre la tapa inferior.
2. Desconecta la batería.
3. Instala o cambia la memoria SO-DIMM, el SSD M.2 y la tarjeta Wi-Fi con sus antenas.
4. Conecta las cintas.
5. Lo cierra todo.

Se entra desde el selector (tarjeta «LABORATORIO 3»), con el mismo nivel de acabado que el lab 1 (ATX), que es tu referencia.

## Antes de tocar nada

1. Lee `CLAUDE.md` (sobre todo §2, §4, §8, §9 y §12) y `Docs/Plan_Laboratorios_Practicos.md` (§4.3, §5, §6 y §7).
2. Comprueba la rama: el trabajo va en `labs-practicos` (o en la que diga el usuario). Si estás en `main`, el editor no tendrá `com.unity.pipeline`. Corre `unity status --project-path "C:\Users\devin\SimuladorArqComp_UNERMB"`: tiene que dar `ready`.
3. Repasa cómo está hecho el lab ATX y, si ya existe, el lab 2:
   - `LabAtxBuilder.cs` (o la clase común `LabBuilderKit`, si el lab 2 la creó), `EstiloMateriales.cs`, `CablePlug`, `CablePortMarker` y `ShadowDistanceOverride`.
   - Las herramientas `Tools/Unity/capturar.sh` y `Tools/Unity/arnes_ensamblaje.cs`.
4. Lee el **manual de servicio oficial del Dell Latitude 5400** (dell.com/support, «Owner's Manual / Service Manual»; léelo con WebFetch). Saca de ahí el orden real de desmontaje, el número y la posición de los tornillos, qué conectores hay y cómo se sueltan. **No inventes datos de hardware:** si el manual no lo dice, pregúntalo o indícalo como aproximado en la ficha.

## Decisiones ya tomadas (no las vuelvas a preguntar)

- **Equipo:** Dell Latitude 5400.
- **Carcasa:** [Dell Latitude 5400, de Yolala3d](https://sketchfab.com/3d-models/c3dfc53a600e427c9db9be83aec9860d) (66k tris, CC BY; confírmalo al descargar).
  - La tapa inferior y el interior se **modelan aparte**: no existe una laptop descargable con piezas sueltas (las de Framework son escaneos de una sola malla).
  - Piezas candidatas en el plan §5.1: SO-DIMM de Cashierobz (2,5k), batería de Graphitage (29k), ventilador de ponchoguy (127k, hay que reducirlo), y el SSD M.2 del lab ATX (`Lab_ATX/SSD_980Pro`, ya preparado).
- **Las 4 mecánicas, todas:**
  1. Tornillos de la tapa.
  2. Batería desconectada primero.
  3. Inserción en ángulo (SO-DIMM y M.2).
  4. Antenas Wi-Fi y cintas planas.
- **Escala real** (1 u = 1 m), escalas uniformes.
- **Materiales en el estilo del proyecto** (`EstiloMateriales`).
- **Evaluación guiada:** fantasmas y contador de errores, sin nota. En este lab el contador es central: las reglas de seguridad son lo que más se aprende.
- **Commits:** sin líneas de co-autoría ni de atribución (regla del usuario, prevalece sobre cualquier otra guía).
- **Sin pendiente:** sonidos y color space Linear siguen aplazados.

## Pregunta al usuario al empezar (una sola tanda, con AskUserQuestion)

1. **Alcance.** ¿Montaje completo (desde la laptop vacía) o mantenimiento (cambiar RAM y SSD, y quitar y volver a poner batería y tarjeta Wi-Fi)? Recomienda mantenimiento: es lo que se hace en la vida real con una laptop.
2. **Tornillos.** ¿Cómo se quitan: arrastrarlos a una bandeja magnética (reutiliza el sistema actual sin código nuevo) o clic para desatornillar (animación de giro, más natural pero con código nuevo)?
3. **Detalle de las cintas.** ¿Pestillo ZIF interactivo (abrir, meter la cinta, cerrar) o la cinta encaja de una vez?

## Cómo resolver cada mecánica (propuestas; elige la más simple que se entienda bien)

**1. Tornillos de la tapa.** Ya funciona con el sistema actual:
- Cada tornillo es una pieza en un lugar «agujero».
- Los agujeros llevan `requires` hacia el lugar de la tapa. Como `EvaluateRemoval` bloquea retirar un lugar del que otro depende, la tapa no se puede quitar mientras quede un tornillo.
- Al final, tapa y tornillos vuelven a su sitio (`required`), así que el objetivo incluye cerrar.
- Si el manual dice que son **tornillos cautivos** (se aflojan pero no salen de la tapa), modélalo: el tornillo se queda en la tapa y la tapa viaja con ellos. Explica en la ficha por qué existen.
- Un clic para desatornillar necesita un componente nuevo (p. ej. `ScrewPoint`, que alterna flojo/apretado con animación y cuenta como «vacío» cuando está flojo).

**2. Batería primero.** Regla de seguridad: con el cable de la batería conectado no se instala ni se retira nada de dentro. Hace falta la regla «exige que otro lugar esté VACÍO»; si el lab 2 la creó (`requiresEmpty` o similar), reutilízala:
- Todos los lugares internos exigen vacío el puerto del cable de la batería.
- El mensaje de rechazo explica el porqué: riesgo de cortocircuito y de dañar la placa, y que hay que descargar la electricidad residual manteniendo pulsado el botón de encendido (compruébalo en el manual).
- Al final, reconectar la batería exige que todo lo demás esté completo.

**3. Inserción en ángulo.** Hoy `PCPart.InstallInto` siempre baja desde arriba (`approachHeight`). La SO-DIMM entra a unos 30° y se baja hasta que las pestañas hacen clic, y el M.2 entra en ángulo y luego se atornilla. Propuesta:
- Que el `SocketSystem` defina su trayectoria de entrada, p. ej. un `approachLocalDirection` y un `approachTilt` en grados.
- `InstallInto` haría tres tramos: llegar a la pose inclinada junto a la ranura → deslizar a lo largo de la ranura → girar hasta la pose final.
- Los fantasmas siguen mostrando la pose final.
- Si el usuario quiere el tornillo del M.2 como paso aparte, es un tornillo más que exige el M.2.

**4. Antenas y cintas.** Reutiliza `CableSpline` y `CablePlug`:
- **Antenas Wi-Fi:** coaxiales finas (`Round`, radio de unos 0,6 mm) con conectores diminutos que encajan a presión en la tarjeta.
  - Son dos, principal y auxiliar. Comprueba en el manual a qué terminal va cada una (suelen distinguirse por color o por un triángulo).
  - Conectarlas al revés es un buen error para el contador: necesitan tipos distintos (p. ej. `Type_AntenaMain` y `Type_AntenaAux`).
  - La tarjeta Wi-Fi exige que la batería esté desconectada, y las antenas exigen la tarjeta.
- **Cintas planas (FPC):** perfil `Flat` de unos 0,1 mm de grosor; el `CableSpline` ya corrige el giro para que llegue orientada.
  - Si el usuario quiere el pestillo ZIF, el pestillo es una pieza con estado abierto/cerrado (como la bisagra del lab 2).
  - La cinta exige el pestillo abierto, y el pestillo tiene que acabar cerrado para completar.
- Los conectores de la laptop miden pocos milímetros. Ajusta la tolerancia de encaje con `SocketSystem.snapRadius`: la automática, de 2 cm como mínimo, es demasiado grande y haría que puertos vecinos se pisaran. También `CablePortMarker.padding`.

## Cómo construirlo

1. **Modelos.**
   - Descarga con `Tools/Modelos/sketchfab_descargar.py`. El token está en `%USERPROFILE%\.sketchfab_token\sketchfab.txt`: **nunca** lo muestres ni lo copies al repo. Licencias: solo CC0 o CC BY.
   - Prepara en Blender por script (`Tools/Modelos/blender_preparar.py`, siempre con `-b --factory-startup --disable-autoexec`).
   - Separa la tapa inferior del modelo de Yolala3d, o modélala, y modela el interior: bandeja, placa simplificada con sus ranuras y conectores, ranuras SO-DIMM ×2, M.2, Wi-Fi, ventilador y disipador, y hueco de la batería.
   - Mide todo con el manual. La laptop real mide unos 323 × 216 × 19 mm: confírmalo.
   - Destino: `Assets/Project/Content/Models/Lab_Laptop/`. Pasa los materiales con `EstiloMateriales.ConvertFolder`. Créditos en `Docs/Creditos_Modelos.md` **y** en el modal de Créditos.
2. **Constructor `LabLaptopBuilder`.** Usa la clase común de ayudantes (si el lab 2 no la creó, créala ahora extrayéndola de `LabAtxBuilder`, que debe seguir generando lo mismo).
3. **Escena `Lab_Laptop`.**
   - Cópiala de `Lab_ATX` (o usa el prefab `Lab_Systems`, si ya existe) y quita `PC (Lab ATX)`.
   - La laptop va **cerrada y boca abajo** sobre la mesa, sobre una alfombrilla antiestática.
   - Es pequeña: ajusta el encuadre (`wideShotDistance`, `zoomLimits` con un mínimo de unos 5 cm, límites de paneo y `wideShotPitch`). Revisa también `InteractionManager.liftHeight` y `hoverClearance`: a esta escala, 5 cm de elevación es mucho.
   - Pon una bandeja para los tornillos en la mesa.
4. **Fichas.**
   - Cada pieza explica qué es, por qué se hace así y qué pasa si no (batería, electricidad estática, antenas al revés).
   - En la tarjeta del selector (`Lab_03_Laptop.asset`): `sceneName = "Lab_Laptop"`, `available = true`, imagen de 1440×600 renderizada con `capturar.sh`, y revisa título, descripción y «qué aprende».
   - Añade la escena a Build Settings.

## Cómo verificar (antes de decir que está hecho)

- **En Play, con `Tools/Unity/arnes_ensamblaje.cs`:**
  - Recorrido completo hasta N/N y modal de victoria.
  - Cada regla con su motivo:
    - Quitar la tapa con tornillos.
    - Tocar algo con la batería conectada.
    - Antena en el terminal equivocado.
    - Cinta con el pestillo cerrado, si hay pestillo.
    - Cerrar la tapa antes de terminar.
  - Los bloqueos al retirar.
  - La inserción en ángulo: haz varias capturas durante la animación.
- **Capturas** con `capturar.sh`: vista general, de cerca y montada. Míralas tú. A esta escala comprueba sobre todo que las señales de puerto y los conectores se vean.
- **Consola limpia.**
- **Recorrido real:** selector ▸ ENTRAR carga la escena.
- **Lo que no puedes probar:** el gesto con el ratón (`simulate_pointer` no llega al juego). Díselo al usuario y pídele que lo pruebe.

## Al terminar

- Actualiza `CLAUDE.md` (§2, §3, §4 con las mecánicas nuevas, §8, §12), `Docs/Plan_Laboratorios_Practicos.md` (§6 para probar el lab 3, §7.4 con el estado) y `HANDOFF.md`.
- Actualiza también la memoria `labs-practicos` y su línea en `MEMORY.md`.
- Commits por hito (modelos → cada mecánica → escena → selector y documentación), en español y **sin co-autoría**. No subas a origin sin preguntar.
- No incluyas los cambios ajenos que ya estaban en el árbol: materiales de `Assets/3D/SceneItems/Texturas`, `Shader Graphs_alpha.mat`, `Assets/_Recovery`, las fuentes TTHoves y `URPProjectSettings.asset`.
