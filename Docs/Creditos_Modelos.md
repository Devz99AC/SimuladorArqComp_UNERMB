# Créditos de modelos 3D de terceros

Modelos usados en el simulador bajo licencia **Creative Commons Atribución 4.0** ([CC BY 4.0](http://creativecommons.org/licenses/by/4.0/)). Esa licencia permite usarlos y modificarlos si se cita al autor, se enlaza la licencia y se indica que hubo cambios. Por eso cada modelo aparece aquí, y antes de publicar el lab también tiene que salir en la pantalla de **Créditos** del juego.

**Cambios hechos a todos:** escalados a tamaño real, simplificados (menos triángulos), separados en piezas, texturas reducidas y exportados a FBX. Los originales descargados están fuera del repositorio, en `C:\Users\devin\SimuladorArqComp_ArtSource\Sketchfab\`; cada carpeta tiene su `licencia.json`.

| Uso en el simulador | Modelo | Autor | Enlace |
|---|---|---|---|
| Gabinete del lab ATX (`Lab_ATX/Gabinete`) | Basic PC case | [Sousinho](https://sketchfab.com/sousinho) | [sketchfab.com/3d-models/2869fb18…](https://sketchfab.com/3d-models/2869fb18575d49128a3115d42d5d3826) |
| Placa base, CPU, RAM y SSD M.2 (`Lab_ATX/PlacaBase`) | MotherBoard + Components | [Daniel Cardona](https://sketchfab.com/DanielCardonaArt) | [sketchfab.com/3d-models/3bc94057…](https://sketchfab.com/3d-models/3bc94057328243d4b341a55f59160f8a) |
| Tarjeta de video (`Lab_ATX/GPU`) | RTX 3060 TI - LOW POLY | [Up1x](https://sketchfab.com/alexlh2003) | [sketchfab.com/3d-models/b65c5c07…](https://sketchfab.com/3d-models/b65c5c0807be4ece9a95090d1a1794bb) |
| Disipador de CPU (`Lab_ATX/Disipador`) | CPU Cooler | [Fochdog](https://sketchfab.com/bazyaev08) | [sketchfab.com/3d-models/672a0a74…](https://sketchfab.com/3d-models/672a0a74a98c452a862016bee99f3579) |
| SSD M.2 nuevo del lab 3 (`Lab_ATX/SSD_980Pro`) | Samsung 980 Pro NVMe 1tb | [BlenderFace](https://sketchfab.com/BlenderFace1) | [sketchfab.com/3d-models/1f386c8d…](https://sketchfab.com/3d-models/1f386c8d782d4b208717bc64474b98b3) |
| Ventilador de 120 mm (`Lab_ATX/Ventilador120`) | Lian Li UNI FAN SL120 RGB BLACK | [DIEKO](https://sketchfab.com/DIEKO) | [sketchfab.com/3d-models/5abd0d8e…](https://sketchfab.com/3d-models/5abd0d8e89ea4241b7216f4b6d5a2ca4) |
| Carcasa y tapa inferior del lab 3 (`Lab_Laptop/Laptop`) | Laptop Dell Latitude 5400 | [Yolala3D \| Y3D](https://sketchfab.com/Yolala3d) | [sketchfab.com/3d-models/c3dfc53a…](https://sketchfab.com/3d-models/c3dfc53a600e427c9db9be83aec9860d) |
| Memoria SO-DIMM del lab 3 (`Lab_Laptop/SODIMM`) | 8 GB RAM SODIMM DDR4 | [Cashierob](https://sketchfab.com/Cashierobz) | [sketchfab.com/3d-models/d5c9a2a4…](https://sketchfab.com/3d-models/d5c9a2a4d15146889aa2e0b360ff00a2) |
| Ventilador de la laptop (`Lab_Laptop/VentiladorLaptop`) | Laptop cooling fan | [Guy in a Poncho](https://sketchfab.com/ponchoguy) | [sketchfab.com/3d-models/0796a914…](https://sketchfab.com/3d-models/0796a914f76944f8bb443dce83dc0c8a) |

**Lab 2 (`Lab_SFF`)**: no usa modelos nuevos. El chasis y las piezas propias de la PC compacta se hacen con primitivas en `LabSffBuilder`, y reutiliza de esta tabla la CPU, la RAM y el SSD de «MotherBoard + Components» y el ventilador, además del disco ligero de los modelos viejos. Los créditos de la pantalla del juego ya los cubren.

**Lab 3 (`Lab_Laptop`)**: la carcasa es el Dell Latitude 5400 de Yolala3D, modificado en Blender (`Tools/Modelos/blender_preparar.py`): se borró lo que queda dentro con la laptop cerrada (teclado, textos, bisel y panel), se cerró la pantalla, se volteó, se llevó a 323,05 × 216 × 20,2 mm (las medidas de Dell; el modelo era un 28 % más bajo), se separaron las caras del fondo como tapa inferior, se dio grosor a las paredes, se corrigieron sus normales y se quitó el hueco del touchpad, que sobresalía hacia dentro. La SO-DIMM se escaló a 69,6 × 30 mm; el ventilador (la variante sin cable de las dos que trae) se redujo de 63k a 7,6k triángulos y se llevó a 62 mm, un tamaño aproximado. El interior (placa, batería, disipador, soportes, tarjeta Wi-Fi y conectores) se hace con primitivas en `LabLaptopBuilder`, y el SSD viejo es el de «MotherBoard + Components». También se descargó «Mac Laptop Battery» (Graphitage), pero no se usa: es una batería cúbica que no se parece a la de Dell.

**En el juego** (desde el 2026-10-18): menú principal ▸ CRÉDITOS, sección «Modelos 3D del laboratorio» (`UI/MainMenu/MainMenu.uxml`). Allí van los modelos que usan las escenas `Lab_ATX`, `Lab_SFF` y `Lab_Laptop`, también el SSD 980 Pro, que desde el lab 3 sí se usa.

Texto corto para la pantalla de Créditos:

> Modelos 3D (CC BY 4.0, modificados): «Basic PC case» de Sousinho · «MotherBoard + Components» de Daniel Cardona · «RTX 3060 TI - LOW POLY» de Up1x · «CPU Cooler» de Fochdog · «Samsung 980 Pro NVMe» de BlenderFace · «Lian Li UNI FAN SL120» de DIEKO · «Laptop Dell Latitude 5400» de Yolala3D · «8 GB RAM SODIMM DDR4» de Cashierob · «Laptop cooling fan» de Guy in a Poncho — vía Sketchfab.

Los modelos de la escena `AssemblyLab` actual (placa, GPU, disco, fuente, RAM, CPU, mesa) son anteriores y su origen no está documentado: hay que averiguarlo antes de distribuir.
