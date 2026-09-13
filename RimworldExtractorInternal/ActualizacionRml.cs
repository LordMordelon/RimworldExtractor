using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorInternal
{
    /// <summary>
    /// Escribe una extraccion sobre la carpeta que ese mod tiene en RML, conservando lo que
    /// ya estaba traducido.
    ///
    /// Vive aca y no en la ventana porque lo usan dos caminos: la traduccion rapida de un mod
    /// suelto y la actualizacion de todo RML de una vez.
    /// </summary>
    public static class ActualizacionRml
    {
        /// <summary>Lo que quedo despues de cruzar la extraccion con lo ya traducido.</summary>
        public readonly record struct Resultado(
            string Destino, int Conservadas, int Pendientes, int SinUso,
            List<TranslationEntry> Rescatadas);

        /// <summary>
        /// Cruza la extraccion con lo que ya esta traducido en RML y escribe el resultado ahi
        /// mismo.
        ///
        /// El arbol de destino se borra antes de escribir: la mezcla ya es el contenido
        /// completo, y si no, los archivos de una version anterior del mod quedarian ahi con
        /// claves que el juego intentaria cargar.
        ///
        /// Entra al cruce tanto lo que esta en uso como lo que quedo apartado en UNUSED.xml,
        /// de forma que una traduccion vieja se recupere sola si el mod vuelve a traer su nodo,
        /// y que siga apartada si no.
        ///
        /// No regenera el LoadFolders.xml: eso lo decide quien llama, porque hacerlo una vez
        /// por mod en una corrida de doscientos serian doscientas compilaciones de mas.
        /// </summary>
        public static Resultado Escribir(ModMetadata mod, List<TranslationEntry> extraccion, string rmlPath)
        {
            var destino = LoadFoldersBuild.CarpetaDe(mod, rmlPath);

            // Lo apartado en corridas anteriores vuelve a entrar al cruce, y va primero:
            // Merge se queda con la ultima entrada de cada clave, asi que ante un empate tiene
            // que ganar la traduccion que esta en uso y no la descartada.
            var existentes = IO.ReadUnused(destino);
            if (Directory.Exists(destino))
                existentes.AddRange(IO.FromLanguageXml(destino));

            var (resultado, sinUso, rescatadas) = TranslationMerge.Merge(extraccion, existentes);

            BorrarArbolAnterior(destino);
            Directory.CreateDirectory(destino);

            // Se fuerza la sobrescritura mientras dura el guardado: con la politica en
            // "conservar el original" no se escribiria nada y la actualizacion no haria
            // absolutamente nada, sin que se note.
            var politica = Prefabs.Policy;
            Prefabs.Policy = Prefabs.DuplicatesPolicy.Overwrite;
            try
            {
                IO.ToLanguageXml(resultado, false, XmlCommentStyle.TranslationTemplate,
                    mod.Identifier.StripInvaildChars(), destino);
            }
            finally
            {
                Prefabs.Policy = politica;
            }

            IO.WriteUnused(sinUso, destino);
            LoadFoldersBuild.Write(mod, destino);

            var conservadas = resultado.Count(x => !string.IsNullOrEmpty(x.Translated));
            return new Resultado(destino, conservadas, resultado.Count - conservadas,
                sinUso.Count, rescatadas);
        }

        /// <summary>
        /// Borra lo que la herramienta genera, y solo eso: el idioma de destino dentro de
        /// Languages —los demas idiomas, si los hubiera, no son asunto nuestro— y los Patches
        /// de traduccion.
        ///
        /// El idioma se borra con los dos nombres, el largo y el corto. Si no, al reescribir
        /// una carpeta de antes del cambio de nombre quedarian las dos, y el juego cargaria
        /// las dos.
        /// </summary>
        private static void BorrarArbolAnterior(string destino)
        {
            foreach (var nombre in new[] { Prefabs.TranslationLanguage, Utils.CarpetaDelIdiomaDestino() }.Distinct())
            {
                var idioma = Path.Combine(destino, "Languages", nombre);
                if (Directory.Exists(idioma))
                    Directory.Delete(idioma, true);
            }

            var patches = Path.Combine(destino, "Patches");
            if (Directory.Exists(patches))
                Directory.Delete(patches, true);
        }
    }
}
