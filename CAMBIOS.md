# Qué cambia respecto del original

Este repositorio deriva de [csh1668/RimworldExtractor](https://github.com/csh1668/RimworldExtractor),
que está en coreano. Son 53 commits y 13 archivos nuevos.

Este documento agrupa los cambios por tema, no commit por commit, y explica **por qué** está cada
uno. El [README](README.md) cuenta qué hace la herramienta; [AGENTS.md](AGENTS.md) es lo que hay
que saber antes de tocar el código.

---

## La regla que ordena todo lo demás

**Nunca se edita un `.Designer.cs`.** Toda la traducción y todos los retoques de maquetación se
aplican en tiempo de ejecución, desde el `ApplyStrings()` de cada formulario.

Es lo que permite seguir mezclando con upstream, que sigue desarrollándose en coreano: los archivos
generados por el diseñador quedan intactos, así que no dan conflictos. El remoto `upstream` sigue
configurado para eso.

De ahí salen las dos consecuencias que se ven en todo el código: ningún texto visible va escrito en
el código —todo pasa por `Strings.cs`— y ningún color va cableado.

---

## Interfaz

| | |
|---|---|
| `Strings.cs` | Todo el texto visible en un solo lugar, con identificadores en inglés y valores en español |
| `Utils/AutoAjuste.cs` | Mide cada control con su fuente real y lo ensancha si su texto no entra |
| `Utils/Rejilla.cs` | Reparte espacio donde los anclajes de WinForms no alcanzan |
| `Utils/Tema.cs` | Tema claro, oscuro o el de Windows, guardado en `Prefabs.dat` |
| `Utils/Aviso.cs` | Reemplaza a `MessageBox` en los 20 cuadros de mensaje |

Las dos últimas existen por motivos concretos. `MessageBox` es un diálogo del propio Windows y no
obedece al tema: con la interfaz en oscuro salía en blanco. Y `Application.SetColorMode` tiene que
llamarse **antes** de crear ventanas —llamarla con la aplicación abierta deja los botones con el
tema anterior—, así que el botón del tema guarda la elección y ofrece reiniciar.

`AutoAjuste` y `Rejilla` hacen falta porque los formularios vienen con posición y tamaño absolutos,
medidos para textos en coreano. El español ocupa bastante más.

---

## Traducción rápida

El agregado más grande. Permite actualizar la traducción de un mod sin volver a traducirla: se
re-extrae contra la versión instalada y se cruza con lo que ya estaba hecho.

- **`TranslationMerge.cs`** — el cruce. Compara por identidad `(clase, nodo)`, y para lo que queda
  sin traducir hace un segundo pase que rescata por texto original y campo. Ese segundo pase
  recuperó 7644 traducciones en una sola corrida: mods que movieron un nodo de lugar sin cambiarle
  el texto.
- **`ActualizacionRml.cs`** — escribe el resultado sobre la carpeta del mod en RML.
- **`ActualizacionPorLotes.cs`** — hace lo mismo con todos los mods de RML de una vez.
- **Formato «XML para traducir a mano»** — deja el original en un comentario y `TODO` donde falta
  traducir, en vez de dejar el inglés como valor. Un `TODO` sin terminar se ve; el inglés copiado
  pasa por traducción y nadie lo revisa.

---

## Integración con RML

`LoadFoldersBuild.cs` genera el `LoadFolders.Build.yaml` que RML necesita para enganchar cada
traducción a su mod, regenera el índice al terminar, y encuentra la carpeta del mod aunque esté
agrupada en subcarpetas.

Los tres datos de ese archivo —el `packageId`, el id del workshop y el nombre— ya los tiene el
extractor. Escribirlos a mano solo agrega una oportunidad de equivocarse, y equivocarse ahí no rompe
nada visible: la traducción simplemente no se carga nunca.

---

## Diagnósticos

Dos avisos que no existían, los dos por fallas que terminaban en menos texto extraído sin que nada
lo dijera:

- **`PatchesSinObjetivo.cs`** — informa cuando un patch no encontró a qué apuntar, y distingue «el
  def no está cargado» de «está pero le falta el nodo interno».
- **`TraduccionPropia.cs`** — avisa si el mod ya trae su propia traducción al idioma de destino.

Y la opción **«Extracción completa»**, que carga el contenido oficial y los mods relacionados como
referencia. Sin eso, un mod que le cambia el nombre a algo del juego base extraía cero.

---

## Arreglos sobre el original

- **La aplicación no arrancaba** y fallaba en silencio. `Main` no puede nombrar nada de
  `RimworldExtractorInternal`: el JIT resuelve las referencias del método antes de ejecutarlo, así
  que mencionar `Prefabs` ahí intenta cargar esa DLL antes de que se registre el manejador que sabe
  dónde buscarla. El arranque real vive en `Arrancar()`, marcado `NoInlining`.
- **Los guiones en los comentarios XML.** La secuencia `--` es ilegal dentro de un comentario, y el
  original lo resolvía reemplazando *todos* los guiones por `ー` (katakana), lo que destruía el
  texto latino: `re-arm` quedaba `reーarm`. Ahora se escapa únicamente la secuencia ilegal.
- **Los Patches se separan por mod parcheado**, un archivo por cada uno con su nombre, en vez de
  quedar todos juntos.
- **Un `Console.WriteLine` de depuración** que imprimía cada entrada extraída a stdout.
- **La relectura de los Patches** se hacía contra una base de defs que la propia extracción ya había
  reemplazado, así que la traducción hecha se perdía sin dejar rastro.

---

## Publicación

`.github/workflows/publish.yml` etiqueta la versión a partir de los mensajes de commit, compila las
dos variantes —portable y estándar— y crea la release. Un commit que empiece con `feat:` sube el
número del medio.
