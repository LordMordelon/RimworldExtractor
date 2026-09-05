namespace RimworldExtractorInternal
{
    /// <summary>
    /// Todo el texto visible para el usuario, centralizado.
    ///
    /// Existe para que el fork en español pueda seguir mezclando cambios de upstream
    /// sin conflictos: los literales viven acá en vez de estar repartidos por el código,
    /// y los archivos .Designer.cs quedan intactos (los formularios se traducen en
    /// tiempo de ejecución mediante su método ApplyStrings).
    ///
    /// Identificadores en inglés, como el resto del código; valores en español.
    /// </summary>
    public static class Strings
    {
        #region Log

        public const string PrefixError = "ERROR";
        public const string PrefixWarning = "ADVERTENCIA";
        public const string PrefixMessage = "MENSAJE";

        #endregion

        #region Utils / Excel

        public static string ErrorReadingCell(string address, string message)
            => $"Error al leer el texto de una celda del archivo Excel: {address}-{message}";

        /// <summary>
        /// Fuente de las planillas generadas. Upstream usaba "맑은 고딕" (Malgun Gothic),
        /// que no existe en un Windows en español.
        /// </summary>
        public const string ExcelFontName = "Calibri";

        public const string XlsxHeaderReadingErrorFormat =
            "Ocurrió un error al leer los encabezados del archivo Excel: {0}";

        #endregion

        #region ModLister

        public static string CouldNotReadAboutXml(string pathAbout, string message)
            => $"No se pudo leer el archivo About.xml ubicado en {pathAbout}. {message}";

        public static string ModNotFoundByPackageId(string packageId)
            => $"No se encontró ningún mod con packageId {packageId} en las carpetas de mods.";

        public static string DuplicatePackageId(string packageId, int count)
            => $"packageId duplicado={packageId}, cantidad de duplicados={count}.";

        #endregion

        #region Extractor

        public static string UnsupportedFolder(string folderName)
            => $"Carpeta no soportada. {folderName}";

        public static string DuplicateNodeWithDifferentOriginal(string className, string node, string other, string original)
            => $"Hay un nodo duplicado con distinto texto original. Nodo: {className}+{node}| {other} | {original} ";

        public static string ErrorReadingFile(string filePath, string message)
            => $"Error al leer {filePath}, {message}";

        public static string ErrorReadingFileColon(string filePath, string message)
            => $"Error al leer {filePath}: {message}";

        public static string DefNameTagNotFound(string nodeName, string innerXml)
            => $"No se encontró la etiqueta 'defName' en el elemento XML {nodeName}, que no es SongDef ni Abstract. InnerXml: {innerXml}";

        public static string DuplicateParentNodeName(string attributeName)
            => $"El nombre del nodo Parent está repetido: {attributeName}. Se sobrescribe con el último.";

        public static string ParentNodeNotFound(string childDefName, string parentName)
            => $"No se encontró el nodo padre={parentName} del nodo hijo={childDefName}. ";

        #endregion

        #region Prefabs

        public static string ErrorAutoDetectingVersion(string message)
            => $"Error al detectar automáticamente la versión {message}";

        #endregion

        #region IO

        public static string InvalidRequiredModsValue(string token)
            => $"La columna Required Mods tiene un valor inválido. Para que los Patches se generen bien, hay que reemplazar a mano ese texto del Excel: \"{token}\" por el nombre del mod.";

        public static string PackageIdInsteadOfModName(string prefix)
            => $"Hay un nombre de paquete ({prefix}) en lugar del nombre del mod. Corregilo al nombre del mod.";

        public static string CommentDeleted(string dateString, string previousTranslation)
            => $"Eliminado el {dateString}. Traducción anterior: '{previousTranslation}'\n";

        public static string CommentPreviousOriginal(string dateString, string previousOriginal)
            => $"Texto original anterior al {dateString}: '{previousOriginal}'\n";

        public static string CommentOriginalRestored(string dateString)
            => $"Se restauró el {dateString} un texto original que se había perdido.\n";

        public static string CommentNewlyAdded(string dateString, int count)
            => $"Nodos agregados el {dateString} ({count})";

        public const string OfficialContentKeepsFileNames =
            "A diferencia de los mods, el contenido oficial se extrae conservando los nombres de archivo.";

        public const string NothingToExtract =
            "No hay datos de traducción, así que no se extrae nada. Tip: en XLSX -> XML no se guarda nada si no hay contenido traducido.";

        public static string OriginalIdentifierNotFound(string targetIdentifier)
            => $"Pointer: no se encontró el Identifier original de {targetIdentifier}.";

        public static string FileInUse(string fileName)
            => $"{fileName}: no se pudo guardar el archivo porque ya está en uso. Cerralo y volvé a intentar.";

        #endregion

        #region DataTypes

        public static string ModDependencySuffix(string requiredPackageId)
            => $"\n[dependencia del mod={requiredPackageId}]";

        public static string NotAValidTranslationXlsx(string path, string message)
            => $"El archivo Excel no tiene el formato de traducción esperado: {path}, {message}";

        public static string XlsxIsOpen(string path)
            => $"No se pudo leer el archivo Excel porque está abierto. Cerralo y volvé a intentar: {path}";

        #endregion

        #region Compats

        public static string CompatsLoaded(int count)
            => $"Se cargaron {count} compats";

        public static string LabelNodeMissing(string innerXml)
            => $"Un nodo Def que no es Abstract no tiene nodo label. {innerXml}";

        public static string ScenarioDefMissingLabelOrDescription(string defName)
            => $"ScenarioDef no tiene etiqueta label ni description. defName: {defName}";

        public static string ScenarioDefAlreadyHasTags(string defName)
            => $"ScenarioDef ya tiene las etiquetas scenario.name o scenario.description. defName: {defName}";

        public const string MvcfGizmoDescriptionHint =
            "Al traducir este nodo se modifica la descripción que se muestra en el gizmo";

        #endregion
    }
}
