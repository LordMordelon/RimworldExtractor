using System.Collections.Generic;
using System.Linq;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorInternal
{
    /// <summary>
    /// Cruza una extraccion nueva con lo que ya estaba traducido.
    ///
    /// Es lo que permite actualizar un mod sin volver a traducirlo: cuando el mod cambia,
    /// la extraccion sale entera sin traducir, y sin este cruce habria que recuperar a mano
    /// todo lo que ya estaba hecho.
    /// </summary>
    public static class TranslationMerge
    {
        /// <summary>
        /// Devuelve la extraccion nueva con las traducciones que ya existian, y aparte las
        /// que quedaron sin lugar porque su nodo ya no esta en el mod.
        /// </summary>
        public static (List<TranslationEntry> Resultado, List<TranslationEntry> SinUso) Merge(
            IEnumerable<TranslationEntry> nuevas, IEnumerable<TranslationEntry> existentes)
        {
            // La clave es la misma que usa el analizador de traducciones.
            var previas = new Dictionary<(string, string), TranslationEntry>();
            foreach (var previa in existentes)
                previas[(previa.ClassName, previa.Node)] = previa;

            var usadas = new HashSet<(string, string)>();
            var resultado = new List<TranslationEntry>();

            foreach (var nueva in nuevas)
            {
                var clave = (nueva.ClassName, nueva.Node);
                if (previas.TryGetValue(clave, out var previa) && !string.IsNullOrEmpty(previa.Translated))
                {
                    // Se conserva la traduccion y se toma el original nuevo: si el texto en
                    // ingles cambio, el comentario EN queda actualizado y el cambio se ve en
                    // el diff, que es donde se revisa.
                    resultado.Add(nueva with { Translated = previa.Translated });
                    usadas.Add(clave);
                }
                else
                {
                    resultado.Add(nueva);
                }
            }

            // Lo que tenia traduccion y ya no tiene donde ir. Lo que estaba sin traducir no
            // se reporta: no hay nada que rescatar.
            var sinUso = previas
                .Where(x => !usadas.Contains(x.Key) && !string.IsNullOrEmpty(x.Value.Translated))
                .Select(x => x.Value)
                .ToList();

            return (resultado, sinUso);
        }
    }
}
