using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

        /// <summary>Ruta del proyecto del builder dentro de un clon de RML.</summary>
        private static string ProyectoDelBuilder(string rmlPath)
            => Path.Combine(rmlPath, "Source", "LoadFoldersBuilder", "LoadFoldersBuilder.csproj");

        /// <summary>Cuanto se espera al builder antes de darlo por colgado.</summary>
        private static readonly TimeSpan Paciencia = TimeSpan.FromMinutes(3);

        /// <summary>Los codigos de color ANSI que escribe el builder, que en el log estorban.</summary>
        private static readonly Regex Colores = new(@"\x1b\[[0-9;]*m", RegexOptions.Compiled);

        /// <summary>Cuantas lineas de la salida del builder se muestran cuando falla.</summary>
        private const int MaximoDeLineasDeError = 10;

        /// <summary>
        /// Rehace el LoadFolders.xml y el ModList.tsv de RML corriendo su LoadFoldersBuilder.
        ///
        /// Hace falta porque el yaml de una carpeta no alcanza: RimWorld lee el
        /// LoadFolders.xml, y ese lo arma el builder a partir de todos los yaml. Sin esto un
        /// mod recien agregado no carga, y no hay ningun sintoma que lo explique.
        ///
        /// Nunca tira: la traduccion ya esta escrita en disco y sigue siendo valida aunque el
        /// indice quede viejo. Lo peor que puede pasar es un aviso en el log.
        /// </summary>
        public static void Regenerar(string rmlPath)
        {
            if (string.IsNullOrWhiteSpace(rmlPath))
                return;

            // La ruta de RML puede apuntar a una copia del mod dentro de Mods/, sin el
            // codigo fuente al lado. Ahi no hay nada que correr.
            var proyecto = ProyectoDelBuilder(rmlPath);
            if (!File.Exists(proyecto))
            {
                Log.Wrn(Strings.LoadFoldersBuilderNotFound);
                return;
            }

            try
            {
                var codigo = Correr(proyecto, rmlPath, out var salida);
                if (codigo == 0)
                {
                    Log.Msg(Strings.LoadFoldersRebuilt);
                    return;
                }

                Log.Wrn(Strings.LoadFoldersBuilderFailed(codigo));
                foreach (var linea in salida)
                    Log.Wrn(linea);
            }
            catch (Exception e)
            {
                // Tipicamente: no hay dotnet en el PATH. Es informacion util, no un fallo
                // de la extraccion.
                Log.Wrn(Strings.LoadFoldersBuilderError(e.Message));
            }
        }

        /// <summary>
        /// Lanza el builder y devuelve su codigo de salida, dejando en <paramref name="salida"/>
        /// las ultimas lineas de lo que escribio.
        /// </summary>
        private static int Correr(string proyecto, string rmlPath, out List<string> salida)
        {
            var arranque = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = rmlPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // Por lista y no como una sola cadena: la ruta de RML puede tener espacios.
            foreach (var argumento in new[] { "run", "--project", proyecto, "-c", "Release", "--", "-build" })
                arranque.ArgumentList.Add(argumento);

            using var proceso = Process.Start(arranque);
            if (proceso is null)
            {
                salida = new List<string>();
                return -1;
            }

            // Se lee antes de esperar: si la tuberia se llena, el hijo se bloquea
            // escribiendo y los dos quedan esperando al otro.
            var texto = proceso.StandardOutput.ReadToEnd() + proceso.StandardError.ReadToEnd();

            if (!proceso.WaitForExit((int)Paciencia.TotalMilliseconds))
            {
                proceso.Kill(true);
                salida = new List<string>();
                return -2;
            }

            salida = texto
                .Split('\n')
                .Select(x => Colores.Replace(x, string.Empty).TrimEnd('\r').Trim())
                .Where(x => x.Length > 0)
                .TakeLast(MaximoDeLineasDeError)
                .ToList();

            return proceso.ExitCode;
        }

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
