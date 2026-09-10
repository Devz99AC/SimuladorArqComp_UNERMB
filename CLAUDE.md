# CLAUDE.md — Simulador de Arquitectura de Computadores (UNERMB)

Guía de arquitectura y **mapa de conexiones** del proyecto. Este archivo se carga en cada sesión de Claude Code: mantenlo actualizado cuando cambien las conexiones.

> Auditoría técnica completa en [AUDITORIA_TECNICA.md](AUDITORIA_TECNICA.md). Estado y prioridades en [HANDOFF.md](HANDOFF.md).

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
[0] MainMenu ──StartSimulation──> [1] ModeSelection ──GoToSimulation──> [3] AssemblyLab
                                        │
                                        └──GoToTheory──> [2] TheoryModule
Retornos: AssemblyLab/TheoryModule ──> ModeSelection ──BackToMenu──> MainMenu
```

- Escenas **no** en build (sandbox): `Test.unity`, `CableTest.unity`.
- **Los nombres de escena son strings serializados** en cada manager (acoplamiento frágil — ver §7):
  - `MainMenuManager.simulationSceneName = "ModeSelection"`
  - `ModeSelectionManager`: `mainMenuScene="MainMenu"`, `simulationScene="AssemblyLab"`, `theoryScene="TheoryModule"` (⚠️ el **default en código** es `"MainSimulation"`, inexistente; funciona solo porque la escena lo sobreescribe)
  - `UIManager.selectorSceneName = "ModeSelection"`, `TheoryManager.backSceneName = "ModeSelection"`

---

## 3. Inventario de scripts

| Script | Ubicación | Escena | Rol |
|---|---|---|---|
| `GameManager` | Systems/Interaction | AssemblyLab | Singleton. Cuenta piezas, dispara victoria |
| `InteractionManager` | Systems/Interaction | AssemblyLab | Raycast, hover, drag & drop, orquesta cámara y UI |
| `ObjectHighlighter` | Systems/Interaction | (por pieza) | Aplica material de resaltado (hover/drag) |
| `PCPart` | Systems/Assembly | (por pieza) | Datos de pieza + lógica de snap |
| `SocketSystem` | Systems/Assembly | (por socket) | Validación de compatibilidad + instalación |
| `CADCameraController` | Systems/Camera | AssemblyLab | Órbita/pan/zoom estilo CAD, auto-reset idle |
| `UIManager` | Scripts | AssemblyLab | Singleton. Paneles HUD, ficha, victoria, settings |
| `MainMenuManager` | Scripts | MainMenu | ⚠️ **Reemplazado por `MainMenuView` (UITK)**; `MenuController` + `Canvas` uGUI **sacados de la escena** a un prefab de respaldo `UI/_Legacy/MainMenu_LegacyUGUI.prefab` |
| `MainMenuView` | UI/MainMenu | MainMenu | **UITK**. Controlador del `UIDocument` del menú: binding, modales, settings (vía `SettingsService`), fondo animado |
| `ModeSelectionManager` | Scripts | ModeSelection | Navegación entre modos |
| `TheoryManager` | Scripts | TheoryModule | Índice dinámico + lector de temas |
| `AudioManager` | Scripts | (persistente) | Singleton `DontDestroyOnLoad`. Pool de AudioSources |
| `UISoundController` | Scripts | (por botón) | Sonido hover/click → AudioManager |
| `AutoSelectOnHover` | Scripts | (por botón) | Selecciona en EventSystem al pasar el ratón |
| `BackgroundScroller` | Scripts | menús | Desplaza el `uvRect` de un RawImage |
| `ConnectionTypeSO` | Data/SO/Connections Type | — | SO: tipo de conexión (label + color) |
| `TheoryTopicSO` | Data/SO/TheoryContent | — | SO: un TEMA (título, resumen, contenido con marcado ligero, imagen + pie) |
| `TheoryUnitSO` | Data/SO/TheoryContent | — | SO: una UNIDAD (número, título, descripción, array de temas) |
| `TheorySyllabusSO` | Data/SO/TheoryContent | — | SO raíz: datos de la unidad curricular + array de unidades + bibliografía |
| `TheoryMarkup` | UI/Theory | — | Estático. Convierte el marcado ligero en elementos UITK con jerarquía |
| `SimulationControls` | Systems/Interaction | — | **Autogenerado** por Input System — no editar a mano |

---

## 4. Conexiones del sistema de ensamblaje (el núcleo) ⭐

**Cadena de datos completa de "arrastrar y encajar una pieza":**

```
InteractionManager (Update)
  ├─ HandleHover()  → ObjectHighlighter.SetHighlight(hoverMaterial)   [naranja]
  ├─ HandleLeftClick()
  │     → CADCameraController.SetTopDownView()/FocusOnObject()
  │     → guarda _currentPart, ObjectHighlighter.SetHighlight(dragMaterial)  [verde]
  ├─ MovePart()  → mueve la pieza sobre un Plane, clamp a los límites de la mesa
  └─ HandleRelease()
        → PCPart.TryToSnap()
             → Physics.OverlapSphere(pos, 0.5f)   [busca colliders cercanos]
             → SocketSystem.CanAccept(part)
                   → !isOccupied && part.connectionType == allowedType   [ÚNICA regla]
             → SocketSystem.InstallPart(part)
                   → reparenta al snapPoint, isOccupied = true
                   → GameManager.Instance.RegisterInstallation(partName)
                        → _installedParts++; OnPartInstalled?.Invoke()
                        → si _installedParts >= totalPartsToInstall → FinishSimulation()
                             → OnAssemblyComplete?.Invoke()
                             → UIManager.Instance.ShowVictory()   [llamada directa]
```

**Componentes de datos (ScriptableObjects como "clave de compatibilidad"):**

| Socket (en escena) | `allowedType` | Piezas que encajan |
|---|---|---|
| SnapCPU | `Type_CPU` | CPU |
| SnapGPU | `Type_GPU` | GPU |
| SnapRAM1 / SnapRAM2 | `Type_RAM` | RAM (×2) |
| *(no existe)* | `Type_PSU`, `Type_HDD` | ⚠️ definidos pero **sin socket** — piezas no instalables |

- `GameManager.totalPartsToInstall = 4` (CPU + GPU + 2×RAM). **Se sincroniza a mano** — si añades piezas/sockets, actualízalo (frágil, ver AUDITORIA §13.1).
- La escena tiene **~28 `PCPart`**, pero solo **4 son `isDraggable`**; el resto son etiquetas informativas fijas (`isDraggable=0` → `IsInstalled=true` en `Start`).

---

## 5. Singletons, eventos y ciclo de vida

| Singleton | Nace en | Vive | Notas |
|---|---|---|---|
| `AudioManager.Instance` | MainMenu | `DontDestroyOnLoad` (persiste entre escenas) | ⚠️ **ausente si abres AssemblyLab directo** → botones mudos (AUDITORIA §2.4) |
| `GameManager.Instance` | AssemblyLab | por escena | Destruye duplicados en `Awake` |
| `UIManager.Instance` | AssemblyLab | por escena | Destruye duplicados en `Awake` |

**Eventos** (`GameManager`): `OnPartInstalled(string)`, `OnAssemblyComplete()` — **declarados pero sin suscriptores**; la victoria se resuelve con llamada directa `UIManager.Instance.ShowVictory()`. Al refactorizar, migrar a suscripción por eventos (AUDITORIA §3.1).

---

## 6. Input (SimulationControls)

Mapa **Player** (usado en AssemblyLab): `Point` (posición ratón), `Delta`, `Select` (click izq), `Inspect` (click der), `Zoom` (scroll), `Pan` (botón central + arrastre), `ResetView` (Espacio), `ToggleInstructions` (tecla I).
Mapa **UI**: navegación estándar (Point, Click, Scroll, Maps, Submit, Cancel).

⚠️ **Conflicto conocido**: `Inspect` (click der) hace dos cosas a la vez — abre ficha en `InteractionManager` **y** orbita cámara en `CADCameraController` (AUDITORIA §6.1).
⚠️ Se instancian **dos** `new SimulationControls()` (InteractionManager + CADCameraController) y ninguno hace `Dispose()`.

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
- **Teoría**: jerarquía en `Assets/Project/Data/ScriptableObjects/TheoryContent/` — `TheorySyllabus.asset` (raíz) → `Units/U01..U11.asset` → `Topics/U01_T01..U11_T05.asset` (49 temas). Generados por script a partir de fuentes de texto plano. Los 20 SO planos antiguos están archivados en `TheoryContent/_Legacy/`.
- **Layers**: `Interactable = 3` (⚠️ slot reservado, debería ser 8+), `UI = 5`.

---

## 9. Cómo trabajar en este repo

**Añadir un componente ensamblable hoy** (proceso manual, frágil): crear/ubicar prefab con `PCPart` + `ObjectHighlighter` + collider en layer `Interactable` → asignar `connectionType` → crear `SocketSystem` con `allowedType` + `snapPoint` → **incrementar `GameManager.totalPartsToInstall`** → rellenar `title`/`description` en el inspector. *(El refactor data-driven de la auditoría reduce esto a "crear un SO + arrastrar prefab".)*

**Añadir un tema teórico**: crear un `TheoryTopicSO` (menú `Assets/Create/Simulador/Theory Topic`), rellenar `contentText` con el marcado ligero (`## título`, `- viñeta`, `1.` numerada, `: Término | Definición`, `> dato clave`, `**negrita**`) y añadirlo al array `topics` de la `TheoryUnitSO` correspondiente. El índice y la navegación se reconstruyen solos.

**Convenciones**: sin namespaces (global); campos `public` para exposición en inspector (preferir `[SerializeField] private`); serialización **Force Text** (YAML legible). Escribe código que respete el estilo existente del archivo.

**Consistencia entre pantallas**: los valores que deben coincidir en varias vistas viven como tokens en `UI/Common/variables.uss`, no duplicados en cada hoja. Ejemplo: `--menu-panel-width` (ancho del panel lateral) lo comparten MainMenu y ModeSelection — duplicarlo provocó un salto visible al cambiar de escena. Si un ajuste afecta a dos pantallas, hazlo token.

**Reparación del editor** (`Tools/`, fuera de `Assets/`): `reparar-shadergraph.ps1` borra el caché del paquete Shader Graph; `reparar-libreria.ps1` borra `Library/` salvo `PackageCache`. Ambos exigen Unity cerrado. Se usaron para un caso real de corrupción de disco (430 archivos del paquete rellenos de bytes nulos tras un cierre sucio) que se manifestaba como `undeclared identifier 'DecodeHDRSample'` en todos los Shader Graph. Lección: borrar el `PackageCache` no basta — la `ArtifactDB` conserva las huellas viejas y Unity marca el paquete como *immutable package unexpectedly altered*, y entonces no compila sus ensamblados de editor.

**Unity MCP**: hay integración MCP con el editor (`Project Settings > AI > Unity MCP`). Si devuelve "Connection revoked", el usuario debe aprobar el cliente en ese panel y **reconectar el servidor MCP** (una aprobación nueva no se aplica a una sesión ya iniciada). Puede haber varias instancias de Unity abiertas: dejar solo la de este proyecto.

**Build**: escenas ya configuradas en Build Settings (orden §2). `productName`/`companyName` ya fijados; splash de Unity desactivado.

---

## 10. Deuda técnica prioritaria (resumen — detalle en AUDITORIA_TECNICA.md)

1. 🔴 **Modelo de datos**: componentes no data-driven; textos hardcodeados en la escena → crear `ComponentSpecSO`.
2. 🔴 **Compatibilidad**: una sola regla (igualdad de tipo); sin motor de reglas ni "porqué" educativo; sin tests.
3. 🟠 **Rendimiento**: 607k tris / ~259 draw calls en AssemblyLab; modelos de 200k+.
4. 🟠 **UX**: sin feedback de acierto/rechazo, sin undo/desinstalar, conflicto de click derecho.
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
