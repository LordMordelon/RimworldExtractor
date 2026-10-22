# Plan: lo que queda después de nombrar los comps por su clase

El 21-oct-2026 el extractor pasó a nombrar los comps como el juego (`comps.CompCamouflage.x` en vez de
`comps.11.x`). Ver el commit `bbe4434` y la viñeta de `AGENTS.md` en «Escribir sobre RML». Al revisar
la reextracción de RML aparecieron tres cosas más, que quedaron para otras sesiones.

## Próxima sesión (21-oct-2026)

**Objetivo:** que los parches condicionados a Combat Extended funcionen con la versión testing de CE 1.7 y
no se rompan al reextraer.

**Estado:** rama `master` en los dos repositorios, **sin subir**. Extractor: `bbe4434`. RML: `691e6a7`,
`1346339`, `c99f619` y `105f577`. Angel tiene que probar en el juego la carta del nóctulo antes de subir.

**Antes de empezarla (Angel):**
1. Abrir RimWorld y mirar que la carta del nóctulo o del jaeger camuflado salga en español.
2. Si sale bien, decir «sube los commits» al empezar la sesión (o en esta misma, antes).

**Al empezar (Claude):**
1. Si Angel lo aprobó, subir primero los commits pendientes (extractor y RML, cada uno en su orden).
2. **Tarea 1: la condición de Combat Extended.** Angel usa la versión testing no oficial de CE 1.7,
   con packageId `CETeam.CombatExtended.17UnofficialVer` y nombre «Combat Extended» (Workshop 3822862833).
   Al reextraer, el extractor reescribe la condición de los parches de `FindMod` «Combat Extended» a
   `MayRequire="CETeam.CombatExtended"`, que con esa versión no se cumple, y los parches dejan de
   aplicarse. Hoy RML tiene 13 archivos con `<li>Combat Extended</li>`, en 12 mods, y ninguno con el
   `MayRequire`: el 21-oct se dejaron como estaban en git. Pasa también sin el cambio de comps.
   - Mirar por qué el packageId no se traduce a un nombre: `ModLister.TryGetModMetadataByPackageId` y
     los commits `82a67fa` y `018b5f5` (`MayRequire` cuando el mod no tiene nombre conocido).
   - Prueba primero: un patch con `IfModActive="CETeam.CombatExtended"` y el mod instalado como
     `CETeam.CombatExtended.17UnofficialVer` tiene que salir con `FindMod` «Combat Extended».
   - `CombatExtendedDevuelveSuMunicionGenerica` sale omitida por la misma causa: busca el packageId oficial.
   - Cabe en una sesión.
3. **Tarea 2 (otra sesión): las herramientas por su etiqueta.** El juego nombra cada herramienta por su
   etiqueta (`tools.fist.label`, así está la traducción oficial) y el extractor la nombra por posición
   (`tools.0.label`). RML tiene 1814 claves así en 73 mods, más 1459 `xpath` con `tools/li[N]`. Es la
   causa del cruce cañón↔culata de Apex Mechanoids. Se hace igual que los comps: `Extractor.Nombres`,
   `NodoAnterior` para recuperar lo traducido y la cuarta comprobación del diff. Es grande: pide plan y
   aprobación, y la reextracción va en una sesión aparte.
4. **Tarea 3 (chica): el «fusil gauss» de Coilguns.** En Vanilla Weapons Expanded - Coilguns,
   `VWE_Gun_GaussRifle.verbs.Verb_Shoot-0.label` (inglés «low power shot») dice «fusil gauss», y lo mismo
   en las otras armas gauss. Parece un error viejo; la regla es tocar solo los `TODO`, así que va con el
   visto bueno de Angel. Se puede hacer al final de la tarea 1.

**Decisiones de Angel:**
- Tarea 1: si el extractor tiene que reconocer la versión testing por su nombre (recomendado: sí, la
  condición por nombre funciona con las dos versiones de CE) o si se agrega su packageId a mano.
- Tarea 3: si se corrige «fusil gauss» por «disparo de baja potencia» (recomendado: sí).

**Guías:** `traspaso` al terminar cada una. No hay tareas de GitHub abiertas para esto.

**Dónde leer:** `AGENTS.md` («Escribir sobre RML», «Publicación»), `../RML/AGENTS.md` («Antes de
commitear: mirar el diff»), y la memoria `extractor-comps-por-posicion`.
