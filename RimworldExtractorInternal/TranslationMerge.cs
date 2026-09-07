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
        /// Devuelve la extraccion nueva con las traducciones que ya existian, las que quedaron
        /// sin lugar porque su nodo ya no esta en el mod, y las que se rescataron porque el
        /// nodo se movio pero el texto es el mismo.
        /// </summary>
        public static (List<TranslationEntry> Resultado, List<TranslationEntry> SinUso,
            List<TranslationEntry> Rescatadas) Merge(
            IEnumerable<TranslationEntry> nuevas, IEnumerable<TranslationEntry> existentes)
        {
            // La clave es la misma que usa el analizador de traducciones, con el prefijo de
            // patches normalizado (ver Clave).
            var previas = new Dictionary<(string, string), TranslationEntry>();
            foreach (var previa in existentes)
            {
                var clave = Clave(previa);

                // Gana la ultima, y quien arma la lista decide el orden. Pero si las dos estan
                // traducidas y no dicen lo mismo, sobra una: se avisa en vez de elegir callado,
                // que es como veinte traducciones correctas se dieron vuelta sin que nada lo
                // marcara.
                if (previas.TryGetValue(clave, out var anterior)
                    && !string.IsNullOrEmpty(anterior.Translated)
                    && !string.IsNullOrEmpty(previa.Translated)
                    && anterior.Translated != previa.Translated)
                {
                    Log.Wrn(Strings.TraduccionDuplicadaEnConflicto(
                        previa.ClassName, previa.Node, anterior.Translated!, previa.Translated!));
                }

                previas[clave] = previa;
            }

            var usadas = new HashSet<(string, string)>();
            var resultado = new List<TranslationEntry>();

            foreach (var nueva in nuevas)
            {
                var clave = Clave(nueva);
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

            // Las que no encontraron su clave. Lo que estaba sin traducir no cuenta: no hay
            // nada que rescatar.
            var huerfanas = previas
                .Where(x => !usadas.Contains(x.Key) && !string.IsNullOrEmpty(x.Value.Translated))
                .Select(x => x.Value)
                .ToList();

            var rescatadas = Rescatar(resultado, huerfanas);

            return (resultado, huerfanas.Except(rescatadas).ToList(), rescatadas);
        }

        /// <summary>
        /// Segundo pase: recupera las traducciones que quedaron huerfanas solo porque el mod
        /// movio el nodo de lugar.
        ///
        /// Pasa seguido: un mod deja de nombrar las partes de un cuerpo y pasa a indexarlas por
        /// posicion, y de golpe "cola mecanica" queda sin dueño aunque el ingles sea identico.
        /// La clave cambio, el texto no.
        ///
        /// Modifica <paramref name="resultado"/> en el lugar y devuelve las que se aprovecharon.
        /// </summary>
        private static List<TranslationEntry> Rescatar(
            List<TranslationEntry> resultado, List<TranslationEntry> huerfanas)
        {
            var rescatadas = new List<TranslationEntry>();
            if (huerfanas.Count == 0)
                return rescatadas;

            // Se indexa por texto original y campo. El campo es lo que evita el caso feo: un
            // mod donde label y labelFemale comparten el ingles ("hunter") pero no la
            // traduccion ("cazador" contra "cazadora").
            var porTexto = new Dictionary<(string, string), List<TranslationEntry>>();
            foreach (var huerfana in huerfanas)
            {
                if (string.IsNullOrWhiteSpace(huerfana.Original))
                    continue;

                var clave = ClaveDeTexto(huerfana);
                if (!porTexto.TryGetValue(clave, out var lista))
                    porTexto[clave] = lista = new List<TranslationEntry>();
                lista.Add(huerfana);
            }

            for (var i = 0; i < resultado.Count; i++)
            {
                var entrada = resultado[i];
                if (!string.IsNullOrEmpty(entrada.Translated) || string.IsNullOrWhiteSpace(entrada.Original))
                    continue;

                if (!porTexto.TryGetValue(ClaveDeTexto(entrada), out var candidatas))
                    continue;

                // Con dos traducciones distintas para el mismo texto no se elige ninguna: no
                // hay forma de saber cual, y equivocarse es peor que dejarlo por traducir,
                // porque un TODO se ve y una traduccion mal puesta no.
                var traducciones = candidatas.Select(x => x.Translated).Distinct().ToList();
                if (traducciones.Count != 1)
                    continue;

                resultado[i] = entrada with { Translated = traducciones[0] };
                rescatadas.Add(candidatas[0]);
            }

            return rescatadas;
        }

        /// <summary>Texto original y ultimo segmento del nodo, que es el nombre del campo.</summary>
        private static (string, string) ClaveDeTexto(TranslationEntry entrada)
        {
            var nodo = entrada.Node;
            var punto = nodo.LastIndexOf('.');
            var campo = punto >= 0 ? nodo[(punto + 1)..] : nodo;

            return (entrada.Original.Trim(), campo);
        }

        /// <summary>El prefijo que le pone el extractor a las clases que salen por un patch.</summary>
        private const string PrefijoDePatches = "Patches.";

        /// <summary>
        /// La clave con la que se cruzan las traducciones, sin el prefijo de patches.
        ///
        /// Un mismo nodo del mismo def se entrega de dos formas segun de quien sea el def: si es
        /// del mod que se extrae, va como DefInjected y la clase es "ThingDef"; si es de otro mod
        /// o del juego base, va como PatchOperation y la clase pasa a ser "Patches.ThingDef".
        /// Comparar con el prefijo puesto hace que una traduccion cambie de identidad al cambiar
        /// de forma de entrega, y entonces se perderia: iria a UNUSED y volveria a salir como
        /// TODO. Es la misma traduccion, asi que la clave la ignora.
        /// </summary>
        private static (string, string) Clave(TranslationEntry entrada)
        {
            var clase = entrada.ClassName.StartsWith(PrefijoDePatches, System.StringComparison.Ordinal)
                ? entrada.ClassName[PrefijoDePatches.Length..]
                : entrada.ClassName;

            return (clase, entrada.Node);
        }
    }
}
