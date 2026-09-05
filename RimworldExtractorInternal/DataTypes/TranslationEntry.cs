using System.Diagnostics;

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

            _extensions = new Dictionary<string, object>();
            foreach (var otherExtension in other._extensions)
            {
                _extensions.Add(otherExtension.Key, otherExtension.Value);
            }
        }

        private readonly Dictionary<string, object> _extensions = new();
        public bool TryGetExtension(string key, out object? extension)
        {
            extension = null;
            if (_extensions.TryGetValue(key, out extension) == true)
            {
                return true;
            }

            return false;
        }

        public bool HasRequiredMods()
        {
            return RequiredMods == null || RequiredMods.CountAllowed > 0 || RequiredMods.CountDisallowed > 0;
        }

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
