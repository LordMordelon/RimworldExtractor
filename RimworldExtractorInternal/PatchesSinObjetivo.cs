using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorInternal
{
    /// <summary>
    /// Informa sobre los patches que no encontraron a que apuntar.
    ///
    /// Un patch que modifica un def de otro mod —por ejemplo, el que le cambia el nombre al
    /// tanque de combustible de Odyssey— solo produce traduccion si ese def esta cargado como
    /// referencia. Si no lo esta, la operacion no da nada y antes eso no se notaba: la
    /// extraccion terminaba bien, simplemente con menos texto del que deberia.
    /// </summary>
    public static class PatchesSinObjetivo
    {
        /// <summary>De un xpath tipo Defs/ThingDef[defName="ChemfuelTank"]/label saca el defName.</summary>
        private static readonly Regex DefNameEnXpath = new("defName=\"([^\"]+)\"", RegexOptions.Compiled);

        /// <summary>Cuantos defs se nombran en el aviso antes de cortar la lista.</summary>
        private const int MaximoQueSeListan = 10;

        /// <summary>
        /// Deja en el log lo que falto y que hacer al respecto. No hace nada si esta vez
        /// resolvieron todos.
        /// </summary>
        public static void Informar(ModMetadata mod)
        {
            var xpaths = PatchOperations.XpathsSinObjetivo;
            if (xpaths.Count == 0)
                return;

            var faltantes = xpaths
                .Select(x => DefNameEnXpath.Match(x))
                .Where(m => m.Success)
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .ToList();

            Log.Wrn(Strings.PatchesWithoutTarget(xpaths.Count));

            if (faltantes.Count == 0)
                return;

            // Que el xpath no encuentre nada tiene dos causas distintas, y confundirlas
            // manda al usuario a cargar un mod que ya tenia cargado: o el def no esta, o
            // esta pero le falta el nodo interno al que apunta el patch —tipicamente
            // porque ese nodo lo agrega otro mod—.
            var cargados = _defsCargados;
            var ausentes = faltantes.Where(x => !cargados.Contains(x)).ToList();
            var conNodoQueNoExiste = faltantes.Count - ausentes.Count;

            if (ausentes.Count > 0)
            {
                var (porMod, sinIdentificar) = BuscarDuenios(ausentes, mod);

                if (porMod.Count > 0)
                    Log.Wrn(Strings.PatchesMissingModsAre(string.Join(", ", porMod)));

                if (sinIdentificar.Count > 0)
                    Log.Wrn(Strings.PatchesUnidentifiedDefs(Resumir(sinIdentificar)));

                Log.Wrn(Strings.PatchesWithoutTargetHint);
            }

            if (conNodoQueNoExiste > 0)
                Log.Wrn(Strings.PatchesTargetNodeMissing(conNodoQueNoExiste));
        }

        /// <summary>Los defName que habia cargados cuando se extrajeron los defs.</summary>
        private static HashSet<string> _defsCargados = new();

        /// <summary>
        /// Toma nota de que defs habia cargados, para poder distinguir despues «el def no
        /// esta» de «el def esta pero el nodo al que apunta el patch, no».
        ///
        /// Hay que llamarla justo despues de extraer los defs: al terminar de procesar los
        /// patches, DoXmlInheritance reemplaza la base entera por una que solo tiene los
        /// defs que agregaron los patches, y mirarla ahi no diria nada util.
        /// </summary>
        internal static void RegistrarDefsCargados(System.Xml.XmlDocument? defs)
        {
            _defsCargados = new HashSet<string>();
            var raiz = defs?.DocumentElement;
            if (raiz == null)
                return;

            foreach (System.Xml.XmlNode nodo in raiz.ChildNodes)
            {
                var defName = nodo["defName"]?.InnerText;
                if (!string.IsNullOrWhiteSpace(defName))
                    _defsCargados.Add(defName.Trim());
            }
        }

        /// <summary>
        /// Busca de que mod son los defs que faltan, entre los mods relacionados con el que se
        /// esta extrayendo. Es un conjunto acotado y esto corre solo cuando hubo fallos, asi
        /// que no cuesta nada en el caso normal.
        /// </summary>
        private static (List<string> PorMod, List<string> SinIdentificar) BuscarDuenios(
            List<string> faltantes, ModMetadata mod)
        {
            var pendientes = new HashSet<string>(faltantes);
            var duenios = new List<string>();

            foreach (var candidato in ModLister.FindAllReferenceMods(mod))
            {
                if (pendientes.Count == 0)
                    break;

                var suyos = DefNamesDe(candidato);
                var encontrados = pendientes.Where(suyos.Contains).ToList();
                if (encontrados.Count == 0)
                    continue;

                duenios.Add(candidato.ModName.Trim());
                foreach (var encontrado in encontrados)
                    pendientes.Remove(encontrado);
            }

            return (duenios, pendientes.ToList());
        }

        /// <summary>Los defName que define un mod, leyendo sus carpetas Defs.</summary>
        private static HashSet<string> DefNamesDe(ModMetadata mod)
        {
            var nombres = new HashSet<string>();
            var carpetas = ModLister.GetExtractableFolders(mod)
                .Where(x => Path.GetFileName(x.FolderName) == "Defs");

            foreach (var carpeta in carpetas)
            {
                foreach (var archivo in IO.DescendantFiles(carpeta.FullPath)
                             .Where(x => x.ToLower().EndsWith(".xml")))
                {
                    try
                    {
                        // Con leer el texto alcanza: buscar el defName no necesita parsear el
                        // XML, y hacerlo seria bastante mas lento sobre todo Core.
                        foreach (Match match in Regex.Matches(File.ReadAllText(archivo), "<defName>([^<]+)</defName>"))
                            nombres.Add(match.Groups[1].Value.Trim());
                    }
                    catch
                    {
                        // Un archivo ilegible no puede hacer fallar un aviso.
                    }
                }
            }

            return nombres;
        }

        private static string Resumir(List<string> defNames)
        {
            var listados = defNames.Take(MaximoQueSeListan).ToList();
            var texto = string.Join(", ", listados);
            return defNames.Count > listados.Count
                ? $"{texto} (y {defNames.Count - listados.Count} más)"
                : texto;
        }
    }
}
