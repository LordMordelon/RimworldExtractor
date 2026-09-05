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

        [ClassInitialize]
        public static void Setup(TestContext _)
        {
            Prefabs.Init();
            ModLister.ResetCache();
            _mod = ModLister.WorkshopMods.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.ModName));
        }

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

        [TestMethod]
        public void DevuelveNullCuandoElNombreNoCorrespondeANingunMod()
        {
            var encontrado = TranslationAnalyzerTool.GetModMetadataFromFilePath(
                @"Data\esto-no-es-un-mod-000000\a.xlsx");
            Assert.IsNull(encontrado);
        }
    }
}
