using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorInternal
{
    /// <summary>
    /// Avisa cuando el mod que se esta extrayendo ya trae su propia traduccion al idioma
    /// de destino.
    ///
    /// El extractor no la mira: saca el original de Languages/OriginalLanguage y deja todo
    /// por traducir, asi que sin este aviso el traductor no se entera. Y conviene enterarse
    /// por dos motivos: puede que buena parte del trabajo ya este hecha, y si esa carpeta
    /// se suma a RML hay que poner el packageId del mod en el loadAfter del About, porque
    /// si no la traduccion del propio mod se carga despues y pisa la de RML.
    /// </summary>
    public static class TraduccionPropia
    {
        /// <summary>
        /// Deja en el log lo que trae el mod. No hace nada en el caso normal, que es que
        /// no traiga nada en este idioma.
        /// </summary>
        public static void Informar(ModMetadata mod)
        {
            var carpetas = CarpetasDelIdiomaDestino(mod);
            if (carpetas.Count == 0)
                return;

            var archivos = carpetas.Sum(x =>
                IO.DescendantFiles(x).Count(y => y.ToLower().EndsWith(".xml")));

            Log.Wrn(Strings.ModShipsOwnTranslation(Prefabs.TranslationLanguage, archivos));
            Log.Wrn(Strings.ModShipsOwnTranslationHint);
        }

        /// <summary>
        /// Las carpetas del mod que corresponden al idioma de destino.
        ///
        /// Se miran la raiz y cada subcarpeta inmediata, porque muchos mods cuelgan las
        /// traducciones de una carpeta de version (1.6/Languages) o de Common. Recorrer el
        /// arbol entero no agregaria casos reales y en un mod grande se notaria.
        /// </summary>
        private static List<string> CarpetasDelIdiomaDestino(ModMetadata mod)
        {
            var encontradas = new List<string>();

            try
            {
                var raiz = mod.RootDir;
                if (!Directory.Exists(raiz))
                    return encontradas;

                var candidatas = new List<string> { raiz };
                candidatas.AddRange(Directory.GetDirectories(raiz));

                foreach (var candidata in candidatas)
                {
                    var languages = Path.Combine(candidata, "Languages");
                    if (!Directory.Exists(languages))
                        continue;

                    encontradas.AddRange(Directory.GetDirectories(languages)
                        .Where(x => EsElIdiomaDestino(Path.GetFileName(x))));
                }
            }
            catch
            {
                // Un aviso no puede hacer fallar una extraccion. Si Steam esta actualizando
                // el mod justo ahora, se pierde el aviso y nada mas.
            }

            return encontradas;
        }

        /// <summary>
        /// Misma tolerancia que usa IO.FromLanguageXml: los mods escriben tanto el nombre
        /// completo del idioma como su primera palabra.
        ///
        /// Se compara contra el idioma configurado y no contra "Spanish" a secas, por dos
        /// razones. Una carpeta Spanish (Español(Castellano)) no pisa nada, porque para
        /// RimWorld el castellano y el español latino son idiomas distintos; y asi el aviso
        /// sigue sirviendo si alguien cambia el idioma de destino en Opciones.
        /// </summary>
        private static bool EsElIdiomaDestino(string nombre)
        {
            var destino = Prefabs.TranslationLanguage;
            if (string.IsNullOrWhiteSpace(destino))
                return false;

            return nombre.Equals(destino, StringComparison.OrdinalIgnoreCase)
                   || nombre.Equals(destino.Split(' ').First(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
