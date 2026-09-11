namespace RimworldExtractorInternal.DataTypes
{
    public record ModMetadata(string RootDir, string Id, string ModName, string PackageId, bool IsOfficialContent, List<string>? ModDependencies = null)
    {
        /// <summary>
        /// El autor tal cual lo declara el About.xml, sin normalizar: puede venir vacio, con
        /// varios separados por coma, o escrito distinto que en otro mod de la misma persona.
        /// Quien agrupa por autor lo normaliza con <see cref="Agrupador.AutorPrincipal"/>.
        ///
        /// Va como propiedad y no como parametro del record para no tocar la firma: hay
        /// llamadas posicionales en el extractor y en las pruebas que seguirian compilando
        /// pero pasarian a significar otra cosa si se corre un parametro de lugar.
        ///
        /// Queda fuera de Equals y GetHashCode a proposito: sale del mismo About.xml que el
        /// resto, asi que no distingue dos mods que ya no se distingan por packageId, y
        /// meterlo en el hash cambiaria la identidad de un mod porque su autor se renombro.
        /// </summary>
        public string Author { get; init; } = "";

        public string Identifier
        {
            get
            {
                if (IsOfficialContent) return ModName;
                return Id == "???" ? ModName : $"{ModName} - {Id}";
            }
        }

        public virtual bool Equals(ModMetadata? other)
        {
            return other != null && GetHashCode() == other.GetHashCode();
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RootDir.GetHashCode();
                hashCode = (hashCode * 397) ^ Id.GetHashCode();
                hashCode = (hashCode * 397) ^ ModName.GetHashCode();
                hashCode = (hashCode * 397) ^ PackageId.GetHashCode();
                hashCode = (hashCode * 397) ^ IsOfficialContent.GetHashCode();
                hashCode = (hashCode * 397) ^ (string.Concat(ModDependencies ?? new List<string>())).GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"{(IsOfficialContent ? "Official" : "Mod")}:{Identifier}:requires={string.Join(',', ModDependencies ?? new List<string>())}";
        }

        public static ModMetadata Emptry => new("", "", "", "", false, null);
    }
}
