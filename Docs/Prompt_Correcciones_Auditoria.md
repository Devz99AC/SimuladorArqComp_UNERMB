# Prompt — Atender la Auditoría Técnica (Simulador de Ensamblaje de PCs)

> Pégalo en una sesión de Claude Code (o asistente con acceso al repo). Trabaja **por fases**, no todo de golpe. Requiere el editor de Unity abierto con Unity MCP aprobado para verificar en runtime.

---

## Contexto

Estás en el proyecto **SimuladorArqComp_UNERMB** (Unity 6000.4.7f1, URP, New Input System). Es un simulador educativo 3D de ensamblaje de PCs. Ya existe una auditoría técnica en `AUDITORIA_TECNICA.md` y el mapa de conexiones en `CLAUDE.md`. **Léelos antes de tocar nada.**

Tu tarea: implementar las correcciones de la auditoría, **por fases**, verificando cada cambio en runtime vía Unity MCP (consola limpia, comportamiento observado) antes de darlo por hecho.

## Reglas de trabajo

- **Una fase por vez.** Al terminar cada fase: compila, revisa la consola de Unity (0 errores nuevos), y resume qué cambió y cómo lo verificaste. Pídeme confirmación antes de la siguiente fase.
- **Respeta el estilo existente** de cada archivo (sin namespaces hoy, comentarios en español, convenciones actuales).
- **No edites textos dentro de `AssemblyLab.unity` a mano** (22k líneas, conflictos de merge). Si un cambio toca datos de piezas, hazlo por código o por el editor vía MCP.
- Cita `archivo:línea` real en cada cambio. No inventes rutas.
- Después de cada fase, actualiza `CLAUDE.md` y `AUDITORIA_TECNICA.md` si la conexión o el hallazgo cambió.

---

## Fase 1 — Estabilizar (quick wins, alto impacto / bajo esfuerzo)

Implementa, en este orden:

1. **Guard de null en la validación de compatibilidad.** En `SocketSystem.cs:CanAccept`, rechazar si `allowedType == null || part.connectionType == null`. Añadir un `OnValidate()` que loguee error si `allowedType` o `snapPoint` están sin asignar. *(AUDITORIA §5.2)*
2. **Auto-calcular `totalPartsToInstall`.** En `GameManager`, en `Start()`, contar las `PCPart` con `isDraggable` (o los `SocketSystem`) en vez del valor manual. *(§13.1)*
3. **Feedback audible de acierto/rechazo.** Reproducir un sonido al instalar (`SocketSystem.InstallPart`) y otro al rechazar (`PCPart` cuando vuelve a la mesa), usando `AudioManager` (ya existe pooling). *(§7.1)*
4. **Mensaje visible de rechazo.** Mostrar en `UIManager` un aviso corto ("Esta pieza no encaja aquí") cuando `TryToSnap` falla. Placeholder del "porqué" detallado que llegará en Fase 2. *(§7.1)*
5. **`AudioManager` disponible en AssemblyLab.** Garantizar la instancia aunque se abra la escena directa (colocar un `AudioManager` en la escena — el singleton destruye duplicados — o un bootstrap `RuntimeInitializeOnLoadMethod`). *(§2.4)*
6. **Victoria por evento.** Que `UIManager` se suscriba a `GameManager.OnAssemblyComplete` en `OnEnable/OnDisable` y eliminar la llamada directa `UIManager.Instance.ShowVictory()`. Añadir null-check de `GameManager.Instance` en `SocketSystem`. *(§3.1)*
7. **Bloquear interacción en pausa/victoria.** Deshabilitar `InteractionManager` y `CADCameraController` (o un flag `InputLocked` que ambos consulten) al abrir pausa/victoria. Verificar en Play mode que no se puede arrastrar "en pausa". *(§7.2)*
8. **Corregir navegación de escena.** Cambiar el default `ModeSelectionManager.simulationScene` de `"MainSimulation"` a `"AssemblyLab"` y aplicar el guard `Application.CanStreamedLevelBeLoaded` en todas las cargas (como ya hace `GoToTheory`). *(§3.3)*
9. **HDD ligero (−112k triángulos).** En la escena, cambiar la malla del HDD de `hdd 1.fbx` (117k) a `hdd.fbx` (5k). Verificar visualmente con captura MCP. *(§9.3)*
10. **Color space a Linear.** Cambiar en Player Settings; revisar la iluminación de las 4 escenas de build tras el cambio. *(§9.2)*
11. **Fix del leak de materiales del highlighter.** En `ObjectHighlighter`, restaurar con `sharedMaterials` y evitar clonar en cada hover (o usar `MaterialPropertyBlock`). *(§9.1)*
12. **Limpieza.** Borrar `gpu.fbx` (duplicado muerto, 212k) y assets sandbox si no se referencian; verificar que compila. *(§1.1, §9.3)*

**Criterio de aceptación Fase 1:** consola limpia; se oye y se ve el resultado de encajar/rechazar; no se puede interactuar en pausa; navegación robusta; escena más ligera. Sin cambios de arquitectura mayores.

---

## Fase 2 — Corazón data-driven

1. **`ComponentSpecSO : ScriptableObject`** con la identidad + specs de cada componente (partName, description, connectionType, y campos para reglas futuras: wattage/consumo, lengthMm, ramType, formFactor…). `PCPart` pasa a referenciar un único `spec`. Migrar las 28 instancias de la escena a referencias a SO (por script/MCP, no a mano). *(§4.1, §2.1)*
2. **Motor de reglas de compatibilidad puro** (clases C# sin MonoBehaviour), con `ICompatibilityRule` y un `CompatResult { bool Ok; string ReasonEs; }`. `SocketSystem.CanAccept` delega en la lista de reglas y devuelve el primer motivo de rechazo (que alimenta el mensaje del §7.1). Primera regla "wow": consumo total vs. vataje de PSU. *(§5.1)*
3. **Tests EditMode** (Unity Test Framework) cubriendo cada regla: compatible, incompatible, socket ocupado, tipos null, wattage insuficiente. *(§12.1)*
4. **Completar sockets PSU y HDD** (o decidir explícitamente que quedan como decoración). *(§4.2)*

**Criterio de aceptación Fase 2:** añadir un componente = crear un SO + arrastrar prefab (sin tocar código); el rechazo explica el motivo real; los tests pasan en verde.

---

## Fase 3 — UX educativa

1. **Desinstalar / undo**: `SocketSystem.RemovePart()` que libera `isOccupied`, desparenta, decrementa el contador y permite re-arrastrar. *(§6.2)*
2. **Highlight de sockets candidatos durante el drag** (verde compatible / gris-rojo incompatible), consultando el motor de reglas en tiempo real. *(§6.4)*
3. **Panel de "porqué" accesible**: icono + texto (no solo color) explicando cada incompatibilidad. *(§7.1, accesibilidad)*
4. **Resolver el conflicto del click derecho** (ficha vs. órbita): separar por interacción `Press`/`SlowTap` o mover la órbita a otro control. *(§6.1)*
5. **Contador de errores por pieza** como métrica de evaluación. *(§8.2)*

---

## Fase 4 — Pulido y deuda estructural

- Unificar settings en un `SettingsService` (elimina ~90 líneas duplicadas). *(§3.2)*
- Optimizar modelos pesados (decimar GPU/motherboard a <30-40k, Mesh Compression). *(§9.3)*
- Reorganizar `Assets/3D` dentro de `Assets/Project` (desde el editor, para preservar GUIDs). *(§1.1)*
- Layer `Interactable` al slot 8+. *(§10.2)*
- Modo guiado, evaluación/progreso persistente. *(§8.2)*

---

**Empieza confirmándome que leíste `AUDITORIA_TECNICA.md` y `CLAUDE.md`, y propón el detalle de la Fase 1 antes de escribir código.**
