using System.IO.Compression;
using System.Xml;
using RimworldExtractorInternal;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorTest
{
    /// <summary>
    /// Cubre la configuracion persistida, que se lee por posicion y sin nombres de campo.
    /// </summary>

    [TestClass]
    public class PrefabsTests
    {
        /// <summary>
        /// Las lineas que quedaron reservadas —CommentOriginal y Method, que se fueron con la
        /// planilla, y EnableTkey, PathBaseRefList y Policy, que se fueron de Opciones— se
        /// siguen escribiendo y se saltean al leer. Si alguien las sacara, todo lo que viene
        /// despues se correria una posicion y un Prefabs.dat ya escrito devolveria el tema en
        /// PathRml y la ruta en ningun lado.
        /// </summary>
        [TestMethod]
        public void LasLineasReservadasMantienenLasPosiciones()
        {
            var archivo = Path.Combine(Path.GetTempPath(), "prefabs-" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                Prefabs.Init();
                Prefabs.PathRml = "una/ruta/cualquiera/RML";
                Prefabs.Theme = Prefabs.ColorTheme.Dark;
                Prefabs.Save(archivo);

                // Se escribe en cada una el valor fijo que una version anterior sabe leer.
                var lineas = File.ReadAllLines(archivo);
                Assert.AreEqual("False", lineas[2]);
                Assert.AreEqual("", lineas[5]);
                Assert.AreEqual("Overwrite", lineas[16]);

                // Y se leen bien aunque traigan otro valor, como uno escrito por una version
                // anterior que todavia las usaba.
                lineas[2] = "True";
                lineas[5] = @"C:\lista.refMods";
                lineas[11] = "True";
                lineas[16] = "Stop";
                lineas[17] = "Excel";
                File.WriteAllLines(archivo, lineas);

                Prefabs.PathRml = "otra cosa";
                Prefabs.Theme = Prefabs.ColorTheme.Light;
                Prefabs.Load(archivo);

                Assert.AreEqual("una/ruta/cualquiera/RML", Prefabs.PathRml);
                Assert.AreEqual(Prefabs.ColorTheme.Dark, Prefabs.Theme);
            }
            finally
            {
                if (File.Exists(archivo)) File.Delete(archivo);
            }
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
        /// Cuando la extraccion trae el mismo nodo de las dos formas, cada una conserva la suya.
        ///
        /// No es un duplicado: son dos textos distintos y validos. Es GravTech, donde el def dice
        /// "(110 t)" y el patch de Combat Extended lo cambia a "(130 t)". Unificar la clave hacia
        /// que las dos tomaran la misma traduccion, y el patch de CE se quedaba con "(110 t)".
        /// </summary>
        [TestMethod]
        public void LasDosFormasDelMismoNodoConservanCadaUnaLaSuya()
        {
            const string nodo = "GravRifle_LooongShot.description";
            var extraccion = new[]
            {
                new TranslationEntry("AbilityDef", nodo, "Long-range shot. (110 t)", null, null, null),
                new TranslationEntry("Patches.AbilityDef", nodo, "Long-range shot. (130 t)", null, null, null)
            };
            // En el orden en que las lee IO.FromLanguageXml: primero Patches, despues DefInjected.
            var previas = new[]
            {
                new TranslationEntry("Patches.AbilityDef", nodo, "", "Disparo de largo alcance. (130 t)", null, null),
                new TranslationEntry("AbilityDef", nodo, "Long-range shot. (110 t)", "Disparo de largo alcance. (110 t)", null, null)
            };

            var (resultado, sinUso, _) = TranslationMerge.Merge(extraccion, previas);

            Assert.AreEqual("Disparo de largo alcance. (110 t)", resultado.Single(x => x.ClassName == "AbilityDef").Translated);
            Assert.AreEqual("Disparo de largo alcance. (130 t)", resultado.Single(x => x.ClassName == "Patches.AbilityDef").Translated,
                "el patch de CE se quedo con la traduccion del def");
            Assert.AreEqual(0, sinUso.Count);
        }

        /// <summary>
        /// El caso real que motivo que gane DefInjected sigue igual: si la extraccion trae una
        /// sola forma, la otra traduccion era de una version anterior y no se usa.
        /// </summary>
        [TestMethod]
        public void ConUnaSolaFormaEnLaExtraccionGanaLaDeEsaForma()
        {
            var extraccion = new[] { new TranslationEntry("ThingDef", "Cuchillo.tools.1.label", "point", null, null, null) };
            var previas = new[]
            {
                new TranslationEntry("Patches.ThingDef", "Cuchillo.tools.1.label", "", "filo", null, null),
                new TranslationEntry("ThingDef", "Cuchillo.tools.1.label", "point", "punta", null, null)
            };

            var (resultado, sinUso, _) = TranslationMerge.Merge(extraccion, previas);

            Assert.AreEqual("punta", resultado.Single().Translated);
            Assert.AreEqual(0, sinUso.Count, "el duplicado viejo no tiene que ir a UNUSED");
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
        /// Una traduccion guardada con el nombre largo del idioma, como estaban todas antes de
        /// pasar al corto, se lee igual y al reescribirse queda solo bajo el corto. Si quedaran
        /// las dos carpetas, el juego cargaria las dos.
        ///
        /// El nombre corto es por el largo de las rutas: ver Utils.CarpetaDelIdiomaDestino.
        /// </summary>
        [TestMethod]
        public void PasaElIdiomaAlNombreCortoSinPerderNada()
        {
            var rml = Montar(out var carpeta);
            try
            {
                var resultado = ActualizacionRml.Escribir(
                    Mod(), Extraccion("Sigue.label", "Vieja.label"), rml);

                Assert.AreEqual(2, resultado.Conservadas,
                    "no se leyo lo traducido bajo el nombre largo del idioma");
                Assert.IsFalse(Directory.Exists(Path.Combine(carpeta, "Languages", Idioma)),
                    "quedo la carpeta con el nombre largo al lado de la nueva");

                var corta = Path.Combine(carpeta, "Languages", "SpanishLatin", "DefInjected");
                Assert.IsTrue(Directory.Exists(corta), "no se escribio con el nombre corto");
                StringAssert.Contains(
                    string.Join("\n", Directory.GetFiles(corta, "*.xml", SearchOption.AllDirectories)
                        .Select(File.ReadAllText)),
                    "artilugio");
            }
            finally
            {
                Directory.Delete(rml, true);
            }
        }

        /// <summary>
        /// Un RML de mentira con las dos entradas ya traducidas, de las cuales la extraccion
        /// va a traer una sola. Van bajo el nombre largo del idioma, como quedaron escritas
        /// antes de pasar al corto.
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
    /// Cubre dos mods del Workshop con el mismo packageId y distinto numero. Paso con The Dead
    /// Man's Switch (3121742525, hasta la 1.6) y The Dead Man's Switch II (3819799478, solo 1.7):
    /// son dos mods vivos, no uno que se volvio a publicar. Tomarlos por el mismo convirtio la
    /// traduccion del primero en la del segundo, y en 1.6 se perdieron 416 traducciones.
    ///
    /// Desde aca no se puede saber si el numero viejo sigue publicado, asi que lo decide una
    /// persona: el extractor no renombra ni reextrae la carpeta del otro numero, solo avisa.
    /// </summary>
    [TestClass]
    public class OtroNumeroMismoPackageIdTests
    {
        private static ModMetadata Mod(string id) =>
            new("", id, "The Dead Man's Switch", "Aoba.DeadManSwitch.Core", false) { Author = "AobaKuma" };

        /// <summary>Una carpeta de RML con una traduccion, escrita para el numero viejo.</summary>
        private static string Montar(out string vieja)
        {
            Prefabs.Init();

            var rml = Path.Combine(Path.GetTempPath(), "rml-numero-" + Guid.NewGuid().ToString("N"));
            vieja = Path.Combine(rml, "Data", "!AobaKuma", LoadFoldersBuild.FolderNameFor(Mod("3121742525")));

            var defInjected = Path.Combine(vieja, "Languages", Utils.CarpetaDelIdiomaDestino(), "DefInjected", "ThingDef");
            Directory.CreateDirectory(defInjected);
            File.WriteAllText(Path.Combine(defInjected, "cosas.xml"), """
                <?xml version="1.0" encoding="utf-8"?>
                <LanguageData>
                  <!-- EN: switch -->
                  <Interruptor.label>interruptor</Interruptor.label>
                </LanguageData>
                """);
            LoadFoldersBuild.Write(Mod("3121742525"), vieja);
            return rml;
        }

        /// <summary>
        /// El otro numero no se queda con la carpeta: el mod instalado escribe la suya, al lado,
        /// y la del otro numero queda como estaba.
        /// </summary>
        [TestMethod]
        public void ElOtroNumeroEscribeSuPropiaCarpetaYNoTocaLaVieja()
        {
            var rml = Montar(out var vieja);
            try
            {
                var mod = Mod("3819799478");
                var antes = File.ReadAllText(Path.Combine(vieja, LoadFoldersBuild.FileName));
                var extraccion = new List<TranslationEntry>
                {
                    new("ThingDef", "Interruptor.label", "switch", null, null, null)
                };

                var r = ActualizacionRml.Escribir(mod, extraccion, rml);

                Assert.AreEqual(Path.Combine(Path.GetDirectoryName(vieja)!, LoadFoldersBuild.FolderNameFor(mod)), r.Destino);
                Assert.IsTrue(Directory.Exists(vieja), "se llevo la carpeta del otro numero");
                Assert.AreEqual(antes, File.ReadAllText(Path.Combine(vieja, LoadFoldersBuild.FileName)));
                Assert.AreEqual(1, Directory.GetFiles(vieja, "*.xml", SearchOption.AllDirectories).Length);
            }
            finally
            {
                Directory.Delete(rml, true);
            }
        }

        /// <summary>
        /// «Actualizar todo RML» reconoce la carpeta de otro numero: si no, la reextraeria con
        /// el mod instalado, que es otro, y apartaria lo que ese no trae.
        /// </summary>
        [TestMethod]
        public void LaCorridaPorLotesReconoceLaCarpetaDeOtroNumero()
        {
            var rml = Montar(out var vieja);
            try
            {
                var yaml = Path.Combine(vieja, LoadFoldersBuild.FileName);
                Assert.IsTrue(ActualizacionPorLotes.EsDeOtroNumero(yaml, Mod("3819799478")));
                Assert.IsFalse(ActualizacionPorLotes.EsDeOtroNumero(yaml, Mod("3121742525")));
                // Un mod local no tiene numero, y una carpeta sin numero no se puede comparar.
                Assert.IsFalse(ActualizacionPorLotes.EsDeOtroNumero(yaml, Mod("???")));
            }
            finally
            {
                Directory.Delete(rml, true);
            }
        }

        /// <summary>
        /// Lo que distingue a las dos carpetas es la version del juego en que cargan, y eso va en
        /// el bloque Version del yaml, escrito a mano. Reescribir el yaml no lo puede borrar.
        /// </summary>
        [TestMethod]
        public void ReescribirElYamlConservaLaVersion()
        {
            var rml = Montar(out var vieja);
            try
            {
                var yaml = Path.Combine(vieja, LoadFoldersBuild.FileName);
                File.WriteAllText(yaml, File.ReadAllText(yaml)
                    .Replace("    Default:", "    Default: \"1.6\"")
                    .Replace("    Ban:", "    Ban: [\"1.7\"]"));

                LoadFoldersBuild.Write(Mod("3121742525"), vieja);

                var texto = File.ReadAllText(yaml);
                StringAssert.Contains(texto, "    Default: \"1.6\"");
                StringAssert.Contains(texto, "    Ban: [\"1.7\"]");
                StringAssert.Contains(texto, "WorkshopID: \"3121742525\"");
            }
            finally
            {
                Directory.Delete(rml, true);
            }
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
    /// Cubre los nombres de la municion generica de Combat Extended, que viven en las
    /// modExtensions de cada municion y no en sus campos de siempre.
    /// </summary>
    [TestClass]
    public class MunicionGenericaTests
    {
        private const string Idioma = "SpanishLatin (Español(Latinoamérica))";

        /// <summary>Los tres campos salen con la ruta que DefInjected entiende.</summary>
        [TestMethod]
        public void ExtraeLosTresCamposDeLaMunicionGenerica()
        {
            Prefabs.Init();

            var doc = new XmlDocument();
            doc.LoadXml("""
                <Defs>
                  <ThingDef>
                    <defName>Shell_HE</defName>
                    <label>81mm mortar shell (HE)</label>
                    <modExtensions>
                      <li>
                        <genericLabel>mortar shell (HE)</genericLabel>
                      </li>
                    </modExtensions>
                  </ThingDef>
                  <RecipeDef>
                    <defName>MakeShell_HE</defName>
                    <label>make 81mm mortar shells (HE) x5</label>
                    <modExtensions>
                      <li>
                        <genericLabel>make mortar shells (HE) x5</genericLabel>
                        <genericDescription>Craft 5 mortar shells (HE).</genericDescription>
                      </li>
                    </modExtensions>
                  </RecipeDef>
                  <ThingDef>
                    <defName>Bullet_45ACP_FMJ</defName>
                    <label>.45 ACP bullet</label>
                    <projectile>
                      <genericLabelOverride>pistol</genericLabelOverride>
                    </projectile>
                  </ThingDef>
                </Defs>
                """);

            List<TranslationEntry> entradas;
            var previo = Extractor.CombinedDefs;
            try
            {
                Extractor.CombinedDefs = doc;
                entradas = Extractor.ExtractDefs().ToList();
            }
            finally
            {
                Extractor.CombinedDefs = previo;
            }

            CollectionAssert.IsSubsetOf(new[]
            {
                "Shell_HE.modExtensions.0.genericLabel",
                "MakeShell_HE.modExtensions.0.genericLabel",
                "MakeShell_HE.modExtensions.0.genericDescription",
                "Bullet_45ACP_FMJ.projectile.genericLabelOverride",
            }, entradas.Select(x => x.Node).ToList());
        }

        /// <summary>
        /// Un Prefabs.dat guardado antes de que existieran los tres campos trae la lista de
        /// etiquetas sin ellos, y la lista se lee del archivo, no del valor por defecto. Sin
        /// sumarlos al leer, solo los veria quien borrara su configuracion.
        /// </summary>
        [TestMethod]
        public void UnPrefabsViejoTambienLosExtrae()
        {
            var archivo = Path.Combine(Path.GetTempPath(), "prefabs-" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                Prefabs.Init();
                Prefabs.ExtractableTags = new(Prefabs.ExtractableTags.Where(x => !x.StartsWith("generic")));
                Prefabs.Save(archivo);

                Prefabs.Load(archivo);

                foreach (var tag in new[] { "genericLabel", "genericDescription", "genericLabelOverride" })
                    Assert.IsTrue(Prefabs.CanExtract(tag, "CualquierDef"), tag);
            }
            finally
            {
                File.Delete(archivo);
                Prefabs.Init();
            }
        }

        /// <summary>
        /// Sobre el Combat Extended instalado: los 38 textos de su Defs/ tienen que salir.
        /// Son 29 genericLabel, 8 genericDescription y un genericLabelOverride, que esta en
        /// una base abstracta y lo heredan las tres balas de .45 ACP: salen tres entradas.
        /// </summary>
        [TestMethod]
        public void CombatExtendedDevuelveSuMunicionGenerica()
        {
            Prefabs.Init();
            Prefabs.TranslationLanguage = Idioma;

            if (!Directory.Exists(Path.Combine(Prefabs.PathRimworld, "Data", "Core")))
                Assert.Inconclusive("Este test necesita RimWorld instalado.");

            var ce = ModLister.AllMods.FirstOrDefault(x => x.PackageId == "CETeam.CombatExtended");
            if (ce == null)
                Assert.Inconclusive("Este test necesita Combat Extended instalado.");

            var extraibles = ModLister.GetExtractableFolders(ce).AutoSeleccionables()
                .Where(x => Path.GetFileName(x.FolderName) == "Defs" && !x.FolderName.Contains("ModPatches"))
                .ToList();
            var referencias = ModLister.FindAllReferenceMods(ce).ToList();

            var genericos = Extractor.ExtractTranslationData(ce, extraibles, referencias)
                .Where(x => x.Node.Split('.')[^1].StartsWith("generic"))
                .ToList();

            Assert.AreEqual(29, genericos.Count(x => x.Node.EndsWith(".genericLabel")), "genericLabel");
            Assert.AreEqual(8, genericos.Count(x => x.Node.EndsWith(".genericDescription")), "genericDescription");
            Assert.AreEqual(3, genericos.Count(x => x.Node.EndsWith(".genericLabelOverride")), "genericLabelOverride");
        }
    }

    /// <summary>
    /// Cubre la carta que avisa que un automatroide camuflado quedo a la vista. Su titulo y su
    /// texto viven en el CompProperties_Camouflage del framework Fortified (lo usan el nóctulo y
    /// el jaeger de The Dead Man's Switch II), y salian en ingles en el juego.
    /// </summary>
    [TestClass]
    public class CartaDeCamuflajeTests
    {
        /// <summary>Los dos campos de la carta salen del comp del def.</summary>
        [TestMethod]
        public void ExtraeLaCartaDelCamuflaje()
        {
            Prefabs.Init();

            var doc = new XmlDocument();
            doc.LoadXml("""
                <Defs>
                  <ThingDef>
                    <defName>DMS_Mech_Jaeger</defName>
                    <label>jaeger</label>
                    <comps>
                      <li Class="Fortified.CompProperties_Camouflage">
                        <ticksUntilRecover>1200</ticksUntilRecover>
                        <detectedLetterLabel>Jaeger revealed</detectedLetterLabel>
                        <detectedLetterDesc>An enemy automatroid has appeared out of thin air!</detectedLetterDesc>
                      </li>
                    </comps>
                  </ThingDef>
                </Defs>
                """);

            List<TranslationEntry> entradas;
            var previo = Extractor.CombinedDefs;
            try
            {
                Extractor.CombinedDefs = doc;
                entradas = Extractor.ExtractDefs().ToList();
            }
            finally
            {
                Extractor.CombinedDefs = previo;
            }

            Assert.AreEqual(1, entradas.Count(x => x.Node.EndsWith(".detectedLetterLabel") && x.Original == "Jaeger revealed"));
            Assert.AreEqual(1, entradas.Count(x => x.Node.EndsWith(".detectedLetterDesc")));
        }

        /// <summary>Un Prefabs.dat anterior a los dos campos tambien los extrae.</summary>
        [TestMethod]
        public void UnPrefabsViejoTambienExtraeLaCarta()
        {
            var archivo = Path.Combine(Path.GetTempPath(), "prefabs-" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                Prefabs.Init();
                Prefabs.ExtractableTags = new(Prefabs.ExtractableTags.Where(x => !x.StartsWith("detectedLetter")));
                Prefabs.Save(archivo);

                Prefabs.Load(archivo);

                foreach (var tag in new[] { "detectedLetterLabel", "detectedLetterDesc" })
                    Assert.IsTrue(Prefabs.CanExtract(tag, "CualquierDef"), tag);
            }
            finally
            {
                File.Delete(archivo);
                Prefabs.Init();
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

        /// <summary>Un def con un elemento de lista que tiene TranslationHandle (su verbClass).</summary>
        private const string MochilaConVerbo = """
            <Defs>
              <ThingDef>
                <defName>Mochila</defName>
                <verbs>
                  <li Class="CombatExtended.VerbPropertiesCE">
                    <verbClass>CombatExtended.Verb_ShootCE</verbClass>
                    <label>deploy turret</label>
                  </li>
                </verbs>
              </ThingDef>
            </Defs>
            """;

        /// <summary>Un Patches de RML con una sola traduccion del label del verbo de la mochila.</summary>
        private static string PatchDelVerbo(string rutaDelVerbo) => $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Patch>
              <Operation Class="PatchOperationReplace">
                <success>Always</success>
                <xpath>/Defs/ThingDef[defName="Mochila"]/verbs/{rutaDelVerbo}/label</xpath>
                <value>
                  <!-- EN: deploy turret -->
                  <label>desplegar torreta</label>
                </value>
              </Operation>
            </Patch>
            """;

        /// <summary>
        /// Un xpath que entra a un elemento de lista se relee con el mismo nombre con que se
        /// extrae: por su TranslationHandle, el verbo. Se releia por el indice (verbs.0.label),
        /// no cruzaba con la extraccion y quedaba una copia en UNUSED. Asi quedaron ocho en
        /// Combat Extended: las mochilas, las armaduras, la driade y las torretas de hombro.
        /// </summary>
        [TestMethod]
        public void UnVerboSeReleeConElNombreConQueSeExtrae()
        {
            var raiz = Montar(PatchDelVerbo("*[.//*[contains(text(), 'Verb_ShootCE')]]"));
            try
            {
                var leidas = ConLaBase(MochilaConVerbo, () => IO.FromLanguageXml(raiz));

                CollectionAssert.AreEqual(new[] { "Mochila.verbs.Verb_ShootCE.label" },
                    leidas.Where(x => x.Translated == "desplegar torreta").Select(x => x.Node).ToList());
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// El paso intermedio: RML ya tiene rutas por indice (verbs/li[1]) para elementos que
        /// ahora se nombran por su verbo. La relectura evaluada las encuentra y las nombra por el
        /// verbo; la literal no tiene que sumar ademas la del indice, que no cruzaria con nada y
        /// quedaria para siempre en UNUSED.
        /// </summary>
        [TestMethod]
        public void UnaRutaViejaPorIndiceNoDejaUnaCopia()
        {
            var raiz = Montar(PatchDelVerbo("li[1]"));
            try
            {
                var leidas = ConLaBase(MochilaConVerbo, () => IO.FromLanguageXml(raiz));

                CollectionAssert.AreEqual(new[] { "Mochila.verbs.Verb_ShootCE.label" },
                    leidas.Where(x => x.Translated == "desplegar torreta").Select(x => x.Node).ToList());
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// Del lado de la extraccion: un patch del mod que agrega un verbo a la lista lo nombra
        /// por su verbo, igual que la extraccion de los defs. Por el indice, el nombre cambiaba
        /// si otro patch agregaba un verbo antes.
        /// </summary>
        [TestMethod]
        public void UnVerboAgregadoPorUnPatchSeNombraPorSuClase()
        {
            var raiz = Montar("""
                <?xml version="1.0" encoding="utf-8"?>
                <Patch>
                  <Operation Class="PatchOperationAdd">
                    <xpath>/Defs/ThingDef[defName="Mochila"]/verbs</xpath>
                    <value>
                      <li Class="CombatExtended.VerbPropertiesCE">
                        <verbClass>CombatExtended.Verb_ShootCEOneUse</verbClass>
                        <label>throw grenade</label>
                      </li>
                    </value>
                  </Operation>
                </Patch>
                """);
            try
            {
                var patches = new ExtractableFolder(ModMetadata.Emptry, Path.Combine(raiz, "Patches"), null);
                var extraidas = ConLaBase(MochilaConVerbo, () => Extractor.ExtractPatches(patches).ToList());

                CollectionAssert.AreEqual(new[] { "Mochila.verbs.Verb_ShootCEOneUse.label" },
                    extraidas.Select(x => x.Node).ToList());
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// Un def declarado como ThingDef con Class="CombatExtended.AmmoDef" tiene que salir de
        /// un patch como ThingDef, que es lo que dice NodeReplacement. Con la clase del atributo,
        /// el xpath que se escribe es /Defs/CombatExtended.AmmoDef[...], que en el juego no
        /// encuentra nada: la traduccion estaba escrita y no se aplicaba. Le pasaba a la granada
        /// toxica y a las flechas Black Widow de Combat Extended.
        /// </summary>
        [TestMethod]
        public void UnPatchSobreUnaClaseReemplazadaSaleConLaClaseDelElemento()
        {
            var raiz = Montar("""
                <?xml version="1.0" encoding="utf-8"?>
                <Patch>
                  <Operation Class="PatchOperationReplace">
                    <success>Always</success>
                    <xpath>/Defs/ThingDef[defName="GranadaToxica"]/label</xpath>
                    <value>
                      <!-- EN: tox grenades -->
                      <label>granadas tóxicas</label>
                    </value>
                  </Operation>
                </Patch>
                """);
            try
            {
                var leidas = ConLaBase("""
                    <Defs>
                      <ThingDef Class="CombatExtended.AmmoDef">
                        <defName>GranadaToxica</defName>
                        <label>tox grenades</label>
                      </ThingDef>
                    </Defs>
                    """, () => IO.FromLanguageXml(raiz));

                var entrada = leidas.Single(x => x.Node == "GranadaToxica.label");
                Assert.AreEqual("Patches.ThingDef", entrada.ClassName);
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// Un Patches escrito antes del arreglo de arriba trae /Defs/CombatExtended.AmmoDef[...],
        /// que no encuentra nada y se relee literal. Si la clase quedara como la del xpath, la
        /// traduccion no cruzaria con la extraccion nueva, que ya sale como ThingDef: iria a
        /// UNUSED y volveria a TODO. Asi pasaron seis de Combat Extended en la primera prueba.
        /// </summary>
        [TestMethod]
        public void UnPatchViejoConLaClaseReemplazadaSeReleeComoElElemento()
        {
            var raiz = Montar("""
                <?xml version="1.0" encoding="utf-8"?>
                <Patch>
                  <Operation Class="PatchOperationReplace">
                    <success>Always</success>
                    <xpath>/Defs/CombatExtended.AmmoDef[defName="GranadaToxica"]/label</xpath>
                    <value>
                      <!-- EN: tox grenades -->
                      <label>granadas tóxicas</label>
                    </value>
                  </Operation>
                </Patch>
                """);
            try
            {
                var leidas = ConLaBase("""
                    <Defs>
                      <ThingDef Class="CombatExtended.AmmoDef">
                        <defName>GranadaToxica</defName>
                        <label>tox grenades</label>
                      </ThingDef>
                    </Defs>
                    """, () => IO.FromLanguageXml(raiz));

                var entrada = leidas.Single(x => x.Node == "GranadaToxica.label");
                Assert.AreEqual("Patches.ThingDef", entrada.ClassName);
                Assert.AreEqual("granadas tóxicas", entrada.Translated);
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
        /// Dos carpetas de RML que traducen defs del mismo mod escribian el mismo nombre de
        /// archivo, y las de Data/ son carpetas de un solo mod de RimWorld, que las recorre
        /// deduplicando por ruta relativa: cargaba una y descartaba el resto sin decir nada.
        /// De nueve "Patches/Odyssey.xml" quedaba uno, y con el las traducciones de los otros
        /// ocho no llegaban nunca al juego. Por eso el nombre lleva el id del workshop adelante.
        /// </summary>
        [TestMethod]
        public void DosModsQueParcheanElMismoDuenioNoEscribenElMismoArchivo()
        {
            var unMod = EscribirPatchDeOtroMod("Un Mod - 3609835606");
            var otroMod = EscribirPatchDeOtroMod("Otro Mod - 3545374124");
            try
            {
                var unNombre = Path.GetFileName(Directory.GetFiles(Path.Combine(unMod, "Patches")).Single());
                var otroNombre = Path.GetFileName(Directory.GetFiles(Path.Combine(otroMod, "Patches")).Single());

                Assert.AreEqual(Utils.GenerateFileName("Un Mod - 3609835606", "Odyssey") + ".xml", unNombre,
                    "el nombre no sale del generador, asi que no se puede rehacer sin reextraer");
                Assert.AreNotEqual(unNombre, otroNombre,
                    "los dos archivos se llaman igual: el juego carga uno y descarta el otro");
            }
            finally
            {
                Directory.Delete(unMod, true);
                Directory.Delete(otroMod, true);
            }
        }

        /// <summary>
        /// Escribe una carpeta de mod cuyo unico patch traduce un def de otro mod, que es el
        /// caso en el que el nombre del archivo sale del dueño y no del mod que se traduce.
        /// </summary>
        private static string EscribirPatchDeOtroMod(string modName)
        {
            Prefabs.Init();
            Prefabs.TranslationLanguage = Idioma;

            var raiz = Path.Combine(Path.GetTempPath(), "rml-nombres-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(raiz);

            // Un defName que no exista en ningun lado: asi no se lo lleva el reencaminado de lo
            // que el juego ya traduce, que sacaria el patch de Patches/ y no habria que mirar.
            var entrada = new TranslationEntry("Patches.ThingDef", "UnDefDeOdyssey.label",
                "grav anchor", "ancla gravitacional", null, null);

            Extractor.DuenioPorDefName["UnDefDeOdyssey"] = "Odyssey";
            try
            {
                IO.ToLanguageXml(new List<TranslationEntry> { entrada }, false,
                    XmlCommentStyle.TranslationTemplate, modName, raiz);
            }
            finally
            {
                Extractor.DuenioPorDefName.Remove("UnDefDeOdyssey");
            }

            return raiz;
        }

        /// <summary>
        /// Corre algo con una base de defs vacia, que es el estado en el que los xpath de RML no
        /// encuentran nada. Es justo el caso que se perdia.
        /// </summary>
        private static T ConBaseVacia<T>(Func<T> hacer) => ConLaBase("<Defs />", hacer);

        /// <summary>
        /// Corre algo con la base de defs que se le pase. Tambien como base completa: si no, la
        /// relectura usa la que haya dejado registrada otro test que extrajo de verdad, y los
        /// xpath no encuentran los defs de este.
        /// </summary>
        private static T ConLaBase<T>(string defs, Func<T> hacer)
        {
            var previo = Extractor.CombinedDefs;
            var doc = new XmlDocument();
            doc.LoadXml(defs);
            try
            {
                Extractor.CombinedDefs = doc;
                Extractor.RegistrarBaseCompleta(doc);
                return hacer();
            }
            finally
            {
                Extractor.CombinedDefs = previo;
                Extractor.RegistrarBaseCompleta(null);
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
    /// Cubre la condicion de un patch cuyo mod solo se conoce por packageId.
    ///
    /// PatchOperationFindMod compara nombres. Cuando el mod no esta instalado no hay nombre, y
    /// el patch salia con "##packageId##..." como si lo fuera: no coincide nunca, asi que la
    /// traduccion no se aplicaba ni teniendo el mod, y el juego no avisa nada. Paso con Cerebrex
    /// en GravTech y estaba en diez mods de RML.
    /// </summary>
    [TestClass]
    public class CondicionPorPackageIdTests
    {
        private const string Idioma = "SpanishLatin (Español(Latinoamérica))";

        /// <summary>Un packageId que no puede estar instalado en ninguna maquina.</summary>
        private const string SinInstalar = "nadie.mod.que.no.esta";

        private const string Defs = """
            <Defs>
              <ThingDef>
                <defName>NucleoDeComputadora</defName>
                <description>Computer core.</description>
              </ThingDef>
            </Defs>
            """;

        private static TranslationEntry Entrada(RequiredMods requiere) =>
            new("Patches.ThingDef", "NucleoDeComputadora.description", "Computer core.",
                "Núcleo de computadora.", requiere, null);

        /// <summary>
        /// Sin nombre, la condicion va por MayRequire en un li de la secuencia, y la operacion
        /// queda adentro. Nada de FindMod ni del prefijo.
        /// </summary>
        [TestMethod]
        public void SinNombreSaleComoMayRequire()
        {
            var requiere = new RequiredMods();
            requiere.AddAllowedByPackageId(SinInstalar);

            var raiz = Escribir(Entrada(requiere));
            try
            {
                var patch = LeerPatch(raiz);

                Assert.IsFalse(patch.OuterXml.Contains(RequiredMods.PACKAGE_ID_PREFIX), "salio el packageId como nombre");
                Assert.IsNull(patch.SelectSingleNode("//*[@Class='PatchOperationFindMod']"),
                    "un FindMod con un packageId no coincide nunca");
                Assert.IsNotNull(patch.SelectSingleNode(
                        $"/Patch/Operation[@Class='PatchOperationSequence']/operations/li[@MayRequire='{SinInstalar}']" +
                        "/operations/li[@Class='PatchOperationReplace']"),
                    "la operacion no quedo dentro de la condicion");
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// Con un grupo por nombre y otro por packageId, los dos condicionan: el FindMod por
        /// fuera y el MayRequire por dentro, en AND como estaban. Lo que ya salia bien no cambia.
        /// </summary>
        [TestMethod]
        public void ConviveConUnFindModPorNombre()
        {
            var requiere = new RequiredMods();
            requiere.AddAllowedByModName("Combat Extended");
            requiere.AddAllowedByPackageId(SinInstalar);

            var raiz = Escribir(Entrada(requiere));
            try
            {
                var patch = LeerPatch(raiz);

                Assert.IsNotNull(patch.SelectSingleNode(
                    "/Patch/Operation[@Class='PatchOperationFindMod'][mods/li='Combat Extended']" +
                    $"/match[@Class='PatchOperationSequence']/operations/li[@MayRequire='{SinInstalar}']" +
                    "/operations/li[@Class='PatchOperationReplace']"));
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// El mismo mod pedido dos veces da una sola condicion. Es GravTech, que pide Cerebrex
        /// en el IfModActive de la carpeta y en el MayRequire del patch.
        /// </summary>
        [TestMethod]
        public void ElMismoModPedidoDosVecesDaUnaSolaCondicion()
        {
            var requiere = new RequiredMods();
            requiere.AddAllowedByPackageId(SinInstalar);
            requiere.AddAllowedByPackageId(SinInstalar);

            var raiz = Escribir(Entrada(requiere));
            try
            {
                Assert.AreEqual(1, LeerPatch(raiz).SelectNodes("//li[@MayRequire]")!.Count);
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// Varios mods en "||" van en un solo MayRequireAnyOf: un MayRequire con coma los
        /// exigiria todos.
        /// </summary>
        [TestMethod]
        public void UnGrupoConAlternativasSaleComoMayRequireAnyOf()
        {
            var requiere = new RequiredMods();
            requiere.AddAllowedByPackageIds(new[] { SinInstalar, "otro.mod.que.no.esta" });

            var raiz = Escribir(Entrada(requiere));
            try
            {
                var patch = LeerPatch(raiz);

                Assert.IsNotNull(patch.SelectSingleNode(
                    $"//li[@MayRequireAnyOf='{SinInstalar},otro.mod.que.no.esta']/operations/li[@Class='PatchOperationReplace']"));
                Assert.IsNull(patch.SelectSingleNode("//@MayRequire"));
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// Lo escrito asi se tiene que poder releer, o la proxima actualizacion lo pierde: con
        /// el def cargado, que evalua el xpath dentro de la secuencia anidada, y sin el, que es
        /// lo que lee LeerPatchesLiteral.
        /// </summary>
        [TestMethod]
        public void SeReleeConElDefYSinEl()
        {
            var requiere = new RequiredMods();
            requiere.AddAllowedByPackageId(SinInstalar);

            var raiz = Escribir(Entrada(requiere));
            try
            {
                foreach (var defs in new[] { Defs, "<Defs />" })
                {
                    var leidas = ConLaBase(defs, () => IO.FromLanguageXml(raiz));

                    var entrada = leidas.FirstOrDefault(x => x.Node == "NucleoDeComputadora.description");
                    Assert.IsNotNull(entrada, $"no se releyo la traduccion con la base {defs}");
                    Assert.AreEqual("Núcleo de computadora.", entrada.Translated);
                }
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>Escribe las entradas en un mod de mentira y devuelve su carpeta.</summary>
        private static string Escribir(params TranslationEntry[] entradas)
        {
            Prefabs.Init();
            Prefabs.TranslationLanguage = Idioma;

            var raiz = Path.Combine(Path.GetTempPath(), "mayrequire-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(raiz);

            IO.ToLanguageXml(entradas.ToList(), false, XmlCommentStyle.TranslationTemplate, "UnMod", raiz);

            return raiz;
        }

        private static XmlDocument LeerPatch(string raiz)
        {
            var doc = new XmlDocument();
            doc.Load(Directory.GetFiles(Path.Combine(raiz, "Patches"), "*.xml").Single());
            return doc;
        }

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
                IO.ToLanguageXml(entradas.ToList(), false, XmlCommentStyle.TranslationTemplate,
                    "UnMod", raiz);

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
            // La 1.7 salio sin las traducciones oficiales de los DLC: solo trae English/.
            var tar = Path.Combine(odyssey, "Languages", Idioma + ".tar");
            if (!File.Exists(tar))
                Assert.Inconclusive($"El juego instalado no trae {tar}.");

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
                IO.ToLanguageXml(entradas.ToList(), false, XmlCommentStyle.TranslationTemplate,
                    "UnMod", raiz);

                return (Juntar(Path.Combine(raiz, "Languages", Utils.CarpetaDelIdiomaDestino(), "DefInjected")),
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
            // Sin los .dll no se sabe que nombre tiene el comp de esa clase (ver CompsPorNombreTests).
            ClasesDeComp.Usar(Array.Empty<string>());
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
        /// <summary>Un cuerpo de release como los que escribe publish.yml.</summary>
        private const string CuerpoDeRelease =
            "### [1.0.0](https://github.com/LordMordelon/RimworldExtractor/compare/0.1.30...1.0.0) (2026-10-20)\r\r\n" +
            "\r\n" +
            "- Correr «Actualizar todo RML» por consola\r\n" +
            "- feat!: quitar las planillas Excel y el analizador de traducciones\r\n" +
            "- fix(gui): ver el `log` en [la ventana](https://example.com)\r\n" +
            "\r\n" +
            "Dos descargas que hacen lo mismo:\r\n" +
            "\r\n" +
            "- **Portable** (`RimworldExtractor-Portable.exe`): un único archivo.\r\n";

        /// <summary>
        /// Al ofrecer la actualizacion solo importa que cambio. Las instrucciones de descarga
        /// sobran, porque la aplicacion se actualiza sola, y el formato de GitHub se veia crudo.
        /// </summary>
        [TestMethod]
        public void LasNotasSonSoloLaListaDeCambios()
        {
            CollectionAssert.AreEqual(new[]
            {
                "Correr «Actualizar todo RML» por consola",
                "Quitar las planillas Excel y el analizador de traducciones",
                "Ver el log en la ventana"
            }, Actualizador.CambiosDe(CuerpoDeRelease));
        }

        /// <summary>Sin cuerpo o sin lista no hay cambios que mostrar, y no es un error.</summary>
        [TestMethod]
        public void SinListaNoHayCambios()
        {
            Assert.AreEqual(0, Actualizador.CambiosDe(null).Count);
            Assert.AreEqual(0, Actualizador.CambiosDe("Solo un parrafo.").Count);
        }

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

    /// <summary>
    /// Cubre que un packageId con el sufijo "_steam" encuentre a su mod.
    ///
    /// Cuando no lo encuentra no falla nada visible: el patch sale con ##packageId## en vez del
    /// nombre, el PatchOperationFindMod no coincide nunca y la traduccion no se aplica ni con el
    /// mod puesto. Pasaba con siete mods de AobaKuma que piden CETeam.CombatExtended_steam.
    /// </summary>
    /// <summary>
    /// El cache de defs de referencia y la lectura en paralelo no pueden cambiar la extraccion.
    ///
    /// Las dos cosas se pusieron para que «Actualizar todo RML» no reparsee los 1558 archivos
    /// del juego base una vez por mod, que eran 408.196 parseos donde alcanzaban 1558. Pero
    /// tocan justo el armado de la base de defs: si el arbol combinado sale distinto, las
    /// traducciones salen distintas y nada falla.
    /// </summary>
    [TestClass]
    public class CacheDeDefsTests
    {
        private const string Idioma = "SpanishLatin (Español(Latinoamérica))";

        /// <summary>
        /// La misma extraccion, con el cache apagado y con el cache ya lleno, tiene que dar
        /// exactamente las mismas entradas y en el mismo orden.
        /// </summary>
        [TestMethod]
        public void ElCacheNoCambiaLaExtraccion()
        {
            Prefabs.Init();
            Prefabs.TranslationLanguage = Idioma;

            if (!Directory.Exists(Path.Combine(Prefabs.PathRimworld, "Data", "Core")))
                Assert.Inconclusive("Este test necesita RimWorld instalado.");

            // Un mod con Defs propios y con dependencias, que es lo que hace pasar por
            // LoadReferenceDefs, que es lo que el cache toca.
            var mod = ModLister.AllMods.FirstOrDefault(x =>
                !x.IsOfficialContent &&
                ModLister.GetExtractableFolders(x).Any(f => Path.GetFileName(f.FolderName) == "Defs"));

            if (mod == null)
                Assert.Inconclusive("No hay ningun mod con Defs instalado.");

            var sinCache = Extraer(mod);

            CacheDeDefs.Abrir();
            try
            {
                // Dos veces: la primera llena el cache y la segunda lo usa, que es la
                // situacion real a partir del segundo mod de la corrida.
                Extraer(mod);
                var conCache = Extraer(mod);

                Assert.AreEqual(sinCache.Count, conCache.Count,
                    "el cache cambio cuantas entradas salen de la extraccion");

                for (var i = 0; i < sinCache.Count; i++)
                {
                    Assert.AreEqual(sinCache[i].ClassName, conCache[i].ClassName, $"entrada {i}");
                    Assert.AreEqual(sinCache[i].Node, conCache[i].Node, $"entrada {i}");
                    Assert.AreEqual(sinCache[i].Original, conCache[i].Original,
                        $"entrada {i}: {sinCache[i].Node}");
                    Assert.AreEqual(sinCache[i].Translated, conCache[i].Translated,
                        $"entrada {i}: {sinCache[i].Node}");
                }
            }
            finally
            {
                CacheDeDefs.Cerrar();
            }

            Assert.IsFalse(CacheDeDefs.Encendido, "el cache quedo encendido despues de cerrarlo");
        }

        /// <summary>Una extraccion completa de ese mod, como la que hace la corrida por lotes.</summary>
        private static List<TranslationEntry> Extraer(ModMetadata mod)
        {
            var extraibles = ModLister.GetExtractableFolders(mod).AutoSeleccionables();
            var referencias = ModLister.FindAllReferenceMods(mod)
                .Concat(ModLister.FindModsNamedInPatches(extraibles))
                .Distinct()
                .ToList();

            return Extractor.ExtractTranslationData(mod, extraibles, referencias);
        }
    }

    /// <summary>
    /// Un mod que no trae Defs propios y solo modifica los del juego con Patches. Better
    /// Transhumanists es asi: le agrega etapas a un pensamiento de Ideology. La extraccion
    /// cargaba las referencias solo si el mod tenia Defs, asi que sus patches no encontraban
    /// el def y el mod salia con «nada que extraer».
    /// </summary>
    [TestClass]
    public class ModSoloDePatchesTests
    {
        private const string Idioma = "SpanishLatin (Español(Latinoamérica))";

        [TestMethod]
        public void ExtraeLosPatchesDeUnModSinDefsPropios()
        {
            Prefabs.Init();
            Prefabs.TranslationLanguage = Idioma;

            if (!Directory.Exists(Path.Combine(Prefabs.PathRimworld, "Data", "Core")))
                Assert.Inconclusive("Este test necesita RimWorld instalado.");

            var raiz = Path.Combine(Path.GetTempPath(), "ModSoloDePatches-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(raiz, "Patches"));
            File.WriteAllText(Path.Combine(raiz, "Patches", "Prueba.xml"), """
                <?xml version="1.0" encoding="utf-8"?>
                <Patch>
                  <Operation Class="PatchOperationReplace">
                    <xpath>/Defs/ThingDef[defName="Steel"]/label</xpath>
                    <value>
                      <label>acero de prueba</label>
                    </value>
                  </Operation>
                </Patch>
                """);

            try
            {
                var mod = new ModMetadata(raiz, "1", "Solo patches", "prueba.solopatches", false);
                var extraibles = ModLister.GetExtractableFolders(mod).AutoSeleccionables();
                var referencias = ModLister.FindAllReferenceMods(mod).ToList();

                var extraccion = Extractor.ExtractTranslationData(mod, extraibles, referencias);

                Assert.IsTrue(extraccion.Any(x => x.Node.EndsWith("Steel.label") && x.Original == "acero de prueba"),
                    "el patch sobre un def del juego no se extrajo: " +
                    string.Join(", ", extraccion.Select(x => $"{x.ClassName}:{x.Node}")));
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }

        /// <summary>
        /// El mismo archivo de Patches en la raiz y en la carpeta de la version. El juego carga
        /// solo el de la version, como con cualquier archivo de igual ruta relativa. Better
        /// Transhumanists trae asi su patch principal, que agrega ocho etapas: aplicando los dos,
        /// la extraccion inventaba ocho etapas mas que en el juego no existen.
        /// </summary>
        [TestMethod]
        public void ElPatchDeLaVersionTapaAlDeLaRaizConElMismoNombre()
        {
            Prefabs.Init();
            Prefabs.TranslationLanguage = Idioma;

            if (!Directory.Exists(Path.Combine(Prefabs.PathRimworld, "Data", "Core")))
                Assert.Inconclusive("Este test necesita RimWorld instalado.");

            var raiz = Path.Combine(Path.GetTempPath(), "ModSoloDePatches-" + Guid.NewGuid().ToString("N"));
            void Patch(string carpeta, string label)
            {
                Directory.CreateDirectory(Path.Combine(raiz, carpeta));
                File.WriteAllText(Path.Combine(raiz, carpeta, "Prueba.xml"), $"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <Patch>
                      <Operation Class="PatchOperationAdd">
                        <xpath>/Defs/ThoughtDef[defName="AteWithoutTable"]/stages</xpath>
                        <value>
                          <li>
                            <label>{label}</label>
                          </li>
                        </value>
                      </Operation>
                    </Patch>
                    """);
            }
            Patch("Patches", "de la raiz");
            Patch(Path.Combine("1.4", "Patches"), "de la version");

            try
            {
                var mod = new ModMetadata(raiz, "1", "Solo patches", "prueba.solopatches", false);
                var extraibles = ModLister.GetExtractableFolders(mod).AutoSeleccionables();
                var referencias = ModLister.FindAllReferenceMods(mod).ToList();

                var originales = Extractor.ExtractTranslationData(mod, extraibles, referencias)
                    .Select(x => x.Original)
                    .ToList();

                CollectionAssert.Contains(originales, "de la version");
                CollectionAssert.DoesNotContain(originales, "de la raiz",
                    "se aplico tambien el patch de la raiz, que el juego no carga");
            }
            finally
            {
                Directory.Delete(raiz, true);
            }
        }
    }

    [TestClass]
    public class ModsConPackageIdTests
    {
        private static readonly ModMetadata Ce =
            new(@"D:\mods\ce", "2890901044", "Combat Extended", "CETeam.CombatExtended", false);

        /// <summary>El caso real: el About.xml de CE no lleva el sufijo que piden los patches.</summary>
        [TestMethod]
        public void EncuentraAlModAunqueElPackageIdTraigaElSufijoSteam()
        {
            var encontrados = ModLister.ModsConPackageId(new[] { Ce }, "CETeam.CombatExtended_steam");

            Assert.AreEqual(Ce, encontrados.Single());
        }

        /// <summary>
        /// Si hay un mod que se llama exactamente asi, gana ese: el sufijo es solo el ultimo
        /// recurso, no una forma de unificar dos mods distintos.
        /// </summary>
        [TestMethod]
        public void SiHayUnoConElSufijoGanaEse()
        {
            var conSufijo = new ModMetadata(@"D:\mods\otro", "1", "Otro", "CETeam.CombatExtended_steam", false);

            var encontrados = ModLister.ModsConPackageId(new[] { Ce, conSufijo }, "CETeam.CombatExtended_steam");

            Assert.AreEqual(conSufijo, encontrados.Single());
        }

        /// <summary>Un mod que no esta instalado sigue sin encontrarse, con sufijo o sin el.</summary>
        [TestMethod]
        public void UnPackageIdQueNoEstaNoEncuentraNada()
        {
            Assert.AreEqual(0, ModLister.ModsConPackageId(new[] { Ce }, "CerebrexNodeUnleashed.Sentinel").Count);
            Assert.AreEqual(0, ModLister.ModsConPackageId(new[] { Ce }, "CerebrexNodeUnleashed.Sentinel_steam").Count);
        }
    }

    /// <summary>
    /// Cubre que carpeta de version se extrae. RimWorld carga la de la version actual y, si el
    /// mod no la trae, la mas cercana hacia atras. Al salir la 1.7 casi ningun mod tenia 1.7/:
    /// pidiendo la version exacta, la extraccion no tomaba ninguna carpeta de version.
    /// </summary>
    [TestClass]
    public class VersionQueCargaTests
    {
        private static List<string> AutoSeleccionadas(string version, params string[] carpetas)
        {
            var raiz = Path.Combine(Path.GetTempPath(), "mod-" + Guid.NewGuid().ToString("N"));
            var anterior = Prefabs.CurrentVersion;
            try
            {
                foreach (var carpeta in carpetas)
                    Directory.CreateDirectory(Path.Combine(raiz, carpeta));
                Prefabs.Init();
                Prefabs.CurrentVersion = version;

                var mod = new ModMetadata(raiz, "1", "Mod de prueba", "prueba.mod", false);
                return ModLister.GetExtractableFolders(mod).AutoSeleccionables()
                    .Select(x => x.FolderName.Replace('\\', '/'))
                    .OrderBy(x => x)
                    .ToList();
            }
            finally
            {
                Prefabs.CurrentVersion = anterior;
                Directory.Delete(raiz, true);
            }
        }

        [TestMethod]
        public void SinCarpetaDeLaVersionActualTomaLaAnteriorMasCercana()
        {
            var elegidas = AutoSeleccionadas("1.7", "Defs", "1.5/Defs", "1.6/Defs", "1.6/Patches");

            CollectionAssert.AreEqual(new[] { "1.6/Defs", "1.6/Patches", "Defs" }, elegidas);
        }

        [TestMethod]
        public void ConCarpetaDeLaVersionActualTomaEsa()
        {
            var elegidas = AutoSeleccionadas("1.7", "1.6/Defs", "1.7/Defs");

            CollectionAssert.AreEqual(new[] { "1.7/Defs" }, elegidas);
        }

        /// <summary>El juego no carga una carpeta de una version que todavia no salio.</summary>
        [TestMethod]
        public void NoTomaUnaVersionPosterior()
        {
            var elegidas = AutoSeleccionadas("1.7", "Common/Defs", "1.8/Defs");

            CollectionAssert.AreEqual(new[] { "Common/Defs" }, elegidas);
        }

        /// <summary>1.10 es posterior a 1.9: se comparan como versiones, no como texto.</summary>
        [TestMethod]
        public void ComparaComoVersionYNoComoTexto()
        {
            var elegidas = AutoSeleccionadas("1.10", "1.9/Defs", "1.10/Defs");

            CollectionAssert.AreEqual(new[] { "1.10/Defs" }, elegidas);
        }
    }

    /// <summary>
    /// El log de la ultima extraccion: el archivo trae todo, la ventana solo lo que sirve
    /// mirar, y lo tecnico llega marcado para que la ventana lo pueda dejar afuera.
    /// </summary>
    [TestClass]
    public class LogDetalladoTests
    {
        private static string ArchivoTemporal()
            => Path.Combine(Path.GetTempPath(), "log-" + Guid.NewGuid().ToString("N") + ".log");

        private static string Leer(string ruta)
        {
            Log.CerrarArchivo();
            try
            {
                return File.ReadAllText(ruta);
            }
            finally
            {
                File.Delete(ruta);
            }
        }

        /// <summary>
        /// Cada extraccion empieza el archivo de cero. Si se agregara al final, con una
        /// corrida de todo RML encima el archivo de la ultima seria imposible de leer.
        /// </summary>
        [TestMethod]
        public void ElArchivoSoloTieneLaUltimaExtraccion()
        {
            var ruta = ArchivoTemporal();

            Log.EmpezarArchivo(ruta);
            Log.Msg("primera extraccion");
            Log.EmpezarArchivo(ruta);
            Log.Msg("segunda extraccion");

            var texto = Leer(ruta);
            StringAssert.Contains(texto, "segunda extraccion");
            Assert.IsFalse(texto.Contains("primera extraccion"), texto);
        }

        /// <summary>
        /// Lo que la ventana no muestra tiene que estar en el archivo, con el nivel escrito
        /// en palabras y sin los simbolos que usaba antes, que el Bloc de notas no siempre
        /// muestra bien.
        /// </summary>
        [TestMethod]
        public void ElArchivoTraeLoTecnicoConElNivelEnPalabras()
        {
            var ruta = ArchivoTemporal();

            Log.EmpezarArchivo(ruta);
            Log.Det("se cargaron los compats");
            Log.WrnDet("un nodo padre que no aparece");
            Log.Err("un archivo que no se pudo leer");

            var texto = Leer(ruta);
            StringAssert.Contains(texto, "se cargaron los compats");
            StringAssert.Contains(texto, Strings.PrefixWarning + " un nodo padre que no aparece");
            StringAssert.Contains(texto, Strings.PrefixError + " un archivo que no se pudo leer");
            Assert.IsFalse(texto.Contains('✖') || texto.Contains('⚠'), texto);
        }

        /// <summary>
        /// Si la ruta del archivo esta tomada —otra instancia abierta—, abrirlo falla, pero el
        /// log tiene que seguir andando: la ventana avisa con un Log.Wrn justo despues, y si
        /// el archivo anterior quedara cerrado pero puesto, ese aviso tiraria y cortaria la
        /// extraccion.
        /// </summary>
        [TestMethod]
        public void UnArchivoTomadoNoRompeElLog()
        {
            var primera = ArchivoTemporal();
            var tomada = ArchivoTemporal();
            Log.EmpezarArchivo(primera);
            try
            {
                using (new FileStream(tomada, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    Assert.ThrowsExactly<IOException>(() => Log.EmpezarArchivo(tomada));
                    Log.Wrn("no se pudo abrir el log detallado");
                }
            }
            finally
            {
                Log.CerrarArchivo();
                File.Delete(primera);
                File.Delete(tomada);
            }
        }

        /// <summary>
        /// La ventana recibe todo, pero lo tecnico le llega marcado: si no, no tendria como
        /// separarlo de lo que si tiene que mostrar.
        /// </summary>
        [TestMethod]
        public void LoTecnicoLlegaMarcadoALaPantalla()
        {
            var recibidas = new List<LogEntry>();
            Log.Pantalla = recibidas.Add;
            try
            {
                Log.Det("detalle");
                Log.WrnDet("aviso tecnico");
                Log.Msg("resultado del mod");
                Log.Wrn("aviso que pide hacer algo");
            }
            finally
            {
                Log.Pantalla = null;
            }

            CollectionAssert.AreEqual(
                new[] { true, true, false, false },
                recibidas.Select(x => x.SoloArchivo).ToArray());
            Assert.AreEqual(LogLevel.Warning, recibidas[1].Level);
        }
    }

    /// <summary>
    /// Los comps se nombran por su clase, como los nombra el juego, y no por su posicion.
    ///
    /// La posicion depende de que mods esten activos: The Dead Man's Switch II le agrega dos comps
    /// a sus automatroides cuando esta Combat Extended, y con eso DMS_Mech_Noctula.comps.11 pasaba
    /// a ser otro comp. El juego no encontraba el campo y la carta del noctulo salia en ingles,
    /// sin avisar. La clase del comp casi nunca esta en el XML: la fija el constructor de su
    /// CompProperties, y por eso se lee de los .dll.
    /// </summary>
    [TestClass]
    public class CompsPorNombreTests
    {
        private const string Idioma = "SpanishLatin (Español(Latinoamérica))";

        private static string DllDelJuego => Path.Combine(Prefabs.PathRimworld, "Assembly-CSharp.dll");

        private static string DllDeFortified => Path.Combine(Prefabs.PathWorkshop, "3498575851", "1.7",
            "Assemblies", "Fortified.dll");

        private const string Jaeger = """
            <Defs>
              <ThingDef>
                <defName>DMS_Mech_Jaeger</defName>
                <label>jaeger</label>
                <comps>
                  <li>
                    <compClass>CompColorable</compClass>
                  </li>
                  <li Class="CompProperties_Refuelable">
                    <fuelLabel>Chemfuel</fuelLabel>
                  </li>
                  <li Class="Fortified.CompProperties_Camouflage">
                    <detectedLetterLabel>Jaeger revealed</detectedLetterLabel>
                    <detectedLetterDesc>An enemy automatroid has appeared out of thin air!</detectedLetterDesc>
                  </li>
                </comps>
              </ThingDef>
            </Defs>
            """;

        [TestMethod]
        public void LeeLaClaseQueLeAsignaElConstructor()
        {
            ConLosDll(() =>
            {
                Assert.AreEqual("CompCamouflage", ClasesDeComp.Nombre("Fortified.CompProperties_Camouflage"));
                // Sin namespace, como lo escriben los XML del juego.
                Assert.AreEqual("CompRefuelable", ClasesDeComp.Nombre("CompProperties_Refuelable"));
                Assert.AreEqual("HediffComp_TendDuration", ClasesDeComp.Nombre("HediffCompProperties_TendDuration"));
                // Lo que no es un comp no tiene nombre, aunque exista.
                Assert.IsNull(ClasesDeComp.Nombre("VerbProperties"));
                Assert.IsNull(ClasesDeComp.Nombre("NoExiste.CompProperties_Algo"));
            });
        }

        /// <summary>El caso del jaeger: la carta va por el nombre del comp y recuerda su posicion vieja.</summary>
        [TestMethod]
        public void UnCompSinCompClassSeNombraPorSuClase()
        {
            var entradas = ConLosDll(() => Extraer(Jaeger));

            var carta = entradas.Single(x => x.Node.EndsWith(".detectedLetterDesc"));
            Assert.AreEqual("DMS_Mech_Jaeger.comps.CompCamouflage.detectedLetterDesc", carta.Node);
            Assert.AreEqual("DMS_Mech_Jaeger.comps.2.detectedLetterDesc", carta.NodoAnterior);

            var combustible = entradas.Single(x => x.Node.EndsWith(".fuelLabel"));
            Assert.AreEqual("DMS_Mech_Jaeger.comps.CompRefuelable.fuelLabel", combustible.Node);
        }

        /// <summary>Si no hay .dll que leer, todo queda como antes: por posicion y sin nombre viejo.</summary>
        [TestMethod]
        public void SinLosDllSigueSiendoPorPosicion()
        {
            Prefabs.Init();
            ClasesDeComp.Usar(Array.Empty<string>());

            var carta = Extraer(Jaeger).Single(x => x.Node.EndsWith(".detectedLetterDesc"));
            Assert.AreEqual("DMS_Mech_Jaeger.comps.2.detectedLetterDesc", carta.Node);
            Assert.IsNull(carta.NodoAnterior);
        }

        /// <summary>
        /// Dos comps con el mismo nombre llevan su orden detras de un guion, como en el juego
        /// (TranslationHandleUtility.GetBestHandleWithIndexForListElement).
        /// </summary>
        [TestMethod]
        public void DosCompsConElMismoNombreLlevanSuOrden()
        {
            var entradas = ConLosDll(() => Extraer("""
                <Defs>
                  <ThingDef>
                    <defName>Tanque</defName>
                    <comps>
                      <li Class="CompProperties_Refuelable"><fuelLabel>Chemfuel</fuelLabel></li>
                      <li Class="CompProperties_Refuelable"><fuelLabel>Wood</fuelLabel></li>
                    </comps>
                  </ThingDef>
                </Defs>
                """));

            CollectionAssert.AreEquivalent(
                new[] { "Tanque.comps.CompRefuelable-0.fuelLabel", "Tanque.comps.CompRefuelable-1.fuelLabel" },
                entradas.Select(x => x.Node).ToArray());
        }

        /// <summary>
        /// Lo que estaba traducido con la posicion pasa al nombre, y no queda en UNUSED. El ingles
        /// cambio a proposito: el rescate por texto no lo salvaria, solo el nombre viejo.
        /// </summary>
        [TestMethod]
        public void LaTraduccionPorPosicionPasaAlNombre()
        {
            var nueva = new TranslationEntry("ThingDef", "DMS_Mech_Jaeger.comps.CompCamouflage.detectedLetterDesc",
                "An enemy automatroid has appeared!", null, null, null)
            {
                NodoAnterior = "DMS_Mech_Jaeger.comps.11.detectedLetterDesc"
            };
            var vieja = new TranslationEntry("ThingDef", "DMS_Mech_Jaeger.comps.11.detectedLetterDesc",
                "An enemy automatroid has appeared out of thin air!", "¡Un automatroide enemigo apareció de la nada!",
                null, null);

            var (resultado, sinUso, _) = TranslationMerge.Merge(new[] { nueva }, new[] { vieja });

            Assert.AreEqual("¡Un automatroide enemigo apareció de la nada!", resultado.Single().Translated);
            Assert.AreEqual(0, sinUso.Count, "la traduccion vieja quedo apartada como si sobrara");
        }

        /// <summary>
        /// El caso de Ushankas Luciferium Expansion: el UNUSED ya tenia una traduccion vieja con la
        /// clave por nombre, y en uso estaba la de la clave por posicion. La apartada coincidia
        /// exacto con la clave nueva y le ganaba a la que estaba en uso, que se iba al UNUSED.
        /// </summary>
        [TestMethod]
        public void LaApartadaConLaClaveNuevaNoLeGanaALaQueEstaEnUso()
        {
            var nueva = new TranslationEntry("ThingDef", "Bomba.comps.CompLuciferiumPump.jobString", "activate",
                null, null, null) { NodoAnterior = "Bomba.comps.3.jobString" };
            var apartada = new TranslationEntry("ThingDef", "Bomba.comps.CompLuciferiumPump.jobString", "activate",
                "activando bomba médica de luciferio", null, null) { Apartada = true };
            var enUso = new TranslationEntry("ThingDef", "Bomba.comps.3.jobString", "activate", "activar", null, null);

            var (resultado, sinUso, _) = TranslationMerge.Merge(new[] { nueva }, new[] { apartada, enUso });

            Assert.AreEqual("activar", resultado.Single().Translated);
            Assert.AreEqual("activando bomba médica de luciferio", sinUso.Single().Translated,
                "la apartada tiene que seguir apartada");
        }

        /// <summary>
        /// El caso de GravTech: el mismo nodo llega en uso con la clave nueva y con la vieja (un
        /// Patches de RML releido por su xpath literal), con el mismo texto. La vieja no sobra:
        /// es la misma traduccion, y no tiene que aparecer como copia en el UNUSED.
        /// </summary>
        [TestMethod]
        public void LaCopiaConLaClaveViejaNoQuedaEnElUnused()
        {
            var nueva = new TranslationEntry("Patches.HediffDef", "GravHands.comps.HediffComp_VerbGiver.tools.0.label",
                "grav fist", null, null, null) { NodoAnterior = "GravHands.comps.0.tools.0.label" };
            var conLaNueva = nueva with { Translated = "gravipuño", NodoAnterior = null };
            var conLaVieja = conLaNueva with { Node = "GravHands.comps.0.tools.0.label" };

            var (resultado, sinUso, _) = TranslationMerge.Merge(new[] { nueva }, new[] { conLaVieja, conLaNueva });

            Assert.AreEqual("gravipuño", resultado.Single().Translated);
            Assert.AreEqual(0, sinUso.Count, "quedo una copia de la misma traduccion en el UNUSED");
        }

        /// <summary>
        /// Un Patches no puede buscar el comp por un nombre que el XML no tiene: va por su Class,
        /// y la relectura vuelve al mismo nodo.
        /// </summary>
        [TestMethod]
        public void ElPatchBuscaElCompPorSuClaseYSeRelee()
        {
            var carta = ConLosDll(() => Extraer(Jaeger)).Single(x => x.Node.EndsWith(".detectedLetterDesc"));

            Assert.AreEqual(
                "/Defs/ThingDef[defName=\"DMS_Mech_Jaeger\"]/comps/li[@Class=\"Fortified.CompProperties_Camouflage\"]/detectedLetterDesc",
                carta.Xpath);

            var leida = ConLosDll(() => LeerPatchSuelto(carta.Xpath!, "detectedLetterDesc", "carta"));
            Assert.IsNotNull(leida, "no se pudo releer el xpath por Class");
            Assert.AreEqual(carta.Node, leida.Node);
        }

        /// <summary>
        /// La prueba contra el juego: los comps de Core salen con la misma clave que usa la
        /// traduccion oficial, que es la que genera el propio RimWorld.
        /// </summary>
        [TestMethod]
        public void LosCompsDeCoreSalenComoEnLaTraduccionOficial()
        {
            Prefabs.Init();
            Prefabs.TranslationLanguage = Idioma;
            var core = ModLister.OfficialMods.FirstOrDefault(x => x.PackageId.Equals("ludeon.rimworld", StringComparison.OrdinalIgnoreCase));
            if (core == null || !File.Exists(DllDelJuego))
                Assert.Inconclusive("Este test necesita RimWorld instalado.");

            TraduccionOficial.Limpiar();
            var extraibles = ModLister.GetExtractableFolders(core).AutoSeleccionables();
            // Las herramientas de un comp quedan afuera: el juego tambien las nombra, por su
            // etiqueta (tools.fist.label), pero el extractor todavia las nombra por posicion.
            var comps = Extractor.ExtractTranslationData(core, extraibles, null)
                .Where(x => x.Node.Contains(".comps.") && !x.Node.Contains(".tools."))
                .ToList();

            var sinOficial = comps.Where(x => !TraduccionOficial.Cubre(x.ClassName, x.Node)).ToList();
            Assert.IsTrue(comps.Count > 50, $"solo salieron {comps.Count} campos de comps de Core");
            Assert.AreEqual(0, sinOficial.Count,
                "claves de comps que la traduccion oficial no tiene: " +
                string.Join(", ", sinOficial.Take(10).Select(x => x.Node)));
        }

        private static T ConLosDll<T>(Func<T> hacer)
        {
            Prefabs.Init();
            if (!File.Exists(DllDelJuego) || !File.Exists(DllDeFortified))
                Assert.Inconclusive("Este test necesita RimWorld y Fortified Features Framework instalados.");

            ClasesDeComp.Usar(new[] { DllDelJuego, DllDeFortified });
            try
            {
                return hacer();
            }
            finally
            {
                ClasesDeComp.Usar(Array.Empty<string>());
            }
        }

        private static void ConLosDll(Action hacer) => ConLosDll(() => { hacer(); return 0; });

        private static List<TranslationEntry> Extraer(string defs)
        {
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

        /// <summary>Como el de DesarmarXpathTests: lo relee con la base vacia, solo por la lectura literal.</summary>
        private static TranslationEntry? LeerPatchSuelto(string xpath, string etiqueta, string traduccion)
        {
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
}
