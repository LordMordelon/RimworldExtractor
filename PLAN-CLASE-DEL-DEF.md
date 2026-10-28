# Plan: nombrar cada def por su etiqueta, no por su `Class`

El 28-oct-2026 una extracción de Zoology dejó en el log cinco líneas así:

```
'/Defs/ZoologyMod.LifeStagePenetrationDef, ZoologyMod[defName="AnimalBabyTiny"]/label' has an invalid token.
```

No es del autor: esa dirección la escribe el extractor. Investigándola apareció que la misma causa
rompe otras cuatro cosas, una de ellas visible para quien juega Custom Weapons Expanded sin Combat
Extended.

## La causa

El extractor nombra cada def por su atributo `Class` si lo tiene, y si no por la etiqueta:
`node.Attributes?["Class"]?.Value ?? node.Name`. Está en seis lugares: `Extractor.cs:400` y
`PatchOperations.cs:151`, `198`, `233`, `238` y `301`.

El juego hace lo contrario. `DirectXmlLoader.DefFromNode` busca el tipo con
`GenTypes.GetTypeInAnyAssembly(node.Name)`, o sea por la etiqueta; el `Class` solo dice qué subclase
instanciar. Y `DefDatabase<T>.AddAllInMods` mete cada def con `OfType<T>`, así que un
`<ThingDef Class="VEF.Weapons.LaserBeamDef">` queda en la base de `ThingDef` y también en la de
`LaserBeamDef`. Por eso una carpeta `DefInjected/ThingDef` siempre lo encuentra. En cambio, una carpeta
`DefInjected/VEF.Weapons.LaserBeamDef` solo lo encuentra si ese tipo existe: si es de un mod que no
está, `LoadedLanguage` la descarta entera («doesn't correspond to any def type»).

Lo mismo para `Patches`: el `xpath` recorre el XML, donde el def se llama por su etiqueta. Con el `Class`,
`/Defs/CombatExtended.CustomWeaponTraitDef[...]` no encuentra nada, y con una coma, como el de
Zoology, ni siquiera es un xpath válido.

Hoy esto se tapa con una lista escrita a mano, `Prefabs.NodeReplacement` (`CombatExtended.AmmoDef` →
`ThingDef`, `AlienRace.ThingDef_AlienRace` → `ThingDef` y otras 20). Cubre los casos conocidos, no los
nuevos, y como se guarda en `Prefabs.dat`, una clase que se agrega no le llega a una copia ya instalada.

## Lo que rompe hoy en RML

Contado contra los mods instalados en `D:\SteamLibrary` y contra `RML/Data`:

1. **Zoology (5 entradas).** Su parche de CE agrega `<LifeStagePenetrationDef Class="ZoologyMod.LifeStagePenetrationDef, ZoologyMod">`.
   El xpath de RML no es válido. En 1.6, con el CE oficial, el juego lanza la excepción dentro de
   `PatchOperationSequence`, que no la atrapa: sale un error en rojo en el log y se cae el bloque entero.
   Ese bloque de `Patches/kl4gbm.xml` solo tiene esas cinco etiquetas, que son internas. Con el CE no
   oficial de 1.7 no pasa nada, porque ni Zoology ni RML cargan esa parte (`IfModActive="CETeam.CombatExtended"`).
2. **Rasgos de armas con CE (6 xpath que no encuentran nada, en silencio).** CE y CWE le cambian el
   `Class` a sus `WeaponTraitDef` con un `PatchOperationAttributeSet`. Están en `Combat Extended - 2890901044`
   (`ExtendedMagazine`, `GrenadeLauncher`) y `Custom Weapons Expanded - 3560528949` (cuatro).
3. **La traducción oficial pisa a la de CE y el extractor no se entera.** `ExtendedMagazine` y
   `GrenadeLauncher` son rasgos de Odyssey. Su traducción oficial dice «Esta arma dispara un 50% más de
   tiros en una sola ráfaga», que es el texto sin CE. RML tiene el correcto para CE («El cargador de esta
   arma admite un 50% más de munición»), pero `IO.PatchesQueElJuegoPisa` busca la clave oficial con
   `CombatExtended.CustomWeaponTraitDef` en vez de `WeaponTraitDef`, no la encuentra y no la pasa a
   `DefInjected`. Aunque el xpath estuviera bien, en el juego seguiría ganando el texto oficial.
4. **Custom Weapons Expanded sin CE (13 entradas en inglés).** Se extrajo con CE cargado, así que sus
   rasgos quedaron en `DefInjected/CombatExtended.CustomWeaponTraitDef/`. Cinco de ellos son del mod
   base (Bioquemador, lanzabengalas, lanzaespuma, escopeta y pulso eléctrico). Quien juega CWE sin CE,
   que es opcional, no tiene ese tipo, y el juego descarta la carpeta: esos rasgos salen en inglés. Las
   otras 4 entradas son de rasgos que solo existen con CE.
5. **La clave depende de qué mods se cargaron al extraer.** El mismo `CWE_Trait_BioBurner` es
   `WeaponTraitDef` sin CE y `CombatExtended.CustomWeaponTraitDef` con CE. Si algún día se extrae sin
   CE, las traducciones no cruzan y van a `UNUSED`.

Además hay 47 entradas en 7 carpetas `DefInjected/VEF.Weapons.LaserBeamDef/`. Hoy funcionan, porque
VEF siempre está cuando esos mods están, pero con el arreglo pasan a `ThingDef`.

## El arreglo

1. **Un solo lugar decide la clase de un def:** un `Extractor.ClaseDelDef(XmlNode def)` que devuelve la
   etiqueta (con la primera letra en mayúscula, como hoy). Los seis lugares lo llaman.
2. **Recuperar lo ya traducido con la clase vieja.** Las claves de RML cambian de clase en los casos de
   arriba, y sin un puente irían a `UNUSED` y volverían a `TODO`. Se hace como con `NodoAnterior` en los
   comps: la entrada guarda su `ClaseAnterior` (el `Class`, si difiere de la etiqueta) y
   `TranslationMerge` la prueba cuando no encuentra la clave nueva. El xpath viejo con coma ya se relee
   (`DesarmarXpath`, test `ReleeUnaClaseConEnsamblado`).
3. **`NodeReplacement`** sigue igual. Sus reemplazos de clase entera dejan de hacer falta, pero
   sacarlos obliga a tocar `Prefabs.dat`, y no molestan. Los de nodo (`BackstoryDef+baseDesc`) siguen
   sirviendo.

Con eso los cinco problemas se arreglan solos: el xpath sale con `/Defs/LifeStagePenetrationDef[...]` y
`/Defs/WeaponTraitDef[...]`; `PatchesQueElJuegoPisa` encuentra la clave oficial y pasa los dos rasgos de
Odyssey a `DefInjected/WeaponTraitDef` dentro de la carpeta de CE, que solo se carga con CE; y CWE
escribe `DefInjected/WeaponTraitDef`, que el juego encuentra con CE y sin él.

## Pruebas primero

Cada una tiene que fallar antes del arreglo (en `UnitTest1.cs`, una clase nueva `ClaseDelDefTests`):

1. Un parche que agrega un def con `Class="Mod.Tipo, Mod"`: el xpath escrito es `/Defs/Tipo[...]` y
   `SelectNodes` lo evalúa sin excepción y encuentra el nodo.
2. Un `PatchOperationAttributeSet` que le pone `Class` a un `WeaponTraitDef`: la entrada sale como
   `Patches.WeaponTraitDef`.
3. Un def `<ThingDef Class="VEF.Weapons.LaserBeamDef">` en `Defs`: sale como `ThingDef`.
4. Ida y vuelta: un RML con la traducción en `DefInjected/VEF.Weapons.LaserBeamDef/` y otro con el
   xpath viejo con coma. Después de `ActualizacionRml.Escribir`, cada traducción queda en su lugar nuevo,
   igual, y sin nada en `UNUSED`.
5. `PatchesQueElJuegoPisa` con una clave oficial `WeaponTraitDef` + `ExtendedMagazine.description` y
   el def con su `Class` cambiado por parche: se reencamina.

Los tests de `UnPatchSobreUnaClaseReemplazadaSaleConLaClaseDelElemento`,
`UnPatchViejoConLaClaseReemplazadaSeReleeComoElElemento` y `DesarmarXpathTests` tienen que seguir en verde.

## Reextraer RML

Con `RimworldExtractorLotes`, solo las carpetas afectadas:

- `Zoology Realistic Animal Overhaul - 3679396881`
- `Combat Extended - 2890901044`
- `Custom Weapons Expanded - 3560528949`
- `!Oskar Potocki/`: Vanilla Factions Expanded - Pirates, Vanilla Weapons Expanded - Laser y - Non-Lethal
- `!Sarg Bjornson/`: Alpha Armoury y Alpha Mechs
- `!Smxrez/`: Laser Weapons Re-Examined y Non-Lethal Re-Examined

Revisión de cada una antes de commitear: las tres comprobaciones del diff de `RML/AGENTS.md`, y además
contar las traducciones antes y después (las de `DefInjected` por clave y las de `Patches` por xpath +
valor), como se hizo con Zoology el 28-oct. Tiene que dar el mismo número, sin `TODO` nuevos y sin
`UNUSED.xml` que crezca. Un commit por mod.

## Tamaño y orden

Es **grande**: cambia la identidad de las claves y mueve traducciones en 10 carpetas de RML. **Angel lo
aprobó el 28-oct-2026.**

- Sesión 1: pruebas, arreglo y `AGENTS.md` del extractor (una viñeta en «Escribir sobre RML»). Commit en
  el extractor, sin subir.
- Sesión 2 (o la misma, si alcanza): reextraer las 10 carpetas y revisarlas.
- Después: Angel prueba en el juego con CE (el texto del cargador extendido) y CWE sin CE (el nombre del
  bioquemador). Recién ahí se suben los dos repositorios.

## Fuera de este plan

- La reextracción de Zoology del 28-oct (`eb7dc049` en RML) se revisó y está bien: 383 traducciones de
  `Patches` antes y después, 0 `TODO`, y la única diferencia es que los comps de las prótesis pasaron a
  ubicarse por su clase.
- Que los `MayRequire="CETeam.CombatExtended"` no se cumplan con el CE no oficial de 1.7 es la tarea 1
  de [PLAN-POSICIONES.md](PLAN-POSICIONES.md), no esta.
