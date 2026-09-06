# RimworldExtractor — español latino

Herramienta para extraer los datos de traducción del contenido oficial de RimWorld
y de sus mods (`Defs`, `Keyed`, `Strings`, `Patches`), con la interfaz en español.

Fork privado de [csh1668/RimworldExtractor](https://github.com/csh1668/RimworldExtractor),
que está en coreano. Se usa junto con [RML](https://github.com/LordMordelon/RML),
el mod que empaqueta las traducciones producidas con esta herramienta.

## Qué cambia respecto del original

- **Interfaz en español.** Todo el texto visible vive en
  [`RimworldExtractorInternal/Strings.cs`](RimworldExtractorInternal/Strings.cs).
  Los `.Designer.cs` quedan intactos: cada formulario traduce sus controles en
  tiempo de ejecución desde su método `ApplyStrings()`, para que los merges con
  upstream sigan siendo limpios.
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
- **Chequeo de versión y enlaces apuntando a este fork**, no al original. Mientras
  el repositorio siga siendo privado la consulta falla y la aplicación lo avisa en
  la franja del log, sin que eso afecte a nada más.

## Compilar

Requiere el **SDK de .NET 10**. Los tres proyectos apuntan a `net10.0`, que es LTS
con soporte hasta noviembre de 2028; es el mismo SDK que usan las herramientas del
repositorio del mod, así que alcanza con uno solo para todo.

```
dotnet build RimworldExtractor.sln -c Debug
```

Para el ejecutable de uso diario:

```
dotnet publish RimworldExtractorGUI/RimworldExtractorGUI.csproj -c Release -r win-x64 ^
  --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true
```

Hay que nombrar el `.csproj` explícitamente: la carpeta contiene además un
`RimworldExtractorGUI - Backup.csproj` que confunde a MSBuild.

El resultado queda en `RimworldExtractorGUI/bin/Release/net10.0-windows/win-x64/publish/`.

## Sincronizar con upstream

```
git fetch upstream
git merge upstream/master
```

`origin` es el fork privado y `upstream` el repositorio original (sin push).

## Créditos

- [csh1668/RimworldExtractor](https://github.com/csh1668/RimworldExtractor) — proyecto original.
- [Han-ju/AlphaExtractor](https://github.com/Han-ju/AlphaExtractor) — el extractor que lo precedió.

Licencia MIT; ver [LICENSE.txt](LICENSE.txt).
