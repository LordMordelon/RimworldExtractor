# RimworldExtractor — Español latino

<img src="RimworldExtractorGUI/Resources/logo.png" width="112" alt="Logo: un planeta con anillo que es a la vez un globo de diálogo">

Herramienta para extraer los datos de traducción del contenido oficial de RimWorld
y de sus mods (`Defs`, `Keyed`, `Strings`, `Patches`), con la interfaz en español.

Deriva de [csh1668/RimworldExtractor](https://github.com/csh1668/RimworldExtractor),
que está en coreano. Se usa junto con [RML](https://github.com/LordMordelon/RML),
el mod que empaqueta las traducciones producidas con esta herramienta.

> **No es un fork de GitHub**, aunque comparta su historia. Se creó clonando y
> empujando a un repositorio nuevo, que es la única manera de tener una copia privada
> de un repositorio público —lo fue al principio, ya no—, y por eso no figura en la red de
> forks del original, pero en esencia lo es. Hoy es independiente: los arreglos de upstream se
> traen a mano, mirando su commit, y no por merge.

**Qué cambia respecto del original:** la interfaz en español, el tema claro/oscuro, la traducción
rápida —actualizar un mod sin volver a traducirlo—, la actualización de todo RML de una vez, la
integración con RML y varios arreglos. Está todo en **[CAMBIOS.md](CAMBIOS.md)**.

## Descargar

En la **[página de Releases](https://github.com/LordMordelon/RimworldExtractor/releases/latest)**
hay dos descargas que hacen exactamente lo mismo:

- **Portable** (`RimworldExtractor-Portable.exe`) — un único archivo, no se instala nada.
  Pesa bastante (~80 MB) porque trae todo lo que necesita adentro. La primera vez que se
  abre tarda unos segundos más, porque se descomprime.
- **Standard** (`RimworldExtractor-Standard.zip`) — hay que descomprimirlo, pesa mucho
  menos y requiere el
  [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/es-es/download/dotnet/10.0).

Si vas a traducir y no a programar, empezá por la
**[guía para traductores](https://github.com/LordMordelon/RML/blob/master/TRADUCIR.md)**.

## Qué cambia respecto del original

- **Interfaz en español.** Todo el texto visible vive en
  [`RimworldExtractorInternal/Strings.cs`](RimworldExtractorInternal/Strings.cs).
- **Idioma de destino por defecto:** `SpanishLatin (Español(Latinoamérica))`.
- **Rutas por defecto** apuntando a `D:\SteamLibrary`.
- **Los guiones ya no se corrompen.** El original reemplazaba cada `-` por `ー`
  (un glifo katakana) para que la secuencia `--` no rompiera los comentarios XML.
  En textos latinos eso destruía el original (`re-arm` → `reーarm`). Ahora se
  escapa únicamente la secuencia ilegal.
- **Fuente de las planillas:** Calibri en lugar de Malgun Gothic, que no existe en
  un Windows en español.
- **Cabeceras de Excel más tolerantes:** se acepta cualquier columna terminada en
  `[Source string]` / `[Translation]`, así que una planilla vieja se puede
  reimportar aunque se haya cambiado el idioma configurado.
- **Chequeo de versión y enlaces apuntando acá**, no al original. Al abrir la
  aplicación se consulta la última release publicada y se muestra abajo a la
  izquierda; si la consulta falla, queda un aviso en el log y nada más.
- **Tema claro, oscuro o el de Windows**, con un botón en la ventana principal. La
  elección se guarda; se aplica al abrir la aplicación, así que cambiarla ofrece
  reiniciar. Cambiar el tema en caliente deja los botones con el anterior.
- **Cuadros de mensaje propios.** `MessageBox` es un diálogo del sistema y no obedece
  al tema: con la interfaz en oscuro seguía saliendo en blanco.
- **Log con formato:** hora, un símbolo por nivel y colores que salen del fondo real,
  en vez de una línea con el nombre del método que la escribió.
- **Modo "Archivo XML para traducir a mano"**, que deja el original en un comentario y
  `TODO` donde falta traducir:

  ```xml
  <!-- EN: Storage -->
  <BuildingsNeatStorage.label>TODO</BuildingsNeatStorage.label>
  ```

- **Traducción rápida**, una casilla al elegir el mod. Cruza la extracción con lo que ya
  está traducido en [RML](https://github.com/LordMordelon/RML) y escribe el resultado ahí:
  conserva lo traducido aunque cambie el texto original —el comentario `EN` se actualiza y
  el cambio se ve en el diff—, deja `TODO` solo en lo nuevo, y lo que el mod ya no tiene
  sale del árbol a un `UNUSED.xml`. Requiere indicar la carpeta de RML en Opciones.
- **Actualizar todo RML**, un botón en la ventana principal. Hace la traducción rápida de
  todos los mods que RML ya tiene, uno detrás de otro, contra la versión instalada. Es lo que
  hace falta después de una actualización del juego. Los mods que no están instalados
  quedan como están.
- **`LoadFolders.Build.yaml` generado solo**, con el `packageId`, el ID del workshop y el
  nombre del mod, que el extractor ya conoce.
- **Maquetación reacomodada.** Los formularios vienen con medidas fijas pensadas para
  el coreano y el español ocupa bastante más. Se están pasando a `TableLayoutPanel`, y
  mientras tanto los que faltan se miden y se acomodan al arrancar. Ver [AGENTS.md](AGENTS.md).
- **Sin el empaquetador de imágenes**, que pegaba un `.zip` dentro de un `.jpg` para
  foros coreanos.

## Compilar

Requiere el **SDK de .NET 10**, que es LTS con soporte hasta noviembre de 2028; es el
mismo SDK que usan las herramientas del repositorio del mod, así que alcanza con uno
solo para todo. La interfaz apunta a `net10.0-windows` y los otros dos proyectos de la
solución a `net10.0`.

```
dotnet build RimworldExtractor.sln -c Debug
```

Para el ejecutable de uso diario:

```
dotnet publish RimworldExtractorGUI/RimworldExtractorGUI.csproj -c Release -r win-x64 ^
  --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true
```

El resultado queda en `RimworldExtractorGUI/bin/Release/net10.0-windows/win-x64/publish/`.
`Prefabs.dat` (la configuración) y `log.txt` se guardan al lado del ejecutable.

## Publicar

Cada push a `master` etiqueta una versión nueva y publica una release, con las dos
variantes de descarga. Antes de tocar el código conviene leer
**[AGENTS.md](AGENTS.md)**: están las reglas del proyecto y las trampas que ya costaron
tiempo una vez.

`origin` es este repositorio y `upstream` el original, sin push. Sirve para mirar qué
cambió allá: lo que valga la pena se trae a mano.

## Créditos

- [csh1668/RimworldExtractor](https://github.com/csh1668/RimworldExtractor) — proyecto original.
- [Han-ju/AlphaExtractor](https://github.com/Han-ju/AlphaExtractor) — el extractor que lo precedió.

Licencia MIT; ver [LICENSE.txt](LICENSE.txt).
