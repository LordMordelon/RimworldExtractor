using System.Text.RegularExpressions;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorInternal
{
    /// <summary>
    /// Mantiene agrupadas por autor las carpetas de Data/ en RML.
    ///
    /// Los mods de un mismo autor con cuatro traducciones o mas viven en Data/!Autor/. El "!"
    /// las fija arriba: en orden ordinal es 33, antes que el "[" de las carpetas [FSF], [HRK]
    /// o [sbz] que ya existen, asi que las carpetas de autor no quedan mezcladas entre los
    /// mods sueltos.
    ///
    /// Esto se hizo una vez a mano y despues se desactualizaba solo: cada mod nuevo caia plano
    /// en Data/ y nadie se enteraba hasta revisar. Ahora vive en el flujo de la extraccion.
    ///
    /// El autor sale del About.xml del mod instalado, no del packageId. No son lo mismo: los
    /// mods de Oskar Potocki usan al menos cuatro prefijos distintos —VanillaExpanded,
    /// OskarPotocki, vanillaracesexpanded, VE— y agrupar por prefijo los partiria en cuatro.
    /// </summary>
    public static class Agrupador
    {
        /// <summary>
        /// Desde cuantos mods vale la pena darle carpeta propia a un autor. Agrupar de a uno o
        /// dos no ordena nada y agrega un nivel de carpeta para nada.
        /// </summary>
        public const int Umbral = 4;

        /// <summary>Lo que llevan adelante las carpetas de autor para quedar arriba de todo.</summary>
        public const string Prefijo = "!";

        /// <summary>
        /// Como viene declarada una lista de autores en un About.xml. El " and " pide espacios
        /// a los dos lados para no partir al medio a alguien que se llame "Anderson".
        /// </summary>
        private static readonly Regex Separadores =
            new(@"\s*[,;/&]\s*|\s+and\s+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Con quien se agrupa un mod que declara varios autores: con el primero.
        ///
        /// Los colaborativos de Vanilla Expanded se firman "Oskar Potocki, Sarg Bjornson" o
        /// "Oskar Potocki, Taranchuk", y tratar cada combinacion como un autor distinto parte
        /// los mods de Oskar Potocki en varios grupos de los cuales ninguno llega al umbral.
        /// </summary>
        public static string AutorPrincipal(string? autor)
        {
            if (string.IsNullOrWhiteSpace(autor))
                return "";

            return Separadores.Split(autor.Trim())
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim() ?? "";
        }

        /// <summary>
        /// Con que se comparan dos autores. Sin mayusculas y sin nada que no sea letra o
        /// numero, porque "Oskar Potocki" y "OskarPotocki" son la misma persona y firmando
        /// distinto contaban como dos.
        ///
        /// Letra en cualquier alfabeto, no solo a-z: con a-z un autor que firma en japones o
        /// en chino se quedaba sin clave y nunca se agrupaba.
        /// </summary>
        public static string Clave(string? autor)
            => string.Concat(AutorPrincipal(autor).ToLowerInvariant().Where(char.IsLetterOrDigit));

        /// <summary>
        /// La carpeta Data/!Autor/ de este autor, si ya existe. Null si el autor no tiene
        /// carpeta propia, que es el caso de los autores con uno a tres mods.
        ///
        /// Se compara por <see cref="Clave"/> y no por nombre exacto para que el nombre de la
        /// carpeta no tenga que coincidir caracter por caracter con el About.xml.
        /// </summary>
        public static string? CarpetaDeAutor(string? autor, string rmlPath)
        {
            var clave = Clave(autor);
            if (clave.Length == 0)
                return null;

            var data = Path.Combine(rmlPath, "Data");
            if (!Directory.Exists(data))
                return null;

            try
            {
                return Directory.EnumerateDirectories(data, Prefijo + "*", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(x => Clave(Path.GetFileName(x)[Prefijo.Length..]) == clave);
            }
            catch
            {
                // Una carpeta ilegible no puede voltear una extraccion: sin agrupar tambien anda.
                return null;
            }
        }

        /// <summary>Un autor que tiene traducciones sueltas en Data/ pudiendo estar agrupadas.</summary>
        /// <param name="Autor">Como lo firma su About.xml.</param>
        /// <param name="Carpeta">Su Data/!Autor/, o null si todavia no tiene.</param>
        /// <param name="Sueltos">Las carpetas que estan colgando derecho de Data/.</param>
        public record Pendiente(string Autor, string? Carpeta, List<string> Sueltos);

        /// <summary>
        /// Que autores llegan al umbral y todavia tienen alguna traduccion suelta en Data/.
        ///
        /// No toca nada: solo mira. El autor de cada carpeta se resuelve por el packageId que
        /// ya guarda su LoadFolders.Build.yaml, asi que un mod desinstalado simplemente no
        /// cuenta, en vez de contar como un autor vacio.
        /// </summary>
        public static List<Pendiente> Revisar(string rmlPath) => Revisar(rmlPath, ModLister.AllMods);

        /// <summary>
        /// Lo mismo, contra una lista de mods dada en vez de la instalacion. Existe para poder
        /// probar el agrupado sin depender de los mods que haya en la maquina.
        /// </summary>
        internal static List<Pendiente> Revisar(string rmlPath, IEnumerable<ModMetadata> mods)
        {
            var data = Path.Combine(rmlPath, "Data");
            if (!Directory.Exists(data))
                return new List<Pendiente>();

            // Igual que en la actualizacion por lotes: el indice se arma una sola vez porque
            // resolverlo por carpeta significaria releer cientos de About.xml.
            var porPackageId = new Dictionary<string, ModMetadata>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in mods)
            {
                if (!string.IsNullOrWhiteSpace(mod.PackageId))
                    porPackageId.TryAdd(mod.PackageId.Trim(), mod);
            }

            var porAutor = new Dictionary<string, (string Autor, List<string> Carpetas)>();
            foreach (var yaml in Directory.GetFiles(data, LoadFoldersBuild.FileName, SearchOption.AllDirectories))
            {
                var packageId = LoadFoldersBuild.PackageIdDe(yaml);
                if (packageId is null || !porPackageId.TryGetValue(packageId, out var mod))
                    continue;

                var clave = Clave(mod.Author);
                if (clave.Length == 0)
                    continue;

                if (!porAutor.TryGetValue(clave, out var grupo))
                    porAutor[clave] = grupo = (AutorPrincipal(mod.Author), new List<string>());
                grupo.Carpetas.Add(Path.GetDirectoryName(yaml)!);
            }

            var pendientes = new List<Pendiente>();
            foreach (var (_, grupo) in porAutor)
            {
                if (grupo.Carpetas.Count < Umbral)
                    continue;

                // Suelta es la que cuelga derecho de Data/. Las que ya estan en una carpeta de
                // autor —o en cualquier otra agrupacion— se dejan donde estan.
                var sueltos = grupo.Carpetas
                    .Where(x => string.Equals(Path.GetDirectoryName(x), data, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (sueltos.Count == 0)
                    continue;

                pendientes.Add(new Pendiente(grupo.Autor, CarpetaDeAutor(grupo.Autor, rmlPath), sueltos));
            }

            return pendientes
                .OrderBy(x => x.Autor, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Mueve a su carpeta de autor las traducciones sueltas de los autores que ya tienen
        /// una, y avisa por log de los que llegaron al umbral sin tenerla.
        ///
        /// La diferencia entre los dos casos es a proposito. Mover un mod suelto a una carpeta
        /// que ya existe es lo que el propio extractor habria hecho de haber estado la carpeta
        /// cuando se extrajo por primera vez, y se deshace con un git mv. Crear una carpeta
        /// nueva y meterle cuatro traducciones adentro cambia la forma de Data/ y es una
        /// decision, no un acomodo: eso se avisa y lo hace una persona.
        ///
        /// Devuelve cuantas movio. Nunca tira: la traduccion ya esta escrita y es valida este
        /// donde este, asi que un problema al mover no puede voltear la extraccion.
        /// </summary>
        public static int Reagrupar(string rmlPath) => Reagrupar(rmlPath, ModLister.AllMods);

        /// <summary>Lo mismo, contra una lista de mods dada. Ver <see cref="Revisar(string, IEnumerable{ModMetadata})"/>.</summary>
        internal static int Reagrupar(string rmlPath, IEnumerable<ModMetadata> mods)
        {
            var movidas = 0;
            foreach (var pendiente in Revisar(rmlPath, mods))
            {
                if (pendiente.Carpeta is null)
                {
                    Log.Msg(Strings.AutorSinAgrupar(pendiente.Autor, pendiente.Sueltos.Count));
                    continue;
                }

                foreach (var suelto in pendiente.Sueltos)
                {
                    var nombre = Path.GetFileName(suelto);
                    var carpeta = Path.GetFileName(pendiente.Carpeta);
                    var destino = Path.Combine(pendiente.Carpeta, nombre);

                    if (Directory.Exists(destino))
                    {
                        // Estaria pisando una traduccion. No es un caso esperable, pero
                        // resolverlo a ciegas seria elegir cual de las dos se pierde.
                        Log.Wrn(Strings.AgrupadoDuplicado(nombre, carpeta));
                        continue;
                    }

                    try
                    {
                        Directory.Move(suelto, destino);
                        Log.Msg(Strings.Agrupado(nombre, carpeta));
                        movidas++;
                    }
                    catch (Exception e)
                    {
                        Log.Wrn(Strings.AgrupadoFallo(nombre, e.Message));
                    }
                }
            }

            return movidas;
        }
    }
}
