using System.IO;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorInternal
{
    /// <summary>
    /// Escribe el LoadFolders.Build.yaml que RML necesita para enganchar una traduccion
    /// al mod al que corresponde.
    ///
    /// Existe porque los tres datos que lleva ese archivo —el packageId, el id del
    /// workshop y el nombre del mod— ya los tiene el extractor: escribirlos de nuevo a
    /// mano solo agrega una oportunidad de equivocarse, y equivocarse ahi no rompe nada
    /// visible, simplemente la traduccion no se carga nunca.
    ///
    /// Se genera a mano y no con una libreria de YAML: el archivo es chico, de forma fija,
    /// y no vale la pena una dependencia mas para producirlo.
    /// </summary>
    public static class LoadFoldersBuild
    {
        public const string FileName = "LoadFolders.Build.yaml";

        /// <summary>
        /// Deja el archivo junto a la carpeta Languages recien generada, rehaciendolo si
        /// ya estaba. Devuelve si lo escribio.
        /// </summary>
        public static bool Write(ModMetadata? mod, string rootDirPath)
        {
            // El contenido oficial no lleva esta regla: su packageId es de Ludeon y en RML
            // se engancha de otra manera.
            if (mod is null || mod.IsOfficialContent || string.IsNullOrWhiteSpace(mod.PackageId))
                return false;

            var destino = Path.Combine(rootDirPath, FileName);

            // Se rehace en cada extraccion, asi que siempre refleja los datos actuales del
            // mod. Si se extrae directamente sobre una carpeta de RML, las reglas de orden
            // o de version que se hayan escrito a mano ahi se pierden.
            File.WriteAllText(destino, Contents(mod));
            Log.Msg(Strings.LoadFoldersYamlWritten(FolderNameFor(mod)));
            return true;
        }

        /// <summary>
        /// Como tiene que llamarse la carpeta dentro de Data/ en RML. Es el mismo formato
        /// que ya usa <see cref="ModMetadata.Identifier"/>.
        /// </summary>
        public static string FolderNameFor(ModMetadata mod) => mod.Identifier.StripInvaildChars();

        /// <summary>El texto del archivo. Separado para poder probarlo sin tocar el disco.</summary>
        public static string Contents(ModMetadata mod)
        {
            // Un mod local no tiene id del workshop: el campo queda vacio, que es lo mismo
            // que hace un LoadFolders.Build.yaml escrito a mano para un mod asi.
            var workshopId = mod.Id == "???" ? string.Empty : mod.Id;

            return $"""
                    BuildRule:
                      Binding:
                        PackageID: ["{Valor(mod.PackageId)}"]
                        Mode: "None"
                        Dependency: "Independent"
                      Order:
                        After:
                        Before:
                      Version:
                        Default:
                        LeftBoundary:
                        RightBoundary:
                        Designate:
                        Ban:
                    Metadata:
                      WorkshopID: "{Valor(workshopId)}"
                      ModName: "{Valor(mod.ModName)}"

                    """;
        }

        /// <summary>
        /// Prepara un valor para meterlo entre comillas dobles en el YAML.
        ///
        /// Recorta los espacios de los bordes —hay mods cuyo About.xml trae el nombre con
        /// un espacio adelante— y escapa lo que romperia la cadena entrecomillada.
        /// </summary>
        private static string Valor(string valor) => valor
            .Trim()
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }
}
