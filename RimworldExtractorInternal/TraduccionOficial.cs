using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Linq;
using System.Xml;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorInternal
{
    /// <summary>
    /// Que nodos ya trae traducidos el propio juego en el idioma de destino.
    ///
    /// RimWorld aplica las PatchOperation antes de inyectar los DefInjected, asi que una
    /// traduccion escrita como patch la pisa la oficial y en pantalla queda el texto viejo.
    /// No hay orden de mods que lo arregle: son dos etapas distintas de la carga, y la de los
    /// DefInjected va ultima. Lo unico que le gana a un DefInjected es otro DefInjected, y el
    /// de RML gana porque los DLC cargan siempre antes que cualquier mod.
    ///
    /// Esta tabla es la que deja reconocer esos casos para emitirlos como DefInjected en vez
    /// de como patch. Se consulta desde IO.ToLanguageXml.
    /// </summary>
    public static class TraduccionOficial
    {
        /// <summary>
        /// (carpeta de DefInjected, nombre del nodo) -> cuantos li declara, 0 si no es una
        /// inyeccion de lista entera.
        /// </summary>
        private static Dictionary<(string Clase, string Nodo), int> _claves = new();

        /// <summary>
        /// Para que ruta de RimWorld y que idioma se cargo la tabla. Son unas 28.000 claves
        /// repartidas en seis .tar, y «Actualizar todo RML» recorre decenas de mods: sin
        /// cachear se reparsean todos en cada uno.
        /// </summary>
        private static string? _cargadaPara;

        /// <summary>Los tests arman la tabla a mano y no tienen que leer el disco.</summary>
        private static bool _sembrada;

        /// <summary>Si el juego ya traduce ese nodo exacto.</summary>
        public static bool Cubre(string clase, string nodo)
        {
            Cargar();
            return _claves.ContainsKey((clase, nodo));
        }

        /// <summary>
        /// Cuantos elementos declara la inyeccion de lista entera de ese nodo, o null si esa
        /// clave no existe o no es una lista.
        ///
        /// Hace falta porque una inyeccion de lista reemplaza la lista completa: si se emite
        /// con menos elementos de los que tiene, RimWorld avisa por conteo y no aplica nada.
        /// </summary>
        public static int? CantidadDeLista(string clase, string nodoLista)
        {
            Cargar();
            return _claves.TryGetValue((clase, nodoLista), out var cantidad) && cantidad > 0
                ? cantidad
                : null;
        }

        internal static void Sembrar(IEnumerable<(string Clase, string Nodo, int Elementos)> claves)
        {
            _claves = claves.ToDictionary(x => (x.Clase, x.Nodo), x => x.Elementos);
            _sembrada = true;
        }

        internal static void Limpiar()
        {
            _claves = new Dictionary<(string, string), int>();
            _cargadaPara = null;
            _sembrada = false;
        }

        private static void Cargar()
        {
            if (_sembrada)
                return;

            var actual = $"{Prefabs.PathRimworld}|{Prefabs.TranslationLanguage}";
            if (_cargadaPara == actual)
                return;

            var claves = new Dictionary<(string, string), int>();
            try
            {
                foreach (var oficial in ModLister.OfficialMods)
                    CargarMod(oficial, claves);
            }
            catch (Exception e)
            {
                // Quedarse sin tabla solo significa seguir emitiendo patches, que es lo que se
                // hacia antes. No puede hacer fallar una extraccion.
                Log.Wrn(Strings.OfficialTranslationUnreadable(e.Message));
            }

            _claves = claves;
            _cargadaPara = actual;
        }

        private static void CargarMod(ModMetadata mod, Dictionary<(string, string), int> claves)
        {
            var languages = Path.Combine(mod.RootDir, "Languages");
            if (!Directory.Exists(languages))
                return;

            // El juego distribuye cada idioma como un .tar. Antes venian como carpeta suelta y
            // sigue habiendo instalaciones asi, por eso se miran las dos formas.
            foreach (var archivo in Directory.EnumerateFiles(languages, "*.tar"))
            {
                if (Utils.EsElIdiomaDestino(Path.GetFileNameWithoutExtension(archivo)))
                    LeerTar(archivo, claves);
            }

            foreach (var carpeta in Directory.EnumerateDirectories(languages))
            {
                if (Utils.EsElIdiomaDestino(Path.GetFileName(carpeta)))
                    LeerCarpeta(carpeta, claves);
            }
        }

        private static void LeerTar(string archivo, Dictionary<(string, string), int> claves)
        {
            using var stream = File.OpenRead(archivo);
            using var tar = new TarReader(stream);

            while (tar.GetNextEntry() is { } entrada)
            {
                if (entrada.DataStream == null)
                    continue;

                var clase = ClaseDeLaRuta(entrada.Name);
                if (clase == null)
                    continue;

                Anotar(claves, clase, entrada.DataStream);
            }
        }

        private static void LeerCarpeta(string carpeta, Dictionary<(string, string), int> claves)
        {
            var defInjected = Path.Combine(carpeta, "DefInjected");
            if (!Directory.Exists(defInjected))
                return;

            foreach (var claseDir in Directory.EnumerateDirectories(defInjected))
            {
                var clase = Path.GetFileName(claseDir);
                foreach (var archivo in Directory.EnumerateFiles(claseDir, "*.xml", SearchOption.AllDirectories))
                {
                    using var stream = File.OpenRead(archivo);
                    Anotar(claves, clase, stream);
                }
            }
        }

        /// <summary>
        /// De "DefInjected/ThingDef/Buildings_Gravship.xml" saca "ThingDef". Devuelve null
        /// para todo lo que no sea un XML de DefInjected: Keyed, Strings y las carpetas.
        /// </summary>
        private static string? ClaseDeLaRuta(string ruta)
        {
            var normalizada = ruta.Replace('\\', '/').TrimStart('.', '/');
            if (!normalizada.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                return null;

            var tramos = normalizada.Split('/');
            return tramos.Length >= 3 && tramos[0] == "DefInjected" ? tramos[1] : null;
        }

        private static void Anotar(Dictionary<(string, string), int> claves, string clase, Stream contenido)
        {
            var doc = new XmlDocument();
            try
            {
                doc.Load(contenido);
            }
            catch (XmlException)
            {
                // Un archivo roto del juego no tiene por que voltear la extraccion: se pierde
                // ese archivo y el resto de la tabla sigue sirviendo.
                return;
            }

            if (doc.DocumentElement == null)
                return;

            foreach (XmlNode nodo in doc.DocumentElement.ChildNodes)
            {
                if (nodo.NodeType != XmlNodeType.Element)
                    continue;

                claves[(clase, nodo.Name)] = nodo.ChildNodes.Cast<XmlNode>().Count(x => x.Name == "li");
            }
        }
    }
}
