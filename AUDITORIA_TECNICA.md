# Auditoría Técnica Integral — Simulador de Ensamblaje de PCs (UNERMB)

**Fecha:** 2026-07-11 · **Método:** análisis estático de código, escenas (YAML), assets y ProjectSettings + **verificación en runtime** vía Unity MCP (editor 6000.4.7f1, escena `AssemblyLab` cargada). Los datos de runtime están en la sección final "Verificación en runtime".

## ⚙️ Configuración detectada

| Campo | Valor |
|---|---|
| Versión de Unity | **6000.4.7f1** (Unity 6.4 — no LTS) |
| Render Pipeline | **URP 17.4.0** (activo: `Assets/3D/URP/New Universal Render Pipeline Asset 1.asset`) |
| Dimensión | 3D (UI 2D overlay) |
| Plataforma | PC standalone (Windows) |
| Input System | **New Input System 1.19** (`SimulationControls.inputactions`) |
| Idioma UI | Español |
| Público | Estudiantes (contexto universitario, PNF Informática) |
| Estado | **Prototipo / MVP temprano** |

---

## 1. Resumen ejecutivo

El proyecto es un prototipo pequeño (17 scripts, ~1.200 líneas propias, 6 escenas) con **fundamentos mejores que el promedio de un proyecto estudiantil**: usa prefabs (28 instancias en la escena principal), New Input System, ScriptableObjects para tipos de conexión y contenido teórico, un pool de audio, eventos C#, serialización Force Text y un `.gitignore` correcto. La estructura `Assets/Project/{Content,Data,Prefabs,Scenes,Scripts,Systems}` es sana.

Las debilidades se concentran exactamente en lo que el simulador dice ser: **el modelo de datos de componentes y la lógica de compatibilidad son mínimos** (una sola regla: igualdad de `ConnectionTypeSO`), el contenido educativo de las piezas vive hardcodeado dentro de la escena, no existe feedback que explique *por qué* una pieza no encaja, no hay forma de desinstalar/deshacer, y no hay un solo test. Nada de esto es grave hoy porque el alcance es 4 piezas y 4 sockets, pero es el eje que decidirá si el proyecto escala o se vuelve inmantenible.

### Tabla de salud

| # | Categoría | Estado | Nota |
|---|---|---|---|
| 1 | Estructura y organización | 🟡 | Base buena; `Assets/3D` paralelo, duplicados y assets basura |
| 2 | Escenas y jerarquía | 🟡 | Prefabs bien usados; datos educativos atrapados en la escena |
| 3 | Arquitectura de código | 🟡 | Managers pequeños; eventos declarados pero sin uso, duplicación de settings |
| 4 | Modelo de datos ⭐ | 🔴 | No hay specs de componentes; no es data-driven |
| 5 | Compatibilidad ⭐ | 🔴 | Una sola regla por igualdad de tipo; sin motor de reglas; hueco null==null |
| 6 | Interacción / ensamblaje | 🟡 | Snap funcional; conflicto de click derecho; sin undo/desinstalar |
| 7 | UI / UX | 🟡 | Canvas Scaler ok; feedback solo por color; pausa no bloquea el mundo |
| 8 | Capa educativa | 🟡 | Teoría data-driven ✓; ensamblaje sin "porqué" ni evaluación |
| 9 | Rendimiento | 🟡→🟠 | Sin física innecesaria ✓; pero **607k tris / 259 draw calls** en escena, FBX de 200k+, leak de materiales, Gamma |
| 10 | Buenas prácticas Unity | 🟡 | Campos públicos, magic numbers, layer en slot reservado |
| 11 | Configuración y build | 🟢 | Build Settings correctos; paquetes AI pre-release sobran |
| 12 | Testing | 🔴 | Cero tests; la lógica de compatibilidad es testeable y no está testeada |
| 13 | Escalabilidad | 🟠 | Añadir una pieza = ~6 pasos manuales en escena + contador manual |

---

## 2. Hallazgos por categoría

### Categoría 4 — Modelo de datos de componentes ⭐

**4.1 — No existe un modelo de specs; los componentes no son data-driven**
- **Severidad:** 🔴 Crítico (para el objetivo del proyecto)
- **Ubicación:** `Assets/Project/Data/ScriptableObjects/Connections Type/ConnectionTypeSO.cs:5-9`, `Assets/Project/Systems/Assembly/PCPart.cs:9-16`
- **Problema:** El único dato de un componente es un `ConnectionTypeSO` con `labelName` y `debugColor`. No hay socket, factor de forma, TDP, tipo de RAM, versión PCIe, dimensiones, vataje ni precio. La identidad de cada pieza (`partName`, `title`, `description`) son strings sueltos en cada instancia `PCPart` **dentro de la escena** `AssemblyLab.unity` (p. ej. líneas 499, 5631, 12720). No hay única fuente de verdad.
- **Impacto:** Editar la descripción de la CPU exige abrir y modificar la escena (conflictos de merge en YAML de 22.000 líneas). Añadir un componente nuevo exige tocar escena + inspector + contador manual. Las validaciones ricas (sección 5) son imposibles sin datos.
- **Recomendación:** Crear `ComponentSpecSO : ScriptableObject` con los campos de identidad + specs, y que `PCPart` solo referencie ese SO:
  ```csharp
  [CreateAssetMenu(menuName = "Simulador/Component Spec")]
  public class ComponentSpecSO : ScriptableObject {
      public string partName;
      [TextArea] public string description;
      public ConnectionTypeSO connectionType;
      public int wattage;          // consumo o capacidad PSU
      public float lengthMm;       // clearance GPU
      public RamType ramType;      // enum DDR4/DDR5
      // ... ampliar según reglas de la sección 5
  }
  ```
  `PCPart` pasa a tener un único campo `public ComponentSpecSO spec;`. Las 28 instancias de la escena migran a referencias.
- **Esfuerzo:** M

**4.2 — Contenido muerto: PSU y HDD tienen tipo pero no socket**
- **Severidad:** 🟡 Medio
- **Ubicación:** `AssemblyLab.unity:12829` (PSU, `Type_PSU`), `:16539` (HDD, `Type_HDD`); sockets en `:3085, :7708, :8412, :8961` (solo RAM×2, GPU, CPU)
- **Problema:** Existen `Type_PSU.asset` y `Type_HDD.asset` y piezas que los usan, pero **verificado en runtime solo hay 4 sockets (CPU, GPU, RAM×2)** — ninguno acepta PSU ni HDD, y ambas piezas están con `isDraggable: 0`. Son contenido a medio integrar: el catálogo aparenta 6 tipos pero solo 3 son instalables.
- **Impacto:** Confusión para quien mantenga el proyecto; el catálogo aparenta 6 tipos pero solo 3 son instalables.
- **Recomendación:** O completar sus sockets (bahía HDD, hueco PSU del gabinete) o marcar explícitamente el estado con un comentario/issue.
- **Esfuerzo:** S (decidir) / M (implementar sockets)

### Categoría 5 — Lógica de compatibilidad y validación ⭐

**5.1 — La validación es una sola igualdad de referencia; no hay motor de reglas**
- **Severidad:** 🔴 Crítico (para el objetivo del proyecto)
- **Ubicación:** `Assets/Project/Systems/Assembly/SocketSystem.cs:16-26`
- **Problema:** `CanAccept` = `!isOccupied && part.connectionType == allowedType`. No existe ninguna de las validaciones del dominio (CPU↔socket específico, RAM↔generación, GPU↔clearance, consumo↔PSU, form factor↔gabinete, slots disponibles). La lógica está acoplada a MonoBehaviours, no es extensible ni testeable sin escena.
- **Impacto:** El simulador enseña "encaja/no encaja" pero no puede enseñar *compatibilidad*, que es su propuesta de valor educativa. Cada regla nueva hoy implicaría `if/else` dentro de `SocketSystem`.
- **Recomendación:** Extraer un motor de reglas puro (clase C# sin `MonoBehaviour`):
  ```csharp
  public interface ICompatibilityRule {
      CompatResult Evaluate(ComponentSpecSO part, SocketSpec socket, BuildState build);
  }
  public record CompatResult(bool Ok, string ReasonEs); // el "porqué" educativo
  ```
  `SocketSystem.CanAccept` delega en una lista de reglas y devuelve el primer motivo de rechazo. `BuildState` (piezas ya instaladas) habilita reglas globales como consumo total vs. vataje de PSU.
- **Esfuerzo:** L

**5.2 — Hueco lógico: `null == null` pasa la validación**
- **Severidad:** 🟠 Alto
- **Ubicación:** `SocketSystem.cs:22`; en escena hay **18 `PCPart` con `connectionType: {fileID: 0}`** (`AssemblyLab.unity:1231, 2826, 4379, 6645, 14842, 16261…`)
- **Problema:** Si un `SocketSystem` queda con `allowedType` sin asignar (error de inspector fácil de cometer), cualquier pieza sin tipo lo satisface, porque `null != null` es `false`. **Verificado en runtime:** los 4 sockets (`SnapCPU→Type_CPU`, `SnapGPU→Type_GPU`, `SnapRAM1/RAM2→Type_RAM`) tienen `allowedType` y `snapPoint` asignados, así que es un **riesgo latente, no un bug activo hoy**.
- **Impacto:** Un descuido de configuración produce encajes absurdos sin ningún error visible.
- **Recomendación:** Blindar la validación y avisar en editor:
  ```csharp
  if (allowedType == null || part.connectionType == null) return false;
  ```
  más un `OnValidate()` que loguee error si `allowedType` o `snapPoint` están vacíos.
- **Esfuerzo:** S

**5.3 — Las reglas no están testeadas (ver hallazgo 12.1)**

### Categoría 6 — Sistema de interacción / ensamblaje

**6.1 — Conflicto de input: el click derecho hace dos cosas a la vez**
- **Severidad:** 🟠 Alto
- **Ubicación:** `InteractionManager.cs:58` y `:175-200` (Inspect → ficha técnica) vs. `CADCameraController.cs:121-138` (Inspect → órbita)
- **Problema:** La acción `Player/Inspect` (botón derecho) abre la ficha informativa **y simultáneamente** orbita la cámara al arrastrar. Al inspeccionar una pieza, cualquier micro-movimiento del mouse gira la cámara mientras se abre el panel.
- **Impacto:** UX confusa en la interacción más frecuente del modo educativo.
- **Recomendación:** Separar acciones (p. ej. click derecho corto = ficha, arrastre derecho = órbita, usando el interaction `Press`/`SlowTap` del Input System, o mover la órbita a otro botón/modificador). Como mínimo, suprimir la órbita durante el frame en que se abre la ficha.
- **Esfuerzo:** M

**6.2 — No hay desinstalación ni undo; los sockets nunca se liberan**
- **Severidad:** 🟠 Alto
- **Ubicación:** `SocketSystem.cs:29-42` (`isOccupied = true` sin contraparte), `PCPart.cs:19` (`IsInstalled` bloquea re-arrastre), `PCPart.cs:68-73` (`ResetToTable` existe pero nada lo invoca tras instalar)
- **Problema:** El ensamblaje es unidireccional: una vez instalada la pieza no puede retirarse; equivocarse de slot RAM (hay 2) es permanente hasta reiniciar la escena. Tampoco hay botón de reinicio del ensamblaje.
- **Impacto:** En un contexto educativo, el ensayo-error es el mecanismo de aprendizaje; impedir deshacer castiga el error en lugar de enseñar.
- **Recomendación:** Implementar `SocketSystem.RemovePart()` (libera `isOccupied`, desparenta, decrementa contador en `GameManager`, llama `ResetToTable`) y permitir arrastrar piezas instaladas para extraerlas.
- **Esfuerzo:** M

**6.3 — Snap por esfera hardcodeada y sin máscara de capas**
- **Severidad:** 🟡 Medio
- **Ubicación:** `PCPart.cs:39` — `Physics.OverlapSphere(transform.position, 0.5f)`
- **Problema:** Radio mágico `0.5f` global (una RAM y una GPU tienen tolerancias muy distintas) y sin `LayerMask`, por lo que la esfera colisiona contra todos los colliders de la escena (motherboard, decoración) y filtra por `GetComponent` a posteriori.
- **Impacto:** Fragilidad al escalar (piezas pequeñas cerca de varios sockets) y trabajo físico innecesario.
- **Recomendación:** `[SerializeField] float snapRadius = 0.5f;` por pieza + `LayerMask` de sockets dedicada; opcionalmente usar el mismo raycast layer ya existente (`Interactable`).
- **Esfuerzo:** S

**6.4 — Sin feedback de socket candidato durante el arrastre**
- **Severidad:** 🟡 Medio
- **Ubicación:** `InteractionManager.cs:209-229` (`MovePart`), `SocketSystem.cs` (no tiene visual)
- **Problema:** Mientras se arrastra, el usuario no ve qué sockets existen ni cuál aceptaría la pieza; el resultado solo se conoce al soltar (encaja o vuelve a la mesa).
- **Impacto:** Descubribilidad pobre; el estudiante no aprende dónde va cada pieza, adivina.
- **Recomendación:** Durante el drag, resaltar los sockets compatibles (verde) e incompatibles (gris/rojo + icono) consultando `CanAccept` en tiempo real; ya existe la infraestructura de materiales de highlight.
- **Esfuerzo:** M

### Categoría 3 — Arquitectura de código

**3.1 — Eventos del GameManager declarados pero sin un solo suscriptor**
- **Severidad:** 🟡 Medio
- **Ubicación:** `GameManager.cs:17-18` (`OnPartInstalled`, `OnAssemblyComplete`); la victoria se resuelve llamando directo a `UIManager.Instance.ShowVictory()` en `GameManager.cs:65-72`
- **Problema:** El proyecto tiene el patrón correcto (eventos) declarado y documentado, pero lo puentea con llamadas directas singleton→singleton. `SocketSystem.cs:37` también llama `GameManager.Instance` sin null-check.
- **Impacto:** Acoplamiento innecesario; `SocketSystem` lanza `NullReferenceException` en escenas sin GameManager (p. ej. `Test.unity`/`CableTest.unity`). Código muerto que confunde.
- **Recomendación:** Que `UIManager` se suscriba a `OnAssemblyComplete` en `OnEnable/OnDisable` y eliminar la llamada directa; en `SocketSystem`, guardar con `if (GameManager.Instance != null)`.
- **Esfuerzo:** S

**3.2 — Lógica de settings duplicada íntegramente en dos managers**
- **Severidad:** 🟡 Medio
- **Ubicación:** `MainMenuManager.cs:60-158` vs `UIManager.cs:58-196` (LoadSettings/SaveSettings/UpdateVolume/UpdateVolumeUI/ToggleScreenMode/UpdateScreenUI, ~90 líneas clonadas, los propios comentarios lo admiten: "IGUAL AL MENÚ PRINCIPAL")
- **Problema:** Volumen por niveles, mapeo a dB `{-80,-20,-10,0}`, claves de PlayerPrefs `"VolumeLevel"`/`"Fullscreen"` y actualización de iconos existen dos veces.
- **Impacto:** Cualquier cambio (añadir un nivel de volumen, renombrar la clave) debe hacerse en dos sitios; ya hay una divergencia (el null-check de `mainMixer` con warning existe en UIManager:143 pero no en MainMenuManager:111).
- **Recomendación:** Extraer `SettingsService` (clase plana o un `SettingsManager` persistente) con las constantes de PlayerPrefs, y que ambos managers solo pinten UI.
- **Esfuerzo:** M

**3.3 — Default de escena inexistente y carga sin validación**
- **Severidad:** 🟢 Bajo (mitigado por override en escena)
- **Ubicación:** `ModeSelectionManager.cs:11` (`simulationScene = "MainSimulation"` — esa escena no existe; el valor real `AssemblyLab` está serializado en `ModeSelection.unity:257`), `ModeSelectionManager.cs:18-22` (`GoToSimulation` no valida, a diferencia de `GoToTheory`:28)
- **Problema:** El default del código está desactualizado; funciona solo porque la escena lo sobreescribe. Nombres de escena como strings mágicos en 4 scripts.
- **Impacto:** Colocar el componente en un objeto nuevo rompe la navegación silenciosamente en build (en editor `LoadScene` loguea error).
- **Recomendación:** Corregir el default a `"AssemblyLab"` y aplicar el mismo guard `Application.CanStreamedLevelBeLoaded` de `GoToTheory` en todas las cargas; idealmente centralizar los nombres en una clase `SceneNames`.
- **Esfuerzo:** S

**3.4 — Dos copias de `SimulationControls` y ninguna se libera**
- **Severidad:** 🟢 Bajo
- **Ubicación:** `InteractionManager.cs:41` y `CADCameraController.cs:47` crean cada uno `new SimulationControls()`; ninguno llama `Dispose()` (el finalizer generado lanza asserts: `SimulationControls.cs:531-535`)
- **Problema:** Duplicación del asset de input en memoria y warning de leak al cerrar/recargar escena.
- **Impacto:** Menor; ruido en consola y estado de input duplicado.
- **Recomendación:** `Dispose()` en `OnDestroy()` de ambos, o compartir una única instancia (p. ej. expuesta por un `InputService`).
- **Esfuerzo:** S

**3.5 — Sin namespaces**
- **Severidad:** 🟢 Bajo — Todos los scripts viven en el namespace global. Con 17 scripts no es un problema objetivo, pero adoptarlo ahora (`Unermb.Simulador.Assembly`, `.UI`, etc.) es barato y evita colisiones con paquetes. **Esfuerzo:** S

### Categoría 2 — Escenas y jerarquía

**2.1 — El contenido educativo de 28 piezas vive serializado en la escena**
- **Severidad:** 🟠 Alto (mantenibilidad de contenido)
- **Ubicación:** `AssemblyLab.unity` — 28 componentes `PCPart` con `partName/title/description` inline (líneas 499-22160); la escena pesa 22.337 líneas
- **Problema:** Es el mismo hallazgo 4.1 visto desde la escena: los textos que el docente querrá iterar están dentro del archivo más conflictivo del repo. El módulo de teoría ya resolvió esto bien con `TheoryTopicSO` — el patrón existe en el propio proyecto.
- **Recomendación:** Migrar a `ComponentSpecSO` (hallazgo 4.1). 
- **Esfuerzo:** M

**2.2 — Escenas de laboratorio y assets de prueba en el repositorio**
- **Severidad:** 🟢 Bajo
- **Ubicación:** `Assets/Project/Scenes/Test.unity`, `CableTest.unity` (fuera de Build Settings ✓); `Assets/3D/Typo/robotperosinosirvemalamia.fbx`; `Assets/leaf.mat` en la raíz de Assets
- **Problema:** Material de experimentación mezclado con producción.
- **Recomendación:** Carpeta `Assets/_Sandbox/` explícita (o borrar lo que no se use). No es urgente.
- **Esfuerzo:** S

**2.3 — Missing scripts: 0, confirmado en runtime** ✅ — Barrido de todos los GameObjects de `AssemblyLab` en el editor: **0 componentes nulos / missing scripts**. Sin referencias rotas.

**2.4 — `AudioManager` ausente en `AssemblyLab` → botones del HUD mudos si se entra directo**
- **Severidad:** 🟡 Medio
- **Ubicación:** jerarquía de `AssemblyLab` (no existe `AudioManager`); `AudioManager.cs:22` (`DontDestroyOnLoad`), consumidores `UISoundController.cs:27,36` (`AudioManager.Instance`) en `Btn_Info`, `Btn_Salir`, `Btn_Config`
- **Problema:** El `AudioManager` es un singleton persistente que solo nace en el `MainMenu`. En el flujo normal (Menú → Selección → Ensamblaje) persiste, pero **abrir `AssemblyLab` directamente** (lo habitual al desarrollar y testear) deja `AudioManager.Instance == null` y los botones del HUD no suenan. El código ya se protege con null-checks, así que no crashea — pero es un fallo silencioso fácil de diagnosticar mal.
- **Recomendación:** Colocar un `AudioManager` en cada escena jugable (el propio singleton destruye los duplicados en `Awake`, `AudioManager.cs:26`), o un bootstrap/`RuntimeInitializeOnLoad` que lo garantice.
- **Esfuerzo:** S

### Categoría 1 — Estructura del proyecto

**1.1 — Organización dual `Assets/Project` vs `Assets/3D` + duplicados**
- **Severidad:** 🟡 Medio
- **Ubicación:** `Assets/3D/` (67 MB: modelos, sus propios `prefabs/` y `URP/`) vs `Assets/Project/` (15 MB); duplicados: `Assets/3D/Componentes/gpu.fbx` (4,8 MB) vs `gpu 1.fbx` (6 MB), `hdd.fbx` vs `hdd 1.fbx`; prefabs de piezas en `Assets/3D/prefabs/` pero `Assets/Project/Prefabs/Parts/` también existe
- **Problema:** Dos árboles compiten por ser la raíz del proyecto; los FBX duplicados suman ~7 MB muertos y hacen ambiguo cuál es la fuente.
- **Recomendación:** Absorber `Assets/3D` dentro de `Assets/Project/Content/Models` (mover desde el editor de Unity para preservar GUIDs), borrar los FBX no referenciados y unificar prefabs en `Assets/Project/Prefabs/`.
- **Esfuerzo:** M

**1.2 — Configuración sana confirmada:** `.gitignore` estándar de Unity ✓; Asset Serialization en **Force Text** (`ProjectSettings/EditorSettings.asset:7`, `m_SerializationMode: 2`) ✓; sin abuso de `Resources/` (solo TextMesh Pro) ✓; sin `.asmdef` (aceptable a esta escala; considerar cuando haya tests). La documentación (`Prompt_Auditoria_Unity_Ensamblaje_PCs.md`) está dentro de `Assets/`, lo que genera `.meta` innecesarios — mover a `/Docs` en la raíz del repo. **Esfuerzo:** S

**1.3 — Paquetes sobrantes / pre-release**
- **Severidad:** 🟡 Medio
- **Ubicación:** `Packages/manifest.json:3-4` (`com.unity.ai.assistant 2.14.0-pre.1`, `com.unity.ai.inference`), `:9` (`multiplayer.center`), `:14` (`visualscripting`), `:5` (`animation.rigging`), `:12` (`timeline`)
- **Problema:** Un paquete **pre-release** de AI Assistant en un proyecto académico estable, más paquetes sin uso aparente en el código (no hay grafos de VisualScripting ni Timelines en `Assets/Project`).
- **Impacto:** Tiempos de import/compilación, superficie de bugs ajenos, tamaño del repo (`ProjectSettings/Packages/com.unity.ai.assistant/` ya aparece sin versionar en git).
- **Recomendación:** Remover los que no se usen y verificar que el proyecto compile tras cada remoción.
- **Esfuerzo:** S

### Categoría 7 — UI / UX

**7.1 — El feedback de compatibilidad depende solo del color**
- **Severidad:** 🟠 Alto (accesibilidad + pedagogía)
- **Ubicación:** `InteractionManager.cs:12-15` (hover naranja / drag verde), `SocketSystem.cs:39` (el éxito solo se loguea a consola), `PCPart.cs:63-65` (el fallo es un teleport silencioso a la mesa)
- **Problema:** No hay texto, icono ni sonido que comunique éxito/fracaso del encaje, y menos el motivo. Un usuario daltónico ni siquiera distingue los estados de highlight.
- **Impacto:** Es la brecha educativa más importante: el momento de máximo aprendizaje (el error) hoy no comunica nada.
- **Recomendación:** Al rechazar, mostrar un toast/panel con icono + texto ("La RAM no encaja aquí: esta ranura es PCIe x16"), y reproducir sonidos de acierto/error con el `AudioManager` ya existente (hoy el pool de audio solo lo usan los botones del menú — `UISoundController.cs`). El motivo textual sale gratis del motor de reglas del hallazgo 5.1.
- **Esfuerzo:** M

**7.2 — La pausa y la victoria no congelan la simulación**
- **Severidad:** 🟡 Medio (requiere verificación en runtime)
- **Ubicación:** `UIManager.cs:88-93` (`OpenPauseMenu` solo activa un panel), `InteractionManager.cs:120` (solo bloquea input cuando el puntero está sobre UI)
- **Problema:** No se toca `Time.timeScale` ni se deshabilita `InteractionManager`; si los paneles de pausa/victoria no cubren el 100% de la pantalla con raycast target, se puede seguir arrastrando piezas "en pausa". La cámara sigue con su auto-reset idle.
- **Recomendación:** En pausa/victoria, deshabilitar `InteractionManager` y `CADCameraController` (o un flag `InputLocked` consultado por ambos).
- **Esfuerzo:** S

**7.3 — Inconsistencias menores de UI**
- **Severidad:** 🟢 Bajo
- **Ubicación:** `ModeSelection.unity:463` (`m_MatchWidthOrHeight: 1`) vs `0` en las otras 3 escenas; `UIManager.cs:95` y `:122` (`ClosePauseMenu`/`CloseInfoPanel` desreferencian sin null-guard, mientras el resto del archivo sí los usa); colores de barras de volumen hardcodeados idénticos en dos archivos (`UIManager.cs:155-156`, `MainMenuManager.cs:119-120`)
- **Recomendación:** Unificar match a `0.5` (o 1 si la UI es vertical-safe), añadir los guards, mover colores a constantes del futuro `SettingsService`.
- **Esfuerzo:** S

### Categoría 8 — Capa educativa

**8.1 — Módulo de teoría: bien resuelto** ✅ — `TheoryTopicSO` + 20 temas en assets (`Assets/Project/Data/ScriptableObjects/TheoryContent/`), índice generado dinámicamente (`TheoryManager.cs:43-58`), navegación con estados correctos. Es el patrón a replicar en el ensamblaje.

**8.2 — El ensamblaje no tiene modo guiado, evaluación ni progreso**
- **Severidad:** 🟡 Medio (según alcance académico)
- **Ubicación:** `GameManager.cs` (solo cuenta piezas: `:39-53`)
- **Problema:** No hay tutorial paso a paso, ni puntuación, ni registro de intentos fallidos (el dato pedagógico más valioso), ni persistencia de progreso entre sesiones (los PlayerPrefs solo guardan volumen/pantalla).
- **Recomendación:** Fase posterior: secuencia guiada opcional (instalar CPU → RAM → GPU en orden con pistas) + contador de errores por pieza como métrica de evaluación.
- **Esfuerzo:** L

### Categoría 9 — Rendimiento

**9.1 — `ObjectHighlighter` instancia materiales en cada hover (leak + rompe batching)**
- **Severidad:** 🟡 Medio
- **Ubicación:** `ObjectHighlighter.cs:43` (`renderer.materials = newMats`) y `:57` (la "restauración" también asigna vía `.materials`)
- **Problema:** Cada asignación a `Renderer.materials` clona los materiales en instancias únicas que no se destruyen; pasar el mouse repetidamente por las 28 piezas acumula materiales huérfanos hasta el cambio de escena y desactiva el batching de esos renderers desde el primer hover.
- **Impacto:** Memoria y draw calls crecientes en la escena donde el usuario pasa todo el tiempo.
- **Recomendación:** Restaurar con `renderer.sharedMaterials = _originalMaterials[renderer]` y aplicar el highlight sin clonar (array con `sharedMaterials` + material de highlight compartido), o mejor: `MaterialPropertyBlock`/Rendering Layers de URP para el outline.
- **Esfuerzo:** S-M

**9.2 — Color space en Gamma con URP**
- **Severidad:** 🟡 Medio
- **Ubicación:** `ProjectSettings/ProjectSettings.asset:50` (`m_ActiveColorSpace: 0`)
- **Problema:** URP en PC debería usar **Linear** (estándar desde hace años); Gamma altera el resultado de la iluminación física.
- **Impacto:** La iluminación y los materiales se ven distintos a como se autorarían en cualquier referencia URP moderna.
- **Recomendación:** Cambiar a Linear en Player Settings y re-revisar la iluminación de las 4 escenas (cambio visual seguro pero visible; hacerlo antes de pulir arte, no después).
- **Esfuerzo:** S (cambio) + M (repaso visual)

**9.3 — Modelos 3D extremadamente pesados (verificado en runtime)** ⬆️ *severidad subida tras medir*
- **Severidad:** 🟠 Alto
- **Ubicación:** `Assets/3D/Componentes/` — poly counts reales medidos en el editor:
  | Modelo | Triángulos | Sub-meshes | En escena |
  |---|---|---|---|
  | `motherboard.fbx` | **217.789** | 67 | sí |
  | `gpu.fbx` | 212.755 | 1 | **no (duplicado muerto)** |
  | `gpu 1.fbx` | **211.603** | 19 | sí |
  | `hdd 1.fbx` | **117.297** | 6 | sí |
  | `powersupply.fbx` | 25.016 | 7 | sí |
  | `cpu.fbx` | 6.038 | 3 | sí |
  | `hdd.fbx` | **5.131** | 1 | no (versión ligera sin usar) |
  | `ram.fbx` | 1.233 | 1 | sí (×2) |
- **Problema:** La escena `AssemblyLab` renderiza **607.797 triángulos activos, 201 renderers y ~259 slots de material (≈ draw calls)**. Solo motherboard + GPU + HDD suman ~546k tris para piezas que ocupan una fracción de pantalla. La motherboard con **67 sub-meshes** son 67 sub-renderers. Es ~7× el presupuesto orientativo (<30k/pieza) y una carga seria para los portátiles modestos típicos de un aula.
- **Impacto:** Frame rate bajo y consumo en equipos de gama media/baja; agravado por el color space Gamma (9.2), 5 luces realtime y el churn de materiales del highlighter (9.1).
- **Recomendación (accionables concretos, alto ROI):**
  1. **HDD:** la escena usa `hdd 1.fbx` (117k) cuando existe `hdd.fbx` (5k) — **cambiar el prefab ahorra 112k tris de un golpe**.
  2. **GPU/motherboard:** decimar en el DCC o con un importador de reducción (objetivo <30-40k); son los dos peores.
  3. Borrar `gpu.fbx` (212k, no referenciado): duplicado muerto en el repo.
  4. Activar *Mesh Compression* y *Optimize Mesh* en el importador de los pesados.
- **Esfuerzo:** M

**9.4 — Positivos confirmados** ✅ — Sin `Rigidbody` en la escena (0 encontrados): la física solo sirve raycasts, correcto. Sin `GetComponent`/`Find` en `Update` fuera del raycast de hover (patrón estándar). `AudioManager` con pooling y tope (`AudioManager.cs:31-114`). `BackgroundScroller` es trivialmente barato. Sin LINQ ni concatenación de strings en rutas calientes.

### Categoría 10 — Buenas prácticas Unity

**10.1 — Estado mutable expuesto como campos públicos**
- **Severidad:** 🟢 Bajo
- **Ubicación:** `SocketSystem.cs:13` (`public bool isOccupied`), `PCPart.cs:7-16`, y en general campos `public` en vez de `[SerializeField] private` en todos los managers
- **Problema:** `isOccupied` es escribible desde cualquier script; los campos de configuración no distinguen "editable en inspector" de "API pública". `AudioManager.cs:10` sí usa el patrón correcto — hay inconsistencia, no ignorancia.
- **Recomendación:** `[SerializeField] private` + propiedades de solo lectura donde otros scripts leen (`IsInstalled` de `PCPart.cs:19` ya lo hace bien).
- **Esfuerzo:** S

**10.2 — Layer "Interactable" en el slot 3 (reservado)**
- **Severidad:** 🟢 Bajo
- **Ubicación:** `ProjectSettings/TagManager.asset:11`
- **Problema:** Los layers de usuario van del 8 en adelante; el slot 3 es builtin reservado (el inspector normalmente no permite editarlo, esto parece editado a mano). Funciona, pero una versión futura de Unity podría reclamar el slot.
- **Recomendación:** Mover "Interactable" al slot 8 y reasignar en prefabs/escena (rápido con búsqueda por layer).
- **Esfuerzo:** S

**10.3 — Números y strings mágicos** — radio de snap `0.5f` (`PCPart.cs:39`), dB de volumen (dos archivos), claves de PlayerPrefs, nombres de escenas, `10f` idle (`CADCameraController.cs:36` — este sí es campo serializado ✓). Consolidar en constantes/SO de configuración. **Esfuerzo:** S

### Categoría 11 — Configuración y build

**11.1 — Estado general correcto** ✅ — 4 escenas correctas y ordenadas en Build Settings (`EditorBuildSettings.asset:8-19`); escenas de test excluidas; `productName` correcto.

**11.2 — Pipeline assets URP duplicados y con nombres por defecto**
- **Severidad:** 🟢 Bajo
- **Ubicación:** `Assets/3D/URP/`: el pipeline activo global es `New Universal Render Pipeline Asset 1.asset` (referenciado por `GraphicsSettings.asset:39`), conviviendo con `New Universal Render Pipeline Asset.asset` y `PC_RPAsset.asset` (este último, pese al nombre intencional, **no** está en uso); todos los quality levels tienen `customRenderPipeline: {fileID: 0}`
- **Problema:** Tres assets de pipeline, y el activo es el de nombre autogenerado; `PC_RPAsset` sugiere que alguien intentó ordenar esto y quedó a medias.
- **Recomendación:** Decidir un asset (renombrarlo `URP_PC.asset`), asignarlo en Graphics y en los quality levels usados, borrar el resto.
- **Esfuerzo:** S

**11.3 — `companyName: DefaultCompany`** (`ProjectSettings.asset:15`) — cambiar a UNERMB/equipo antes de distribuir. **Esfuerzo:** S

**11.4 — Imágenes de teoría con procedencia de stock**
- **Severidad:** 🟡 Medio (legal, no técnico)
- **Ubicación:** `Assets/Project/Content/Pictures/TheoryPictures/pngtree-chip-...png`, `vecteezy_...png`, `Screenshot 26-11-2025 171118.png`
- **Problema:** Nombres crudos de bancos de imágenes; las licencias gratuitas de pngtree/vecteezy suelen exigir atribución incluso en uso educativo.
- **Recomendación:** Verificar licencias y añadir atribuciones en el panel de créditos (ya existe `creditsPanel` en `MainMenuManager.cs:16`); renombrar los archivos a algo descriptivo.
- **Esfuerzo:** S

### Categoría 12 — Testing y mantenibilidad

**12.1 — Cero tests; la lógica crítica es testeable y no lo está**
- **Severidad:** 🔴 Crítico (por la trayectoria, no por el estado actual)
- **Ubicación:** No existe carpeta `Tests/` ni `.asmdef` de tests en todo `Assets/`
- **Problema:** `CanAccept` es la función de la que depende todo el valor del simulador y hoy solo se prueba jugando a mano. Con el motor de reglas del hallazgo 5.1 (clases puras), testear pasa de "requiere escena y PlayMode" a "EditMode trivial".
- **Recomendación:** Al crear el motor de reglas, añadir Unity Test Framework con casos por regla (compatible, incompatible, socket ocupado, tipos null, wattage insuficiente…). Es además contenido perfecto para la memoria/documentación académica del proyecto.
- **Esfuerzo:** M (junto con 5.1)

**12.2 — Higiene de git**
- **Severidad:** 🟢 Bajo
- **Ubicación:** Historial: `2d95852 "asd"`, `0e0d1cf "sasdasd"`, `7c2cbd5 "adsasdsd"`
- **Problema:** Mensajes de commit sin información; imposible auditar cambios de escena (binarios conceptuales) sin mensajes.
- **Recomendación:** Convención mínima (`feat:`, `fix:`, `art:` + qué escena/sistema). 
- **Esfuerzo:** S

### Categoría 13 — Escalabilidad

**13.1 — Añadir una pieza nueva cuesta ~6 pasos manuales y un contador a mano**
- **Severidad:** 🟠 Alto
- **Ubicación:** `GameManager.cs:11` (`totalPartsToInstall` — vale `4` serializado en `AssemblyLab.unity:9382` y debe mantenerse sincronizado a mano con los sockets/piezas), más prefab + escena + textos + socket + tipo
- **Problema:** El caso "olvidé subir el contador" produce victoria imposible; el caso inverso, victoria prematura. Es el punto de fragilidad más barato de eliminar.
- **Recomendación:** Auto-calcular en `Start()`:
  ```csharp
  totalPartsToInstall = FindObjectsByType<PCPart>(FindObjectsSortMode.None)
      .Count(p => p.isDraggable);
  ```
  (o contar `SocketSystem`). Con el modelo data-driven (4.1), añadir piezas se reduce a crear un SO + arrastrar un prefab.
- **Esfuerzo:** S

---

## 3. Quick wins (alto impacto, bajo esfuerzo — orden sugerido)

1. **Guard de null en `CanAccept` + `OnValidate` en `SocketSystem`** (5.2) — elimina el riesgo latente más serio de la validación. `S`
2. **Auto-contar `totalPartsToInstall`** (13.1) — elimina la fragilidad de sincronización manual. `S`
3. **Sonido de acierto/rechazo al soltar piezas** usando el `AudioManager` ya existente (7.1 parcial) — el pool de audio hoy solo suena en botones de menú; el hook está señalado por el propio código en `SocketSystem.cs:41`. `S`
4. **Mensaje visible de rechazo** ("Esta pieza no va aquí") aunque aún sin motivo detallado (7.1 parcial). `S`
5. **Suscribir `UIManager` a `OnAssemblyComplete`** y quitar la llamada directa + null-check de `GameManager.Instance` en `SocketSystem` (3.1). `S`
6. **Bloquear interacción en pausa/victoria** (7.2). `S`
7. **Corregir default `"MainSimulation"` y validar cargas de escena** (3.3). `S`
8. **Cambiar el HDD de la escena de `hdd 1` (117k tris) a `hdd` (5k tris)** (9.3) — **−112.000 triángulos** solo con reasignar el prefab; la mejor relación esfuerzo/impacto de rendimiento. `S`
9. **Borrar FBX duplicados/muertos** (`gpu.fbx` 212k sin uso) y assets basura (1.1, 2.2) — repo más ligero. `S`
9. **Color space a Linear** (9.2) — cuanto antes, menos arte que re-revisar. `S+`
10. **Fix del leak de materiales del highlighter** restaurando por `sharedMaterials` (9.1). `S`

## 4. Deuda técnica / refactors mayores

1. **`ComponentSpecSO` + migración de textos de la escena a assets** (4.1/2.1) — el refactor con mejor relación valor/riesgo. Riesgo medio: tocar 28 instancias en escena.
2. **Motor de reglas de compatibilidad puro + Unity Test Framework** (5.1/12.1) — habilita el "porqué" educativo, las reglas ricas (PSU, clearance, generación RAM) y los tests. Hacerlo *después* de 1 (necesita las specs).
3. **Desinstalación/undo de piezas** (6.2) — toca `SocketSystem`, `PCPart`, `GameManager` e `InteractionManager`.
4. **`SettingsService` unificado** (3.2) — bajo riesgo, elimina 90 líneas duplicadas.
5. **Resolución del conflicto click-derecho ficha/órbita** (6.1) — requiere decisión de diseño de input antes de codificar.
6. **Reorganización `Assets/3D` → `Assets/Project`** (1.1) — mecánico pero delicado (hacerlo dentro del editor para preservar GUIDs/referencias).

## 5. Roadmap priorizado

**Fase 1 — Estabilizar (1-2 días):** todos los quick wins (1-10). Resultado: validación blindada, feedback audible/visible mínimo, sin leaks, proyecto limpio.

**Fase 2 — Corazón data-driven (1-2 semanas):** `ComponentSpecSO` → motor de reglas con motivos en español → tests EditMode → sockets de PSU/HDD completados. Resultado: añadir componentes sin tocar código y con validaciones reales (consumo vs. PSU como primera regla "wow" educativa).

**Fase 3 — UX educativa (1-2 semanas):** desinstalar/undo, highlight de sockets candidatos durante drag, panel de "porqué" en cada rechazo con icono+texto (accesible), resolución del click derecho, contador de errores como métrica.

**Fase 4 — Pulido y crecimiento (continuo):** modo guiado, evaluación/progreso persistente, optimización de modelos (poly count, compresión), reorganización de carpetas, integración del sistema de cables (`CableTest.unity`), atribuciones de imágenes en créditos.

---

## Verificación en runtime (Unity MCP, escena `AssemblyLab` cargada)

Datos medidos directamente en el editor 6000.4.7f1, no inferidos del YAML:

| Métrica | Valor medido | Relación con hallazgos |
|---|---|---|
| Errores/warnings de consola del proyecto | **0** | Escena compila y corre limpia (el único warning es del puente MCP) |
| Missing scripts en la escena | **0** | Confirma 2.3 — sin referencias rotas |
| Triángulos activos en escena | **607.797** | Confirma y sube 9.3 a 🟠 |
| Renderers activos / slots de material (≈ draw calls) | **201 / 259** | Alto para el contenido; agravado por 67 sub-meshes de la motherboard |
| Luces realtime/mixed | **5** | Carga de iluminación en tiempo real |
| Sockets en escena | **4**: SnapCPU→Type_CPU, SnapGPU→Type_GPU, SnapRAM1/RAM2→Type_RAM (todos con snapPoint) | Confirma 5.2 (riesgo latente, no activo) y 4.2 (PSU/HDD sin socket) |
| `AudioManager` en escena | **ausente** | Nuevo hallazgo 2.4 |
| Layer "Interactable" | **slot 3** (reservado) | Confirma 10.2 |
| Poly counts por modelo | ver tabla en 9.3 | motherboard 217k, gpu 211k, hdd-en-uso 117k |

**Pendiente de verificar en Play mode** (requiere entrar en ejecución e interactuar, no cubierto en esta pasada): comportamiento real de pausa/victoria congelando o no la simulación (7.2), y el churn de materiales del highlighter bajo hover repetido (9.1) — ambos diagnosticados por lectura de código, no observados en vivo.

## Notas de método

- Los conteos de escena por YAML (`isDraggable`, `connectionType` nulos) son exactos al estado del archivo en disco, que **incluye cambios sin commitear**.
- `SimulationControls.cs` es autogenerado por el Input System y se excluyó del juicio de estilo.
