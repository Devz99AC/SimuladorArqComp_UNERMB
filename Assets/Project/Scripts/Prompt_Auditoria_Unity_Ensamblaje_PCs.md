# Auditoría Técnica Integral — Herramienta Virtual de Ensamblaje de PCs (Unity)

> Prompt para ejecutar con un asistente que tenga acceso al repositorio (Claude Code, Cursor, etc.).
> Rellena el bloque de configuración y pégalo tal cual.

---

## ⚙️ Configuración del proyecto (rellenar antes de ejecutar)

- **Versión de Unity:** `[2022.3 LTS / Unity 6 / ...]`
- **Render Pipeline:** `[Built-in / URP / HDRP]`
- **Dimensión:** `[3D / 2D / híbrido]`
- **Plataforma(s) objetivo:** `[PC standalone / WebGL / Mobile / VR]`
- **Input System:** `[Input Manager clásico / New Input System]`
- **Idioma(s) de la UI:** `[Español / Inglés / bilingüe]`
- **Público objetivo:** `[estudiantes / técnicos en formación / público general]`
- **Estado del proyecto:** `[prototipo / MVP / en producción]`

---

## 🎯 Rol y objetivo

Actúa como **Ingeniero Senior de Unity y Arquitecto Técnico** especializado en simuladores educativos e interactivos 3D. Vas a auditar **todo el proyecto de pies a cabeza**: código, escenas, prefabs, datos, arquitectura, rendimiento, UX y valor educativo.

Tu objetivo NO es reescribir el proyecto, sino entregar un **diagnóstico priorizado y accionable** de qué mejorar, con severidad, ubicación exacta y recomendación concreta para cada hallazgo.

Antes de opinar, **recorre realmente los archivos**. Si algo no se puede determinar sin ejecutar el juego o sin más contexto, decláralo en vez de asumir.

---

## 🔍 Alcance de la auditoría

### 1. Estructura del proyecto y organización
- Coherencia de carpetas (¿por tipo, por feature, o caos?)
- Assets sin usar, duplicados o huérfanos
- Uso (o abuso) de la carpeta `Resources/` frente a Addressables
- Assembly Definitions (`.asmdef`) para tiempos de compilación
- `.gitignore` correcto (`Library/`, `Temp/`, `Logs/`, `Obj/`) y *Asset Serialization* en **Force Text**

### 2. Escenas y jerarquía
- Número y propósito de escenas; ¿todo en una escena monolítica?
- Jerarquía limpia vs. GameObjects vacíos y basura
- Objetos que deberían ser prefabs y no lo son
- Referencias rotas o *missing scripts*

### 3. Arquitectura de código y patrones
- MonoBehaviours "dios" con demasiada responsabilidad
- Acoplamiento fuerte vs. sistema de eventos / mensajería
- Singletons y managers: ¿justificados o regados por todos lados?
- Separación datos / lógica / vista (patrón MVC / MVP)
- Namespaces, convenciones de nombres, código muerto
- Uso de ScriptableObjects para arquitectura, no solo para datos

### 4. Modelo de datos de componentes  ⭐ CRÍTICO
Aquí vive o muere una herramienta de ensamblaje. Evalúa:
- ¿Los componentes (CPU, GPU, RAM, placa base, PSU, gabinete, almacenamiento, cooler) son **ScriptableObjects** o están hardcodeados / metidos en prefabs?
- ¿Existe una **única fuente de verdad** por componente?
- Campos modelados: socket, factor de forma (ATX/mATX/ITX), TDP/consumo, tipo de RAM (DDR4/DDR5), versión PCIe, dimensiones/clearance, nº de slots (M.2, SATA, RAM), precio.
- ¿Es **data-driven**? ¿Se puede añadir un componente nuevo **sin tocar código**?
- Escalabilidad del catálogo: pipeline para cargar / editar / versionar componentes.

### 5. Lógica de compatibilidad y validación  ⭐ CRÍTICO
El corazón del simulador. Verifica que exista y esté bien resuelto:
- CPU ↔ socket de placa base
- Tipo/generación de RAM ↔ placa base
- Longitud de GPU ↔ clearance del gabinete
- Consumo total ↔ vataje de la PSU (con margen de seguridad)
- Altura del cooler ↔ gabinete
- Factor de forma de la placa ↔ gabinete
- Disponibilidad de slots (M.2, SATA, RAM)

Y sobre todo: ¿la lógica está **centralizada en un motor de reglas** o dispersa en `if/else` por todo el código? ¿Es extensible? ¿Es correcta? ¿Está testeada?

### 6. Sistema de interacción / ensamblaje
- Mecánica de colocación: drag & drop, *snap points* / sockets, o colocación libre + validación
- Diseño del sistema de anclajes/sockets (¿escalable a piezas nuevas?)
- Feedback cuando una pieza encaja vs. no encaja (visual + audio)
- Rotación, zoom, controles de cámara, reset / undo
- Manejo de colisiones y solapamientos

### 7. UI / UX
- Configuración de Canvas y **Canvas Scaler** (escalado responsivo por resolución)
- Rebuilds de Canvas y separación en varios canvases según frecuencia de cambio
- Onboarding y descubribilidad de las interacciones
- Claridad de los mensajes de error / compatibilidad
- **Accesibilidad**: no depender solo de rojo/verde para indicar compatibilidad (daltonismo); usar iconos + texto

### 8. Capa educativa
- Modo guiado vs. sandbox
- Tutoriales, pistas y explicaciones del **porqué** de una incompatibilidad (no solo "no encaja")
- Seguimiento de progreso / puntuación / evaluación
- Localización / soporte multi-idioma si aplica

### 9. Rendimiento
- `GetComponent` / `Find` en `Update` o rutas calientes
- Churn de `Instantiate` / `Destroy` → object pooling
- Draw calls, materiales, batching, atlas de texturas
- Poly count de los modelos 3D de componentes (suelen ser pesados)
- Allocations y GC (concatenación de strings, LINQ en `Update`, boxing)
- Física: rigidbodies / colliders haciendo trabajo innecesario
- Iluminación: realtime vs. baked; *color space* (Linear / Gamma)

### 10. Buenas prácticas de Unity
- Serialización correcta (`[SerializeField]` privado vs. `public`)
- Prefab Variants para familias de componentes
- Coroutines vs. async/await
- Strings mágicos y números mágicos hardcodeados
- Tags / Layers organizados

### 11. Configuración de proyecto y build
- Escenas en Build Settings
- Quality Settings por plataforma
- Player Settings, resolución y aspect ratio
- Tamaño de build y assets incluidos innecesariamente

### 12. Testing y mantenibilidad
- Tests EditMode / PlayMode existentes
- En particular: ¿las **reglas de compatibilidad** están cubiertas por unit tests? (es lógica pura, ideal para testear)
- Consistencia de convenciones, documentación y comentarios útiles

### 13. Escalabilidad y mantenimiento a futuro
- ¿Cuánto cuesta añadir un componente nuevo o una regla de compatibilidad nueva?
- Pipeline de contenido y de localización
- Puntos donde el proyecto se volverá frágil al crecer

---

## 📋 Formato de entrega

**1. Resumen ejecutivo** — Estado general y tabla de "salud" por categoría (🟢 bien / 🟡 mejorable / 🔴 problemático).

**2. Hallazgos por categoría** — cada uno con:
- **Severidad:** 🔴 Crítico · 🟠 Alto · 🟡 Medio · 🟢 Bajo
- **Ubicación:** `ruta/archivo.cs:línea` o escena / prefab
- **Problema:** qué está mal
- **Impacto:** por qué importa (usuario final y/o mantenibilidad)
- **Recomendación:** cómo resolverlo, con snippet si aplica
- **Esfuerzo:** S / M / L

**3. Quick wins** — alto impacto, bajo esfuerzo (lo que arreglarías primero).

**4. Deuda técnica / refactors mayores** — cambios estructurales con más riesgo.

**5. Roadmap priorizado** — por fases, ordenado por impacto.

---

## 📐 Reglas

- Cita **rutas y líneas reales**; nunca inventes archivos ni código.
- Si algo requiere ejecutar el juego o más contexto para juzgarse, decláralo explícitamente.
- Distingue **problema objetivo** de **preferencia estilística**.
- Prioriza por **impacto**, no por cantidad de hallazgos.
- Cero consejos genéricos ("mejora el rendimiento"): siempre **qué, dónde y cómo**.
- Entrega el análisis en español.
