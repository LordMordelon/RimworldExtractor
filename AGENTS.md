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

Requiere el **SDK de .NET 10**. La GUI apunta a `net10.0-windows`; `Internal` y los tests,
a `net10.0`, porque no dependen de WinForms.

En `RimworldExtractorGUI/` quedó además un `RimworldExtractorGUI - Backup.csproj` que
apunta a `net7.0-windows`. No está en la solución y no se compila: es un resto que hay que
ignorar, no la configuración real.

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

**Los `.Designer.cs` se editan como cualquier otro archivo.** Hubo una regla que lo
prohibía, para que los merges con el upstream coreano no dieran conflictos en archivos
generados. Se sacó, y conviene que quede escrito por qué para que no se reinstaure sola:

- Upstream no publica nada desde el 13-jul-2026: **0 commits contra 59 propios**. La
  compatibilidad se estaba pagando y no se estaba usando.
- **El fork es independiente.** Si los coreanos arreglan algo, ese arreglo se trae a mano
  mirando su commit, no por merge.
- La regla costaba **843 líneas** de maquetación en tiempo de ejecución: 428 repartidas en
  los `ApplyStrings()` y 415 en dos ayudantes que reimplementaban a mano lo que
  `TableLayoutPanel` ya hace. La maquetación va en el `.Designer.cs`, declarada.

Lo demás sigue igual, porque nada de esto era una concesión al fork:

- **Ningún texto visible va escrito en el código.** Todo pasa por `Strings.cs`, cuyos
  identificadores están en inglés y sus valores en español.
- **Ningún color va cableado.** El tema se elige desde la ventana principal —claro,
  oscuro o el de Windows— y se guarda en `Prefabs.dat`, así que los colores salen de
  `SystemColors` o del `e.ForeColor` que llega al evento. Las listas de `FormSelectMod`
  se dibujan por código y ya tuvieron este error una vez.
- **`Application.SetColorMode` va antes de crear ventanas, como dice su documentación.**
  Llamarla con la aplicación abierta deja el cambio a medias: el fondo del formulario y
  el panel de log siguen al tema nuevo, pero **los botones conservan el anterior**,
  porque su aspecto queda fijado al crearse su handle. Comprobado renderizando
  `FormMain` antes y después del cambio. Por eso el botón del tema guarda la elección y
  ofrece reiniciar en vez de aplicarla en caliente.
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
  de trabajo: si no, la aplicación se cae al lanzarla desde otra carpeta. `tools.py`
  arma el zip publicado con esa misma disposición.
- **`Main` no puede nombrar nada de `RimworldExtractorInternal`.** Es consecuencia de lo
  anterior y ya rompió la aplicación una vez. El JIT resuelve las referencias de un
  método justo antes de ejecutarlo, así que mencionar `Prefabs` dentro de `Main` intenta
  cargar esa DLL —que no está al lado del ejecutable— **antes** de que la primera línea
  registre el manejador que sabe dónde buscarla. El arranque real vive en `Arrancar()`,
  marcado `[MethodImpl(MethodImplOptions.NoInlining)]` para que el JIT no lo incorpore de
  vuelta. Las `const` no cuentan: se incrustan como literal y no dejan referencia.
  Compila igual y **falla en silencio**, porque al ser `WinExe` no hay consola donde
  aparezca la excepción; el rastro queda en el visor de eventos de Windows.
- **`Prefabs.dat` se lee por posición**, no por nombre de campo. Un campo nuevo va
  **al final** y se lee sólo si está (`idx < lines.Length`): así un archivo viejo sigue
  sirviendo. Meterlo en el medio obliga a subir `Version`, y eso descarta el archivo
  entero y le borra la configuración a todo el mundo.
- **El tamaño del `.zip` publicado.** Una vez pasó de 2,8 MB a 17,8 MB porque un paquete
  de test arrastró instrumentación de Linux, macOS y ARM. La CI quedó en verde igual: el
  único síntoma fue el tamaño del artefacto.

## Escribir sobre RML

`ActualizacionRml.Escribir` **borra y reescribe el árbol entero** del mod, así que lo ya
traducido se conserva solo si la relectura lo encuentra. Ahí es donde se pierde trabajo, y
ya pasó cuatro veces por motivos distintos. Todas comparten la misma forma: algo se
escribió bien, se releyó mal, no falló nada, y el daño solo se vio mirando el diff.

- **Los `Patches` no se releen parseando, se releen evaluando.** `IO.FromLanguageXml` corre
  `ExtractPatches`, o sea que cada `xpath` se evalúa contra `CombinedDefs`. Todo lo que ese
  camino no devuelve se comporta como si no existiera: no entra al cruce y **tampoco queda
  apartado en `UNUSED`**. Hay dos defensas y las dos hacen falta:
  `Extractor.ConLaBaseCompleta`, que repone la base completa —si no, los xpath se evalúan
  contra el documento reducido que dejó el paso de patches—, y `LeerPatchesLiteral`, que
  lee del archivo lo que el xpath no encontró. Ese segundo caso aparece cuando el def lo
  agrega otro mod o vive en una carpeta condicional.

- **`DesarmarXpath` devuelve `null` a propósito** para lo que no puede reconstruir sin
  ambigüedad. Reconstruir mal una clave es peor que no reconstruirla: pone la traducción en
  el campo de al lado, y un `TODO` se ve mientras que una traducción mal puesta no.
  Cuidado con el índice de lista: es 1-based en el xpath (`li[2]`) y 0-based en el nodo.

- **El orden de lectura decide quién gana.** `TranslationMerge.Clave` unifica
  `Patches.ThingDef` con `ThingDef` a propósito, así que un mismo nodo traducido de las dos
  formas colisiona y gana el que se lee último. Por eso los `Patches` se leen **primero** y
  gana `DefInjected`, que es lo que el juego aplica al final. En `ActualizacionRml` el
  `UNUSED` va antes que todo, para que nunca le gane a lo que está en uso.

- **El `UNUSED.xml` se relee.** Si no se releyera duraría una sola extracción: la corrida
  que aparta una traducción la saca de `Languages/`, así que la siguiente no la encuentra,
  no le sobra nada y borra el archivo con todo adentro.

Los tests de `PatchesRoundTripTests`, `UnusedSobreviveTests` y `PatchesDeRmlTests` cubren
estos cuatro casos. Todos se verificaron fallando sin su arreglo: un test de regresión que
pasa igual sin el arreglo no guarda nada, y ya escribí uno así una vez.

## Qué no se extrae

Además de las reglas por etiqueta de `Prefabs`, hay dos exclusiones por contenido:

- **`InteractionDef.symbol`**, en `Prefabs.NoTraducibles`, porque sus valores son rutas de
  textura. Va por `(clase, etiqueta)` y no por etiqueta suelta: los `symbol` de `GeneDef` y
  `RuleDef` sí son palabras.
- **Los defs de `category` `Mote`**, en `ExtractDefsInternal`. Son los iconos que flotan
  sobre el colono y su etiqueta no se muestra nunca. Se mira la categoría y no el
  `ParentName`, porque las cadenas de herencia varían.

Antes de agregar una exclusión conviene medirla sobre RML, que es la muestra real: las dos
salieron de contar cuántas entradas afectaba y de comprobar que no se llevaran nada bueno.

## Publicación

**El número de versión sale de los mensajes de commit.** `mathieudutour/github-tag-action`
sube el último número por defecto; un commit que empiece con `feat:` sube el del medio
(`0.0.35` → `0.1.0`). Es la única convención de este tipo en el repositorio, y se usa sólo
cuando se quiere marcar una versión: el resto de los mensajes van en prosa.

`.github/workflows/publish.yml` corre en cada push a `master`: etiqueta la versión,
compila las dos variantes (estándar y portable) y crea la release. `tools.py` es la
herramienta auxiliar que usa para normalizar codificaciones, escribir la versión y
armar el texto de la release.
