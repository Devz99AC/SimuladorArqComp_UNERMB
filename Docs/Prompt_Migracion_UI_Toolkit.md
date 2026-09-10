# Prompt — Migración de UI (uGUI → UI Toolkit) del Simulador de Ensamblaje de PCs

> Pégalo en una sesión de Claude Code con el editor de Unity abierto y Unity MCP aprobado. El objetivo es **reemplazar la UI actual (uGUI/Canvas + TextMeshPro) por UI Toolkit**, tomando la UI existente como **referencia de diseño y comportamiento**, y **perfeccionándola** al pasar (accesibilidad, consistencia, deduplicación).

---

## Contexto

Proyecto **SimuladorArqComp_UNERMB** (Unity 6000.4.7f1, URP, New Input System). Simulador educativo 3D en español. La UI actual es **uGUI**: Canvas + `CanvasScaler` (1920×1080), TextMeshPro, `Image`/`RawImage`/`Button`, con managers en `Assets/Project/Scripts/`. Lee `CLAUDE.md` (mapa de conexiones) y `AUDITORIA_TECNICA.md` (categoría 7 = UI/UX) **antes de empezar**.

**Regla de oro:** la UI actual es la **referencia**, no el techo. Reprodúcela funcionalmente y **mejórala** aplicando de paso los hallazgos de UI de la auditoría. La parte 3D (raycasting de piezas, cámara) **no cambia**: solo migra la capa de UI de pantalla.

---

## Alcance: qué UI migrar (inventario actual)

Toma cada una como referencia visual y funcional:

| Escena | UI actual (uGUI) | Controlador | Elementos clave |
|---|---|---|---|
| **MainMenu** | Panel principal, opciones (volumen 0-3 + barras, pantalla completa/ventana), créditos | `MainMenuManager.cs` | `BackgroundScroller` (fondo animado), iconos de sonido/pantalla, navegación por teclado |
| **ModeSelection** | Selección de modo (Simulador / Teoría / Volver) | `ModeSelectionManager.cs` | 3 botones grandes |
| **TheoryModule** | Índice (lista de 20 temas generada dinámicamente) + lector (título, cuerpo, imagen, prev/next) | `TheoryManager.cs` | **`Instantiate` de botones por tema** → ideal `ListView` |
| **AssemblyLab (HUD)** | InfoPanel (ficha técnica), VictoryPanel, ConfigPanel (settings), PanelInstrucciones, HUD_TopRight (Info/Salir/Config) | `UIManager.cs` | Overlay sobre el 3D; toggle con tecla I |

Datos que la UI consume (no cambian): `TheoryTopicSO` (título/texto/imagen), settings en PlayerPrefs (`VolumeLevel`, `Fullscreen`), `AudioMixer` param `MasterVolume`, niveles de volumen dB `{-80,-20,-10,0}`.

---

## Arquitectura objetivo (UI Toolkit)

- **Un `UIDocument` por escena** con su `PanelSettings` compartido (`Assets/Project/UI/PanelSettings.asset`), theme y hoja de estilos raíz.
- **UXML** para estructura, **USS** para estilo, **controladores C#** que hacen binding con `UQuery` (`root.Q<Button>("...")`). Un controlador por vista.
- **Estructura de carpetas nueva:**
  ```
  Assets/Project/UI/
    Common/        theme.tss, variables.uss (colores, tipografía, spacing), controles reutilizables
    MainMenu/      MainMenu.uxml, MainMenu.uss, MainMenuView.cs
    ModeSelection/ ...
    Theory/        TheoryIndex.uxml, TheoryReader.uxml, ...
    Assembly/      HUD.uxml, InfoPanel.uxml, VictoryPanel.uxml, ...
    PanelSettings.asset
  ```
- **Fuentes**: registrar `TT Hoves Pro Trial` (Bold/Black) como Font Asset de UI Toolkit; TextMeshPro deja de usarse en la capa de UI.
- **Índice de teoría** → `ListView`/`ScrollView` enlazado al array de `TheoryTopicSO` (elimina el `Instantiate` manual de `TheoryManager.GenerateIndex`).
- **Input**: UI Toolkit gestiona su propio evento de puntero. Coordinar con el New Input System (`InputSystemUIInputModule` / `EventSystem`) para que el raycast 3D de `InteractionManager` siga bloqueándose cuando el puntero está sobre UI (hoy usa `EventSystem.current.IsPointerOverGameObject()` — su equivalente en UITK es comprobar `panel.Pick()` / `focusController`). **Esto es el punto de integración más delicado: pruébalo explícitamente.**

---

## Mejoras obligatorias al transicionar (no solo "portar")

Aplica estos hallazgos de la auditoría durante la migración:

1. **Accesibilidad (§7.1, §8)**: nada de estado comunicado solo por color. Todo estado (compatibilidad, volumen, éxito/error) lleva **icono + texto**. Contraste AA. Foco de teclado visible.
2. **Escalado consistente (§7.3)**: `PanelSettings` con `Scale Mode = Scale with Screen Size`, referencia 1920×1080 y `Match` coherente (hoy `ModeSelection` usa `Match=1` y el resto `0`; unifica en `0.5`).
3. **Deduplicación de settings (§3.2)**: la lógica de volumen/pantalla está clonada en `MainMenuManager` y `UIManager`. Extrae un **`SettingsService`** (POCO o singleton) con las constantes de PlayerPrefs y el mapeo de dB; los controladores de UI solo pintan y delegan. Úsalo en ambas pantallas.
4. **Null-safety y feedback**: los avisos de rechazo/victoria del ensamblaje (que la Fase 1 del prompt de correcciones introduce) deben implementarse ya en UI Toolkit, con icono + texto y **motivo** cuando exista el motor de reglas.
5. **Sonido de UI (§2.4)**: reemplazar `UISoundController` (que depende de `IPointerEnter/Click` de uGUI) por callbacks de UI Toolkit (`RegisterCallback<MouseEnterEvent>`, `clicked +=`) que llamen a `AudioManager`. Garantizar `AudioManager` disponible en cada escena.

---

## Plan de trabajo sugerido (por fases, valida cada una)

1. **Cimientos**: crear `PanelSettings`, `theme.tss`, `variables.uss` (paleta, tipografía, spacing, tokens de estado con icono). Registrar las fuentes. Definir 3-4 controles reutilizables (botón primario, panel modal, fila de setting).
2. **MainMenu** (la más rica en settings): migrar como piloto. Extraer `SettingsService` aquí. Verificar volumen, pantalla, navegación por teclado, fondo animado (el scroll de fondo puede seguir siendo un elemento 3D/URP o un `VisualElement` con animación USS).
3. **ModeSelection**: rápida (3 botones).
4. **TheoryModule**: índice con `ListView` enlazado a los SO + lector (título/cuerpo/imagen/prev-next). Aprovechar para el reflow automático (elimina `LayoutRebuilder.ForceRebuildLayoutImmediate`).
5. **AssemblyLab HUD**: overlay sobre el 3D. **Punto crítico**: verificar que el raycast de piezas se bloquea correctamente sobre la UI y que el toggle de instrucciones (tecla I) sigue funcionando con el New Input System. Migrar InfoPanel, VictoryPanel, ConfigPanel, PanelInstrucciones, HUD_TopRight.
6. **Limpieza**: eliminar los Canvas/EventSystem uGUI obsoletos, `UISoundController`, y los `[SerializeField]` de UI muertos en los managers. Los managers conservan solo la lógica de negocio; la presentación vive en los controladores UITK.

**Verificación por fase (Unity MCP):** consola limpia, captura de la escena, y prueba de interacción real (Play mode) — clic de botones, cambio de settings persistente, y en AssemblyLab que arrastrar piezas NO ocurre cuando el puntero está sobre un panel.

---

## Restricciones y avisos

- **No rompas** las conexiones documentadas en `CLAUDE.md` (singletons, eventos, nombres de escena, flujo). Si cambias una, actualiza `CLAUDE.md`.
- **No toques** la lógica 3D (`InteractionManager` raycast/drag, `CADCameraController`, `PCPart`, `SocketSystem`) salvo el punto de integración puntero-sobre-UI.
- Mantén **todo en español** (textos visibles) y respeta la identidad visual actual (fondos, iconos en `Content/Pictures/`, fuentes TT Hoves) como referencia.
- Trabaja **una escena por vez** y pídeme validación antes de pasar a la siguiente. No migres las 4 de golpe.

---

**Empieza confirmando que leíste `CLAUDE.md` y la categoría 7 de `AUDITORIA_TECNICA.md`, resume el inventario de UI que vas a migrar, y propón los cimientos (PanelSettings + USS de variables + controles reutilizables) antes de escribir la primera vista.**
