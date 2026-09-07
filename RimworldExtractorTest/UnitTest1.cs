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
}
