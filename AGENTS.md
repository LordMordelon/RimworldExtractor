# Notas para agentes

Contexto para trabajar en este repositorio sin tener que redescubrirlo. El
[README](README.md) explica qué hace la herramienta y cómo usarla; acá está lo que
importa a la hora de tocar el código.

## Qué es esto

Fork en español latino de [csh1668/RimworldExtractor](https://github.com/csh1668/RimworldExtractor),
que está en coreano. Extrae los datos traducibles de RimWorld y de sus mods
(`Defs`, `Keyed`, `Strings`, `Patches`) y los devuelve como XML o como planilla de Excel.

Tiene un repositorio hermano, **RML** (`../RML`), que es el mod de RimWorld que
empaqueta las traducciones producidas con esta herramienta. Son proyectos separados
con licencias distintas: esta herramienta es MIT, RML deriva de código GPL-3.0.

## Estructura

| Proyecto | Qué contiene |
|---|---|
| `RimworldExtractorInternal` | Toda la lógica: extracción, lectura y escritura de XML/XLSX, listado de mods, configuración. No depende de WinForms. |
| `RimworldExtractorGUI` | Los nueve formularios, más los ayudantes de `Utils/`. |
| `RimworldExtractorTest` | MSTest. Los tests usan **la instalación real de RimWorld de la máquina**, así que se declaran `Inconclusive` si no la encuentran en vez de fallar. |

Archivos que conviene conocer antes de tocar nada:

- **`RimworldExtractorInternal/Strings.cs`** — todo el texto visible, en un solo lugar.
- **`RimworldExtractorInternal/Prefabs.cs`** — la configuración persistida en `Prefabs.dat`.
- **`RimworldExtractorInternal/IO.cs`** — lectura y escritura de XML y planillas.
- **`RimworldExtractorGUI/Utils/`** — `AutoAjuste` y `Rejilla` (ver más abajo).

## Compilar y probar

Requiere el **SDK de .NET 10**. Los tres proyectos apuntan a `net10.0-windows`.

```
dotnet build RimworldExtractor.sln -c Debug
dotnet test  RimworldExtractor.sln
```

Dos cosas que muerden:

- **Si la app está abierta, la compilación falla** con `MSB3027`: el `.exe` queda
  bloqueado. Compilar en `-c Release` usa otra carpeta y esquiva el problema sin
  tener que cerrarle la aplicación a nadie.
- **La compilación incremental a veces no toma los cambios de maquetación**, porque
  el resultado depende de código que corre en tiempo de ejecución. Ante un cambio de
  interfaz que "no se aplica", probar `--no-incremental` antes de buscar el error en
  otro lado.

## Reglas del fork

**Nunca editar un `.Designer.cs`.** Es la regla principal. Toda la traducción y todos
los retoques de maquetación se hacen en tiempo de ejecución, desde el método
`ApplyStrings()` de cada formulario. Así los merges con upstream —que sigue
desarrollándose en coreano— no dan conflictos en los archivos generados.

De ahí se desprende el resto:

- **Ningún texto visible va escrito en el código.** Todo pasa por `Strings.cs`, cuyos
  identificadores están en inglés y sus valores en español.
- **Ningún color va cableado.** El tema se elige desde la ventana principal —claro,
  oscuro o el de Windows— y se guarda en `Prefabs.dat`, así que los colores salen de
  `SystemColors` o del `e.ForeColor` que llega al evento. Las listas de `FormSelectMod`
  se dibujan por código y ya tuvieron este error una vez. La documentación de
  `Application.SetColorMode` pide llamarla antes de crear ventanas, pero cambiarla
  después funciona: está comprobado que las ventanas abiertas se repintan.
- **Comentarios en español**, explicando *por qué* y no *qué*. En los comentarios se
  suelen omitir las tildes (se conserva la `ñ`); en los textos de `Strings.cs` van
  completas.

## Maquetación

Los formularios vienen de upstream con posición y tamaño absolutos, medidos para
textos en coreano. El español ocupa bastante más, así que hay dos ayudantes que
reacomodan todo al arrancar:

- **`AutoAjuste`** mide cada control con su fuente real, lo ensancha si su texto no
  entra, corre a los vecinos que quedarían tapados y agranda la ventana. Al final fija
  el `MinimumSize`, para que no se pueda encoger hasta romper lo que acaba de acomodar.
- **`Rejilla`** reparte espacio donde los anclajes de WinForms no alcanzan. Un anclaje
  solo sabe mantener la distancia a un borde, así que no puede repartir el ancho entre
  dos controles hermanos: para columnas parejas o rejillas hay que calcularlo a mano.

**El orden importa** dentro de `ApplyStrings()`: primero los anclajes, después los
textos, después `Rejilla`, y `AutoAjuste` al final, que es lo que fija el tamaño
definitivo de la ventana. Cualquier cálculo que dependa de ese tamaño va después.

**Cuidado con el orden Z.** Hay controles superpuestos en el diseño original que no se
notan porque su texto está centrado o vacío. En `FormMain` el enlace de versión arranca
en la misma columna que el rótulo del log, y lo tapa. Este tipo de choque no lo detecta
una revisión visual, porque el control que tapa puede estar vacío hasta que responde una
consulta de red.

### Verificar la interfaz

Windows bloquea la captura de ventanas que no están en primer plano, así que no sirve
sacar una captura de pantalla. Lo que funciona es `Control.DrawToBitmap` sobre un
formulario mostrado fuera de pantalla: se puede renderizar cada ventana a PNG, medir
los controles, agrandar la ventana y comprobar que lo que tiene que crecer creció.

Sí tiene un punto ciego: **los controles nativos de Windows se capturan por
`WM_PRINTCLIENT`**, que reproduce lo que pinta Windows pero no lo que WinForms dibuja
encima al repintar. El texto de sugerencia de un `TextBox` (`PlaceholderText`), por
ejemplo, nunca aparece en el render aunque en pantalla se vea.

## Trampas conocidas

- **`MessageBox` no obedece al tema.** Es un diálogo del propio Windows: con la interfaz
  en oscuro salía en blanco. Los cuadros de mensaje van por `Aviso`
  ([Aviso.cs](RimworldExtractorGUI/Utils/Aviso.cs)), que es una ventana normal de WinForms
  y por eso sí se pinta con el tema puesto. No volver a `MessageBox`.
- **Codificación.** Varios archivos venían en CP949 (coreano). Los bytes no son UTF-8
  válido, así que un `grep` de texto coreano no los encuentra: parecen ya traducidos y
  no lo están. `PatchOperations.cs` estuvo así. Ante un archivo sospechoso, revisar
  primero su codificación.
- **Guiones en comentarios XML.** La secuencia `--` es ilegal dentro de un comentario
  XML. El original resolvía eso reemplazando *todos* los guiones por `ー` (katakana),
  lo que destruía el texto latino (`re-arm` → `reーarm`). Ahora se escapa únicamente la
  secuencia ilegal; no volver atrás.
- **El `PostBuild` mueve las DLL a un subdirectorio `bin/`.** Por eso `Program.cs`
  resuelve los ensamblados desde `AppContext.BaseDirectory`, y no desde el directorio
  de trabajo: si no, la aplicación se cae al lanzarla desde otra carpeta.
- **`Prefabs.dat` se lee por posición**, no por nombre de campo. Un campo nuevo va
  **al final** y se lee sólo si está (`idx < lines.Length`): así un archivo viejo sigue
  sirviendo. Meterlo en el medio obliga a subir `Version`, y eso descarta el archivo
  entero y le borra la configuración a todo el mundo.
- **El tamaño del `.zip` publicado.** Una vez pasó de 2,8 MB a 17,8 MB porque un paquete
  de test arrastró instrumentación de Linux, macOS y ARM. La CI quedó en verde igual: el
  único síntoma fue el tamaño del artefacto.

## Publicación

`.github/workflows/publish.yml` corre en cada push a `master`: etiqueta la versión,
compila las dos variantes (estándar y portable) y crea la release. `tools.py` es la
herramienta auxiliar que usa para normalizar codificaciones, escribir la versión y
armar el texto de la release.
