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
        /// Lo mas importante: el que ya existe puede tener reglas de orden o de version
        /// escritas a mano, y pisarlas en silencio seria peor que no generar nada.
        /// </summary>
        [TestMethod]
        public void NoPisaElArchivoQueYaEstaba()
        {
            var carpeta = CarpetaTemporal();
            var archivo = Path.Combine(carpeta, LoadFoldersBuild.FileName);
            try
            {
                File.WriteAllText(archivo, "escrito a mano");

                Assert.IsFalse(LoadFoldersBuild.Write(Mod(), carpeta));
                Assert.AreEqual("escrito a mano", File.ReadAllText(archivo));
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
