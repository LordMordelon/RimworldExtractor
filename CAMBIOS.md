# Qué cambia respecto del original

Este repositorio deriva de [csh1668/RimworldExtractor](https://github.com/csh1668/RimworldExtractor),
que está en coreano, y hoy es independiente de él.

Este documento agrupa los cambios por tema, no commit por commit, y explica **por qué** está cada
uno. El [README](README.md) cuenta qué hace la herramienta; [AGENTS.md](AGENTS.md) es lo que hay
que saber antes de tocar el código.

---

## Independiente de upstream

Al principio la regla era **no editar nunca un `.Designer.cs`**, para poder seguir mezclando con
upstream sin conflictos: toda la traducción y la maquetación se aplicaban en tiempo de ejecución,
desde el `ApplyStrings()` de cada formulario.

Se abandonó cuando quedó claro que no se usaba: upstream dejó de publicar y el fork siguió solo.
Los arreglos de allá se traen a mano, mirando su commit, y la maquetación pasa a declararse en el
`.Designer.cs` con `TableLayoutPanel`. El motivo completo está en [AGENTS.md](AGENTS.md), para que la
regla no vuelva sola.

De esa época quedan dos reglas que se mantienen porque valen por sí mismas: ningún texto visible va
escrito en el código —todo pasa por `Strings.cs`— y ningún color va cableado.

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
medidos para textos en coreano. El español ocupa bastante más. Son provisorios: se van cuando todos
los formularios estén maquetados con `TableLayoutPanel`.

Se quitó el empaquetador de imágenes (`FormImageFileCombiner`), que pegaba un `.zip` al final de un
`.jpg` para subir archivos a foros coreanos que solo aceptan imágenes. En su lugar de la ventana
principal está «Actualizar todo RML».

---

## Traducción rápida

El agregado más grande. Permite actualizar la traducción de un mod sin volver a traducirla: se
re-extrae contra la versión instalada y se cruza con lo que ya estaba hecho.

- **`TranslationMerge.cs`** — el cruce. Compara por identidad `(clase, nodo)`, y para lo que queda
  sin traducir hace un segundo pase que rescata por texto original y campo. Ese segundo pase
  recuperó 7644 traducciones en una sola corrida: mods que movieron un nodo de lugar sin cambiarle
  el texto.
- **`ActualizacionRml.cs`** — escribe el resultado sobre la carpeta del mod en RML.
- **`ActualizacionPorLotes.cs`** — hace lo mismo con todos los mods de RML de una vez, desde el
  botón «Actualizar todo RML». Es lo que hace falta después de una actualización del juego.
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

`Agrupador.cs` mantiene la agrupación de `Data/` por autor. Los mods de un mismo autor con cuatro
traducciones o más viven en `Data/!Autor/`, y eso se hizo una vez a mano: después se desactualizaba
solo, porque cada mod nuevo caía plano en `Data/` y nadie se enteraba hasta revisar a mano.

Ahora un mod nuevo cae directo en la carpeta de su autor si ese autor ya tiene una, y las
traducciones sueltas de ese autor se acomodan solas en la siguiente traducción rápida. Cuando un
autor recién llega a cuatro y todavía no tiene carpeta, se avisa por log y no se mueve nada: crear
una carpeta cambia la forma de `Data/` y es una decisión, no un acomodo. Alcanza con crearla vacía.

El autor sale del `<author>` del `About.xml` del mod instalado, no del `packageId`. No son lo mismo:
los mods de Oskar Potocki usan al menos cuatro prefijos distintos —`VanillaExpanded`, `OskarPotocki`,
`vanillaracesexpanded`, `VE`— así que agrupar por prefijo lo partiría en cuatro personas. Y el nombre
se normaliza antes de comparar, porque «Oskar Potocki» y «OskarPotocki» son la misma firmando
distinto, y los colaborativos que se firman «Oskar Potocki, Taranchuk» van con el primero.

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
- **`Prefabs.dat` y `log.txt` se buscaban en el directorio de trabajo.** Abrir la aplicación desde
  un acceso directo o desde otra carpeta perdía la configuración y dejaba otro `Prefabs.dat` suelto.
  Ahora viven junto al ejecutable.
- **El límite de espera del builder de RML no actuaba**: la salida se leía hasta el final antes de
  empezar a esperar, así que un builder colgado dejaba la ventana colgada para siempre.

---

## Publicación

`.github/workflows/publish.yml` etiqueta la versión a partir de los mensajes de commit, compila las
dos variantes —portable y estándar— y crea la release. Un commit que empiece con `feat:` sube el
número del medio.

El Portable se publica comprimido: pesa 78 MB en vez de 184.
