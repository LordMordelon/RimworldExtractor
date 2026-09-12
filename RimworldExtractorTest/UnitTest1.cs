using System.IO.Compression;
using System.Xml;
using RimworldExtractorInternal;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorTest
{
    /// <summary>
    /// Cubre el reconocimiento del mod a partir del nombre del archivo.
    ///
    /// Importa mas de lo que parece: TRADUCIR.md le pide al traductor que devuelva
    /// la planilla con el nombre exacto que recibio, y el motivo es justamente que
    /// de ahi se deduce a que mod pertenece.
    /// </summary>
    [TestClass]
    public class ModMetadataFromFilePathTests
    {
        /// <summary>
        /// Un mod cualquiera del workshop del equipo donde corren los tests. Se elige
        /// en tiempo de ejecucion en vez de fijarlo: antes estaba cableado uno que no
        /// existia en esta maquina, asi que el test fallaba siempre.
        /// </summary>
        private static ModMetadata? _mod;

        /// <summary>Elige un mod real del workshop antes de correr los tests.</summary>
        [ClassInitialize]
        public static void Setup(TestContext _)
        {
            Prefabs.Init();
            ModLister.ResetCache();
            _mod = ModLister.WorkshopMods.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.ModName));
        }

        /// <summary>Las cuatro formas de nombre deben resolver al mismo mod.</summary>
        [TestMethod]
        public void ReconoceElModPorLasCuatroFormasDeNombre()
        {
            if (_mod is null)
                Assert.Inconclusive(
                    $"No hay mods del workshop en {Prefabs.PathWorkshop}. " +
                    "Este test necesita RimWorld instalado con al menos un mod suscrito.");

            var nombre = _mod.ModName.StripInvaildChars();
            var id = _mod.Id;

            // Las cuatro formas que acepta GetModMetadataFromFilePath: por nombre de
            // archivo y por nombre de la carpeta que lo contiene.
            var rutas = new[]
            {
                $@"Data\{id}\a.xlsx",
                $@"Data\cualquiera\{id}.xlsx",
                $@"Data\{nombre} - {id}\a.xlsx",
                $@"Data\cualquiera\{nombre} - {id}.xlsx"
            };

            foreach (var ruta in rutas)
            {
                var encontrado = TranslationAnalyzerTool.GetModMetadataFromFilePath(ruta);
                Assert.IsNotNull(encontrado, $"No se reconocio el mod desde la ruta: {ruta}");
                Assert.AreEqual(id, encontrado.Id, $"Se reconocio otro mod desde la ruta: {ruta}");
            }
        }

        /// <summary>Un nombre que no corresponde a ningun mod no debe resolver a uno cualquiera.</summary>
        [TestMethod]
        public void DevuelveNullCuandoElNombreNoCorrespondeANingunMod()
        {
            var encontrado = TranslationAnalyzerTool.GetModMetadataFromFilePath(
                @"Data\esto-no-es-un-mod-000000\a.xlsx");
            Assert.IsNull(encontrado);
        }
    }

    /// <summary>
    /// Cubre la configuracion persistida, que se lee por posicion y sin nombres de campo.
    /// </summary>
    [TestClass]
    public class PrefabsTests
    {
        /// <summary>El formato que alimenta la traduccion rapida es el de arranque.</summary>
        [TestMethod]
        public void ElFormatoPorDefectoEsElXmlParaTraducir()
        {
            Prefabs.Init();
            Assert.AreEqual(Prefabs.ExtractionMethod.LanguagesToTranslate, Prefabs.Method);
        }

        /// <summary>
        /// Los campos nuevos van al final del archivo y se leen solo si estan, para que un
        /// Prefabs.dat de una version anterior siga sirviendo en vez de descartarse entero.
        /// </summary>
        [TestMethod]
        public void UnArchivoSinLosCamposNuevosSigueSirviendo()
        {
            var archivo = Path.Combine(Path.GetTempPath(), "prefabs-" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                Prefabs.Init();
                Prefabs.PathRml = "una/ruta/cualquiera/RML";
                Prefabs.Save(archivo);

                // Se recorta el final, como si lo hubiera escrito una version anterior.
                var lineas = File.ReadAllLines(archivo);
                File.WriteAllLines(archivo, lineas.Take(lineas.Length - 2));

                Prefabs.PathRml = "otra cosa";
                Prefabs.Load(archivo);

                Assert.AreEqual(string.Empty, Prefabs.PathRml, "el campo ausente tiene que quedar en su valor por defecto");
            }
            finally
            {
                if (File.Exists(archivo)) File.Delete(archivo);
            }
        }

        /// <summary>La ruta de RML sobrevive el guardado y la lectura.</summary>
        [TestMethod]
        public void LaRutaDeRmlSobreviveElGuardado()
        {
            var archivo = Path.Combine(Path.GetTempPath(), "prefabs-" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                Prefabs.Init();
                Prefabs.PathRml = "una/ruta/cualquiera/RML";
                Prefabs.Save(archivo);

                Prefabs.PathRml = "otra cosa";
                Prefabs.Load(archivo);

                Assert.AreEqual("una/ruta/cualquiera/RML", Prefabs.PathRml);
            }
            finally
            {
                if (File.Exists(archivo)) File.Delete(archivo);
            }
        }
    }

    /// <summary>
    /// Cubre el cruce entre una extraccion nueva y lo que ya estaba traducido.
    ///
    /// Es el corazon de la traduccion rapida: si se equivoca, o se pierde trabajo hecho o
    /// quedan por traducidas cosas que no lo estan.
    /// </summary>
    [TestClass]
    public class TranslationMergeTests
    {
        private static TranslationEntry Entrada(string node, string original, string? traducida = null)
            => new("ThingDef", node, original, traducida, null, null);

        /// <summary>
        /// Lo central: si la clave coincide se conserva la traduccion, aunque el texto en
        /// ingles haya cambiado, y el original queda actualizado al nuevo para que el
        /// cambio se vea en el diff.
        /// </summary>
        [TestMethod]
        public void ConservaLaTraduccionYActualizaElOriginal()
        {
            var (resultado, sinUso, _) = TranslationMerge.Merge(
                new[] { Entrada("Cosa.label", "Storage unit") },
                new[] { Entrada("Cosa.label", "Storage", "Almacenamiento") });

            Assert.AreEqual(1, resultado.Count);
            Assert.AreEqual("Almacenamiento", resultado[0].Translated);
            Assert.AreEqual("Storage unit", resultado[0].Original);
            Assert.AreEqual(0, sinUso.Count);
        }

        /// <summary>Un nodo que no estaba antes queda sin traducir, o sea en TODO.</summary>
        [TestMethod]
        public void DejaSinTraducirLoQueEsNuevo()
        {
            var (resultado, _, _) = TranslationMerge.Merge(
                new[] { Entrada("Nueva.label", "Brand new") },
                new[] { Entrada("Vieja.label", "Old", "Vieja") });

            Assert.AreEqual(1, resultado.Count);
            Assert.IsTrue(string.IsNullOrEmpty(resultado[0].Translated));
        }

        /// <summary>Lo que ya no existe en el mod se aparta para no perderlo.</summary>
        [TestMethod]
        public void ApartaLoQueYaNoExisteEnElMod()
        {
            var (resultado, sinUso, _) = TranslationMerge.Merge(
                new[] { Entrada("Sigue.label", "Still here") },
                new[]
                {
                    Entrada("Sigue.label", "Still here", "Sigue"),
                    Entrada("Ya no.label", "Gone", "Se fue")
                });

            Assert.AreEqual(1, resultado.Count);
            Assert.AreEqual(1, sinUso.Count);
            Assert.AreEqual("Ya no.label", sinUso[0].Node);
            Assert.AreEqual("Se fue", sinUso[0].Translated);
        }

        /// <summary>
        /// Un nodo que desaparecio pero que nunca se habia traducido no va al informe: no
        /// hay nada que rescatar y solo seria ruido.
        /// </summary>
        [TestMethod]
        public void NoInformaLoQueDesaparecioSinTraducir()
        {
            var (_, sinUso, _) = TranslationMerge.Merge(
                Array.Empty<TranslationEntry>(),
                new[] { Entrada("Ya no.label", "Gone") });

            Assert.AreEqual(0, sinUso.Count);
        }

        /// <summary>
        /// Una entrada vieja sin traducir no puede "pisar" a la nueva dejandola por
        /// traducida: tiene que seguir apareciendo como pendiente.
        /// </summary>
        [TestMethod]
        public void NoTomaComoTraduccionUnaEntradaVacia()
        {
            var (resultado, _, _) = TranslationMerge.Merge(
                new[] { Entrada("Cosa.label", "Storage") },
                new[] { Entrada("Cosa.label", "Storage") });

            Assert.IsTrue(string.IsNullOrEmpty(resultado[0].Translated));
        }

        /// <summary>
        /// Una traduccion no cambia de identidad al cambiar de forma de entrega.
        ///
        /// Un def que es de otro mod sale por un PatchOperation y su clase lleva el prefijo
        /// "Patches."; el mismo nodo guardado como DefInjected no lo lleva. Es el caso de las
        /// traducciones importadas del pack viejo, donde todo estaba como DefInjected: sin
        /// esto, la primera actualizacion las mandaria a UNUSED y las volveria a pedir.
        /// </summary>
        [TestMethod]
        public void CruzaUnDefInjectedConElMismoNodoEmitidoComoPatch()
        {
            var comoPatch = new TranslationEntry("Patches.ThingDef", "ChemfuelTank.label", "chemfuel tank", null, null, null);

            var (resultado, sinUso, _) = TranslationMerge.Merge(
                new[] { comoPatch },
                new[] { Entrada("ChemfuelTank.label", "chemfuel tank", "tanque de combustible") });

            Assert.AreEqual(1, resultado.Count);
            Assert.AreEqual("tanque de combustible", resultado[0].Translated);
            // Sigue saliendo como patch: lo que se hereda es el texto, no la forma.
            Assert.AreEqual("Patches.ThingDef", resultado[0].ClassName);
            Assert.AreEqual(0, sinUso.Count);
        }

        /// <summary>El cruce vale en los dos sentidos, no solo al importar.</summary>
        [TestMethod]
        public void CruzaUnPatchConElMismoNodoEmitidoComoDefInjected()
        {
            var previaComoPatch = new TranslationEntry(
                "Patches.ThingDef", "ChemfuelTank.label", "chemfuel tank", "tanque de combustible", null, null);

            var (resultado, sinUso, _) = TranslationMerge.Merge(
                new[] { Entrada("ChemfuelTank.label", "chemfuel tank") },
                new[] { previaComoPatch });

            Assert.AreEqual("tanque de combustible", resultado[0].Translated);
            Assert.AreEqual(0, sinUso.Count);
        }

        /// <summary>
        /// El caso que motivo el segundo pase: el mod dejo de nombrar las partes de un cuerpo
        /// y paso a indexarlas por posicion. La clave cambio, el ingles no, y la traduccion
        /// terminaba en UNUSED mientras la misma frase volvia a salir como TODO.
        /// </summary>
        [TestMethod]
        public void RescataUnaTraduccionCuyoNodoSeMovio()
        {
            var (resultado, sinUso, rescatadas) = TranslationMerge.Merge(
                new[] { Entrada("Bicho.corePart.parts.0.parts.1.customLabel", "mechanical tail") },
                new[] { Entrada("Bicho.corePart.parts.mechanical_tail.customLabel", "mechanical tail", "cola mecánica") });

            Assert.AreEqual("cola mecánica", resultado[0].Translated);
            Assert.AreEqual(1, rescatadas.Count);
            Assert.AreEqual(0, sinUso.Count, "lo que se rescato no puede quedar tambien como sin uso");
        }

        /// <summary>
        /// Con dos traducciones distintas para el mismo ingles no se elige ninguna: no hay
        /// forma de saber cual, y una traduccion mal puesta es peor que un TODO porque nadie
        /// la vuelve a mirar.
        /// </summary>
        [TestMethod]
        public void NoRescataSiElMismoTextoTieneDosTraducciones()
        {
            var (resultado, sinUso, rescatadas) = TranslationMerge.Merge(
                new[] { Entrada("Nuevo.label", "hunter") },
                new[]
                {
                    Entrada("Viejo.label", "hunter", "cazador"),
                    Entrada("Otro.label", "hunter", "cazadora")
                });

            Assert.IsTrue(string.IsNullOrEmpty(resultado[0].Translated));
            Assert.AreEqual(0, rescatadas.Count);
            Assert.AreEqual(2, sinUso.Count);
        }

        /// <summary>
        /// El campo tiene que coincidir. Sin esta guarda, un label sin traducir se llevaria la
        /// traduccion huerfana de un labelFemale: mismo ingles, genero equivocado.
        /// </summary>
        [TestMethod]
        public void NoRescataDeUnCampoDistinto()
        {
            var (resultado, _, rescatadas) = TranslationMerge.Merge(
                new[] { Entrada("Nuevo.label", "hunter") },
                new[] { Entrada("Viejo.labelFemale", "hunter", "cazadora") });

            Assert.IsTrue(string.IsNullOrEmpty(resultado[0].Translated));
            Assert.AreEqual(0, rescatadas.Count);
        }

        /// <summary>
        /// Un original vacio no puede ser clave de busqueda: si lo fuera, todas las entradas
        /// sin ingles cruzarian entre si.
        /// </summary>
        [TestMethod]
        public void NoCruzaPorOriginalVacio()
        {
            var (resultado, _, rescatadas) = TranslationMerge.Merge(
                new[] { Entrada("Nuevo.label", "") },
                new[] { Entrada("Viejo.label", "", "cualquier cosa") });

            Assert.IsTrue(string.IsNullOrEmpty(resultado[0].Translated));
            Assert.AreEqual(0, rescatadas.Count);
        }
    }

    /// <summary>
    /// Cubre el archivo que RML usa para enganchar cada traduccion a su mod.
    ///
    /// Importa porque un valor mal puesto ahi no rompe nada visible: el mod se instala,
    /// el juego arranca y la traduccion simplemente no aparece nunca.
    /// </summary>
    [TestClass]
    public class LoadFoldersBuildTests
    {
        private static ModMetadata Mod(string id = "2890901044", bool oficial = false) =>
            new(@"D:\mods\ce", id, "Combat Extended", "CETeam.CombatExtended", oficial);

        /// <summary>Los tres valores que el constructor de RML necesita para enganchar la carpeta.</summary>
        [TestMethod]
        public void EscribeLosTresDatosQueRmlNecesita()
        {
            var texto = LoadFoldersBuild.Contents(Mod());

            StringAssert.Contains(texto, "PackageID: [\"CETeam.CombatExtended\"]");
            StringAssert.Contains(texto, "WorkshopID: \"2890901044\"");
            StringAssert.Contains(texto, "ModName: \"Combat Extended\"");
        }

        /// <summary>Un mod local no tiene id del workshop, y el campo queda vacio.</summary>
        [TestMethod]
        public void DejaVacioElIdDelWorkshopCuandoNoSeConoce()
        {
            StringAssert.Contains(LoadFoldersBuild.Contents(Mod("???")), "WorkshopID: \"\"");
        }

        /// <summary>La carpeta de destino en RML es la misma que ya nombra el extractor.</summary>
        [TestMethod]
        public void ProponeLaCarpetaConElFormatoDeRml()
        {
            Assert.AreEqual("Combat Extended - 2890901044", LoadFoldersBuild.FolderNameFor(Mod()));
        }

        /// <summary>El contenido oficial se engancha de otra manera en RML.</summary>
        [TestMethod]
        public void NoLoGeneraParaElContenidoOficial()
        {
            var carpeta = CarpetaTemporal();
            try
            {
                Assert.IsFalse(LoadFoldersBuild.Write(Mod(oficial: true), carpeta));
                Assert.IsFalse(File.Exists(Path.Combine(carpeta, LoadFoldersBuild.FileName)));
            }
            finally
            {
                Directory.Delete(carpeta, true);
            }
        }

        /// <summary>
        /// Se rehace en cada extraccion, para que siempre refleje los datos actuales del
        /// mod y no quede uno viejo si el mod cambio de packageId o de nombre.
        /// </summary>
        [TestMethod]
        public void RehaceElArchivoQueYaEstaba()
        {
            var carpeta = CarpetaTemporal();
            var archivo = Path.Combine(carpeta, LoadFoldersBuild.FileName);
            try
            {
                File.WriteAllText(archivo, "de una extraccion anterior");

                Assert.IsTrue(LoadFoldersBuild.Write(Mod(), carpeta));
                StringAssert.Contains(File.ReadAllText(archivo), "CETeam.CombatExtended");
            }
            finally
            {
                Directory.Delete(carpeta, true);
            }
        }

        /// <summary>El archivo queda en la misma carpeta que el arbol Languages generado.</summary>
        [TestMethod]
        public void LoDejaJuntoALaCarpetaDeLaTraduccion()
        {
            var carpeta = CarpetaTemporal();
            try
            {
                Assert.IsTrue(LoadFoldersBuild.Write(Mod(), carpeta));
                StringAssert.Contains(
                    File.ReadAllText(Path.Combine(carpeta, LoadFoldersBuild.FileName)),
                    "CETeam.CombatExtended");
            }
            finally
            {
                Directory.Delete(carpeta, true);
            }
        }

        private static string CarpetaTemporal()
        {
            var ruta = Path.Combine(Path.GetTempPath(), "rimext-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ruta);
            return ruta;
        }
    }

    /// <summary>
    /// Cubre el ida y vuelta de los Patches, que es lo unico de la traduccion rapida que no se
    /// puede releer parseando el archivo.
    ///
    /// Releer un Patches ya traducido corre ExtractPatches, que evalua cada xpath contra la
    /// base de defs global. Cuando esa base no es la completa, los xpath no encuentran nada y
    /// la traduccion hecha se vuelve invisible: sale todo como TODO y ni siquiera queda
    /// apartada en UNUSED. Paso de verdad y no lo detecto nada, porque este ciclo se habia
    /// verificado a mano una sola vez.
    /// </summary>
    [TestClass]
    public class PatchesRoundTripTests
    {
        /// <summary>
        /// Un Patches ya traducido se tiene que poder releer aunque la extraccion haya dejado
        /// CombinedDefs reducido, que es como queda siempre despues de procesar los patches
        /// del mod.
        ///
        /// El montaje replica ese estado a proposito: la base completa tiene el def, la base
        /// "actual" no. Sin el arreglo la lectura devuelve cero entradas y la traduccion se
        /// pierde sin dejar rastro.
        /// </summary>
        [TestMethod]
        public void ReleeUnPatchAunqueLaBaseActualNoTengaElDef()
        {
            Prefabs.Init();
            Prefabs.TranslationLanguage = "SpanishLatin (Español(Latinoamérica))";

            var raiz = Path.Combine(Path.GetTempPath(), "rml-" + Guid.NewGuid().ToString("N"));
            var patches = Path.Combine(raiz, "Patches");
            Directory.CreateDirectory(patches);

            var completos = new XmlDocument();
            completos.LoadXml("""
                              <Defs>
                                <ThingDef>
                                  <defName>CosaDeOtroMod</defName>
                                  <label>widget</label>
                                </ThingDef>
                              </Defs>
                              """);

            File.WriteAllText(Path.Combine(patches, "OtroMod.xml"), """
                                                                   <?xml version="1.0" encoding="utf-8"?>
                                                                   <Patch>
                                                                     <Operation Class="PatchOperationReplace">
                                                                       <success>Always</success>
                                                                       <xpath>/Defs/ThingDef[defName="CosaDeOtroMod"]/label</xpath>
                                                                       <value>
                                                                         <!-- EN: widget -->
                                                                         <label>artilugio</label>
                                                                       </value>
                                                                     </Operation>
                                                                   </Patch>
                                                                   """);

            var previo = Extractor.CombinedDefs;
            try
            {
                Extractor.RegistrarBaseCompleta(completos);

                // Asi queda CombinedDefs despues de extraer: solo lo que tocaron los patches
                // del propio mod, sin el def de afuera al que apunta la traduccion.
                var reducida = new XmlDocument();
                reducida.LoadXml("<Defs />");
                Extractor.CombinedDefs = reducida;

                var leidas = IO.FromLanguageXml(raiz);

                var entrada = leidas.FirstOrDefault(x => x.Node == "CosaDeOtroMod.label");
                Assert.IsNotNull(entrada, "no se leyo la traduccion del patch");
                Assert.AreEqual("artilugio", entrada.Translated);
                Assert.IsTrue(entrada.ClassName.StartsWith("Patches."),
                    $"tenia que venir marcada como patch y vino como {entrada.ClassName}");
            }
            finally
            {
                Extractor.RegistrarBaseCompleta(null);
                Extractor.CombinedDefs = previo;
                if (Directory.Exists(raiz)) Directory.Delete(raiz, true);
            }
        }
    }

    /// <summary>
    /// Cubre que lo apartado en UNUSED.xml sobreviva a la siguiente extraccion.
    ///
    /// Es el mismo tipo de defecto que PatchesRoundTripTests: algo que se escribe bien pero
    /// no se vuelve a leer, y entonces se pierde sin que nada avise. La corrida que aparta una
    /// traduccion la saca de Languages/, asi que si la siguiente no lee el UNUSED.xml no le
    /// sobra nada, y el archivo se borra con todo adentro. Paso de verdad: se llevo unas 460
    /// traducciones de diez mods, y solo se noto por mirar el diff antes de commitear.
    /// </summary>
    [TestClass]
    public class UnusedSobreviveTests
    {
        private const string Idioma = "SpanishLatin (Español(Latinoamérica))";

        private static ModMetadata Mod() =>
            new(@"D:\mods\ce", "2890901044", "Combat Extended", "CETeam.CombatExtended", false);

        /// <summary>Lo que el mod sigue trayendo. La otra entrada ya no existe en el mod.</summary>
        private static List<TranslationEntry> Extraccion(params string[] nodos) =>
            nodos.Select(x => new TranslationEntry("ThingDef", x, Original(x), null, null, null))
                 .ToList();

        private static string Original(string nodo) => nodo == "Sigue.label" ? "widget" : "gizmo";

        /// <summary>
        /// Dos extracciones iguales seguidas. La segunda no tiene por que llevarse lo que
        /// aparto la primera.
        /// </summary>
        [TestMethod]
        public void NoBorraElUnusedEnLaCorridaSiguiente()
        {
            var rml = Montar(out var carpeta);
            try
            {
                // Primera corrida: el mod ya no trae Vieja.label, asi que se aparta.
                ActualizacionRml.Escribir(Mod(), Extraccion("Sigue.label"), rml);

                var unused = Path.Combine(carpeta, "UNUSED.xml");
                Assert.IsTrue(File.Exists(unused), "la primera corrida no aparto nada");
                StringAssert.Contains(File.ReadAllText(unused), "cosa vieja");

                // Segunda corrida, identica. Sin el arreglo, aca el archivo desaparece.
                ActualizacionRml.Escribir(Mod(), Extraccion("Sigue.label"), rml);

                Assert.IsTrue(File.Exists(unused),
                    "la segunda corrida borro el UNUSED.xml y se perdio lo apartado");
                StringAssert.Contains(File.ReadAllText(unused), "cosa vieja");
            }
            finally
            {
                Directory.Delete(rml, true);
            }
        }

        /// <summary>
        /// La otra mitad de releerlo: si el mod devuelve el nodo a su lugar, la traduccion
        /// apartada se recupera sola. Es lo que el comentario de WriteUnused venia prometiendo
        /// sin poder cumplir.
        /// </summary>
        [TestMethod]
        public void RecuperaLoApartadoCuandoElModDevuelveElNodo()
        {
            var rml = Montar(out var carpeta);
            try
            {
                ActualizacionRml.Escribir(Mod(), Extraccion("Sigue.label"), rml);
                Assert.IsTrue(File.Exists(Path.Combine(carpeta, "UNUSED.xml")));

                // El mod vuelve a traer el nodo.
                var resultado = ActualizacionRml.Escribir(
                    Mod(), Extraccion("Sigue.label", "Vieja.label"), rml);

                Assert.AreEqual(2, resultado.Conservadas, "no se recupero la traduccion apartada");
                Assert.AreEqual(0, resultado.Pendientes);
                Assert.IsFalse(File.Exists(Path.Combine(carpeta, "UNUSED.xml")),
                    "ya no quedaba nada apartado y el archivo tenia que irse");
            }
            finally
            {
                Directory.Delete(rml, true);
            }
        }

        /// <summary>
        /// Un RML de mentira con las dos entradas ya traducidas, de las cuales la extraccion
        /// va a traer una sola.
        /// </summary>
        private static string Montar(out string carpetaDelMod)
        {
            Prefabs.Init();
            Prefabs.TranslationLanguage = Idioma;

            var rml = Path.Combine(Path.GetTempPath(), "rml-unused-" + Guid.NewGuid().ToString("N"));
            carpetaDelMod = Path.Combine(rml, "Data", "Combat Extended - 2890901044");

            var defInjected = Path.Combine(carpetaDelMod, "Languages", Idioma, "DefInjected", "ThingDef");
            Directory.CreateDirectory(defInjected);

            File.WriteAllText(Path.Combine(defInjected, "cosas.xml"), """
                <?xml version="1.0" encoding="utf-8"?>
                <LanguageData>
                  <!-- EN: widget -->
                  <Sigue.label>artilugio</Sigue.label>
                  <!-- EN: gizmo -->
                  <Vieja.label>cosa vieja</Vieja.label>
                </LanguageData>
                """);

            return rml;
        }
    }

    /// <summary>
    /// Cubre que los motes no entren a la traduccion.
    ///
    /// Un mote es el icono que flota sobre el colono: su etiqueta no se le muestra nunca al
    /// jugador. Salian igual porque casi ninguno declara etiqueta y la heredan de MoteBase, que
    /// trae "Mote"; eran 248 entradas en 43 mods de RML, todas iguales y todas inutiles.
    ///
    /// Lo que este test cuida es el otro lado: que el filtro no se lleve puesto nada mas. Mira
    /// la categoria del def, no su nombre, justamente para no tocar una cosa de verdad que se
    /// llame parecido.
    /// </summary>
    [TestClass]
    public class MotesTests
    {
        /// <summary>Un def de categoria Mote no aporta ninguna entrada; el de al lado si.</summary>
        [TestMethod]
        public void NoExtraeLosDefsDeCategoriaMote()
        {
            var entradas = Extraer("""
                <Defs>
                  <ThingDef>
                    <defName>UnMote</defName>
                    <label>Mote</label>
                    <category>Mote</category>
                  </ThingDef>
                  <ThingDef>
                    <defName>UnaSilla</defName>
                    <label>silla</label>
                    <category>Building</category>
                  </ThingDef>
                </Defs>
                """);

            Assert.IsFalse(entradas.Any(x => x.Node.StartsWith("UnMote")),
                "el mote no tenia que salir");
            Assert.IsTrue(entradas.Any(x => x.Node == "UnaSilla.label"),
                "el filtro se llevo puesto un def que no era un mote");
        }

        /// <summary>
        /// El nombre no decide nada. Una cosa de verdad que se llame Mote se sigue traduciendo,
        /// y un mote con un nombre cualquiera se sigue salteando.
        /// </summary>
        [TestMethod]
        public void SeGuiaPorLaCategoriaYNoPorElNombre()
        {
            var entradas = Extraer("""
                <Defs>
                  <ThingDef>
                    <defName>MoteadorDeCafe</defName>
                    <label>moteador de café</label>
                    <category>Item</category>
                  </ThingDef>
                  <ThingDef>
                    <defName>ChispaRara</defName>
                    <label>chispa</label>
                    <category>Mote</category>
                  </ThingDef>
                </Defs>
                """);

            Assert.IsTrue(entradas.Any(x => x.Node == "MoteadorDeCafe.label"),
                "se filtro por el nombre en vez de por la categoria");
            Assert.IsFalse(entradas.Any(x => x.Node.StartsWith("ChispaRara")),
                "un mote con nombre cualquiera tambien tiene que quedar afuera");
        }

        /// <summary>
        /// Se arma el CombinedDefs a mano, ya con la herencia resuelta, que es como le llega a
        /// la extraccion.
        /// </summary>
        private static List<TranslationEntry> Extraer(string defs)
        {
            Prefabs.Init();

            var doc = new XmlDocument();
            doc.LoadXml(defs);

            var previo = Extractor.CombinedDefs;
            try
            {
                Extractor.CombinedDefs = doc;
                return Extractor.ExtractDefs().ToList();
            }
            finally
            {
                Extractor.CombinedDefs = previo;
            }
        }
    }

    /// <summary>
    /// Cubre las dos formas en que la relectura de los Patches de RML danaba traducciones.
    ///
    /// Son el mismo tipo de defecto que PatchesRoundTripTests: algo que se escribio bien, se
    /// relee mal, y el dano no se ve en ningun lado hasta que alguien mira el diff.
    /// </summary>
    [TestClass]
    public class PatchesDeRmlTests
    {
        private const string Idioma = "SpanishLatin (Español(Latinoamérica))";

        /// <summary>
        /// Una traduccion cuyo xpath no encuentra su objetivo no se puede perder.
        ///
        /// Pasa cuando el def lo agrega otro mod, o cuando vive en una carpeta condicional que
        /// esta corrida no cargo. Antes la relectura no la veia, asi que no entraba al cruce ni
        /// quedaba apartada en UNUSED: desaparecia. Con Alpha Mechs fueron 39.
        /// </summary>
        [TestMethod]
        public void NoPierdeLaTraduccionCuandoElXpathNoEncuentraSuObjetivo()
        {
            var raiz = Montar("""
                <?xml version="1.0" encoding="utf-8"?>
                <Patch>
                  <Operation Class="PatchOperationReplace">
                    <success>Always</success>
                    <xpath>/Defs/ThingDef[defName="DefDeOtroMod"]/label</xpath>
                    <value>
                      <!-- EN: librarian -->
                      <label>bibliotecario</label>
                    </value>
                  </Operation>
                </Patch>
                """);
            try
            {
                var leidas = ConBaseVacia(() => IO.FromLanguageXml(raiz));

                var entrada = leidas.FirstOrDefault(x => x.Node == "DefDeOtroMod.label");
                Assert.IsNotNull(entrada, "la traduccion se perdio: no la vio la relectura");
                Assert.AreEqual("bibliotecario", entrada.Translated);
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// El indice de una lista es 1-based en el xpath y 0-based en el nodo. Desarmarlo mal
        /// deja la traduccion en el campo de al lado, que es peor que perderla: un TODO se ve y
        /// una traduccion mal puesta no.
        /// </summary>
        [TestMethod]
        public void DesarmaElIndiceDeListaSinCorrerlo()
        {
            var raiz = Montar("""
                <?xml version="1.0" encoding="utf-8"?>
                <Patch>
                  <Operation Class="PatchOperationReplace">
                    <success>Always</success>
                    <xpath>/Defs/ThingDef[defName="Cuchillo"]/tools/li[2]/label</xpath>
                    <value>
                      <!-- EN: point -->
                      <label>punta</label>
                    </value>
                  </Operation>
                </Patch>
                """);
            try
            {
                var leidas = ConBaseVacia(() => IO.FromLanguageXml(raiz));

                Assert.IsTrue(leidas.Any(x => x.Node == "Cuchillo.tools.1.label"),
                    "li[2] es el indice 1");
                Assert.IsFalse(leidas.Any(x => x.Node == "Cuchillo.tools.2.label"),
                    "el indice quedo corrido un lugar");
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// Cuando el mismo nodo esta traducido en DefInjected y en Patches, gana el de
        /// DefInjected: es el que el juego aplica ultimo y el que en la practica esta al dia.
        /// Antes ganaba el del Patches y daba vuelta traducciones correctas.
        /// </summary>
        [TestMethod]
        public void ElDefInjectedLeGanaAlPatchesCuandoSeContradicen()
        {
            var raiz = Montar("""
                <?xml version="1.0" encoding="utf-8"?>
                <Patch>
                  <Operation Class="PatchOperationReplace">
                    <success>Always</success>
                    <xpath>/Defs/ThingDef[defName="Cuchillo"]/tools/li[2]/label</xpath>
                    <value>
                      <!-- EN: edge -->
                      <label>filo</label>
                    </value>
                  </Operation>
                </Patch>
                """);

            var defInjected = Path.Combine(raiz, "Languages", Idioma, "DefInjected", "ThingDef");
            Directory.CreateDirectory(defInjected);
            File.WriteAllText(Path.Combine(defInjected, "cosas.xml"), """
                <?xml version="1.0" encoding="utf-8"?>
                <LanguageData>
                  <!-- EN: point -->
                  <Cuchillo.tools.1.label>punta</Cuchillo.tools.1.label>
                </LanguageData>
                """);
            try
            {
                // Con el def cargado, para que el xpath del Patches encuentre su objetivo: es el
                // caso real, donde las dos traducciones llegan al cruce y una tiene que ganar.
                var leidas = ConLaBase("""
                    <Defs>
                      <ThingDef>
                        <defName>Cuchillo</defName>
                        <tools>
                          <li><label>handle</label></li>
                          <li><label>point</label></li>
                        </tools>
                      </ThingDef>
                    </Defs>
                    """, () => IO.FromLanguageXml(raiz));

                Assert.IsTrue(leidas.Any(x => x.Node == "Cuchillo.tools.1.label"
                                              && x.ClassName.StartsWith("Patches.")),
                    "el montaje no sirve: el xpath del Patches no encontro su objetivo");

                var extraccion = new List<TranslationEntry>
                {
                    new("ThingDef", "Cuchillo.tools.1.label", "point", null, null, null)
                };
                var (resultado, _, _) = TranslationMerge.Merge(extraccion, leidas);

                Assert.AreEqual("punta", resultado.Single().Translated,
                    "gano la traduccion del Patches, que es la que habia quedado vieja");
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// Corre algo con una base de defs vacia, que es el estado en el que los xpath de RML no
        /// encuentran nada. Es justo el caso que se perdia.
        /// </summary>
        private static T ConBaseVacia<T>(Func<T> hacer) => ConLaBase("<Defs />", hacer);

        /// <summary>Corre algo con la base de defs que se le pase.</summary>
        private static T ConLaBase<T>(string defs, Func<T> hacer)
        {
            var previo = Extractor.CombinedDefs;
            var doc = new XmlDocument();
            doc.LoadXml(defs);
            try
            {
                Extractor.CombinedDefs = doc;
                return hacer();
            }
            finally
            {
                Extractor.CombinedDefs = previo;
            }
        }

        /// <summary>Un mod de mentira con solo la carpeta Patches escrita.</summary>
        private static string Montar(string patch)
        {
            Prefabs.Init();
            Prefabs.TranslationLanguage = Idioma;

            var raiz = Path.Combine(Path.GetTempPath(), "rml-patches-" + Guid.NewGuid().ToString("N"));
            var patches = Path.Combine(raiz, "Patches");
            Directory.CreateDirectory(patches);
            File.WriteAllText(Path.Combine(patches, "OtroMod.xml"), patch);
            return raiz;
        }
    }

    /// <summary>
    /// Cubre que no se le pida al traductor traducir la nada.
    ///
    /// Hay mods que traen claves vacias en su propio archivo en ingles, del estilo
    /// &lt;Defaults_EmptyString&gt;&lt;/Defaults_EmptyString&gt;, y de ahi salian entradas con el
    /// comentario "EN:" vacio y un TODO al lado.
    ///
    /// Lo que este test cuida es el otro lado: que el filtro mire tambien la traduccion. Con el
    /// original vacio igual puede haber trabajo hecho, y es justamente el que pone texto donde
    /// el mod no muestra nada.
    /// </summary>
    [TestClass]
    public class EntradasVaciasTests
    {
        private const string Idioma = "SpanishLatin (Español(Latinoamérica))";

        /// <summary>Sin original y sin traduccion no hay nada que escribir.</summary>
        [TestMethod]
        public void NoEscribeLaEntradaSinOriginalNiTraduccion()
        {
            var escrito = Escribir(
                new TranslationEntry("Keyed", "Defaults_EmptyString", "", null, null, null),
                new TranslationEntry("Keyed", "OtraClave", "real text", null, null, null));

            StringAssert.Contains(escrito, "OtraClave", "se llevo puesta una entrada con original");
            Assert.IsFalse(escrito.Contains("Defaults_EmptyString"),
                "se escribio una entrada sin nada que traducir");
        }

        /// <summary>
        /// Con el original vacio pero traducida, se escribe. En RML son 16 de Simple Sidearms
        /// que ponen texto donde el mod no muestra nada; filtrar solo por original vacio las
        /// sacaria del juego.
        /// </summary>
        [TestMethod]
        public void ConservaLaTraduccionAunqueElOriginalEsteVacio()
        {
            var escrito = Escribir(
                new TranslationEntry("Keyed", "Preset1_label", "", "Solo equipamiento", null, null));

            StringAssert.Contains(escrito, "Preset1_label",
                "se perdio una traduccion hecha por tener el original vacio");
            StringAssert.Contains(escrito, "Solo equipamiento");
        }

        /// <summary>Escribe las entradas y devuelve todo el XML generado, junto.</summary>
        private static string Escribir(params TranslationEntry[] entradas)
        {
            Prefabs.Init();
            Prefabs.TranslationLanguage = Idioma;

            var raiz = Path.Combine(Path.GetTempPath(), "vacias-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(raiz);
            try
            {
                var politica = Prefabs.Policy;
                Prefabs.Policy = Prefabs.DuplicatesPolicy.Overwrite;
                try
                {
                    IO.ToLanguageXml(entradas.ToList(), false, XmlCommentStyle.TranslationTemplate,
                        "UnMod", raiz);
                }
                finally
                {
                    Prefabs.Policy = politica;
                }

                return string.Join("\n", Directory
                    .GetFiles(raiz, "*.xml", SearchOption.AllDirectories)
                    .Select(File.ReadAllText));
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }
    }

    /// <summary>
    /// Cubre que una traduccion que el juego ya trae no salga como patch.
    ///
    /// RimWorld aplica las PatchOperation antes de inyectar los DefInjected, asi que sobre un
    /// def de Core o de un DLC que la traduccion oficial ya cubre, el patch se aplica y se pisa
    /// un paso despues: en pantalla queda el texto oficial y nada avisa. Paso de verdad con
    /// LargeChemfuelTank de Odyssey, y se vio recien cuando alguien fue a mirar por que la
    /// traduccion "no se aplicaba".
    /// </summary>
    [TestClass]
    public class TraduccionOficialPisaTests
    {
        private const string Idioma = "SpanishLatin (Español(Latinoamérica))";

        /// <summary>La tabla y el registro de defs oficiales son estaticos: se vacian entre tests.</summary>
        [TestCleanup]
        public void Limpiar()
        {
            TraduccionOficial.Limpiar();
            Extractor.DefsDeContenidoOficial.Clear();
        }

        /// <summary>
        /// El caso que disparo todo: un patch sobre un def de Odyssey cuyo label el juego ya
        /// traduce. Tiene que salir como DefInjected, que es lo unico que le gana.
        /// </summary>
        [TestMethod]
        public void SobreUnDefOficialYaTraducidoSaleComoDefInjected()
        {
            Extractor.DefsDeContenidoOficial.Add("LargeChemfuelTank");
            TraduccionOficial.Sembrar(new[] { ("ThingDef", "LargeChemfuelTank.label", 0) });

            var (defInjected, patches) = Escribir(
                new TranslationEntry("Patches.ThingDef", "LargeChemfuelTank.label",
                    "large astrofuel tank", "tanque grande de astrobustible", null, null));

            StringAssert.Contains(defInjected, "tanque grande de astrobustible",
                "la traduccion tenia que salir como DefInjected");
            Assert.AreEqual("", patches, "quedo un patch que el juego va a pisar");
        }

        /// <summary>
        /// Sin clave oficial no hay nada que pise el patch, y un patch es lo que corresponde:
        /// el def no es del mod que se extrae, asi que no se puede tocar de otra forma.
        /// </summary>
        [TestMethod]
        public void SinClaveOficialSigueSiendoPatch()
        {
            Extractor.DefsDeContenidoOficial.Add("LargeChemfuelTank");
            TraduccionOficial.Sembrar(new[] { ("ThingDef", "OtraCosa.label", 0) });

            var (defInjected, patches) = Escribir(
                new TranslationEntry("Patches.ThingDef", "LargeChemfuelTank.label",
                    "large astrofuel tank", "tanque grande de astrobustible", null, null));

            StringAssert.Contains(patches, "tanque grande de astrobustible");
            Assert.AreEqual("", defInjected, "se convirtio una traduccion que nadie pisaba");
        }

        /// <summary>
        /// Un def de otro mod no se convierte aunque la clave exista: el orden entre dos mods
        /// lo decide el jugador, asi que ahi la regla deja de valer.
        /// </summary>
        [TestMethod]
        public void SobreUnDefDeOtroModSigueSiendoPatch()
        {
            TraduccionOficial.Sembrar(new[] { ("ThingDef", "CosaDeUnMod.label", 0) });

            var (defInjected, patches) = Escribir(
                new TranslationEntry("Patches.ThingDef", "CosaDeUnMod.label",
                    "widget", "artilugio", null, null));

            StringAssert.Contains(patches, "artilugio");
            Assert.AreEqual("", defInjected);
        }

        /// <summary>
        /// Un PatchOperationFindMod es condicional y un DefInjected no puede serlo: convertirlo
        /// aplicaria la traduccion de un texto que sin ese mod no existe. Se queda como patch,
        /// pero avisado.
        /// </summary>
        [TestMethod]
        public void ConRequiredModsSigueSiendoPatch()
        {
            Extractor.DefsDeContenidoOficial.Add("LargeChemfuelTank");
            TraduccionOficial.Sembrar(new[] { ("ThingDef", "LargeChemfuelTank.label", 0) });

            var requiere = new RequiredMods();
            requiere.AddAllowedByPackageId("algun.mod");

            var (defInjected, patches) = Escribir(
                new TranslationEntry("Patches.ThingDef", "LargeChemfuelTank.label",
                    "large astrofuel tank", "tanque grande de astrobustible", requiere, null));

            StringAssert.Contains(patches, "tanque grande de astrobustible");
            Assert.AreEqual("", defInjected, "se convirtio una traduccion condicional");
        }

        /// <summary>
        /// Una lista se inyecta entera, asi que solo se convierte si estan todos sus elementos.
        /// </summary>
        [TestMethod]
        public void UnaListaCompletaSaleComoDefInjected()
        {
            Extractor.DefsDeContenidoOficial.Add("MechanoidSignal");
            TraduccionOficial.Sembrar(new[]
                { ("QuestScriptDef", "MechanoidSignal.questDescriptionRules.rulesStrings", 2) });

            var (defInjected, patches) = Escribir(
                Lista("MechanoidSignal.questDescriptionRules.rulesStrings.0", "uno"),
                Lista("MechanoidSignal.questDescriptionRules.rulesStrings.1", "dos"));

            StringAssert.Contains(defInjected, "uno");
            StringAssert.Contains(defInjected, "dos");
            Assert.AreEqual("", patches);
        }

        /// <summary>
        /// Con la lista incompleta, emitirla la romperia entera: RimWorld avisa por conteo y no
        /// aplica ni los elementos que si estan. Conviene mas el patch, que al menos es lo que
        /// habia. Es el caso real de Intro_Wimp en Vanilla Factions Expanded - Deserters.
        /// </summary>
        [TestMethod]
        public void UnaListaIncompletaSigueSiendoPatch()
        {
            Extractor.DefsDeContenidoOficial.Add("MechanoidSignal");
            TraduccionOficial.Sembrar(new[]
                { ("QuestScriptDef", "MechanoidSignal.questDescriptionRules.rulesStrings", 3) });

            var (defInjected, patches) = Escribir(
                Lista("MechanoidSignal.questDescriptionRules.rulesStrings.0", "uno"),
                Lista("MechanoidSignal.questDescriptionRules.rulesStrings.1", "dos"));

            StringAssert.Contains(patches, "uno");
            Assert.AreEqual("", defInjected, "se emitio una lista incompleta");
        }

        /// <summary>
        /// Lo convertido se tiene que poder releer, o la proxima actualizacion lo pierde: la
        /// extraccion nueva lo vuelve a traer como Patches.ThingDef y tiene que cruzarse con lo
        /// que quedo escrito como ThingDef. Es el mismo defecto que cubre PatchesRoundTripTests:
        /// se escribe bien, se relee mal, no falla nada.
        /// </summary>
        [TestMethod]
        public void LoConvertidoSeCruzaConLaExtraccionSiguiente()
        {
            var escrita = new TranslationEntry("ThingDef", "LargeChemfuelTank.label",
                "large astrofuel tank", "tanque grande de astrobustible", null, null);
            var nueva = new TranslationEntry("Patches.ThingDef", "LargeChemfuelTank.label",
                "large astrofuel tank", null, null, null);

            var (resultado, sinUso, _) = TranslationMerge.Merge(new[] { nueva }, new[] { escrita });

            Assert.AreEqual("tanque grande de astrobustible", resultado.Single().Translated,
                "la traduccion convertida no se reengancho con la extraccion nueva");
            Assert.AreEqual(0, sinUso.Count, "quedo como huerfana");
        }

        /// <summary>
        /// Que la tabla salga de verdad de los .tar del juego, que es como el juego distribuye
        /// cada idioma. Sin esto lo unico probado seria la regla, con la tabla sembrada a mano,
        /// y el lector podria no leer nada sin que ningun test se entere.
        /// </summary>
        [TestMethod]
        public void LeeLasClavesDeLosTarDelJuego()
        {
            Prefabs.Init();
            Prefabs.TranslationLanguage = Idioma;

            var odyssey = Path.Combine(Prefabs.PathRimworld, "Data", "Odyssey");
            if (!Directory.Exists(odyssey))
                Assert.Inconclusive(
                    $"No esta el DLC Odyssey en {odyssey}. Este test necesita RimWorld instalado.");

            Assert.IsTrue(TraduccionOficial.Cubre("ThingDef", "LargeChemfuelTank.label"),
                "no se leyeron las traducciones oficiales de Odyssey");
            Assert.AreEqual(3, TraduccionOficial.CantidadDeLista(
                "QuestScriptDef", "MechanoidSignal.questDescriptionRules.rulesStrings"));
            Assert.IsFalse(TraduccionOficial.Cubre("ThingDef", "NoExisteEsteDef.label"));
        }

        private static TranslationEntry Lista(string nodo, string traducido) =>
            new("Patches.QuestScriptDef", nodo, "en", traducido, null, null);

        /// <summary>
        /// Escribe las entradas y devuelve, por separado, todo el XML que quedo bajo
        /// DefInjected/ y todo el que quedo bajo Patches/.
        /// </summary>
        private static (string DefInjected, string Patches) Escribir(params TranslationEntry[] entradas)
        {
            Prefabs.Init();
            Prefabs.TranslationLanguage = Idioma;

            var raiz = Path.Combine(Path.GetTempPath(), "oficial-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(raiz);
            try
            {
                var politica = Prefabs.Policy;
                Prefabs.Policy = Prefabs.DuplicatesPolicy.Overwrite;
                try
                {
                    IO.ToLanguageXml(entradas.ToList(), false, XmlCommentStyle.TranslationTemplate,
                        "UnMod", raiz);
                }
                finally
                {
                    Prefabs.Policy = politica;
                }

                return (Juntar(Path.Combine(raiz, "Languages", Idioma, "DefInjected")),
                        Juntar(Path.Combine(raiz, "Patches")));
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        private static string Juntar(string carpeta) => Directory.Exists(carpeta)
            ? string.Join("\n", Directory.GetFiles(carpeta, "*.xml", SearchOption.AllDirectories)
                .Select(File.ReadAllText))
            : "";
    }

    /// <summary>
    /// Cubre que un Patches ya traducido se vuelva a leer aunque su xpath no resuelva.
    ///
    /// IO.LeerPatchesLiteral es la red de seguridad para eso, y se apoya en DesarmarXpath: lo que
    /// no puede reconstruir queda afuera, no entra al cruce, no queda apartado en UNUSED y vuelve
    /// a TODO sin que nada avise. Dos formas que el propio extractor genera no se invertian, y
    /// eso se llevo 21 traducciones de tres mods en una sola corrida de «Actualizar todo RML».
    /// </summary>
    [TestClass]
    public class DesarmarXpathTests
    {
        private const string Idioma = "SpanishLatin (Español(Latinoamérica))";

        /// <summary>
        /// El caso de Zoology: la clase viene con el ensamblado detras de una coma. GetXpath la
        /// escribe asi y la lectura tiene que aceptarla.
        /// </summary>
        [TestMethod]
        public void ReleeUnaClaseConEnsamblado()
        {
            var leida = LeerPatchSuelto(
                "/Defs/ZoologyMod.LifeStagePenetrationDef, ZoologyMod[defName=\"AnimalBabyTiny\"]/label",
                "label", "factores de PA de cría animal diminuta");

            Assert.IsNotNull(leida, "se perdio la traduccion de un xpath con clase con ensamblado");
            Assert.AreEqual("AnimalBabyTiny.label", leida.Node);
            Assert.AreEqual("Patches.ZoologyMod.LifeStagePenetrationDef, ZoologyMod", leida.ClassName);
            Assert.AreEqual("factores de PA de cría animal diminuta", leida.Translated);
        }

        /// <summary>
        /// El caso de EvolvedOrgansRedux y The Dead Man's Switch: el predicado que GetXpath arma
        /// para un TranslationHandle. Adentro esta el handle, que es el token del nodo, asi que
        /// la vuelta es exacta. Ademas el predicado trae ".//", que obliga a partir la ruta
        /// respetando los corchetes en vez de cortar por cada '/'.
        /// </summary>
        [TestMethod]
        public void ReleeUnPredicadoDeTranslationHandle()
        {
            var leida = LeerPatchSuelto(
                "/Defs/ThingDef[defName=\"DMS_Apparel_MissilePod\"]/verbs/*[.//*[contains(text(), 'Verb_ShootCE')]]/label",
                "label", "lanzar misiles");

            Assert.IsNotNull(leida, "se perdio la traduccion de un xpath con predicado");
            Assert.AreEqual("DMS_Apparel_MissilePod.verbs.Verb_ShootCE.label", leida.Node);
            Assert.AreEqual("lanzar misiles", leida.Translated);
        }

        /// <summary>
        /// La vuelta tiene que dar exactamente lo que GetXpath habia generado, o la traduccion
        /// releida se engancharia con un nodo que no le corresponde.
        /// </summary>
        [TestMethod]
        public void LaVueltaCoincideConGetXpath()
        {
            foreach (var (clase, nodo) in new[]
                     {
                         ("ThingDef", "DMS_Apparel_MissilePod.verbs.Verb_ShootCE.label"),
                         ("ThingDef", "Algo.comps.2.label"),
                         ("ZoologyMod.LifeStagePenetrationDef, ZoologyMod", "AnimalBabyTiny.label"),
                         ("HediffDef", "EVOR_X.comps.0.verbs.Verb_ShootCE.label")
                     })
            {
                var leida = LeerPatchSuelto(Utils.GetXpath(clase, nodo), nodo.Split('.').Last(), "x");
                Assert.IsNotNull(leida, $"no se pudo desarmar el xpath de {clase} {nodo}");
                Assert.AreEqual(nodo, leida.Node, $"la vuelta de {clase} {nodo} dio otra cosa");
            }
        }

        /// <summary>
        /// Lo que no salio de GetXpath sigue devolviendo null. Reconstruir mal una clave es peor
        /// que no reconstruirla: un TODO se ve, una traduccion en el campo de al lado no.
        /// </summary>
        [TestMethod]
        public void SigueDescartandoLoQueNoPuedeReconstruir()
        {
            Assert.IsNull(LeerPatchSuelto(
                "/Defs/ThingDef[defName=\"Algo\"]/comps/li[@Class=\"CompProperties_Refuelable\"]/fuelLabel",
                "fuelLabel", "combustible"));

            Assert.IsNull(LeerPatchSuelto("/Defs/ThingDef/label", "label", "algo"));
        }

        /// <summary>
        /// Escribe un Patches con una sola operacion y lo vuelve a leer como lo hace RML, con la
        /// base de defs vacia: asi ningun xpath resuelve y lo unico que puede rescatarla es la
        /// lectura literal.
        /// </summary>
        private static TranslationEntry? LeerPatchSuelto(string xpath, string etiqueta, string traduccion)
        {
            Prefabs.Init();
            Prefabs.TranslationLanguage = Idioma;

            var raiz = Path.Combine(Path.GetTempPath(), "xp-" + Guid.NewGuid().ToString("N"));
            var patches = Path.Combine(raiz, "Patches");
            Directory.CreateDirectory(patches);

            File.WriteAllText(Path.Combine(patches, "OtroMod.xml"), $"""
                <?xml version="1.0" encoding="utf-8"?>
                <Patch>
                  <Operation Class="PatchOperationReplace">
                    <success>Always</success>
                    <xpath>{System.Security.SecurityElement.Escape(xpath)}</xpath>
                    <value>
                      <{etiqueta}>{traduccion}</{etiqueta}>
                    </value>
                  </Operation>
                </Patch>
                """);

            var previo = Extractor.CombinedDefs;
            try
            {
                var vacia = new XmlDocument();
                vacia.LoadXml("<Defs />");
                Extractor.RegistrarBaseCompleta(vacia);
                Extractor.CombinedDefs = vacia;

                return IO.FromLanguageXml(raiz).FirstOrDefault(x => x.Translated == traduccion);
            }
            finally
            {
                Extractor.RegistrarBaseCompleta(null);
                Extractor.CombinedDefs = previo;
                if (Directory.Exists(raiz)) Directory.Delete(raiz, true);
            }
        }
    }

    /// <summary>
    /// Cubre el reemplazo de archivos de la actualizacion.
    ///
    /// Lo que se protege es que una actualizacion nunca deje la instalacion a medias. El
    /// procedimiento se apoya en que Windows deja renombrar un archivo en uso aunque no deje
    /// sobrescribirlo, asi que toca el ejecutable y las DLL que la aplicacion esta usando en
    /// ese momento: si falla a mitad y no vuelve atras, no queda nada que abrir.
    /// </summary>
    [TestClass]
    public class ActualizadorTests
    {
        /// <summary>El caso normal: lo nuevo queda en su lugar y lo viejo corrido a ".viejo".</summary>
        [TestMethod]
        public void ReemplazaElEjecutableYLasDll()
        {
            var (instalacion, paquete) = Montar();
            try
            {
                var reemplazos = Actualizador.Reemplazos(paquete,
                    Actualizador.Variante.Standard, "RimworldExtractorGUI.exe");

                Actualizador.Reemplazar(instalacion, reemplazos);

                Assert.AreEqual("exe nuevo", File.ReadAllText(Path.Combine(instalacion, "RimworldExtractorGUI.exe")));
                Assert.AreEqual("dll nueva", File.ReadAllText(Path.Combine(instalacion, "bin", "Interna.dll")));
                Assert.AreEqual("exe viejo", File.ReadAllText(Path.Combine(instalacion, "RimworldExtractorGUI.exe.viejo")));
                Assert.AreEqual("dll vieja", File.ReadAllText(Path.Combine(instalacion, "bin", "Interna.dll.viejo")));
            }
            finally
            {
                Borrar(instalacion, paquete);
            }
        }

        /// <summary>
        /// El test que importa: si un archivo no se deja mover **despues** de haber reemplazado
        /// otros, la carpeta tiene que quedar exactamente como estaba.
        ///
        /// Se bloquea el .exe y no la DLL a proposito. Los reemplazos van ordenados por ruta,
        /// asi que "bin\Interna.dll" se hace primero: bloqueando el que va segundo, el fallo
        /// encuentra trabajo ya hecho y hay algo real que deshacer. Bloqueando el primero el
        /// test pasa aunque no exista la vuelta atras, y no guardaria nada.
        ///
        /// FileShare.None es justamente lo que Windows NO hace con una imagen cargada —esa se
        /// abre permitiendo el borrado, y por eso el procedimiento funciona en el caso real—.
        /// </summary>
        [TestMethod]
        public void SiAlgoFallaDejaLaCarpetaComoEstaba()
        {
            var (instalacion, paquete) = Montar();
            var bloqueado = Path.Combine(instalacion, "RimworldExtractorGUI.exe");
            try
            {
                var antes = Retrato(instalacion);
                var reemplazos = Actualizador.Reemplazos(paquete,
                    Actualizador.Variante.Standard, "RimworldExtractorGUI.exe");
                Assert.AreEqual(Path.Combine("bin", "Interna.dll"), reemplazos[0].Relativo,
                    "cambio el orden: el archivo bloqueado tiene que ser el segundo");

                using (File.Open(bloqueado, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    Assert.ThrowsExactly<IOException>(
                        () => Actualizador.Reemplazar(instalacion, reemplazos));
                }

                CollectionAssert.AreEqual(antes, Retrato(instalacion),
                    "la actualizacion fallo y dejo la instalacion distinta de como estaba");
            }
            finally
            {
                Borrar(instalacion, paquete);
            }
        }

        /// <summary>El arranque siguiente saca los restos y deja intacto lo que se instalo.</summary>
        [TestMethod]
        public void LimpiarRestosBorraLosViejosYNoTocaLoDemas()
        {
            var (instalacion, paquete) = Montar();
            try
            {
                Actualizador.Reemplazar(instalacion, Actualizador.Reemplazos(paquete,
                    Actualizador.Variante.Standard, "RimworldExtractorGUI.exe"));

                Assert.AreEqual(2, Actualizador.LimpiarRestos(instalacion));

                Assert.AreEqual(0, Directory.GetFiles(instalacion, "*.viejo", SearchOption.AllDirectories).Length);
                Assert.AreEqual("exe nuevo", File.ReadAllText(Path.Combine(instalacion, "RimworldExtractorGUI.exe")));
                Assert.AreEqual("dll nueva", File.ReadAllText(Path.Combine(instalacion, "bin", "Interna.dll")));
            }
            finally
            {
                Borrar(instalacion, paquete);
            }
        }

        /// <summary>Un resto tomado no puede hacer fallar el arranque: se reintenta despues.</summary>
        [TestMethod]
        public void LimpiarRestosNoTiraSiUnoEstaTomado()
        {
            var instalacion = Temporal();
            var resto = Path.Combine(instalacion, "algo.dll.viejo");
            try
            {
                File.WriteAllText(resto, "x");
                using (File.Open(resto, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    Assert.AreEqual(0, Actualizador.LimpiarRestos(instalacion));
                }
                Assert.IsTrue(File.Exists(resto), "se borro un archivo que estaba tomado");
            }
            finally
            {
                Directory.Delete(instalacion, true);
            }
        }

        /// <summary>
        /// El Portable se descomprime en una carpeta temporal antes de arrancar, asi que su
        /// AppContext.BaseDirectory no es donde esta el .exe. En el Standard son la misma.
        /// </summary>
        [TestMethod]
        public void DistingueElPortableDelStandard()
        {
            Assert.AreEqual(Actualizador.Variante.Standard,
                Actualizador.VarianteDe(@"C:\apps\rwe\RimworldExtractorGUI.exe", @"C:\apps\rwe"));
            Assert.AreEqual(Actualizador.Variante.Standard,
                Actualizador.VarianteDe(@"C:\apps\rwe\RimworldExtractorGUI.exe", @"C:\apps\rwe\"));
            Assert.AreEqual(Actualizador.Variante.Portable,
                Actualizador.VarianteDe(@"C:\apps\RimworldExtractor-Portable.exe", @"C:\Temp\.net\abc123"));
        }

        /// <summary>
        /// El Portable conserva el nombre que tenga puesto: quien lo bajo pudo renombrarlo, y
        /// la instalacion es ese archivo y no el nombre con el que se publica.
        /// </summary>
        [TestMethod]
        public void ElPortableReemplazaSuPropioNombre()
        {
            var reemplazos = Actualizador.Reemplazos(@"C:\Temp\descarga.exe",
                Actualizador.Variante.Portable, "extractor viejo.exe");

            Assert.AreEqual(1, reemplazos.Count);
            Assert.AreEqual("extractor viejo.exe", reemplazos[0].Relativo);
        }

        /// <summary>Bajar una pagina de error en vez del archivo es el caso real a atajar.</summary>
        [TestMethod]
        public void RechazaUnPaqueteQueNoSirve()
        {
            var carpeta = Temporal();
            try
            {
                var falso = Path.Combine(carpeta, "falso.exe");
                File.WriteAllText(falso, "<html>404</html>");
                Assert.ThrowsExactly<InvalidDataException>(
                    () => Actualizador.VerificarPaquete(falso, Actualizador.Variante.Portable));

                var vacio = Path.Combine(carpeta, "vacio.zip");
                File.WriteAllBytes(vacio, Array.Empty<byte>());
                Assert.ThrowsExactly<InvalidDataException>(
                    () => Actualizador.VerificarPaquete(vacio, Actualizador.Variante.Standard));

                // Un zip valido pero sin la aplicacion adentro: descomprimirlo encima no
                // actualizaria nada y dejaria archivos sueltos.
                var incompleto = Path.Combine(carpeta, "incompleto.zip");
                using (var zip = ZipFile.Open(incompleto, ZipArchiveMode.Create))
                    zip.CreateEntry("LEEME.txt");
                Assert.ThrowsExactly<InvalidDataException>(
                    () => Actualizador.VerificarPaquete(incompleto, Actualizador.Variante.Standard));

                var bueno = Path.Combine(carpeta, "bueno.zip");
                using (var zip = ZipFile.Open(bueno, ZipArchiveMode.Create))
                    zip.CreateEntry("RimworldExtractorGUI.exe");
                Actualizador.VerificarPaquete(bueno, Actualizador.Variante.Standard);
            }
            finally
            {
                Directory.Delete(carpeta, true);
            }
        }

        /// <summary>Una instalacion de mentira y el paquete que la reemplaza.</summary>
        private static (string Instalacion, string Paquete) Montar()
        {
            var instalacion = Temporal();
            Directory.CreateDirectory(Path.Combine(instalacion, "bin"));
            File.WriteAllText(Path.Combine(instalacion, "RimworldExtractorGUI.exe"), "exe viejo");
            File.WriteAllText(Path.Combine(instalacion, "bin", "Interna.dll"), "dll vieja");

            var paquete = Temporal();
            Directory.CreateDirectory(Path.Combine(paquete, "bin"));
            File.WriteAllText(Path.Combine(paquete, "RimworldExtractorGUI.exe"), "exe nuevo");
            File.WriteAllText(Path.Combine(paquete, "bin", "Interna.dll"), "dll nueva");

            return (instalacion, paquete);
        }

        /// <summary>Cada archivo de la carpeta con su contenido, para comparar antes y despues.</summary>
        private static List<string> Retrato(string carpeta) => Directory
            .GetFiles(carpeta, "*", SearchOption.AllDirectories)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(x => $"{Path.GetRelativePath(carpeta, x)}={File.ReadAllText(x)}")
            .ToList();

        private static string Temporal()
        {
            var ruta = Path.Combine(Path.GetTempPath(), "act-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ruta);
            return ruta;
        }

        private static void Borrar(params string[] carpetas)
        {
            foreach (var carpeta in carpetas)
            {
                try { Directory.Delete(carpeta, true); }
                catch { /* el test ya termino */ }
            }
        }
    }

    /// <summary>
    /// Cubre la agrupacion de Data/ por autor.
    ///
    /// Lo que se protege es que un mod nuevo caiga solo en la carpeta de su autor. Cuando eso
    /// no pasa, no falla nada visible: la traduccion anda igual, simplemente Data/ se
    /// desordena de a un mod por vez hasta que alguien lo nota meses despues.
    /// </summary>
    [TestClass]
    public class AgrupadorTests
    {
        /// <summary>Un RML de mentira con las carpetas que se le pidan dentro de Data/.</summary>
        private static string ArmarRml(params string[] carpetas)
        {
            var raiz = Path.Combine(Path.GetTempPath(), "rml-" + Guid.NewGuid().ToString("N"));
            foreach (var carpeta in carpetas)
                Directory.CreateDirectory(Path.Combine(raiz, "Data", carpeta));
            return raiz;
        }

        /// <summary>
        /// Un mod del workshop cualquiera. El nombre de su carpeta no se escribe a mano en
        /// ningun test: sale de FolderNameFor, que es lo que decide como se llama de verdad.
        /// </summary>
        private static ModMetadata Mod(string nombre, string id, string autor)
            => new("", id, nombre, "autor.mod", false) { Author = autor };

        /// <summary>
        /// Con varios autores manda el primero. Si no, cada combinacion de colaboradores seria
        /// un autor distinto y ninguna llegaria al umbral.
        /// </summary>
        [TestMethod]
        public void ElAutorSeNormalizaAlPrimeroDeLaLista()
        {
            Assert.AreEqual("Oskar Potocki", Agrupador.AutorPrincipal("Oskar Potocki, Taranchuk"));
            Assert.AreEqual("Oskar Potocki", Agrupador.AutorPrincipal("Oskar Potocki, Sarg Bjornson, Taranchuk"));
            Assert.AreEqual("Sarg Bjornson", Agrupador.AutorPrincipal("Sarg Bjornson and Oskar Potocki"));
            Assert.AreEqual("", Agrupador.AutorPrincipal(null));
            Assert.AreEqual("", Agrupador.AutorPrincipal("   "));
        }

        /// <summary>Un apellido que empieza con "and" no se parte al medio.</summary>
        [TestMethod]
        public void ElSeparadorNoPartePalabrasQueEmpiezanIgual()
        {
            Assert.AreEqual("Anderson", Agrupador.AutorPrincipal("Anderson"));
        }

        /// <summary>La misma persona firmando distinto tiene que dar la misma clave.</summary>
        [TestMethod]
        public void LaMismaPersonaFirmandoDistintoDaLaMismaClave()
        {
            Assert.AreEqual(Agrupador.Clave("Oskar Potocki"), Agrupador.Clave("OskarPotocki"));
            Assert.AreEqual(Agrupador.Clave("Oskar Potocki"), Agrupador.Clave("oskar potocki"));
            Assert.AreNotEqual(Agrupador.Clave("Oskar Potocki"), Agrupador.Clave("Sarg Bjornson"));
        }

        /// <summary>El caso que motiva todo: un mod nuevo de un autor que ya tiene carpeta.</summary>
        [TestMethod]
        public void UnModNuevoDeUnAutorConCarpetaCaeAdentro()
        {
            var mod = Mod("Progression Scenarios", "3378384387", "ferny");
            var nombre = LoadFoldersBuild.FolderNameFor(mod);
            var raiz = ArmarRml("!ferny");
            try
            {
                Assert.AreEqual(
                    Path.Combine(raiz, "Data", "!ferny", nombre),
                    LoadFoldersBuild.CarpetaDe(mod, raiz));
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>Sin carpeta de autor sigue cayendo plano: no se inventan carpetas.</summary>
        [TestMethod]
        public void UnModDeUnAutorSinCarpetaSigueCayendoPlano()
        {
            var mod = Mod("Un Mod", "999", "AlguienMas");
            var nombre = LoadFoldersBuild.FolderNameFor(mod);
            var raiz = ArmarRml("!ferny");
            try
            {
                Assert.AreEqual(
                    Path.Combine(raiz, "Data", nombre),
                    LoadFoldersBuild.CarpetaDe(mod, raiz));
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// Una traduccion que ya existe se queda donde esta, aunque el autor tenga carpeta.
        /// Mover lo ya traducido es tarea del <see cref="Agrupador.Reagrupar(string)"/>, que lo hace
        /// una sola vez y lo loguea, no de la eleccion de destino de cada extraccion.
        /// </summary>
        [TestMethod]
        public void UnaTraduccionQueYaExisteNoSeMueveSola()
        {
            var mod = Mod("Progression Scenarios", "3378384387", "ferny");
            var nombre = LoadFoldersBuild.FolderNameFor(mod);
            var raiz = ArmarRml("!ferny", nombre);
            try
            {
                Assert.AreEqual(
                    Path.Combine(raiz, "Data", nombre),
                    LoadFoldersBuild.CarpetaDe(mod, raiz));
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>La carpeta del autor se encuentra aunque este escrita distinto.</summary>
        [TestMethod]
        public void LaCarpetaDelAutorSeEncuentraAunqueEsteEscritaDistinto()
        {
            var raiz = ArmarRml("!Oskar Potocki");
            try
            {
                Assert.IsNotNull(Agrupador.CarpetaDeAutor("OskarPotocki", raiz));
                Assert.IsNotNull(Agrupador.CarpetaDeAutor("Oskar Potocki, Taranchuk", raiz));
                Assert.IsNull(Agrupador.CarpetaDeAutor("Sarg Bjornson", raiz));
                Assert.IsNull(Agrupador.CarpetaDeAutor("", raiz));
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// Un autor que firma fuera del alfabeto latino tambien tiene clave. Con a-z se quedaba
        /// en blanco y el mod no se agrupaba nunca, sin avisar.
        /// </summary>
        [TestMethod]
        public void UnAutorFueraDelAlfabetoLatinoTieneClave()
        {
            Assert.AreEqual("カタストロフ", Agrupador.AutorPrincipal("カタストロフ/NozoMe"));
            Assert.AreNotEqual("", Agrupador.Clave("カタストロフ/NozoMe"));
            Assert.AreNotEqual("", Agrupador.Clave("梦境触感"));
            Assert.AreEqual(Agrupador.Clave("梦境触感"), Agrupador.Clave("梦境 触感"));
        }

        /// <summary>El &amp; separa autores igual que la coma.</summary>
        [TestMethod]
        public void ElAmpersandSeparaAutores()
        {
            Assert.AreEqual("Oskar Potocki", Agrupador.AutorPrincipal("Oskar Potocki & Taranchuk"));
            Assert.AreEqual("A", Agrupador.AutorPrincipal("A&B"));
        }

        /// <summary>
        /// Cuatro mods de un mismo autor, cada uno con su packageId, sueltos en Data/ o donde
        /// se indique. Los yaml salen de LoadFoldersBuild.Contents, igual que en RML.
        /// </summary>
        private static List<ModMetadata> ModsDe(string autor, int cuantos)
            => Enumerable.Range(1, cuantos)
                .Select(i => new ModMetadata("", (1000 + i).ToString(), $"Mod {autor} {i}", $"{autor}.mod{i}", false)
                    { Author = autor })
                .ToList();

        private static string EscribirTraduccion(string raiz, ModMetadata mod, string? carpetaDeAutor = null)
        {
            var nombre = LoadFoldersBuild.FolderNameFor(mod);
            var carpeta = carpetaDeAutor is null
                ? Path.Combine(raiz, "Data", nombre)
                : Path.Combine(raiz, "Data", carpetaDeAutor, nombre);
            Directory.CreateDirectory(carpeta);
            File.WriteAllText(Path.Combine(carpeta, LoadFoldersBuild.FileName), LoadFoldersBuild.Contents(mod));
            return carpeta;
        }

        /// <summary>
        /// Con la carpeta del autor creada, aunque este vacia, las sueltas se mueven todas. Es
        /// lo que promete el aviso de AutorSinAgrupar.
        /// </summary>
        [TestMethod]
        public void ConLaCarpetaDelAutorLasSueltasSeMuevenTodas()
        {
            var mods = ModsDe("ferny", 4);
            var raiz = ArmarRml("!ferny");
            try
            {
                foreach (var mod in mods)
                    EscribirTraduccion(raiz, mod);

                Assert.AreEqual(4, Agrupador.Reagrupar(raiz, mods));
                foreach (var mod in mods)
                {
                    var nombre = LoadFoldersBuild.FolderNameFor(mod);
                    Assert.IsTrue(Directory.Exists(Path.Combine(raiz, "Data", "!ferny", nombre)), nombre);
                    Assert.IsFalse(Directory.Exists(Path.Combine(raiz, "Data", nombre)), nombre);
                }
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>Sin carpeta de autor no se mueve nada: crearla es decision de una persona.</summary>
        [TestMethod]
        public void SinLaCarpetaDelAutorNoSeMueveNada()
        {
            var mods = ModsDe("ferny", 4);
            var raiz = ArmarRml();
            try
            {
                var carpetas = mods.Select(x => EscribirTraduccion(raiz, x)).ToList();

                Assert.AreEqual(0, Agrupador.Reagrupar(raiz, mods));
                Assert.IsTrue(carpetas.All(Directory.Exists));
                Assert.AreEqual(1, Agrupador.Revisar(raiz, mods).Count);
                Assert.IsNull(Agrupador.Revisar(raiz, mods)[0].Carpeta);
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>Por debajo del umbral no se agrupa, aunque el autor tenga carpeta.</summary>
        [TestMethod]
        public void PorDebajoDelUmbralNoSeAgrupa()
        {
            var mods = ModsDe("ferny", Agrupador.Umbral - 1);
            var raiz = ArmarRml("!ferny");
            try
            {
                var carpetas = mods.Select(x => EscribirTraduccion(raiz, x)).ToList();

                Assert.AreEqual(0, Agrupador.Reagrupar(raiz, mods));
                Assert.IsTrue(carpetas.All(Directory.Exists));
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// Si en la carpeta del autor ya hay una con el mismo nombre, la suelta se queda donde
        /// esta: resolverlo a ciegas seria elegir cual de las dos traducciones se pierde.
        /// </summary>
        [TestMethod]
        public void NoSePisaUnaCarpetaQueYaEstaEnElDestino()
        {
            var mods = ModsDe("ferny", 4);
            var raiz = ArmarRml("!ferny");
            try
            {
                var suelta = EscribirTraduccion(raiz, mods[0]);
                var agrupada = EscribirTraduccion(raiz, mods[0], "!ferny");
                File.WriteAllText(Path.Combine(agrupada, "marca.txt"), "la de adentro");
                foreach (var mod in mods.Skip(1))
                    EscribirTraduccion(raiz, mod);

                Assert.AreEqual(3, Agrupador.Reagrupar(raiz, mods));
                Assert.IsTrue(Directory.Exists(suelta));
                Assert.AreEqual("la de adentro", File.ReadAllText(Path.Combine(agrupada, "marca.txt")));
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }
    }
}
