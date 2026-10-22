namespace RimworldExtractorInternal.DataTypes
{
    /// <summary>
    /// Datos de traduccion
    /// </summary>
    /// <param name="ClassName">Tipo de dato de traduccion: ○○Def, Keyed, Strings, Patches.○○Def</param>
    /// <param name="Node">Ubicacion</param>
    /// <param name="Original">Texto original</param>
    /// <param name="Translated">Texto traducido</param>
    /// <param name="RequiredMods">Mods requeridos</param>
    /// <param name="SourceFile">Archivo de origen. Se llena solo para el contenido oficial</param>
    public record TranslationEntry(string ClassName, string Node, string Original, string? Translated,
        RequiredMods? RequiredMods, string? SourceFile)
    {
        public TranslationEntry(TranslationEntry other)
        {
            ClassName = other.ClassName;
            Node = other.Node;
            Original = other.Original;
            Translated = other.Translated;
            if (other.RequiredMods != null)
            {
                this.RequiredMods = new RequiredMods(other.RequiredMods);
            }
            SourceFile = other.SourceFile;
            NodoAnterior = other.NodoAnterior;
            Xpath = other.Xpath;
            Apartada = other.Apartada;

            _extensions = new Dictionary<string, object>();
            foreach (var otherExtension in other._extensions)
            {
                _extensions.Add(otherExtension.Key, otherExtension.Value);
            }
        }

        private readonly Dictionary<string, object> _extensions = new();

        /// <summary>
        /// El nodo como se escribia antes de nombrar los comps por su clase, con la posicion en
        /// vez del nombre (ver ClasesDeComp). Null si es el mismo. Sirve para que el cruce
        /// encuentre lo que RML ya tenia traducido con la clave vieja.
        /// </summary>
        public string? NodoAnterior { get; init; }

        /// <summary>
        /// El xpath para escribir esta entrada como Patches, cuando no se puede armar desde el
        /// nodo: un comp nombrado por su clase no tiene ese nombre en el XML, y se busca por su
        /// atributo Class. Null para todo lo demas, que sigue saliendo de Utils.GetXpath.
        /// </summary>
        public string? Xpath { get; init; }

        /// <summary>
        /// Si se leyo del UNUSED.xml. El cruce la usa solo si no hay una en uso para el mismo
        /// nodo: una traduccion apartada nunca le gana a lo que esta en uso.
        /// </summary>
        public bool Apartada { get; init; }

        public TranslationEntry AddExtension(string key, object extension)
        {
            _extensions.Add(key, extension);
            return this;
        }

        public string ClassNode => $"{ClassName}+{Node}";
        public string DefName => Node.Contains('.') ? Node[..Node.IndexOf('.')] : Node;
        public string RealNode => Node.Contains('.') ? Node[(Node.IndexOf('.') + 1)..] : Node;

    }
}
