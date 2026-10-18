# CLAUDE.md — Simulador de Arquitectura de Computadores (UNERMB)

Guía de arquitectura y **mapa de conexiones** del proyecto. Este archivo se carga en cada sesión de Claude Code: mantenlo actualizado cuando cambien las conexiones.

> Auditoría técnica completa en [AUDITORIA_TECNICA.md](AUDITORIA_TECNICA.md). Estado y prioridades en [HANDOFF.md](HANDOFF.md). **Parte práctica (3 labs: ATX / compacta / laptop, cables): diagnóstico y plan en [Docs/Plan_Laboratorios_Practicos.md](Docs/Plan_Laboratorios_Practicos.md).**

---

## 1. Qué es

Simulador educativo 3D de ensamblaje de PCs para estudiantes (PNF Informática, UNERMB). Dos modos: **ensamblaje interactivo** (arrastrar componentes a sus sockets) y **módulo teórico** (11 unidades / 49 temas, alineado al contenido analítico de ACT139). UI en español.

| Config | Valor |
|---|---|
| Unity | **6000.6.0f1** (Unity 6.6, no LTS) |
| Render Pipeline | **URP 17.6** — asset activo: `Assets/3D/URP/New Universal Render Pipeline Asset 1.asset` |
| Input | **New Input System** (`Assets/Project/Systems/Interaction/SimulationControls.inputactions`) |
| UI | **UI Toolkit — migración COMPLETA** (ver §11). Las 4 escenas (MainMenu, ModeSelection, TheoryModule, AssemblyLab) usan `UIDocument`+UITK. Cimientos en `Assets/Project/UI/`. UI uGUI vieja respaldada en `UI/_Legacy/*.prefab`. Prompt: `Docs/Prompt_Migracion_UI_Toolkit.md` |
| Color space | Gamma ⚠️ (debería ser Linear con URP) |
| Nombre | `productName` = **Simulador de Arquitectura del Computador**, `companyName` = **UNERMB**. Splash de Unity **desactivado** (`m_ShowUnitySplashScreen: 0`; en Unity 6 Personal ya es opcional). |
| Plataforma | PC standalone (Windows) |

---

## 2. Flujo de escenas (Build Settings)

```
[0] MainMenu ──StartSimulation──> [1] ModeSelection ──"LABORATORIO DE ENSAMBLAJE"──> selector (modal, 3 tarjetas)
                                        │                                          ├─ENTRAR (Lab_01_ATX)──> [4] Lab_ATX
                                        │                                          ├─ENTRAR (Lab_02_SFF)──> [5] Lab_SFF
                                        │                                          └─ENTRAR (Lab_03_Laptop)──> [6] Lab_Laptop
                                        └──GoToTheory──> [2] TheoryModule
Retornos: Lab_ATX/Lab_SFF/Lab_Laptop/TheoryModule ──> ModeSelection ──BackToMenu──> MainMenu
[3] AssemblyLab: el lab viejo; sigue en Build Settings pero ya no lo abre nadie (se retira en la limpieza)
```

- **Selector de laboratorios** (2026-10-18): `ModeSelectionView.labCatalog` → `Data/ScriptableObjects/Labs/LabCatalog.asset` con `Lab_01_ATX` (escena **`Lab_ATX`**, imagen `Content/Pictures/Labs/Lab_ATX.png`), `Lab_02_SFF` (escena **`Lab_SFF`**, imagen `Lab_SFF.png`) y `Lab_03_Laptop` (escena **`Lab_Laptop`**, imagen `Lab_Laptop.png`). Con `available = false` la tarjeta sale «Próximamente». La escena que abre cada lab es `LabDefinitionSO.sceneName` (y tiene que estar en Build Settings). Sin catálogo, el botón entra directo a `simulationScene`.

- Escenas **no** en build (sandbox): `Test.unity`, `CableTest.unity`.
- **Los nombres de escena son strings serializados** en cada manager (acoplamiento frágil — ver §7):
  - `MainMenuManager.simulationSceneName = "ModeSelection"`
  - `ModeSelectionManager`: `mainMenuScene="MainMenu"`, `simulationScene="AssemblyLab"`, `theoryScene="TheoryModule"` (⚠️ el **default en código** es `"MainSimulation"`, inexistente; funciona solo porque la escena lo sobreescribe)
  - `UIManager.selectorSceneName = "ModeSelection"`, `TheoryManager.backSceneName = "ModeSelection"`

---

## 3. Inventario de scripts

| Script | Ubicación | Escena | Rol |
|---|---|---|---|
| `GameManager` | Systems/Interaction | AssemblyLab, labs | Singleton. Objetivo = sockets `required` (auto). Eventos de progreso/victoria. **Contador de errores** (`Errors`, `RegisterError`, `OnErrorsChanged`): cada intento rechazado con motivo |
| `InteractionManager` | Systems/Interaction | AssemblyLab | Raycast, hover, clic vs. arrastre, encaje/retirada, fantasmas, feedback al HUD |
| `PlacementPreview` | Systems/Interaction | — | POCO. Fantasmas translúcidos de la pieza en los lugares compatibles |
| `IDragConstraint` | Systems/Interaction | — | Interfaz: limita posición/rotación al arrastrar (la implementa `CablePlug`) |
| `ObjectHighlighter` | Systems/Interaction | (por pieza) | Aplica material de resaltado (hover/drag) vía `sharedMaterials` |
| `PCPart` | Systems/Assembly | (por pieza) | Datos de pieza + `BeginDrag`/`InstallInto`/`ReturnToTable` animados. `startInstalledIn` + `restPoint`: empieza instalada (la tapa) y al retirarla reposa en la mesa. `InstallInto` hace la inserción en ángulo si el lugar la pide (SO-DIMM, M.2) |
| `SocketSystem` | Systems/Assembly | (por socket) | Registro `All`, `Evaluate` (con motivo), `requires` (orden), `requiresEmpty` (lo que estorba), `fillAction`/`emptyAction` (textos), `IsSatisfied` (objetivo), lugares de estado (`StateOwner`), instalar/retirar, `FindTarget`. Mantenimiento: `goalPart` (solo cuenta una pieza concreta), `cycleRequired` (quitar y volver a poner), `blockReason` (el porqué de un bloqueo), `onlyOwnType`; inserción en ángulo (`insertTilt` + `insertHinge`) |
| `HingedPart` | Systems/Assembly | Lab_SFF, Lab_Laptop | Pieza fija que se abre/cierra con un clic (bisagra, pestillo, pestillo ZIF, tornillo cautivo con `openLift`). Su «cerrado» es un lugar de estado: reutiliza todas las reglas de `SocketSystem` |
| `CompatResult` | Systems/Assembly | — | struct: Ok + Message + Reason (el "porqué" en español) |
| `CableSpline` | Systems/Cables | (por cable) | Tubo procedural Bézier entre anclas, puntos de paso, holgura, longitud finita (`[ExecuteAlways]`) |
| `CablePlug` | Systems/Cables | (conector) | Extremo arrastrable de un cable: limita el alcance y lo orienta |
| `CablePortMarker` | Systems/Cables | (puerto de cable) | Señal naranja translúcida del tamaño del conector en un puerto libre: tenue mientras falta algo de su `requires`, late cuando ya se puede conectar, se apaga al conectar (pedido del usuario: los cabezales no se notaban) |
| `CableMenu` | Systems/Cables/Editor | — | `GameObject ▸ Simulador ▸ Cable (spline)` / `Puerto de cable` |
| `LabBuilderKit` | Systems/Assembly/Editor | — | Ayudantes comunes de los constructores (`using static`): `Spawn`, `RestOnDesk`, `BoundsAt`, `MakePart`, `MakeSocket`, `Port`, `Cable`/`CaseCable`/`Spline` (`CableRig`), `ConfigureScene` (`SceneFrame`), `SeatAt`, `Disc`, `PartRoot`, `AddHinge`, `EnsureType`, `SetHudInstructions`. El lab 3 los reutiliza |
| `LabAtxBuilder` | Systems/Assembly/Editor | Lab_ATX | `Tools ▸ Simulador ▸ Reconstruir laboratorio ATX`: genera todo `PC (Lab ATX)` (piezas, lugares, añadidos del gabinete, 11 cables). **Fuente de verdad de las posiciones** |
| `LabSffBuilder` | Systems/Assembly/Editor | Lab_SFF | `Tools ▸ Simulador ▸ Reconstruir laboratorio SFF (PC compacta)`: genera todo `PC (Lab SFF)` (chasis, placa y piezas con primitivas a medida del OptiPlex 7070 SFF, 6 cables) y los tipos de conexión propios del lab. **Fuente de verdad** |
| `LabLaptopBuilder` | Systems/Assembly/Editor | Lab_Laptop | `Tools ▸ Simulador ▸ Reconstruir laboratorio Laptop (Dell Latitude 5400)`: genera todo `PC (Lab Laptop)` (carcasa de Yolala3D, interior con primitivas, alfombrilla y bandeja, 22 pasos de mantenimiento) y sus tipos de conexión. **Fuente de verdad** |
| `EstiloMateriales` | Content/Models/Editor | — | Pasa los materiales de un FBX al shader estilizado (Attenuation2) con texturas de color liso; `SolidMaterial()` para piezas añadidas |
| `ShadowDistanceOverride` | Systems/Camera | Lab_ATX, Lab_SFF | Baja la distancia de sombras del URP (compartido) solo durante el Play de una escena a escala real |
| `CADCameraController` | Systems/Camera | AssemblyLab, Lab_ATX, Lab_SFF | Órbita (clic der. + arrastre con umbral)/paneo "agarrado" (`panGrab`, ∝ distancia)/zoom proporcional. `wideShotPitch` = inclinación de la vista general (Espacio) |
| `UIManager` | Scripts | AssemblyLab | Singleton. Paneles HUD, ficha, victoria, settings |
| `MainMenuManager` | Scripts | MainMenu | ⚠️ **Reemplazado por `MainMenuView` (UITK)**; `MenuController` + `Canvas` uGUI **sacados de la escena** a un prefab de respaldo `UI/_Legacy/MainMenu_LegacyUGUI.prefab` |
| `MainMenuView` | UI/MainMenu | MainMenu | **UITK**. Controlador del `UIDocument` del menú: binding, modales, settings (vía `SettingsService`), fondo animado |
| `ModeSelectionManager` | Scripts | ModeSelection | Navegación entre modos |
| `TheoryManager` | Scripts | TheoryModule | Índice dinámico + lector de temas |
| `AudioManager` | Scripts | (persistente) | Singleton `DontDestroyOnLoad`. Pool de AudioSources |
| `UISoundController` | Scripts | (por botón) | Sonido hover/click → AudioManager |
| `AutoSelectOnHover` | Scripts | (por botón) | Selecciona en EventSystem al pasar el ratón |
| `BackgroundScroller` | Scripts | menús | Desplaza el `uvRect` de un RawImage |
| `ConnectionTypeSO` | Data/SO/Connections Type | — | SO: tipo de conexión (label + color + `displayName`/`placementHint` del motivo) |
| `LabDefinitionSO` | Data/SO/Labs | — | SO: un laboratorio (tarjeta del selector: tag, título, descripción, qué aprende, imagen; `sceneName`, `available`) |
| `LabCatalogSO` | Data/SO/Labs | — | SO: lista ordenada de laboratorios que muestra `ModeSelectionView` |
| `ModeSelectionView` | UI/ModeSelection | ModeSelection | **UITK**. Menú de modos + modal selector de laboratorios (tarjetas construidas desde `LabCatalogSO`) |
| `TheoryTopicSO` | Data/SO/TheoryContent | — | SO: un TEMA (título, resumen, contenido con marcado ligero, imagen + pie) |
| `TheoryUnitSO` | Data/SO/TheoryContent | — | SO: una UNIDAD (número, título, descripción, array de temas) |
| `TheorySyllabusSO` | Data/SO/TheoryContent | — | SO raíz: datos de la unidad curricular + array de unidades + bibliografía |
| `TheoryMarkup` | UI/Theory | — | Estático. Convierte el marcado ligero en elementos UITK con jerarquía |
| `SimulationControls` | Systems/Interaction | — | **Autogenerado** por Input System — no editar a mano |

---

## 4. Conexiones del sistema de ensamblaje (el núcleo) ⭐

**Cadena de datos completa de "arrastrar y encajar una pieza"** (reescrita 2026-10-17):

```
InteractionManager (Update)   — congelado si UIBlocker.ModalOpen
  ├─ HandleHover()  → ObjectHighlighter.SetHighlight(hoverMaterial)   [naranja]
  ├─ HandleLeftPress() → guarda _pressPart (aún NO es arrastre; un clic no desinstala)
  ├─ CheckDragStart() → si el ratón se movió > dragStartPixels:
  │     → si está instalada: SocketSystem.EvaluateRemoval()  (¿otro lugar depende de este? → toast aviso)
  │     → BeginDrag(): PCPart.BeginDrag() [libera su socket → GameManager.UnregisterInstallation]
  │                    plano elevado (liftHeight) + desfase de agarre; PlacementPreview.Show(part) [fantasmas];
  │                    SetHighlight(dragMaterial)
  ├─ DragUpdate() → sigue al cursor (IDragConstraint opcional: cables) → SupportHeight(): BoxCast con la huella
  │     de la pieza → pasa por ENCIMA de lo que tenga debajo (paredes del gabinete, otras piezas; los cables no cuentan)
  │     → SocketSystem.FindTarget(part) [centro visual instalado vs. actual, tolerancia ∝ tamaño; ignora los lugares
  │        que lleva la propia pieza; si alguno a tiro la acepta, gana ese]
  │        → fantasma intenso si encaja / borde rojo (_rejectMaterial) si no
  ├─ HandleLeftRelease()
  │     → arrastre: EndDrag() → FindTarget → result.Ok ?
  │          sí: PCPart.InstallInto(socket) → Reserve → anima (encima → baja) → IsInstalled=true
  │              → SocketSystem.InstallPart → GameManager.RegisterInstallation
  │                   → OnPartInstalled, OnProgressChanged(n,total) → si n == total: OnAssemblyComplete
  │                        → AssemblyHudView.ShowVictory()   [suscrito al evento]
  │              + AssemblyHudView.ShowFeedback("Instalado: …", Success), o Warning + socket.installNote si el lugar
  │                tiene nota (p. ej. RAM en A1/B1: válido, pero el manual pide A2/B2)
  │          no: ShowFeedback(result.Message, Error, result.Reason) si soltó sobre un lugar; PCPart.ReturnToTable()
  │     → clic: doble clic sobre la misma pieza → CADCameraController.FocusOnObject()
  └─ Clic derecho: al SOLTAR y sin moverse (≤ dragStartPixels) → AssemblyHudView.ShowPartInfo()
                   (si se movió, era órbita de la cámara)

SocketSystem.Evaluate(part):  null-guard → ocupado → tipo ≠ allowedType (motivo: "Aquí va X. " + placementHint)
                              → requires sin completar ("Antes hay que instalar X.") → Accept
```

**Componentes de datos (ScriptableObjects como "clave de compatibilidad"):**

| Socket (en escena) | `allowedType` | Piezas que encajan |
|---|---|---|
| SnapCPU | `Type_CPU` | CPU |
| SnapGPU | `Type_GPU` | GPU |
| SnapRAM1 / SnapRAM2 | `Type_RAM` | RAM (×2) |
| *(no existe)* | `Type_PSU`, `Type_HDD` | ⚠️ definidos pero **sin socket** — piezas no instalables |
| *(por crear: puertos de cable)* | `Type_ATX24`, `Type_EPS8`, `Type_PCIePower`, `Type_SATAPower`, `Type_SATAData` | conectores de cable (`CablePlug`) |

- **Objetivo automático:** el lab se completa cuando todos los `SocketSystem` con `required = true` están ocupados por una pieza asentada. Ya no hay contador manual (`totalPartsToInstall` eliminado).
- `ConnectionTypeSO.displayName` / `placementHint` son los textos del motivo de rechazo: rellénalos en cada tipo nuevo.
- La escena tiene **~28 `PCPart`**, pero solo **4 son `isDraggable`**; el resto son etiquetas informativas fijas (`isDraggable=0` → `IsInstalled=true` en `Start`).
- **Cables en AssemblyLab (2026-10-17):** `Cable ATX 24 pines` y `Cable EPS 8 pines (CPU)` (bajo `Desk`) ya usan `CableSpline` + `CablePlug`, con sus puertos `Puerto ATX 24 pines` / `Puerto EPS 8 pines` en `Desk/SnapPoints` (`required` → el objetivo pasa a **6**). Apoyo en la mesa: `Desk/Mesa (apoyo de cables)` (y = 6.5725). Los IK `Cable PSU`/`Cable PSU2` están **desactivados** (no borrados). Quedan con IK `CableHDD`/`CableHDDEnergy` (SATA, decorativos). Los dos módulos de RAM se movieron 0.15 a la izquierda para dejar el pasillo de los cables.
- Los puertos van bajo `SnapPoints` (escala 1), **nunca** bajo la placa: su escala no uniforme (49×58×46) deformaría el conector instalado.

**Lab_ATX (2026-10-18)**: escena a escala real generada por `LabAtxBuilder` (no editar `PC (Lab ATX)` a mano: se rehace). **21 lugares obligatorios:**
- Piezas: placa en la bandeja, CPU, disipador (`requires` CPU), RAM A2/B2 (A1/B1 opcionales, con nota de doble canal), SSD M.2, GPU (`requires` placa en el gabinete), fuente, disco y ventilador trasero.
- 11 puertos de cable: ATX 24 y EPS 8 (placa + fuente), PCIe 6+2 en la GPU (GPU + fuente), alimentación SATA en el disco (disco + fuente), datos SATA en la placa (placa + disco; el cable sale del disco), F_PANEL, USB 3.0, HD Audio y CHA_FAN1 (ventiladores frontales con divisor) desde el frente del gabinete, CHA_FAN2 (ventilador trasero) y CPU_FAN (`Type_CPUFan`: solo el del disipador).
- Los lugares de la placa son hijos de la placa: viajan con ella al gabinete. La placa se prepara en la mesa con la misma orientación que tendrá dentro (meterla = trasladarla).

**Reglas de estado (2026-10-18, para los labs 2 y 3)** — todo sobre `SocketSystem`, sin sistemas paralelos:
- **`requiresEmpty`**: lugares que tienen que estar VACÍOS para instalar *o retirar* aquí («Antes hay que quitar la tapa.»). Se comprueba antes que `requires`, en `Evaluate` y en `EvaluateRemoval`. La señal de los puertos (`PrerequisitesMet`) también lo mira.
- **`fillAction` / `emptyAction`**: cómo se dice llenar/vaciar ese lugar en los mensajes («bajar el módulo de discos», «levantar el módulo de discos (clic sobre él)»). Vacío = «instalar/quitar» + `displayName` (los textos del lab ATX no cambian).
- **Pieza que empieza instalada** (`PCPart.startInstalledIn` + `restPoint`): la tapa del equipo cerrado. Se coloca en `Start` sin animación (`PlaceInstantly`); al retirarla y soltarla en vacío reposa en `restPoint`. `explainWrongPlace = false` en piezas grandes como la tapa: su tolerancia (∝ tamaño) alcanzaría puertos pequeños de otro tipo.
- **Objetivo = `IsSatisfied`**, no `IsComplete`: un lugar cuenta si está completo *y* lo que exige también. Para un lugar normal es lo mismo; la tapa (que `requires` todo lo de dentro) no cuenta al empezar, así que el progreso empieza en 0 y no baja al quitarla. Cerrarla con todo dentro es el último paso.
- **Bisagras y pestillos** (`HingedPart`): pieza fija (`isDraggable = false`) que un clic izquierdo sin arrastre abre o cierra (`InteractionManager.ToggleHinge` → toast). Su «cerrado» es un **lugar de estado** (`closedState`, ocupado = cerrado; `StateOwner` lo marca: `FindTarget` y los fantasmas lo ignoran). Otros lugares lo exigen cerrado (`requires`: el disco va sobre el módulo bajado) o abierto (`requiresEmpty`: la memoria). Abrir = vaciarlo → `EvaluateRemoval` (no se abre con el disco encima ni con la tapa puesta). Gira sobre `hinge` (hermano, Z+ = eje); sus hijos (el lugar del disco) giran con él. Lab 3: el pestillo ZIF de una cinta es lo mismo; la batería conectada es un puerto normal que los demás piden vacío.
- `FindTarget`: si ninguno acepta la pieza, el motivo sale de un lugar de su **mismo tipo** a tiro (el disipador sin CPU dice «antes hay que instalar la CPU», no «aquí va el ventilador», que comparte centro).

**Lab_SFF (2026-10-18)**: Dell OptiPlex 7070 SFF acostado, generado por `LabSffBuilder` (no editar `PC (Lab SFF)` a mano). **16 lugares obligatorios** (10 piezas + 6 cables), orden del Service Manual de Dell:
- Empieza con la **tapa puesta**: lo primero es quitarla y lo último, ponerla (`requires` todo, el módulo bajado y el pestillo cerrado).
- **Módulo de discos** (bisagra en la pared izquierda): tapa memoria, CPU, disipador, ventilador, M.2 y el acceso a la fuente (todos `requiresEmpty` su estado). Solo se levanta sin el disco, que va encima (`requires` el módulo bajado).
- **SSD M.2 y fuente antes que el disipador** (`requiresEmpty` el disipador). Ventilador del disipador aparte (`requires` el disipador; el 7070 SFF no tiene ventilador de sistema). DIMM1 y DIMM2 obligatorias; DIMM3/4 válidas con nota (suposición: el manual no da el orden de llenado).
- **Tarjeta de perfil bajo** en la PCIe x16 con el **pestillo de las tarjetas** abierto. (Hubo una de perfil completo para rechazarla; el usuario la quitó: no hace falta.)
- **Fuente propia** (`Type_PSU_SFF`), solo dos cables: alimentación del sistema y del procesador (`Type_DellSysPower`/`Type_DellCpuPower`). Los dos cables del disco (datos y alimentación) **salen de la placa**. Botón de encendido → PWR_SW, ventilador → FAN_CPU.

**Mantenimiento (2026-10-18, lab 3)** — también sobre `SocketSystem`:
- **`GoalMet`** = completo + la pieza objetivo (`goalPart`: el SSD nuevo; el viejo entra igual, pero no termina el trabajo) + vaciado y vuelto a llenar si es `cycleRequired` (la tapa, los tornillos, la batería, reasentar la Wi-Fi). Es lo que miran `requires`, `IsSatisfied` y la señal de los puertos; para un lugar normal es igual a `IsComplete`. `PendingText` dice lo que falta (`fillAction`, `goalAction` o `cycleAction`). Así un equipo que llega con todo puesto empieza en 0/N.
- **`blockReason`**: el porqué de un lugar ocupado que estorba. Se añade al «Antes hay que…» (la batería conectada: riesgo de cortocircuito) o sustituye a «depende de esta pieza» cuando el que depende es él (los tornillos).
- **`onlyOwnType`**: destino solo para piezas de su tipo. Lo usan la tapa de la laptop (cubre todo el equipo), el soporte de las antenas y los agujeros de tornillo (están junto a terminales diminutos).
- **Inserción en ángulo** (`insertTilt`, `insertHinge` = el borde de los contactos con su Z+ como eje, `insertSlide`): `PCPart.InstallInto` llega inclinada a la boca del conector, se desliza dentro y baja girando sobre ese borde. Tarda 3 veces lo normal (0,9 s) para que se vea. Los fantasmas siguen mostrando la pose final.
- **Tornillo cautivo** = `HingedPart` con `openAngle = -1080` (3 vueltas; en Unity el ángulo positivo aprieta) y `openLift = 1,5 mm`: gira, sube y no sale de la tapa (hijo de ella). Su estado «apretado» `requires` la tapa puesta; la tapa no se retira mientras alguno esté apretado (dependencia). Los **tornillos sueltos** (M2x3) son piezas que van de su agujero (lugar con `requires` el soporte) a la bandeja (`restPoint`).
- **Contador de errores** (evaluación guiada, sin nota): `InteractionManager` llama a `GameManager.RegisterError` en cada rechazo con motivo (soltar en un lugar equivocado, retirar algo bloqueado, una bisagra que no abre). Soltar en vacío no cuenta. El HUD muestra «ERRORES n» bajo el progreso, y el modal de victoria, el resumen agrupado por motivo. `AssemblyHudView.progressNoun` / `victoryTitle` / `victoryText` cambian los textos por lab («PASOS» en la laptop).

**Lab_Laptop (2026-10-18)**: mantenimiento de un **Dell Latitude 5400** cerrado y boca abajo sobre una alfombrilla antiestática, con la bisagra al fondo (+Z), generado por `LabLaptopBuilder` (no editar `PC (Lab Laptop)` a mano). Orden y datos del Owner's Manual de Dell (P98G) y de su Setup and Specifications. **22 lugares obligatorios**; progreso «PASOS 0/22» al empezar:
- **Tapa inferior** con **8 tornillos cautivos** (clic: aflojar o apretar), en las posiciones de la figura del manual. Empieza puesta; se cierra al final (`requires` la batería conectada).
- **Batería primero**: todos los lugares de dentro `requiresEmpty` [tapa, puerto de la batería]. El puerto `requires` todo lo de dentro terminado, así que **reconectarla es lo último antes de cerrar**. La batería no se saca (el manual lo pide, pero basta desconectarla; la ficha lo explica).
- **Memoria**: ranura A con su módulo (no obligatoria, pero el puerto de la batería la exige llena) y ranura B vacía; entra en ángulo (30°).
- **SSD (falla) → nuevo**: 2 tornillos M2x3 → soporte (chapa negra) → SSD viejo; y al revés con el nuevo (`goalPart`, 20°). El viejo es el SSD de «MotherBoard + Components» y el nuevo, el 980 Pro. Los dos tienen el pivote en el borde de los contactos, abajo, para encajar en el mismo lugar.
- **Wi-Fi reasentada**: tornillo → soporte de las antenas → antenas (MAIN blanca, AUX negra; tipos distintos, cruzarlas es un error con motivo) → tarjeta M.2 2230 (20°), y vuelta. Los terminales son hijos de la tarjeta. Colores de las antenas: convención de otros manuales de Dell (el del 5400 no los da), indicada como tal en la ficha.
- **Cinta del touchpad** (llegó suelta: la avería): pestillo ZIF (`HingedPart`, 100°) levantado → cinta → pestillo bajado. El pestillo `requires` la cinta; la cinta `requiresEmpty` el pestillo.
- Posiciones internas **aproximadas** (de las figuras del manual), marcadas como tal en la ficha de la placa.

---

## 5. Singletons, eventos y ciclo de vida

| Singleton | Nace en | Vive | Notas |
|---|---|---|---|
| `AudioManager.Instance` | MainMenu | `DontDestroyOnLoad` (persiste entre escenas) | ⚠️ **ausente si abres AssemblyLab directo** → botones mudos (AUDITORIA §2.4) |
| `GameManager.Instance` | AssemblyLab | por escena | Destruye duplicados en `Awake` |
| `UIManager.Instance` | AssemblyLab | por escena | Destruye duplicados en `Awake` |

**Eventos** (`GameManager`): `OnPartInstalled(string)`, `OnPartRemoved(string)`, `OnProgressChanged(int installed, int total)`, `OnAssemblyComplete()`. `AssemblyHudView` se suscribe en `Start` (progreso + victoria) y se da de baja en `OnDestroy`. `TotalRequired`/`InstalledCount` se calculan en vivo desde `SocketSystem.All` (los sockets se registran en `OnEnable`, antes de cualquier `Start`).

---

## 6. Input (SimulationControls)

Mapa **Player** (usado en AssemblyLab): `Point` (posición ratón), `Delta`, `Select` (click izq), `Inspect` (click der), `Zoom` (scroll), `Pan` (botón central + arrastre), `ResetView` (Espacio), `ToggleInstructions` (tecla I).
Mapa **UI**: navegación estándar (Point, Click, Scroll, Maps, Submit, Cancel).

`Inspect` (click der) **ya no tiene conflicto**: la cámara solo orbita si el ratón se mueve > `orbitStartPixels` con el botón pulsado; `InteractionManager` abre la ficha al soltar solo si no hubo movimiento (AUDITORIA §6.1 resuelto).
Se instancian **dos** `new SimulationControls()` (InteractionManager + CADCameraController); ambos hacen `Dispose()` en `OnDestroy`.

---

## 7. Configuración persistente (settings)

- **PlayerPrefs**: `"VolumeLevel"` (int 0-3), `"Fullscreen"` (int 0/1).
- **AudioMixer**: `Assets/Project/Content/Audio/MainMixer.mixer`, parámetro expuesto **`"MasterVolume"`**.
- Niveles de volumen → dB: `0→-80, 1→-20, 2→-10, 3→0`.
- ⚠️ Esta lógica está **duplicada** casi idéntica en `MainMenuManager` y `UIManager` (~90 líneas). Candidata a un `SettingsService` (AUDITORIA §3.2).

---

## 8. Assets y datos

- **Modelos 3D**: `Assets/3D/` (paralelo a `Assets/Project/` — ver AUDITORIA §1.1). ⚠️ **Muy pesados**: motherboard 217k tris, gpu 211k, `hdd 1` 117k. Duplicados muertos: `gpu.fbx` (212k), `hdd.fbx` (5k, versión ligera **sin usar** — cambiar a esta ahorra 112k tris).
- **Prefabs de piezas**: repartidos entre `Assets/3D/prefabs/` y `Assets/Project/Prefabs/` (unificar).
- **Modelos del lab ATX nuevo** (2026-10-18): `Assets/Project/Content/Models/Lab_ATX/`, con `Gabinete` (Chasis, PanelLateral, 2 ventiladores), `PlacaBase` (PlacaBase, CPU, SSD_M2, RAM_1..4 **en su posición instalada**: sus transforms son los puntos de encaje), `GPU`, `Disipador`, `Ventilador120` y `SSD_980Pro`. Están a **escala real (1 u = 1 m)** con texturas ≤ 2048 en las carpetas `.fbm`. En total ≈ 90k tris. **Materiales: shader estilizado del proyecto** (decisión del usuario 2026-10-18), generados por `EstiloMateriales` en `<modelo>/Materiales/` + `Lab_ATX/Colores/` y aplicados por remapeo del FBX (reimportar no los pisa). La paleta lisa va de #1B1B1B a #E9E9E9, como las piezas viejas. Los materiales del gabinete son de doble cara (`_Cull = 0`) porque el modelo trae paredes de una sola cara. La fuente (`3D/Componentes/powersupply.fbx`, ×0,045) y el disco (`hdd.fbx`, el ligero) se reutilizan de los modelos viejos. Son de terceros (CC BY 4.0): créditos en `Docs/Creditos_Modelos.md`. Tubería de descarga y preparación: `Tools/Modelos/` (Blender por script).
- **Lab_SFF** (2026-10-18): sin modelos nuevos. El chasis (290 × 92,6 × 292 mm, chapa de 1,2 mm), la placa, el módulo de discos, el pestillo, el disipador con conducto, la fuente de 200 W y la tarjeta de perfil bajo se hacen con primitivas en `LabSffBuilder`. Sus materiales estilizados (`EstiloMateriales.SolidMaterial`) quedan en `Content/Models/Lab_SFF/Materiales` + `Colores`. Se reutilizan del lab ATX la CPU, la RAM y el SSD M.2 (del FBX de la placa), el ventilador (×0,68 → 80 mm) y el disco ligero. Los créditos son los mismos del lab ATX.
- **Lab_Laptop** (2026-10-18): `Content/Models/Lab_Laptop/` con `Laptop` (carcasa + `TapaInferior`, del Dell Latitude 5400 de Yolala3D), `SODIMM` (Cashierob) y `VentiladorLaptop` (Guy in a Poncho, reducido de 127k a 7,6k triángulos y llevado a unos 62 mm, tamaño aproximado). La carcasa se preparó en Blender (`blender_preparar.py`, modelo `dell_latitude_5400`): sin teclado, bisel ni panel; cerrada, volteada y llevada a 323,05 × 216 × 20,2 mm. El modelo era un 28 % más bajo: el alto se estiró. Se separó la tapa inferior, se le dio grosor a las paredes, se corrigieron las normales (media malla venía invertida) y se quitó el hueco del touchpad, que sobresalía hacia dentro. En Unity: interior a 6,3/8,7 mm, tapa por dentro a 18,6 mm. El constructor pone encima una bandeja plana a 8,9 mm y los materiales de la carcasa de doble cara (`_Cull = 0`). Interior con primitivas (materiales en `Lab_Laptop/Materiales` + `Colores`). Del lab ATX reutiliza el SSD de la placa (el viejo) y el 980 Pro (el nuevo). Créditos en `Docs/Creditos_Modelos.md` y en el modal.
- **Teoría**: jerarquía en `Assets/Project/Data/ScriptableObjects/TheoryContent/` — `TheorySyllabus.asset` (raíz) → `Units/U01..U11.asset` → `Topics/U01_T01..U11_T05.asset` (49 temas). Generados por script a partir de fuentes de texto plano. Los 20 SO planos antiguos están archivados en `TheoryContent/_Legacy/`.
- **Layers**: `Interactable = 3` (⚠️ slot reservado, debería ser 8+), `UI = 5`.

---

## 9. Cómo trabajar en este repo

**Añadir un componente ensamblable hoy**: crear/ubicar prefab con `PCPart` + `ObjectHighlighter` + collider en layer `Interactable` → asignar `connectionType` (con `displayName`/`placementHint` rellenos) → crear `SocketSystem` con `allowedType` + `snapPoint` (la pieza queda con la pose del snapPoint) → `required` si cuenta para completar, `requires` si exige otro lugar antes → rellenar `title`/`description`. El objetivo de victoria se recalcula solo. *(El refactor data-driven de la auditoría reduce esto a "crear un SO + arrastrar prefab".)*

**Lab ATX / Lab SFF / Lab Laptop**: cambiar posiciones, textos o cables en `LabAtxBuilder.cs` / `LabSffBuilder.cs` / `LabLaptopBuilder.cs` y ejecutar `Tools ▸ Simulador ▸ Reconstruir laboratorio ATX` / `… SFF (PC compacta)` / `… Laptop (Dell Latitude 5400)` con su escena abierta; luego guardar la escena. Por CLI, el constructor de la laptop pasa de los 5 s del hilo principal: ejecutarlo con un `eval_file` que escriba su log en `Temp/` (la operación termina aunque el CLI dé timeout). Los ayudantes comunes van en `LabBuilderKit` (un cambio ahí afecta a los dos: reconstruye `Lab_ATX` y compara con un volcado de la jerarquía antes de darlo por bueno). Un lab nuevo: copiar `Lab_ATX.unity` en disco (`AssetDatabase.CopyAsset`), abrir la copia, borrar `PC (Lab ATX)` y ejecutar su constructor (`SceneFrame` ajusta cámara y arrastre; `AssemblyHudView.extraInstructions` añade viñetas propias). El constructor mide el interior del gabinete con rayos y lo deja en su log. Los puertos de la placa se midieron sobre una captura cenital ortográfica del modelo (0,19 mm/px).

**Añadir un tema teórico**: crear un `TheoryTopicSO` (menú `Assets/Create/Simulador/Theory Topic`), rellenar `contentText` con el marcado ligero (`## título`, `- viñeta`, `1.` numerada, `: Término | Definición`, `> dato clave`, `**negrita**`) y añadirlo al array `topics` de la `TheoryUnitSO` correspondiente. El índice y la navegación se reconstruyen solos.

**Convenciones**: sin namespaces (global); campos `public` para exposición en inspector (preferir `[SerializeField] private`); serialización **Force Text** (YAML legible). Escribe código que respete el estilo existente del archivo.

**Consistencia entre pantallas**: los valores que deben coincidir en varias vistas viven como tokens en `UI/Common/variables.uss`, no duplicados en cada hoja. Ejemplo: `--menu-panel-width` (ancho del panel lateral) lo comparten MainMenu y ModeSelection — duplicarlo provocó un salto visible al cambiar de escena. Si un ajuste afecta a dos pantallas, hazlo token.

**Reparación del editor** (`Tools/`, fuera de `Assets/`): `reparar-shadergraph.ps1` borra el caché del paquete Shader Graph; `reparar-libreria.ps1` borra `Library/` salvo `PackageCache`. Ambos exigen Unity cerrado. Se usaron para un caso real de corrupción de disco (430 archivos del paquete rellenos de bytes nulos tras un cierre sucio) que se manifestaba como `undeclared identifier 'DecodeHDRSample'` en todos los Shader Graph. Lección: borrar el `PackageCache` no basta — la `ArtifactDB` conserva las huellas viejas y Unity marca el paquete como *immutable package unexpectedly altered*, y entonces no compila sus ensamblados de editor.

**Unity CLI + Pipeline** (instalado 2026-10-17, `com.unity.pipeline` en el manifest): con el editor abierto, `unity command <cmd> --project-path "C:\Users\devin\SimuladorArqComp_UNERMB"` lo controla en vivo (`unity status` → `ready`). Útiles: `open_scene`, `editor_play`/`editor_stop`, `console`/`console_status`, `eval --code '...'` / `eval_file --file Temp/x.cs` (sin `using`: nombres completos, p. ej. `UnityEngine.UIElements.UQueryExtensions.Q<T>(root, "nombre")`), `get_scene_hierarchy`, `save_scene`. ⚠️ **Abrir escenas con `open_scene`, nunca con `EditorSceneManager.OpenScene` dentro de un `eval`**: el cambio no persiste (la escena vuelve a la anterior y lo asignado no se guarda). Patrón fiable: `open_scene` → `eval` que modifica + `MarkSceneDirty` → `save_scene`. Para llamar a un método privado en Play: `go.SendMessage("Metodo", SendMessageOptions.RequireReceiver)`. ⚠️ `capture_game_view --save_path X` guarda **dentro de `Assets/`** (crea `Assets/X`; una ruta fuera del proyecto falla y deja un error en la consola): borrarlo después con `AssetDatabase.DeleteAsset`. ⚠️ Antes de cambiar de escena por CLI, vaciar la selección (`Selection.activeObject = null`): el overlay de Animation Rigging (`RigEffectorWizard.cs:35`) no comprueba si el Transform seleccionado fue destruido y lanza `MissingReferenceException` si la escena con los cables IK se descarga con un rig seleccionado (bug del paquete, solo en el editor e inofensivo; desaparece al quitar los cables IK). **Pruebas en Play por CLI:** `simulate_pointer` no llega al juego (el `Mouse.current` se queda en 0,0 aunque se ponga `InputSettings.editorInputBehaviorInPlayMode = AllDeviceInputAlwaysGoesToGameView` y `backgroundBehavior = IgnoreFocus`). Para probar la lógica de encaje, llamar por `eval` a lo mismo que `InteractionManager`: `PCPart.BeginDrag` → mover → `SocketSystem.FindTarget` → `PCPart.InstallInto`. Si se cambian esos ajustes de input, restaurarlos después (están en memoria, no en un asset). Para capturar sin crear assets: cámara temporal con `HideAndDontSave` → RenderTexture → `File.WriteAllBytes` fuera del proyecto. Unity recompila en cuanto detecta un cambio en un `.cs`, aunque esté en Play: no editar código con el Play abierto (los estáticos/singletons vuelven a null; los managers ya se recuperan en `OnEnable`). Sin foco, el editor no reimporta: `unity command recompile` funciona igual (o `editor_focus`). Las operaciones del hilo principal se cortan a los 5 s: en scripts largos, escribir el resultado a un archivo de `Temp/` (la operación sigue aunque el CLI dé timeout). ⚠️ `open_scene` falla si Unity está recompilando (p. ej. justo al salir de Play tras editar código): **comprobar la escena activa antes de `save_scene`**, que guarda la activa sin preguntar. `Tools/Unity/esperar_compilacion.sh` recompila, espera (`completed` o `up_to_date`) y muestra los avisos. `EditorSceneManager.ClearSceneDirtiness` (por reflexión) deja limpia una escena reconstruida solo para comparar. Si `unity status` da `unreachable` tras cambiar de rama, comprobar que la rama tiene `com.unity.pipeline` en el manifest (en `main` no está). **Blender 4.5** instalado en `C:\Program Files\Blender Foundation\Blender 4.5\blender.exe` (no está en el PATH) para preparar modelos por script.

**Unity MCP**: hay integración MCP con el editor (`Project Settings > AI > Unity MCP`). Si devuelve "Connection revoked", el usuario debe aprobar el cliente en ese panel y **reconectar el servidor MCP** (una aprobación nueva no se aplica a una sesión ya iniciada). Puede haber varias instancias de Unity abiertas: dejar solo la de este proyecto.

**Build**: escenas ya configuradas en Build Settings (orden §2). `productName`/`companyName` ya fijados; splash de Unity desactivado.

---

## 10. Deuda técnica prioritaria (resumen — detalle en AUDITORIA_TECNICA.md)

1. 🔴 **Modelo de datos**: componentes no data-driven; textos hardcodeados en la escena → crear `ComponentSpecSO`.
2. 🟠 **Compatibilidad**: igualdad de tipo + orden (`requires`) con "porqué" educativo (`CompatResult`); faltan reglas de dimensiones/consumo y tests.
3. 🟠 **Rendimiento**: 607k tris / ~259 draw calls en AssemblyLab; modelos de 200k+.
4. 🟢 **UX**: feedback de acierto/rechazo, desinstalar, fantasmas y conflicto de click derecho → **resueltos** (2026-10-17, pendiente de prueba en Play).
4b. 🟠 **Labs**: ATX (`Lab_ATX`), PC compacta (`Lab_SFF`) y laptop (`Lab_Laptop`) hechos, con el contador de errores y su resumen. Falta retirar `AssemblyLab` (cables antiguos de ChainIK), los conectores con forma y el prefab `Lab_Systems` → ver `Docs/Plan_Laboratorios_Practicos.md`.
5. 🟡 **Duplicación**: settings clonados → **ya resuelto en `SettingsService`** (§11); `AudioManager` ausente en AssemblyLab.

---

## 11. Migración a UI Toolkit (en curso)

Se está reemplazando la UI uGUI por **UI Toolkit**, escena por escena. Trabajar una escena por vez y validar antes de la siguiente. Referencia: `Docs/Prompt_Migracion_UI_Toolkit.md`.

**Cimientos compartidos** — `Assets/Project/UI/`:
- `PanelSettings.asset` — panel raíz (`ScaleWithScreenSize`, 1920×1080, **Match 0.5** — unifica la inconsistencia AUDITORIA §7.3). Compartido por todas las escenas UITK.
- `Common/theme.tss` — hereda `unity-theme://default`, importa `variables.uss` + `Controls.uss`, fija **TT Hoves** (Font Assets UITK en `Common/Fonts/`) como fuente raíz.
- `Common/variables.uss` — tokens (color de marca real `#FD4320`/`#FF4500`, spacing, tipografía, estado).
- `Common/Controls.uss` — clases reutilizables: `btn-primary`/`btn-ghost`, `modal-overlay`+`panel-modal`, `setting-row`+`volume-bar`, `toast`, `list-item`.
- `Common/SettingsService.cs` — **POCO estático**. Fuente única de settings (dedup §3.2): claves PlayerPrefs (`VolumeLevel`/`Fullscreen`), param `MasterVolume`, mapeo dB `{-80,-20,-10,0}`. Lo usan las vistas; ya NO se duplica lógica.
- `Common/UiSound.cs` — reemplazo UITK de `UISoundController` (callbacks hover/click → `AudioManager`).
- `Common/ModalPanel.cs` — `[UxmlElement]`. Velo modal que absorbe el puntero + evento `OnOpenChanged` (base de §7.2).
- `Common/FeedbackToast.cs` — `[UxmlElement]`. Toast icono+texto+motivo (§7.1), para acierto/rechazo del ensamblaje.
- `Common/DiagonalStripes.cs` — `[UxmlElement]`. **Fondo de rayas diagonales dibujadas por vector (`Painter2D`)**, no textura → nítidas a cualquier grosor sin moiré/blur; se auto-anima. Atributos: `stripe-width`/`gap`/`speed`/`stripe-color`/`back-color`. (Regla: para líneas finas nítidas en UITK, **vector, no texturas**.)
- `Common/UIBlocker.cs` — **POCO estático**. Reemplaza a `EventSystem.IsPointerOverGameObject()` (que NO ve la UI Toolkit). La vista del HUD registra su panel (`Register`) y expone `IsPointerOverUI(screenPos)` (`RuntimePanelUtils.ScreenToPanel` con **flip-Y** + `panel.Pick`; el root/contenedores van `picking-mode="Ignore"` para que las zonas vacías dejen pasar el clic al 3D). `ModalOpen` congela TODO cuando hay un modal abierto. Lo consultan `InteractionManager` y `CADCameraController`.

**Estado por escena — ✅ MIGRACIÓN COMPLETA (las 4 escenas):**
- ✅ **MainMenu** — migrado y escena limpia. Solo queda `MenuUI (UITK)` (UIDocument + `MainMenuView`) como UI; el `Canvas` uGUI y `MenuController` se **sacaron de la escena** a `UI/_Legacy/MainMenu_LegacyUGUI.prefab` (respaldo). **Diseño (mockups del usuario)**: layout dividido — IZQ = parte animada (rayas diagonales `DiagonalStripes` + `escritorio.png` abajo + título + PC ilustrado); DER = panel liso `rgb(240,240,240)` con menú alineado a la izquierda (ítem activo = barra de degradado naranja `grad_orange_h.png` con texto blanco). Modales cabecera-naranja + ✕ (Opciones y Créditos con contenido real). Resaltado del menú SOLO por `:focus` + foco-sigue-al-ratón (replica `AutoSelectOnHover`). `MainMenuView.simulationSceneName = "ModeSelection"`.
- ✅ **ModeSelection** — migrado. `ModeUI (UITK)` (UIDocument + `ModeSelectionView`) **reutiliza `MainMenu.uss`** vía `<Style src="../MainMenu/MainMenu.uss">` + `ModeSelection.uss` (solo ajusta tamaño/ancho de los ítems). 3 botones: MÓDULO TEÓRICO→TheoryModule, LABORATORIO DE ENSAMBLAJE→AssemblyLab, VOLVER AL MENÚ→MainMenu. UI vieja en `_Legacy/ModeSelection_LegacyUGUI.prefab`.
- ✅ **TheoryModule** — **rehecho** (ya no es la lista plana de 20 temas). `TheoryUI (UITK)` (UIDocument + `TheoryView`) en `UI/Theory/` (`Theory.uxml`+`Theory.uss`+`TheoryView.cs`+`TheoryMarkup.cs`), reutiliza de `MainMenu.uss` la cabecera naranja y el fondo `DiagonalStripes`. Estructura **unidad → tema** servida por un único `TheorySyllabusSO` serializado en `TheoryView.syllabus` (data-driven: el temario se cambia editando ese asset). Layout: cabecera + barra de progreso de lectura + **índice lateral** de dos niveles (unidades plegables, buscador que filtra por título/resumen/contenido, marca de tema actual y de temas leídos) + **panel de lectura** con la columna centrada y acotada (`.theory-measure`, ~90 caracteres). Tres estados alternados por `display`: **PORTADA** (datos de ACT139 + tarjetas de unidad), **LECTOR** (miga de pan, título, resumen, contenido renderizado por `TheoryMarkup`, figura opcional con pie, pie con anterior/siguiente y «Tema N de 49») y **BIBLIOGRAFÍA**. Progreso de lectura persistente en PlayerPrefs (`TheoryTopicsRead`). Atajos: ←/→ prev/next, Esc vuelve a portada (o sale). Sonido vía `UiSound.AttachAll` (una sola vez, al final de `Start`). UI vieja → `_Legacy/TheoryModule_LegacyUGUI.prefab`.
- ✅ **AssemblyLab HUD** — migrado y verificado en Play. `AssemblyHudUI (UITK)` (UIDocument + `AssemblyHudView`) en `UI/Assembly/` (`Assembly.uxml`+`Assembly.uss`+`AssemblyHudView.cs`), reutiliza modal/setting-row/iconos de `MainMenu.uss`. `AssemblyHudView` mantiene el singleton `Instance` (como `UIManager`): `ShowPartInfo(title,desc)`, `ShowVictory()`. Piezas: **Instrucciones** (arriba-izq, viñetas naranjas, ✕; se alterna con tecla I / botón ayuda ℹ), **Ficha técnica** (izq, al clic derecho; cabecera=nombre que envuelve, descripción con el nombre resaltado en naranja vía rich-text, imagen anclada abajo; **info e instrucciones son mutuamente excluyentes** — comparten la columna izq), botones **ℹ ayuda + ⚙ opciones** (arriba-der), **modal Opciones** (volumen/pantalla vía `SettingsService` + CONTINUAR/VOLVER AL MENÚ) y **modal Victoria**. **Bloqueo del raycast** (el punto crítico): `EventSystem.IsPointerOverGameObject()` NO ve la UITK → se creó `UIBlocker` (Pick con flip-Y, verificado: izq/paneles bloquean, 3D vacío pasa); `InteractionManager` (3 guardas + `ShowPartInfo`) y `CADCameraController` (1 guarda) ahora consultan `UIBlocker`. **Congelar sim**: los `ModalPanel` activan `UIBlocker.ModalOpen` vía `OnOpenChanged` (red de seguridad total). `GameManager.FinishSimulation` → `AssemblyHudView.Instance.ShowVictory()`. UI vieja: `UIManager` estaba **en el GO de GameManager** → se copió (con cableado, `ComponentUtility`) al respaldo `_Legacy/AssemblyLab_LegacyUGUI.prefab` junto con `MainCanvas`, y se eliminó el componente del GameManager (la clase `UIManager.cs` sigue en el proyecto para el prefab).

**Patrón de limpieza por escena** (establecido en MainMenu): al migrar, agrupar la UI uGUI vieja (Canvas + su manager) bajo un root, guardarla como prefab en `Assets/Project/UI/_Legacy/<Escena>_LegacyUGUI.prefab` y **quitarla de la escena** (no borrarla del proyecto). Conservar `EventSystem` (input UITK) y `AudioManager`. Al final, revisar `UISoundController`/`BackgroundScroller` y `[SerializeField]` de UI muertos.

**Migración terminada.** Todas las escenas usan UI Toolkit. Managers uGUI vivos que quedaron: ninguno en escena (respaldados en `_Legacy/`). `UIManager.cs`/`MainMenuManager.cs`/`ModeSelectionManager.cs`/`TheoryManager.cs` siguen como archivos en el proyecto (referenciados solo por los prefabs `_Legacy/`).

---

## 12. Cables (`Systems/Cables/`) y laboratorios prácticos

Plan completo (diagnóstico, 3 labs, assets): [Docs/Plan_Laboratorios_Practicos.md](Docs/Plan_Laboratorios_Practicos.md).

- **`CableSpline`** reemplaza a los cables de ChainIK (segmentos rígidos con `Target` estático). Bézier cúbica por tramos: sale recto de cada ancla por su **eje Z+ (= hacia donde sale el cable)**, Catmull-Rom en `waypoints`, holgura = `Length` − recorrido combada hacia `slackDirection`, `restSurface` opcional (no la atraviesa), perfil `Round`/`Flat` con corrección de giro (la cinta llega orientada a `endAnchor.up`). Malla `DontSave` regenerada solo si algo se mueve; `[ExecuteAlways]` → se ve al trazarla en el editor. `length <= 0` → automática (recorrido inicial × `autoSlack`), fijada al entrar en Play.
- **`CablePlug`** = extremo arrastrable. Va con un `PCPart` (`connectionType` = `Type_ATX24`…) y es el `endAnchor` del cable → reutiliza fantasma/rechazo/progreso/desinstalar. Implementa `IDragConstraint` (`ClampToReach` + orientación al arrastrar). Con `sourceSocket` + `homeWhenSourceInstalled`, el conector suelto cambia de sitio de reposo cuando la pieza de la que sale el cable se instala o se retira (`PCPart.SetHome`). Así, los de la fuente reposan en la mesa y, con la fuente instalada, en el suelo del gabinete.
- La holgura se comba **perpendicular a la cuerda**: si el cable baja en diagonal junto a una pared, "abajo" proyectado empuja el sobrante contra ella. Remedios: salidas casi a la altura del reposo, `looseSlack` del conector hacia el interior, o reposo justo debajo de la salida.
- **Puerto** = `SocketSystem` normal con el tipo del conector; su `snapPoint` con Z+ **hacia fuera** del puerto.
- Crear: `GameObject ▸ Simulador ▸ Cable (spline)` / `Puerto de cable` (`CableMenu.cs`). Material por defecto `Content/Resources/Mat_Cable.mat`.
- **Señal de puerto** (`CablePortMarker`, material `Content/Resources/Mat_PuertoCable.mat`, mismo shader que los fantasmas): la caja se crea en `Start` (después de que `ObjectHighlighter` guarde sus renderers) en la capa Ignore Raycast. `LabAtxBuilder.AddPortMarker` la pone en los 11 puertos. Su `size` está en metros y descuenta la escala heredada. ⚠️ Los modelos viejos reutilizados vienen escalados (el disco `hdd.fbx` lleva ×100): todo lo que se cuelgue de ellos con tamaño local hereda esa escala (la señal del disco llegó a medir 3,3 m).
- **Cables dentro de un equipo pequeño** (Lab_SFF): los que reposan sobre la placa se arquean hacia arriba (`looseSlack = up`) en vez de combarse, que la atravesaría (`restSurface` es un plano: el de la mesa, o uno a la altura de la placa para los cables fijos a ella). Cortos (≈ 0,10 m) y con `CableRig.stiffness = 0,015`: si sobra mucho, el arco asoma por encima del gabinete.
- **Cables dentro de una laptop** (Lab_Laptop): no hay sitio para arquearlos (atravesarían la tapa). `LabLaptopBuilder.LieFlat` comba el sobrante hacia abajo, sobre su `restSurface` (la placa; la cinta del touchpad, la bandeja), esté conectado o no (`archWhenConnected = false`). La longitud se cuenta desde el último punto de paso (`ReachOrigin`): tiene que sobrar alcance para sostener el conector a la altura del arrastre (2 cm). Con las antenas a 0,118 m no llegaban al terminal; con 0,14 m sí.
- Fantasmas de colocación: `Content/Resources/Mat_GhostPreview.mat` (shader `Custom/GhostPreview`, URP, translúcido + Fresnel; alfa por `MaterialPropertyBlock._BaseColor`).

**Herramientas de prueba** (`Tools/Unity/`): `capturar.sh` renderiza vistas de la escena abierta (edición o Play) a PNG en `Temp/capturas` con una cámara temporal (ortográfica opcional, para medir), y `arnes_ensamblaje.cs` prueba el montaje en Play sin ratón (`Try(pieza, lugar)`, `Toggle(bisagra)`, `Remove(pieza)`, `Progress()` y `Errors()`, vía `eval_file`). `Toggle` y `Remove` van por el mismo camino que el ratón (`InteractionManager.ToggleHinge` / `CheckDragStart`), así que sus rechazos cuentan como errores. Ojo: `Try` empieza arrastrando la pieza, así que sobre una instalada (la tapa) la retira. Para fotografiar una animación (la inserción en ángulo) se baja `Time.timeScale` (0,04) y se vuelve a 1 después. `volcar_jerarquia.cs` vuelca un `PC (Lab …)` entero (transforms y campos serializados, con las referencias por ruta) para comparar antes y después de tocar `LabBuilderKit`. `EditorJsonUtility` no sirve para eso en Unity 6.6: saca las referencias de escena como 0. Los prompts de los tres labs (`Docs/Prompt_Lab_PC_Compacta.md`, `Docs/Prompt_Lab_Laptop.md`) ya están hechos.

**Verificación sin editor:** con el editor sin conexión (sin MCP / sin `com.unity.pipeline`), se compila con una copia del `Assembly-CSharp.csproj` que incluye todos los `.cs` (también `Editor/`) y salida al scratchpad: `dotnet build` → 0 errores. LangVersion 9 / netstandard2.1: no usar `record` ni `init`.
