# Plan: limpieza después de la 1.0

La 1.0 sacó la planilla de Excel, el analizador de traducciones y los otros formatos de
extracción. Quedaron código, opciones y documentación de esos flujos. Este plan los
recorre en etapas, cada una con su commit y su verificación. Lo pidió Angel el 20-oct-2026.

Las cifras salen de una auditoría del 20-oct-2026 (extractor en `c161cd0`): los analizadores de
código sin uso (`IDE0051`, `IDE0052`, `IDE0060`, `IDE0005`) y un recuento de los nombres públicos
que no se mencionan en ningún otro archivo.

## Cómo se trabaja

- **Una etapa, un commit o pocos**, en `master`. Un push a `master` publica una versión, así
  que se sube al final de cada sesión y no después de cada commit.
- **Verificación de cada etapa:** compila en Release, las 92 pruebas en verde, y una
  reextracción de control con `RimworldExtractorLotes` sobre tres mods de RML (uno con `Patches`,
  uno con `Strings` y Combat Extended). El diff de RML tiene que salir vacío: limpiar no puede
  cambiar ninguna traducción.
- **Si toca una ventana,** se renderiza en tema claro y oscuro con `DrawToBitmap` antes de
  dar la etapa por buena (ver «Verificar la interfaz» en `AGENTS.md`).
- **`Prefabs.dat` se lee por posición.** Una opción que se saca de la interfaz **no** se saca del
  archivo: su renglón se sigue leyendo y escribiendo, y solo se deja de usar. Sacarla del medio
  obligaría a subir `Version` y le borraría la configuración a todo el mundo.

## Etapas

| # | Etapa | Tamaño | Estado |
|---|---|---|---|
| 0 | Que la documentación no publique versiones | chica | hecha (`391d881`) |
| 1 | Código sin uso confirmado | media | hecha (`613e424`) |
| 2 | Opciones que quedaron de los flujos viejos | media | hecha (`bf37447`) |
| 3 | Documentación del extractor | media | hecha (ver «Sesiones») |
| 4 | La guía para colaboradores de RML | grande | pendiente, con decisión |
| 5 | Ordenar las pruebas | media | opcional |

Fuera de este plan, a propósito: pasar las tres ventanas que faltan a `TableLayoutPanel` y
borrar `AutoAjuste` y `Rejilla` (ver «Maquetación» en `AGENTS.md`). Es un proyecto propio, de
varias sesiones, y mezclarlo con la limpieza haría imposible revisar el diff.

### 0. Que la documentación no publique versiones

Hoy cualquier push a `master` publica una versión, también uno que solo cambia un `.md`: este
plan, por ejemplo, saldría como la 1.0.3 sin ningún cambio para el usuario. Agregar
`paths-ignore` en `publish.yml` para `**.md` y `docs/`. Se hace primero porque las etapas 3 y 4
son casi solo documentación.

### 1. Código sin uso confirmado

Lo que nadie llama, según el compilador y el recuento. Antes de borrar cada uno se busca su
nombre en todo el repositorio, incluidas las pruebas y los `.Designer.cs`.

- **Miembros privados:** `IO.SinNombreEnNomatch`.
- **Parámetros que no se usan:** `nombreDeArchivos` en `FormMain.cs:232` y `requiredMods` en
  `PatchOperations.cs:369`.
- **Públicos que nadie nombra (19):**
  - 10 de `RequiredMods`: `AddDisallowedByModName`, `AddDisallowedByPackageId`,
    `AddDisallowedByPackageIds`, `ContainsAllowed`, `ContainsDisallowed`,
    `FromStringByPackageIds`, `AddByPackageId`, `AddRangeByPackageIds`, `MatchesByPackageId` y
    `FromModMetadata`.
  - `TranslationEntry.TryGetExtension` y `HasRequiredMods`.
  - `Log.Messages`, `Log.Separator` y `Log.WrnOnce`.
  - `ModLister.ModRootsAll`, `GithubVersionCheker.DiscussionUrl` y `Rejilla.ColumnaALaDerecha`.
  - `XmlCommentStyle.Original`, que no usa nadie.
- **Textos de `Strings.cs` que no se muestran nunca (4):** `PackageIdInsteadOfModName`,
  `FileInUse`, `LabelSelectLanguagesRoot` y `UnsupportedPatchOperation`.
- **`Prefabs.PatternVersionWithV`:** se guarda y se lee pero no se usa. Se deja de usar en el
  código, pero su renglón de `Prefabs.dat` se queda (ver «Cómo se trabaja»).
- **60 `using` sobrantes** en 38 archivos.
- **Tres comentarios de documentación mal formados**: `LoadFoldersBuild.cs:163` (un `summary`
  sin cerrar) y el parámetro `SourceFile` de `TranslationEntry`.

Las 7 clases de `Compats/` parecen sin uso, pero no lo están: `CompatManager` las carga por
reflexión. No se tocan.

Después de borrar, se repite la auditoría: sacar algo puede dejar sin uso a lo que solo usaba
eso. Se para cuando no aparece nada nuevo.

### 2. Opciones que quedaron de los flujos viejos

Estas opciones siguen en la ventana de Opciones o en el código, y no está claro que sirvan en la
1.0, donde casi todo se escribe sobre RML con la traducción rápida. Primero se averigua qué hace
cada una hoy y en qué camino; después Angel decide si se queda.

| Opción | Dónde se usa | Qué hay que averiguar |
|---|---|---|
| `Policy` (qué hacer con un archivo que ya existe) | Opciones, `IO`, `ActualizacionRml` | Escribir sobre RML la fuerza a «sobrescribir»: si solo cuenta en «Extraer en carpeta», sobra en Opciones |
| `StopCallbackXml` / `StopCallbackTxt` y la ventana `FormStopCallback` | `FormMain`, `IO` | Si alguien la usa todavía; era para revisar archivo por archivo al escribir |
| `PathBaseRefList` | Opciones, `FormSelectMod` | Si la lista de referencias base sigue teniendo sentido con «Extracción completa» |
| `EnableTkey` | Opciones, `Extractor.DefsUtils` | Qué es y si lo usa RML |
| `FullListTranslationTags` | Opciones, `IO` | Si se usa fuera de la planilla |

Si una sale, sale de la interfaz y del código, y su renglón de `Prefabs.dat` se queda.

**Lo averiguado (20-oct-2026).** El `Prefabs.dat` del extractor de uso diario tiene `EnableTkey`
en `False`, `PathBaseRefList` vacío, `Policy` en `Overwrite` y `FullListTranslationTags` en el
valor por defecto: ninguna está fuera de lo que viene de fábrica.

| Opción | Qué hace hoy | Recomendación |
|---|---|---|
| `Policy` + `StopCallback*` + `FormStopCallback` | Solo cuenta al «Extraer en carpeta» sobre una carpeta que ya tiene archivos: sobrescribir, dejar el viejo o preguntar archivo por archivo con `FormStopCallback`. Escribir sobre RML la fuerza a sobrescribir | Sacar las tres y sobrescribir siempre. Es una ventana menos que pasar a `TableLayoutPanel` |
| `PathBaseRefList` | Un archivo con mods que `FormSelectMod` marca como referencia al abrirse. Desde la 1.0 las referencias del mod se agregan solas (lo que era «Extracción completa») | Sacarla de Opciones. Los menús «Guardar/Cargar lista» de `FormSelectMod` pueden quedar |
| `EnableTkey` | Prendida, nombra las claves de un nodo con atributo `TKey` como `def.TKey.slateRef` en vez de la ruta completa. Está apagada, y prenderla cambiaría el nombre de claves ya traducidas en RML. Su casilla dice «(provisorio)» | Sacarla, dejándola apagada: no cambia nada de lo que se escribe |
| `FullListTranslationTags` | Se usa en cada escritura sobre RML: las listas `rulesStrings`, `rulesFiles` y `pathList` salen como lista completa en `DefInjected` | Se queda: sacarla cambiaría el formato de las reglas de gramática de RML |

Otras dos cosas que aparecieron al averiguar:

- **Ese `Prefabs.dat` tiene `CurrentVersion` en `1.6`**, y el valor por defecto es `1.7`. En un
  mod con carpetas `1.6/` y `1.7/`, el extractor de uso diario lee la de `1.6/`; la corrida por
  lotes, que toma los valores por defecto, lee la de `1.7/`. Se cambia en Opciones.
- **`TranslationEntry` guarda «extensiones» que nadie lee** (`AddExtension`, que usa
  `Compat_MVCF`). No se sacaron en la etapa 1 a propósito: el diccionario es un campo del
  `record`, y sacarlo cambia cuándo dos entradas son iguales. Hay que mirar antes quién compara
  entradas.

### 3. Documentación del extractor

- **`AGENTS.md`:** dice que el extractor devuelve «XML o planilla de Excel» (línea 11), que `IO`
  lee y escribe planillas (líneas 21 y 30), que hay «ocho formularios» (líneas 22 y 109; hoy son
  cinco) y que todos los tests están en `UnitTest1.cs` (cambia si se hace la etapa 5). La parte
  de betas de «Publicación» se puede acortar ahora que no hay rama de beta.
- **`README.md`:** revisar «Qué cambia respecto del original» y «Compilar» contra la 1.0, y
  sumar `RimworldExtractorLotes`. «Qué se sacó en la 1.0» se queda: es el registro.
- **`CAMBIOS.md`:** revisarlo contra la 1.0, sección por sección.
- **`CLAUDE.md` de la carpeta de trabajo:** su nota sobre `mathieudutour` ya no aplica; el
  `AGENTS.md` del extractor ya explica el reemplazo.

### 4. La guía para colaboradores de RML

`RML/TRADUCIR.md` (364 líneas) explica cómo traducir **con la planilla de Excel**: generar el
`.xlsx`, llenarlo, convertirlo con «XLSX → XML» y entregarlo. Nada de eso existe en la 1.0, así
que hoy un colaborador que la siga no puede trabajar. Lo mismo, en corto, en `RML/README.md`
(líneas 84 y 85) y en `RML/AGENTS.md` (línea 96).

Hay que reescribirla para el camino de la 1.0: el colaborador traduce sobre los XML,
reemplazando los `TODO` (la guía ya tiene ese modo como secundario, en su sección 10b). Las
secciones 4, 5, 8, 9 y 11 son solo de la planilla. Hace
falta la decisión de Angel de abajo.

### 5. Ordenar las pruebas (opcional)

`UnitTest1.cs` tiene 2.836 líneas y 18 clases. Pasarlas a un archivo por tema (`Patches`,
`RML`, `Actualizador`…) no cambia ninguna prueba y hace más fácil encontrarlas. Solo se mueven
clases enteras: el diff tiene que ser mover, no editar.

## Decisiones de Angel

**Plan aprobado el 20-oct-2026**, para hacerlo con la skill `traspaso-continuo`.

1. **Guía de colaboradores (etapa 4): reescribirla para traducir sobre los XML** (Angel,
   20-oct-2026), con un editor de texto (Notepad++ o VS Code), y avisar al principio que quien
   prefiera la planilla puede bajar la 0.1.30 desde la página de releases.
2. **Opciones de la etapa 2** (Angel, 20-oct-2026, las cuatro como se recomendó): salen `Policy`
   con `StopCallbackXml`, `StopCallbackTxt` y `FormStopCallback` (se sobrescribe siempre),
   `PathBaseRefList` y `EnableTkey` (queda apagada). `FullListTranslationTags` se queda.
3. **Las 7 releases beta (`1.0.0-beta.1` a `.7`) de GitHub:** se dejan (recomendación; Angel no
   pidió borrarlas). No le aparecen a nadie como actualización y borrarlas no se puede deshacer.
4. **Etapa 5:** se hace, al final (recomendación).
5. **Subir:** nada durante la cadena. Los commits se acumulan y Angel aprueba la subida una vez.

## Sesiones (estimación)

- Sesión 1: etapas 0 y 1, y la 2 averiguada y decidida (hecho).
- Sesión 2: sacar lo decidido de la etapa 2, y etapa 3 (hecho).
- Sesión 3: etapa 4.
- Sesión 4, si se aprueba: etapa 5.

## Próxima sesión (20-oct-2026, después de la sesión 2 de la cadena)

**Objetivo:** etapa 4, la guía para colaboradores de RML, según la decisión 1 de Angel.

**Estado:** etapas 0 a 3 hechas. La 2 se verificó con las 92 pruebas en verde, la reextracción de
control de los tres mods de siempre sin ningún cambio en RML, y la ventana de Ajustes renderizada
en claro y oscuro: sin las opciones que salieron, la sección «Idiomas» quedó sola y la ventana se
achicó a lo que tiene (antes las dos secciones de la izquierda se repartían el alto). La 3 es solo
documentación del extractor (`AGENTS.md`, `README.md`, `CAMBIOS.md`) y del `CLAUDE.md` de la
carpeta de trabajo, que no está en ningún repositorio. Nada subido.

**Dónde está el trabajo:** rama `limpieza` del extractor, en la copia aislada
`RimworldExtractor/.claude/worktrees/limpieza`. `master` local tiene solo la etapa 0 (`391d881`).
La etapa 4 es de RML. En `RML/` hay cambios sin commitear de otra sesión (The Dead Man's Switch,
`ModList.tsv`, `docs/index.html`): no tocarlos ni agregarlos, y no cambiar de rama ahí, que se los
llevaría. Trabajar en una copia aislada nueva, con
`git -C "<ruta de RML>" -c core.longpaths=true worktree add .claude/worktrees/guia -b limpieza-guia master`
(las rutas de `Data/` pasan los 260 caracteres sin `core.longpaths`), y agregar
`.claude/worktrees/` al `.git/info/exclude` de RML, como se hizo en el extractor.

**Al empezar (Claude):**
- Reescribir `RML/TRADUCIR.md` para traducir sobre los XML con un editor de texto (Notepad++ o VS
  Code), con el aviso del principio sobre la 0.1.30 para quien prefiera la planilla. Las secciones
  4, 5, 8, 9 y 11 son solo de la planilla; la 10b ya tiene el modo XML.
- Lo mismo, en corto, en `RML/README.md` (líneas 84 y 85) y `RML/AGENTS.md` (línea 96).
- Para el enlace a la 0.1.30, copiarlo de la página de releases real (`gh api`), no armarlo.

**Cuidado:** un push a `master` de RML publica una release solo si cambia `output/`; la guía no
cambia `output/`, pero igual no se sube nada sin la aprobación de Angel.

**Guías:** `traspaso-continuo` al cerrar.

**Dónde leer:** este plan («4. La guía para colaboradores de RML» y «Decisiones de Angel»);
`RML/TRADUCIR.md`; `RML/GLOSARIO.md` solo si la guía lo cita.
