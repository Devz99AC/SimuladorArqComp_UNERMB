# Laboratorios prácticos — diagnóstico y plan

**Fecha:** 2026-10-17 · **Alcance:** la parte práctica (`AssemblyLab`) y su división en tres laboratorios: **PC ATX**, **PC compacta** y **laptop**.

Este documento recoge (1) qué funciona mal hoy y por qué, con evidencia de la escena y el código, (2) qué se corrigió ya, (3) cómo funciona el nuevo sistema de cables y (4) el plan para los tres laboratorios, empezando por el ATX.

---

## 1. Diagnóstico del laboratorio actual

Análisis estático de `AssemblyLab.unity` y de los scripts de `Systems/`. Sin conexión al editor en esta sesión, así que nada de esto se ha probado aún en Play.

### 1.1 La cámara le quita el control al estudiante

| Problema | Evidencia | Estado |
|---|---|---|
| Vuelve sola a la vista general tras **5 s** sin input (también mientras lees la ficha) | `CADCameraController.idleTimeBeforeReset: 5` | ✅ Corregido: `autoResetWhenIdle = false` por defecto |
| Vuelve a la vista general **1 s después de soltar cada pieza** | `InteractionManager.ReturnToWideViewAfterDelay(1.0f)` | ✅ Eliminado |
| Clic en una zona vacía → reencuadre completo | `HandleLeftClick` → `GoToWideView()` | ✅ Eliminado (Espacio sigue reencuadrando) |
| La cámara se desplaza **al agarrar** una pieza → la pieza se aleja del cursor mientras arrastras | `FocusOnObject` / `SetTopDownView` en el clic | ✅ Eliminado; ahora el doble clic encuadra la pieza |
| Clic derecho abre la ficha **y** orbita a la vez | Las dos clases leen `Player/Inspect` | ✅ Clic derecho corto = ficha; arrastre con clic derecho = órbita (umbral de 6 px) |
| Zoom de paso fijo 2 en un rango 1–5 → solo ~2 pasos | `zoomStep: 2`, `zoomLimits: {1, 5}` | ✅ Zoom proporcional (12 % por paso) |
| La rueda sobre la ficha técnica hace zoom en vez de desplazar el texto | sin guarda de UI en el zoom | ✅ Los gestos solo empiezan fuera de la UI |

### 1.2 Arrastre y encaje: impreciso y silencioso

| Problema | Evidencia | Estado |
|---|---|---|
| `assemblyZone` **vacío** en la escena: no hay límite de mesa ni altura de arrastre, y la pieza **salta** para poner su pivote bajo el cursor | `InteractionManager.assemblyZone: {fileID: 0}` | ✅ Se conserva el desfase de agarre y la pieza se eleva (`liftHeight`) para pasar sobre la placa |
| El encaje es una esfera de **0.5 u ≈ media placa base** (la placa mide ~1 u): encaja "por magia" en lugares lejanos | `Physics.OverlapSphere(pos, 0.5f)` sin máscara | ✅ El objetivo se calcula por el **centro visual** que tendría la pieza instalada, con tolerancia proporcional a su tamaño |
| No se ve dónde va a quedar la pieza | — | ✅ **Fantasmas** translúcidos en cada lugar compatible; el apuntado se intensifica |
| Error = **teletransporte silencioso** a la mesa | `ResetToTable()` sin feedback | ✅ Toast con icono + texto + **motivo** ("La memoria RAM no va aquí. Aquí va el procesador (CPU). La RAM va en las ranuras DIMM…") y regreso animado |
| No se puede **desinstalar** (un error de ranura obliga a reiniciar) | `isOccupied` nunca se libera | ✅ Arrastrar una pieza instalada la retira |
| Encaje instantáneo, sin sensación de inserción | `localPosition = 0` directo | ✅ Animación: se alinea por encima y baja a su sitio |
| Objetivo de victoria a mano (`totalPartsToInstall = 4`) | `GameManager` | ✅ Se cuenta solo: todos los sockets `required` ocupados |
| Victoria por llamada directa; los eventos no tenían suscriptores | `GameManager.FinishSimulation` | ✅ El HUD se suscribe a `OnProgressChanged` / `OnAssemblyComplete` |
| El resaltado clonaba materiales en cada hover (fuga + rompe batching) | `renderer.materials = …` | ✅ `sharedMaterials` |
| Sin indicador de progreso | — | ✅ Barra "COMPONENTES n / total" arriba al centro |
| Sin orden de montaje | — | ✅ Base lista: `SocketSystem.requires` (p. ej. el disipador exige la CPU, y la CPU no se puede retirar con el disipador puesto) |

### 1.3 Cables: decorativos y sin spline

Los cuatro cables de la escena (`CableHDD` = datos SATA, `CableHDDEnergy` = alimentación SATA, `Cable PSU` = ATX 24 pines, `Cable PSU2` = EPS 12 V) **no son interactivos ni son splines**:

- Son `PCPart` con `isDraggable = 0` y no hay ningún puerto que los acepte: solo muestran una ficha.
- Cada uno es una **cadena de 6 segmentos rígidos** (`root → segment ×6 → head`) que mueve un `ChainIKConstraint` (Animation Rigging) hacia un `Target` **estático**. Por eso:
  - se ven **codos** entre segmentos;
  - la longitud depende del número de segmentos: si el `Target` queda lejos el cable se estira recto y no llega, si queda cerca hace zigzag;
  - no hay caída por gravedad ni apoyo sobre la mesa;
  - el IK solo se resuelve en Play: en el editor lo que ves no es lo que se juega;
  - son 4 `Animator` + `RigBuilder` evaluándose cada frame para algo que no se mueve.

✅ Reemplazado por un sistema nuevo (sección 3). Los 4 cables IK siguen en la escena hasta sustituirlos en el editor.

### 1.4 Escena, escala y arte

- **Escalas no uniformes:** placa base `(49.4, 58.6, 46.7)`, RAM `(10.1, 12.8, 10.1)` → modelos deformados. La escena no está a escala real (1 u ≈ 26 cm) y la RAM sale ~16 % más grande respecto a la placa de lo que es en la realidad.
- **No hay gabinete:** la placa está tumbada sobre el escritorio. Una "PC ATX" sin chasis no enseña lo esencial del montaje: fuente, separadores, bahías y gestión de cables.
- **Fuente y disco duro** están, pero no se pueden instalar (no tienen socket).
- **607k triángulos** en escena (placa 218k, GPU 212k, HDD 117k).
- 23 de las 28 `PCPart` son etiquetas fijas con sus textos guardados **dentro de la escena**.

### 1.5 Pedagogía

- Solo hay 4 pasos (CPU, GPU, RAM ×2) y ningún orden.
- No se registran errores ni intentos (el dato de evaluación más valioso).
- No hay modo guiado frente a modo examen: hoy todo es "adivinar y soltar".

---

## 2. Qué se cambió en esta sesión (código, compila sin errores ni warnings)

| Archivo | Cambio |
|---|---|
| `Systems/Interaction/InteractionManager.cs` | Reescrito: clic frente a arrastre con umbral, doble clic para encuadrar, desfase de agarre, elevación, fantasmas, borde rojo sobre un lugar incompatible, toast con motivo, desinstalar, bloqueo por dependencias, ficha con clic derecho corto, congelado con modal abierto |
| `Systems/Interaction/PlacementPreview.cs` | **Nuevo.** Fantasmas de la pieza en los lugares compatibles |
| `Systems/Interaction/IDragConstraint.cs` | **Nuevo.** Interfaz para limitar el arrastre (la usa el conector de cable) |
| `Systems/Interaction/GameManager.cs` | Objetivo automático, eventos de progreso/retirada/victoria |
| `Systems/Interaction/ObjectHighlighter.cs` | `sharedMaterials` (sin fuga) y sin rehacer arrays si ya está resaltado |
| `Systems/Assembly/SocketSystem.cs` | Registro estático (`All`), `Evaluate` con motivo, `RemovePart`, `required`, `snapRadius`, `requires` (orden), `EvaluateRemoval`, `FindTarget` |
| `Systems/Assembly/PCPart.cs` | `BeginDrag` / `InstallInto` / `ReturnToTable` animados; recuerda su padre original; centro visual y tamaño |
| `Systems/Assembly/CompatResult.cs` | **Nuevo.** Resultado sí/no + mensaje + motivo |
| `Systems/Camera/CADCameraController.cs` | Órbita con umbral, sin auto-reset por defecto, zoom proporcional, gestos que no empiezan sobre la UI, `Dispose` de los controles |
| `Systems/Cables/CableSpline.cs` | **Nuevo.** Cable procedural (sección 3) |
| `Systems/Cables/CablePlug.cs` | **Nuevo.** Conector arrastrable con alcance limitado |
| `Systems/Cables/Editor/CableMenu.cs` | **Nuevo.** `GameObject ▸ Simulador ▸ Cable (spline)` y `▸ Puerto de cable` |
| `Content/Shaders and Materials/GhostPreview.shader` + `Content/Resources/Mat_GhostPreview.mat` | **Nuevo.** Shader translúcido con borde Fresnel (URP) para los fantasmas |
| `Content/Resources/Mat_Cable.mat` | **Nuevo.** Material por defecto del cable (mismo shader estilizado que la escena) |
| `Data/.../ConnectionTypeSO.cs` + 5 assets | Campos `displayName` y `placementHint` (los textos del motivo), rellenados para CPU/GPU/RAM/HDD/PSU |
| `Data/.../Type_ATX24, EPS8, PCIePower, SATAPower, SATAData` | **Nuevos** tipos de conector de cable, con sus textos |
| `UI/Common/FeedbackToast.cs` + `Controls.uss` | Icono **vectorial** (✓ ✕ ! i con Painter2D) cuando no hay sprite; color desde los tokens USS |
| `UI/Assembly/*` | Barra de progreso, capa de toast, instrucciones con los gestos nuevos, victoria por evento |

**Sin cambios en la escena:** todo lo anterior funciona con el cableado actual de `AssemblyLab`. Los campos nuevos tienen valores por defecto razonables y los materiales nuevos se cargan desde `Resources`.

---

## 3. El sistema de cables

### 3.1 Anatomía

```
Cable_ATX24                ← CableSpline (+ MeshFilter/Renderer/Collider) + PCPart (solo ficha) + ObjectHighlighter
 ├─ Inicio                 ← ancla fija (p. ej. sobre la fuente). Z+ = hacia donde sale el cable
 ├─ Brida_1 … (opcional)   ← puntos de paso / pasacables, en orden
 └─ Conector               ← PCPart arrastrable (connectionType = Type_ATX24) + CablePlug + collider
Puerto_ATX24 (en la placa) ← SocketSystem (allowedType = Type_ATX24) + snapPoint con Z+ hacia fuera del puerto
```

- **El conector es una pieza más.** Reutiliza todo el flujo: fantasma, rechazo con motivo, progreso, desinstalar y `requires`. Un puerto `required` cuenta para completar el lab.
- **El tubo sigue al conector solo** (`endAnchor` = el Transform del conector), también cuando queda instalado en el puerto.

### 3.2 Cómo se construye la curva

- Bézier cúbica por tramos. **Sale recto de cada conector** a lo largo de su eje Z+ (`stiffness` = largo de ese tramo recto); en los puntos de paso usa tangentes de Catmull-Rom.
- **Longitud finita:** `length` (o automática = recorrido inicial × `autoSlack`). El conector no se puede alejar más de lo que mide el cable (`CablePlug` → `ClampToReach`).
- **Holgura:** lo que sobra de longitud comba los tramos hacia `slackDirection` (abajo = gravedad; arriba = cable rígido que se arquea por encima de la placa).
- **`restSurface`:** el cable no atraviesa esa superficie, se apoya en ella (la mesa, la bandeja de la placa).
- **Perfil `Round` o `Flat`:** el plano sirve para SATA y para las cintas de la laptop; llega al conector final bien orientado (corrección de giro; verificado: 0° de desvío).
- La malla solo se regenera cuando algo se mueve, se ve **en el editor** mientras se traza y cuesta ~1–2k triángulos por cable.

### 3.3 Montar un cable (en el editor)

1. `GameObject ▸ Simulador ▸ Cable (spline)` → crea `Cable` con `Inicio` y `Conector` (un bloque provisional).
2. Coloca `Inicio` en la salida de la fuente con su eje azul (Z+) hacia donde sale el cable.
3. En el `Conector`: asigna `connectionType` (p. ej. `Type_ATX24`) y rellena `title`/`description`. Sustituye el bloque por el modelo del conector cuando lo haya (deja el `PCPart` en el padre).
4. `GameObject ▸ Simulador ▸ Puerto de cable` sobre la placa → asigna `allowedType` y orienta el `SnapPoint` con Z+ **hacia fuera** del puerto.
5. Ajusta `radius`, `length`/`autoSlack`, `slackDirection` y, si hace falta, añade puntos de paso.
6. Para reemplazar un cable IK antiguo: crea el nuevo con el mismo inicio y desactiva el viejo (`CableHDD`, `CableHDDEnergy`, `Cable PSU`, `Cable PSU2`).

---

## 4. Arquitectura de los tres laboratorios

**Una escena por laboratorio con los mismos sistemas.** Cada lab tiene su arte, su encuadre y sus piezas, pero comparte `InteractionManager`, `CADCameraController`, `GameManager`, HUD y cables.

- **`LabDefinitionSO`** (por crear): id, título, descripción, escena, disponible/"próximamente", imagen y objetivos. Un `LabCatalogSO` con los tres.
- **Prefab `Lab_Systems`**: cámara + managers + HUD ya cableados, para que un lab nuevo no repita el montaje.
- **Selector:** en ModeSelection, "LABORATORIO DE ENSAMBLAJE" abre tres tarjetas (ATX disponible; compacta y laptop marcadas "próximamente" hasta que existan).
- **Orden de montaje y dependencias:** `SocketSystem.requires` (ya implementado).
- **Modo guiado / examen:** `showPlacementHints` (fantasmas sí o no) + contador de errores por pieza. Pendiente: el contador y la pantalla de resultados.

### 4.1 Lab ATX (el primero)

Gabinete mid-tower **tumbado de lado** y sin panel lateral, como se monta en la vida real: la bandeja de la placa queda horizontal y el arrastre sobre un plano sigue funcionando.

| # | Paso | Lo que enseña | Dependencias (`requires`) |
|---|---|---|---|
| 1 | Fuente (PSU) en su compartimento | orientación del ventilador | — |
| 2 | CPU en el socket | alineación del triángulo, sin fuerza | — |
| 3 | Disipador sobre la CPU | pasta térmica | CPU |
| 4 | RAM ×2 en A2/B2 | dual channel (aviso, no error) | — |
| 5 | SSD M.2 | ranura y tornillo | — |
| 6 | Placa al gabinete (sobre los separadores) | I/O shield, separadores | (2–5 conviene hacerlo fuera) |
| 7 | GPU en PCIe x16 | tapas traseras, cierre de la ranura | placa en el gabinete |
| 8 | Disco 2,5"/3,5" en la bahía | bahías | — |
| 9 | Cables: ATX 24, EPS 8, PCIe 6+2, SATA (alimentación + datos) | qué alimenta cada uno, dónde va | componente destino instalado |
| 10 | Panel frontal, USB y audio frontal, ventiladores | cabeceras de la placa | placa en el gabinete |

**Con lo que ya hay** se pueden hacer los pasos 2, 4 y 7, los cables 9 y el disco/fuente (en cuanto tengan socket). **Falta arte:** gabinete, disipador, SSD M.2 y modelos de conectores.

### 4.2 Lab PC compacta (Mini-ITX / SFF)

Mismos sistemas; cambia lo que se enseña: fuente SFX, disipador de perfil bajo (**altura máxima**), longitud máxima de la GPU, solo 2 DIMM, M.2 por detrás de la placa, cables cortos o modulares y un **orden que importa más** (en un gabinete pequeño, si instalas la GPU primero ya no llegas al EPS). Nuevo: regla de **holgura física** (dimensiones de la pieza frente al hueco), que necesita las dimensiones de cada pieza en datos.

### 4.3 Lab laptop

Mecánicas nuevas que hay que añadir al sistema:

- **Tornillos** de la tapa inferior (`ScrewPoint`: quitar en orden, contador de tornillos).
- **Desconectar la batería primero** (regla de seguridad: `requires` invertido; nada se instala o retira con la batería conectada).
- **Inserción en ángulo:** SO-DIMM a ~30° y bajar hasta el clic; M.2 2280 en ángulo + tornillo. Necesita una dirección de aproximación por socket (hoy el encaje entra siempre desde arriba).
- **Cables finos:** antenas Wi-Fi (coaxiales, `Round` fino) y cintas FPC/ZIF de teclado/touchpad (`Flat`) con el mismo `CableSpline`.

---

## 5. Lo que bloquea: modelos 3D

No hay gabinete, laptop ni gabinete compacto en el proyecto. Requisitos para que encajen con el sistema:

- **Escala real** (1 u = 1 m) y **escala uniforme** en el prefab.
- **Pivote en el punto de encaje** de la pieza (o un hijo vacío que lo marque).
- Presupuesto: < 40k triángulos por pieza grande y < 10k por pieza pequeña (hoy la placa y la GPU pasan de 200k).
- Fuentes posibles: Sketchfab con licencia CC0 o CC-BY (atribución en Créditos) o modelado propio. Mientras tanto, un **gabinete greybox** (bandeja, compartimento de la fuente, bahías y pasacables hechos con cajas) permite construir y probar el flujo del lab ATX completo y sustituir el arte después.

### 5.1 Candidatos encontrados (Sketchfab, descargables; búsqueda del 2026-10-17)

Todos **CC-BY** salvo que se indique: se pueden usar y modificar **citando al autor** (añadir a Créditos: nombre del modelo, autor, enlace y licencia). Los descartados por licencia NC/ND no aparecen. Cifras = triángulos según Sketchfab. Lo que no se ve en la ficha (si el panel lateral es separable, la escala, los pivotes) hay que confirmarlo al descargar.

**Lab ATX**

| Pieza | Modelo | Tris | Notas |
|---|---|---|---|
| Gabinete (1.ª opción) | [Basic PC case — sousinho](https://sketchfab.com/3d-models/2869fb18575d49128a3115d42d5d3826) | 27k | Gabinete **vacío** con recortes y PBR 4k: justo lo que hace falta (el interior lo ponen las piezas) |
| Gabinete (realista) | [Corsair 4000D Airflow — kirigami318](https://sketchfab.com/3d-models/34df3ba96cf94e71accdb5779c4b8694) | 98k | Mid-tower real muy común; habría que reducirlo |
| Gabinete (chasis de chapa) | [Computer Case — Mohzayan](https://sketchfab.com/3d-models/44c2b95a20b942ef81f4ecf3deac4a51) | 13k | Chasis de chapa ligero; útil si se ve bien la estructura interior |
| Placa + CPU + RAM + M.2 | [MotherBoard + Components — DanielCardonaArt](https://sketchfab.com/3d-models/3bc94057328243d4b341a55f59160f8a) | 82k | ASUS Z370-E + i7 + RAM + SSD M.2, con texturas. Podría **sustituir a la placa actual de 218k** |
| GPU ligera | [RTX 3060 TI Low Poly — alexlh2003](https://sketchfab.com/3d-models/b65c5c0807be4ece9a95090d1a1794bb) | 14k | Sustituye a la GPU actual de 212k (**−198k tris**) |
| Disipador | [CPU Cooler — bazyaev08](https://sketchfab.com/3d-models/672a0a74a98c452a862016bee99f3579) | 6.5k | Texturizado (Substance) |
| SSD M.2 | [Samsung 980 Pro NVMe — BlenderFace1](https://sketchfab.com/3d-models/1f386c8d782d4b208717bc64474b98b3) | 1.4k | Alternativa: [SSD M.2 genérico — hlupich](https://sketchfab.com/3d-models/ac3e66d1e3bf46dc8684a3e4abae584b) (8k) |
| Almacenamiento | [Storage (SSD, HDD, M.2) — blue-odym](https://sketchfab.com/3d-models/bdd5fd67a7674359ab8648204b5c0575) | 111k | SSD 2,5", HDD y M.2 en un pack; reducir el HDD |
| Ventilador | [Lian Li UNI FAN SL120](https://sketchfab.com/3d-models/5abd0d8e89ea4241b7216f4b6d5a2ca4) / [Corsair Fan — umkhero](https://sketchfab.com/3d-models/27f023b65e814d1c946460fcc0cb8250) | ~5k | |
| Fuente (alternativa) | [Aerocool KCAS 500W — nofurion](https://sketchfab.com/3d-models/bfb1f77fffb0410f9ee0d0be6fb5fc88) | 36k | "Game ready"; la actual (25k) ya sirve |
| Pack de respaldo | [Computer Components — crimsonfalcon](https://sketchfab.com/3d-models/4daeb72925d140809bdfd51634a1908e) | 37k | Fuente, unidad, ventilador, CPU, disipador y RAM ligeros |

**Lab PC compacta** — no hay ningún gabinete compacto con interior utilizable (el [Ncase M1](https://sketchfab.com/3d-models/bf11cc4a694a40238a2f95d4db92b590) no tiene interior, según su autor). Hay placas Mini-ITX a escala: [Mini ITX — cavicomdb](https://sketchfab.com/3d-models/8048cbfb32414c1fa9e71d33df2bdf20) (5k, "modelled to scale") y [Mini-itx Motherboard — even_mel415](https://sketchfab.com/3d-models/b2fbf44947a545469b36baa5dc2dd9bb) (3k, 1:1). Exteriores de PC compacta de oficina: [Slim ThinkCentre](https://sketchfab.com/3d-models/5e09f363c0db468bbfae109cfff4bb3c) (22k) y [HP EliteDesk SFF — GeniusPilot2016](https://sketchfab.com/3d-models/1d86f13480454fe5b6a25e1acea415f3) (7k). **Plan:** modelar el chasis (es una caja de chapa simple) y reutilizar las piezas del lab ATX.

**Lab laptop** — no existe una laptop descargable con tapa inferior separable y componentes sueltos. Las de Framework son **escaneos** de una sola malla (731k–852k tris), así que no sirven para interactuar. **Plan:** carcasa exterior + interior armado con piezas sueltas:
- Carcasa: [Dell Latitude 5400 — Yolala3d](https://sketchfab.com/3d-models/c3dfc53a600e427c9db9be83aec9860d) (66k, detallada, de empresa) o [laptop semi detailed — brian-aa](https://sketchfab.com/3d-models/70b963af167343cd9194ba77d3765e39) (68k, estilo ThinkPad T60). La tapa inferior y la placa interior se modelan aparte.
- SO-DIMM: [8 GB SODIMM DDR4 — Cashierobz](https://sketchfab.com/3d-models/d5c9a2a4d15146889aa2e0b360ff00a2) (2.5k). SSD M.2: el mismo del lab ATX. Batería: [Mac Laptop Battery — Graphitage](https://sketchfab.com/3d-models/e463a4413af647a3b88b6c6d40a12e72) (29k). Ventilador: [Laptop cooling fan — ponchoguy](https://sketchfab.com/3d-models/0796a914f76944f8bb443dce83dc0c8a) (127k, hay que reducirlo).

**Conectores de cable** (ATX 24, EPS 8, PCIe 6+2, SATA) — no hay modelos útiles. **Plan:** generarlos por código (carcasa + rejilla de pines + pestaña), ligeros y a medida del sistema de cables.

**Descartados:** [Ultimate PC Computer Pack](https://assetstore.unity.com/packages/3d/props/ultimate-pc-computer-pack-287175) del Asset Store (29,99 $, 1,5 GB, sin lista de contenido ni polígonos publicados) y [Simple PC parts — NullSys](https://nullsys.itch.io/simple-pc-parts-low-polly) en itch.io (gratis, pero sin licencia formal y en desarrollo).

**Preparación (Blender 4.5 instalado, por script):** reducir a < 40k tris, separar las piezas móviles (panel lateral, tapa inferior), aplicar escala real y uniforme, poner el pivote en el punto de encaje y exportar FBX. En Unity, pasar los materiales al shader estilizado del proyecto para que todo combine.

---

## 6. Cómo probar (Play en `AssemblyLab`)

1. Arrastra la RAM: aparecen dos siluetas en las ranuras DIMM; la que apuntas se intensifica. Suelta → baja a su sitio, toast verde "Instalado: …" y la barra sube (el total es **6**: 4 componentes + 2 cables).
2. Arrastra la RAM sobre el socket de la CPU → borde rojo; al soltar, toast con el motivo y la RAM vuelve a la mesa.
3. Arrastra una pieza ya instalada fuera de su sitio → se retira y la barra baja.
4. Clic derecho corto sobre una pieza → ficha (la cámara no se mueve). Clic derecho y arrastrar → órbita (no abre la ficha).
5. Deja la cámara quieta 10 s → **no** debe reencuadrar sola. Espacio sí reencuadra. Doble clic sobre una pieza → la encuadra.
6. Rueda con el puntero sobre la ficha → desplaza el texto, sin zoom.
7. **Cables:** los dos bloques negros sobre la mesa, entre la RAM y la placa, son los conectores de los cables de la fuente. Agarra el bloque **o el propio cable** y arrastra (un clic solo no hace nada). El ATX de 24 pines va al conector largo del borde trasero de la placa, junto a la RAM; el EPS de 8 pines, al conector pequeño del borde izquierdo, junto a la CPU. En el puerto equivocado sale el motivo. Arrastrarlo fuera lo desconecta.
8. Completa todo (6/6) → modal de victoria tras asentarse la última pieza.

**Ajustes en el inspector si algo no se siente bien:** `InteractionManager.liftHeight` (altura al arrastrar), `settleDuration`, colores del fantasma; `SocketSystem.snapRadius` (tolerancia de un lugar concreto); `CADCameraController.zoomPercent`, `orbitStartPixels`, `autoResetWhenIdle`; en cada cable, `CableSpline.length` / `slackAmount` (cuánto se arquea al conectar) / `radius`.

**Verificado por mí en Play (2026-10-17):** consola limpia, objetivo automático, HUD de progreso, alcance máximo del cable, rechazo con motivo en el puerto equivocado, instalación del conector ATX (progreso 1/6, el cable se arquea por encima de la placa) y fantasma sin errores. **Sin verificar:** el gesto con el ratón real (la herramienta de ratón simulado no llega al juego). Es la primera prueba que hay que hacer a mano.

---

## 7. Decisiones tomadas y plan de trabajo

### 7.1 Decisiones (2026-10-18)

| # | Tema | Decisión | Consecuencia |
|---|---|---|---|
| 1 | Prueba de los cables ATX 24 / EPS 8 | **Funcionan bien** (probado por el usuario) | El sistema de cables queda validado; se replica para el resto |
| 2 | Gabinete del lab ATX | **[Basic PC case](https://sketchfab.com/3d-models/2869fb18575d49128a3115d42d5d3826)** (27k, vacío por dentro) | Hay que separar el panel lateral y fijar la bandeja de la placa |
| 3 | Forma de montaje | **Placa fuera y luego al gabinete** | CPU, disipador, RAM y M.2 se instalan con la placa sobre la mesa; luego la placa entra al gabinete tumbado y se conecta todo |
| 4 | Escala | **Escala real (1 u = 1 m)**, escalas uniformes | El lab ATX se construye en una **escena nueva**; `AssemblyLab` actual queda como referencia hasta reemplazarla |
| 5 | Modelos pesados | **Sustituir los dos:** placa → [MotherBoard + Components](https://sketchfab.com/3d-models/3bc94057328243d4b341a55f59160f8a), GPU → [RTX 3060 TI Low Poly](https://sketchfab.com/3d-models/b65c5c0807be4ece9a95090d1a1794bb) | −330k triángulos; las fichas de los puertos de la placa hay que rehacerlas sobre el modelo nuevo |
| 6 | Pasos del montaje | Los 10 del §4.1, **sin orden obligatorio** | `requires` solo para dependencias físicas: disipador ← CPU; GPU, panel frontal y cables de placa ← placa en el gabinete; cada cable ← su componente destino |
| 7 | Cables | **Todos:** ATX 24 y EPS 8 (hechos), **PCIe 6+2, SATA alimentación + datos, panel frontal, USB/audio frontal y ventiladores** | Panel frontal y cabeceras son conectores pequeños de pines: hacen falta conectores por código y tolerancias finas |
| 8 | PC compacta | **PC pequeña de oficina (SFF)** tipo OptiPlex / ProDesk / ThinkCentre | Se modela el chasis (caja de chapa); enseña perfil bajo, fuente propietaria y poco espacio |
| 9 | Carcasa laptop | **[Dell Latitude 5400](https://sketchfab.com/3d-models/c3dfc53a600e427c9db9be83aec9860d)** | Tapa inferior e interior se modelan aparte |
| 10 | Mecánicas laptop | **Todas:** tornillos de la tapa, batería primero, inserción en ángulo (SO-DIMM/M.2), antenas Wi-Fi y cintas planas | Nuevos sistemas: `ScrewPoint`, regla de seguridad, dirección de aproximación por socket, cables finos/planos |
| 11 | Evaluación | **Guiado + contador de errores, sin nota** | Siluetas siempre visibles; al terminar, resumen de errores por pieza |
| 12 | Selector de labs | **Pantalla con 3 tarjetas** (ATX disponible; compacta y laptop "Próximamente") | `LabDefinitionSO` + catálogo + vista UITK |
| 13 | Descarga de modelos | **Token de la API de Sketchfab** del usuario | Ver §7.3; el token nunca va al repo ni al chat |
| 14 | Limpieza | **Borrar todo** (cables IK, paquete Animation Rigging, escena `CableTest`) cuando ya no se usen | Ocurre al retirar `AssemblyLab` por el lab ATX nuevo |
| 15 | Preferencia de Unity | **Hecho:** *Script Changes While Playing → Recompile After Finished Playing* (EditorPrefs `ScriptCompilationDuringPlay = 1`) | Una recompilación ya no rompe un Play en curso |
| 16 | Commit | **Rama nueva** `labs-practicos`, sin líneas de co-autoría | |
| 17 | Sonidos de acierto/rechazo y color space Linear | **Siguen aplazados** | |

### 7.2 Plan de trabajo, en orden

**Fase A — sin modelos (se puede hacer ya)**
1. ✅ Selector de laboratorios: `LabDefinitionSO` + `LabCatalog` + modal de 3 tarjetas en ModeSelection (2026-10-18, verificado en Play).
2. Contador de errores por pieza + resumen al terminar (en el modal de victoria).
3. Conectores generados por código (24 pines, EPS, PCIe, SATA, pines del panel frontal) para sustituir los bloques.
4. Prefab `Lab_Systems` (cámara + managers + HUD) como base común de las escenas de laboratorio.

**Fase B — lab ATX (necesita el token)**
5. ✅ Descargados (2026-10-18) con el token del usuario. Los originales están en `C:\Users\devin\SimuladorArqComp_ArtSource\Sketchfab\` (fuera del repo) y los créditos en [Creditos_Modelos.md](Creditos_Modelos.md).
6. ✅ Preparados en Blender por script y exportados a `Assets/Project/Content/Models/Lab_ATX/`:
   - Escala real verificada en Unity (1 u = 1 m).
   - Panel lateral separado y paredes con grosor.
   - Texturas de 2048 px como máximo.
   - Total del lab ≈ 90k triángulos (antes 607k).

   **Pendiente:** pasar los materiales (hoy URP Lit) al estilo del proyecto si se quiere el mismo aspecto, y añadir al gabinete lo que no trae: separadores (standoffs) y zona de la fuente.
7. Escena nueva `Lab_ATX` a escala real: mesa, gabinete tumbado, placa sobre la mesa → al gabinete, sockets, todos los cables con `requires` físicos.
8. Sustituir `AssemblyLab` en Build Settings y en el selector; limpieza del punto 14.

**Fase C — labs 2 y 3**
9. Lab PC SFF de oficina (modelar el chasis; reutilizar piezas).
10. Lab laptop (Dell Latitude 5400 + interior + las 4 mecánicas).

### 7.3 Cómo pasar el token de Sketchfab (sin exponerlo)

1. En Sketchfab: *Settings ▸ Password & API* → copia tu **API token**.
2. Guárdalo en un archivo **fuera del repositorio**: `C:\Users\devin\.sketchfab_token` (una sola línea, solo el token).
3. Avísame. Lo leo de ahí al descargar; no lo copio al proyecto ni lo escribo en el chat. La API entrega glTF o el archivo original; Blender los convierte a FBX.
