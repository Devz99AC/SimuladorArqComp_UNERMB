# HANDOFF — Simulador de Arquitectura de Computadores (UNERMB)

**Última actualización:** 2026-07-11 · **Estado:** Prototipo / MVP temprano, funcional.

Documento de traspaso para quien continúe el proyecto (persona o asistente). Lee primero [CLAUDE.md](CLAUDE.md) (mapa de conexiones) y [AUDITORIA_TECNICA.md](AUDITORIA_TECNICA.md) (diagnóstico completo).

> **Actualización 2026-10-18 — parte práctica en curso (rama `labs-practicos`).** La teoría está terminada. El trabajo actual son los 3 laboratorios de ensamblaje (ATX / PC compacta / laptop) y los cables con spline. Diagnóstico, cambios hechos, modelos 3D candidatos y **las decisiones tomadas y el estado actual para retomar** están en [Docs/Plan_Laboratorios_Practicos.md §7 (ver §7.4)](Docs/Plan_Laboratorios_Practicos.md). Parte de las secciones de abajo (UX, `totalPartsToInstall`, cables) quedó desactualizada por ese trabajo.

---

## 1. En una frase

Simulador educativo 3D en Unity 6 (URP) donde el estudiante arrastra componentes de PC a sus sockets y consulta un módulo teórico de 20 temas. Fundamentos sólidos; las debilidades se concentran en el modelo de datos y la lógica de compatibilidad — justo el corazón del simulador.

---

## 2. Qué funciona hoy (verificado en runtime)

- ✅ Compila y corre **sin errores ni warnings** de proyecto (escena `AssemblyLab` verificada en editor).
- ✅ **0 missing scripts** / referencias rotas en `AssemblyLab`.
- ✅ Flujo de menús completo: MainMenu → ModeSelection → {AssemblyLab | TheoryModule} y retornos.
- ✅ Ensamblaje: arrastrar CPU/GPU/RAM×2 a sus 4 sockets, snap por cercanía, victoria al completar.
- ✅ Cámara CAD (órbita/pan/zoom, auto-reset por inactividad).
- ✅ Highlight de piezas (naranja hover / verde drag), ficha informativa (click derecho).
- ✅ Módulo teórico data-driven (índice dinámico + 20 `TheoryTopicSO`).
- ✅ Settings de audio/pantalla persistentes (PlayerPrefs + AudioMixer).

## 3. Qué NO funciona / limitaciones actuales

- ❌ Solo 3 tipos instalables (CPU/GPU/RAM). **PSU y HDD tienen tipo pero no socket** → no se pueden colocar.
- ❌ **No hay desinstalar/undo**: equivocarse de slot de RAM es permanente hasta reiniciar la escena.
- ❌ **El error no comunica nada**: pieza rechazada → vuelve a la mesa en silencio (sin texto, sonido ni motivo).
- ❌ **Feedback solo por color** (accesibilidad: falla con daltonismo).
- ⚠️ **Click derecho ambiguo**: abre ficha y orbita cámara a la vez.
- ⚠️ **`AudioManager` ausente en AssemblyLab**: si abres la escena directa (sin pasar por el menú), los botones del HUD no suenan.
- ⚠️ **Pausa/victoria** puede que no congelen la simulación (no se toca `Time.timeScale`; pendiente de verificar en Play mode).
- ⚠️ **Rendimiento**: 607k triángulos y ~259 draw calls en AssemblyLab; modelos de 200k+ tris.

## 4. Riesgos / trampas para el que continúa

- **`ModeSelectionManager.simulationScene` default = `"MainSimulation"`** (escena inexistente). Funciona solo porque el valor real `"AssemblyLab"` está serializado en la escena `ModeSelection`. Si pones el componente en un objeto nuevo, la navegación se rompe.
- **`GameManager.totalPartsToInstall` se sincroniza a mano** (=4). Añadir/quitar piezas sin actualizarlo → victoria imposible o prematura.
- **`AssemblyLab.unity` pesa 22.000+ líneas** y contiene los textos educativos de las 28 piezas inline → conflictos de merge garantizados. No edites textos ahí a mano si puedes evitarlo.
- **Hay cambios sin commitear** en el árbol de trabajo (ver `git status`).
- **Unity MCP**: la conexión se revoca con facilidad; requiere reaprobar en el editor y reconectar el servidor (ver CLAUDE.md §9).

## 5. Por dónde empezar (roadmap priorizado)

Ejecuta el prompt **[Docs/Prompt_Correcciones_Auditoria.md](Docs/Prompt_Correcciones_Auditoria.md)** para atender lo siguiente por fases:

- **Fase 1 — Estabilizar (1-2 días):** quick wins de la auditoría. Guard de null en `CanAccept`, auto-contar piezas, sonido + mensaje de acierto/rechazo, bloquear input en pausa/victoria, corregir default de escena, **cambiar HDD a la malla ligera (−112k tris)**, color space Linear, fix del leak de materiales.
- **Fase 2 — Corazón data-driven (1-2 sem):** `ComponentSpecSO` → motor de reglas de compatibilidad (con motivos en español) → tests EditMode → completar sockets PSU/HDD.
- **Fase 3 — UX educativa (1-2 sem):** undo/desinstalar, highlight de sockets candidatos durante el drag, panel de "porqué" accesible (icono+texto), resolver click derecho, contador de errores.
- **Fase 4 — Pulido:** modo guiado, evaluación/progreso, optimización de modelos, reorganización de carpetas.

**Migración de UI** (transversal, puede ir en paralelo desde Fase 2): ejecuta **[Docs/Prompt_Migracion_UI_Toolkit.md](Docs/Prompt_Migracion_UI_Toolkit.md)** para pasar la UI uGUI actual a UI Toolkit, tomando la UI actual como referencia y aplicando de paso las mejoras de accesibilidad y deduplicación.

## 6. Estructura del repo (orientación rápida)

```
Assets/
  Project/
    Scenes/        MainMenu, ModeSelection, TheoryModule, AssemblyLab (+ Test, CableTest sandbox)
    Scripts/       Managers de UI, audio, menús, teoría
    Systems/       Assembly (PCPart, SocketSystem), Interaction, Camera
    Data/SO/       ConnectionTypeSO (tipos), TheoryTopicSO (20 temas)
    Content/       Audio (MainMixer), Fonts, Pictures
    Prefabs/       Parts, UI
  3D/              Modelos FBX (pesados), prefabs de piezas, assets URP  ← unificar en Project/
Docs/              Prompts de trabajo (este handoff los referencia)
CLAUDE.md          Mapa de conexiones (cargado cada sesión)
AUDITORIA_TECNICA.md   Diagnóstico completo con severidades
HANDOFF.md         Este archivo
```

## 7. Contacto / contexto del proyecto

- Público: estudiantes de PNF Informática (UNERMB, Venezuela). UI en español.
- Antes de distribuir: cambiar `companyName` (hoy `DefaultCompany`) y verificar **licencias de las imágenes de teoría** (algunas son de bancos de stock — pngtree/vecteezy — que exigen atribución; hay un `creditsPanel` disponible).
