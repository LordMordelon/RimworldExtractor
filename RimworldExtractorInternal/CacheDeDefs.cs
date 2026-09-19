using System.Collections.Concurrent;
using System.Xml;

namespace RimworldExtractorInternal
{
    /// <summary>
    /// Guarda los XML de defs ya parseados mientras dura una corrida por lotes.
    ///
    /// Cada mod rearma su base de defs desde cero —Extractor.Reset— y
    /// ModLister.FindAllReferenceMods siempre devuelve todo el contenido oficial ademas de las
    /// dependencias, asi que los mismos archivos se leian y parseaban una vez por mod. Medido
    /// sobre RML: 1558 archivos de Core y los DLC, 262 mods, 408.196 parseos donde alcanzaban
    /// 1558. Sin contar los frameworks, que se recargan igual.
    ///
    /// Se enciende solo durante la corrida por lotes y se apaga al terminar. Fuera de ahi cada
    /// extraccion vuelve a leer del disco, que es lo que corresponde cuando entre una corrida
    /// y la siguiente el mod pudo actualizarse.
    /// </summary>
    internal static class CacheDeDefs
    {
        /// <summary>
        /// Null cuando esta apagado, que es el estado normal. Concurrente porque desde que la
        /// lectura va en paralelo lo consultan varios hilos a la vez.
        /// </summary>
        private static ConcurrentDictionary<string, XmlDocument>? _documentos;

        internal static bool Encendido => _documentos != null;

        internal static void Abrir() => _documentos = new ConcurrentDictionary<string, XmlDocument>();

        /// <summary>Lo apaga y suelta la memoria: son cientos de MB de arboles XML.</summary>
        internal static void Cerrar() => _documentos = null;

        /// <summary>
        /// El documento de ese archivo, parseado una sola vez por corrida.
        ///
        /// **Quien lo recibe no lo puede modificar**: el mismo arbol se le entrega a todos los
        /// mods de la corrida. Los dos que lo usan copian lo que necesitan con ImportNode y le
        /// ponen los atributos a la copia, nunca al original.
        /// </summary>
        internal static XmlDocument Leer(string filePath)
        {
            // Se toma una referencia: Cerrar puede correr mientras esto avanza.
            var cache = _documentos;
            return cache == null ? IO.ReadXml(filePath) : cache.GetOrAdd(filePath, IO.ReadXml);
        }
    }
}
