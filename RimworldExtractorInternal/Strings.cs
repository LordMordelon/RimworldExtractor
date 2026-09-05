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

        #region GUI - común

        public const string DialogTitleDone = "Listo";
        public const string DialogTitleDoneQuestion = "¿Listo?";
        public const string SelectRimworldExe = "Elegí RimWorldWin64.exe";
        public const string FilterRimworldExe = "Ejecutable de RimWorld|RimWorldWin64.exe";
        public const string SelectWorkshopPath =
            @"Elegí la carpeta del workshop de RimWorld => Steam\steamapps\workshop\content\294100";
        public const string FilterRefModsList = "Archivo de lista de mods de referencia|*.refMods";
        public const string LoadRefModsFromFile = "Carga la lista de mods de referencia desde el archivo elegido.";

        #endregion

        #region FormMain

        public const string FormMainTitle = "Rimworld Extractor (extractor de traducciones)";

        public const string PrefabsDatOutdated =
            "El archivo Prefabs.dat es de una versión anterior o está dañado. Borralo y volvé a intentar.\n";

        public static string ErrorMessagePrefix(string message) => $"Mensaje de error: {message}";

        public static string SelectedMod(string modName) => $"Mod elegido: {modName}";

        public static string SelectedReferenceMods(string list) => $"\nMods elegidos como referencia: {list}";

        public const string ExtractionStarted = "Iniciando extracción...";

        public static string ExtractionSummary(int total, int defs, int keyed, int strings, int patches)
            => $"Datos de traducción: {total} en total — Defs {defs}, Keyed {keyed}, Strings {strings}, Patches {patches}. ¡Listo!";

        public const string DoneWithErrorsOpenFolder =
            "Terminó, pero hubo errores durante la extracción. ¿Abro igual la carpeta con los archivos extraídos?";

        public const string DoneOpenFolder = "¡Listo! ¿Abro la carpeta con los archivos extraídos?";
        public const string DoneOpenConvertedFolder = "¡Listo! ¿Abro la carpeta con los archivos convertidos?";

        public const string SelectExtractorXlsx = "Elegí un archivo Excel generado por el extractor.";
        public const string FilterTranslationData = "Archivo de datos de traducción|*.xlsx";

        public static string ProgressFixed(int current, int total, string path)
            => $"{current}/{total}::Corregido: {path}";

        public const string ConversionDone = "¡La conversión terminó!";
        public const string EditedFileSuffix = "- editado";

        public static string FilesFixed(int count) => $"Se corrigieron {count} archivos.";

        #endregion

        #region FormSettings

        public const string ExtractionMethodExcel = "Archivo Excel (.xlsx) para trabajar la traducción";
        public const string ExtractionMethodXml = "Archivo XML distribuible";
        public const string ExtractionMethodXmlComments = "Archivo XML distribuible (con comentarios)";

        public const string DuplicatePolicyAsk = "Frenar y preguntar";
        public const string DuplicatePolicyOverwrite = "Sobrescribir";
        public const string DuplicatePolicySkip = "Omitir";

        public const string HelpExtractableTags =
            "Define la lista de etiquetas de los nodos que hay que extraer. Se separan con '/' y sin espacios. Salvo que tengas un motivo puntual, dejalo como está.";

        public const string HelpTranslationHandle =
            "Translation Handle es un método de extracción que, al extraer nodos de lista llamados 'li', usa el valor de una etiqueta determinada en lugar del número de índice. Ver https://ludeon.com/forums/index.php?topic=41942.0\n" +
            "Si el nodo tiene una etiqueta que coincide con el Translation Handle, el valor de esa etiqueta define el nombre del nodo de lista.\n" +
            "Ej.) verbs.2.label => verbs.Verb_Shoot.label\nSe separan con '/' y sin espacios.\n" +
            "La forma de extraer un Translation Handle depende de si el tipo de esa etiqueta es Type o no. Para los de tipo Type, se antepone el prefijo '*'.";

        public const string HelpNodeReplacement =
            "En algunos nodos, el nodo del que se extrae no es el mismo al que se aplica la traducción; el reemplazo de nodos sirve justamente para esos casos. " +
            "Se escribe con el formato (tipo de Def)+(nodo original)|(tipo de Def)+(nodo de reemplazo), omitiendo la parte del defName. Si hay varios, se separan con '/' y sin espacios. " +
            "\nAtención: todavía no se admiten los casos que incluyen nodos 'li'.";

        public const string HelpFullListTranslation =
            "Full-list Translation es una forma de guardar nodos de lista que se usa solo en algunos casos. Ver https://ludeon.com/forums/index.php?topic=41942.0\n Se separan con '/' y sin espacios.";

        #endregion

        #region FormSelectMod

        public const string MenuSaveRefModsList = "(selección) Guardar lista de mods de referencia";
        public const string SaveRefModsList = "Guarda la lista de los mods de referencia elegidos.";
        public const string MenuLoadRefModsList = "(selección) Cargar lista de mods de referencia";

        public const string SelectModToExtract = "Elegí el mod que querés extraer";

        public static string ModDependenciesSuffix(string list) => $"\n[mods previos: {list}]";

        public const string MenuSelectAsExtractionTarget = "Elegir como mod a extraer";
        public const string MenuOpenInFileExplorer = "Abrir en el explorador de archivos";
        public const string MenuUnselectAsReference = "Quitar de los mods de referencia";
        public const string MenuSelectAsReference = "Elegir como mod de referencia";
        public const string MenuSelectAllRelatedAsReference =
            "Elegir como referencia todos los mods relacionados con este";
        public const string NoDependencies = "¡Este mod no tiene mods previos ni previos opcionales!";
        public const string ReferencePrefix = "(ref.) ";

        #endregion

        #region FormImageFileCombiner

        public const string SelectImageFile = "Elegí el archivo de imagen.";
        public const string FilterImageFile = "Archivo de imagen|*.jpg;*.png;*.gif";
        public const string SelectZipToPackage = "Elegí la ruta del archivo comprimido a empaquetar.";
        public const string FilterZip = "Archivo ZIP";
        public const string SelectFolderToPackage = "Elegí la ruta de la carpeta a empaquetar.";
        public const string SelectSaveLocation = "Elegí dónde guardar el archivo";

        public const string ImageNotFoundOrNoAccess =
            "La imagen no existe en esa ruta o no hay permisos de acceso.\n" +
            "Si el archivo existe y aun así falla, ejecutá como administrador o movelo a otra ubicación y volvé a intentar.";

        public const string FileNotFoundOrNoAccess =
            "El archivo o la carpeta no existe en esa ruta o no hay permisos de acceso.\n" +
            "Si el archivo existe y aun así falla, ejecutá como administrador o movelo a otra ubicación y volvé a intentar.";

        public const string ReselectFileLocation = "Volvé a elegir la ubicación del archivo.";
        public const string DoneOpenPackagedFolder = "¡Listo! ¿Abro la carpeta con el archivo empaquetado?";

        #endregion

        #region FormTranslationAnalyzer

        public static string AnalyzingProgress(int current, int total)
            => $"Analizando los datos de traducción... {current}/{total}";

        public const string AnalysisDone = "¡Análisis terminado!";
        public const string SomeFilesFailedToAnalyze =
            "Algunos archivos Excel no se pudieron analizar. Revisá el panel de log y volvé a intentar.";

        public const string NeedsAssignment = "Hay que asignarlo";
        public const string Automatic = "Automático";
        public const string Append = "Agregar al final";
        public const string Manual = "Manual";

        public const string SelectModToFix = "Elegí el mod que querés corregir.";
        public const string OriginalModNotFound = "No se encontró el mod original. Asignalo a mano.";
        public const string CannotReextractUnknownMod =
            "No se puede volver a extraer porque no se conoce el mod original de este archivo.";

        public const string MenuOpenXlsxInExplorer = "Abrir el archivo Excel en el explorador";
        public const string MenuOpenModRootInExplorer = "Abrir la carpeta raíz del mod en el explorador";

        public const string SelectXlsxFile = "Elegí el archivo Excel (.xlsx).";
        public const string FilterTranslationXlsx = "Archivo Excel de traducción";
        public const string SelectXlsxRootFolder = "Elegí la carpeta raíz donde están los archivos Excel.";

        #endregion

        #region FormXmlister / FormStopCallback / Program

        public const string SelectLanguagesRootFolder = "Elegí la carpeta raíz que contiene la carpeta Languages.";

        public static string CouldNotSaveFileInUse(string message)
            => $"No se pudo guardar el archivo porque ya está en uso. {message}";

        public const string CompleteFolderSelection = "Completá la selección de carpetas.";

        #endregion

        #region Controles de los formularios
        //
        // Estos son los textos que en upstream viven en los .Designer.cs. No se
        // editan alli: el disenador de Visual Studio regenera esos archivos, y son
        // justo los que upstream toca al agregar controles. En su lugar cada
        // formulario los aplica en tiempo de ejecucion desde su ApplyStrings().

        // --- FormMain ---
        public const string BtnSelectMod = "1. Elegir el mod a extraer";
        public const string BtnExtract = "2. Extraer los datos de traducción";
        public const string BtnOptions = "Opciones";
        public const string LabelMainDescription =
            "Programa para extraer los datos de traducción del contenido oficial de RimWorld y de sus mods.\r\n";
        public const string BtnJpgPackager = "Unir imagen + archivo";
        public const string BtnOpenTranslationAnalyzer = "Abrir el analizador\r\nde traducciones (WIP)";
        public const string LabelNoModSelected = "No hay ningún mod elegido.";

        // --- FormSettings ---
        public const string LabelRimworldPath = "Ruta de RimWorld:";
        public const string LabelWorkshopPath = "Ruta del workshop:";
        public const string LabelVersionPattern = "Expresión regular de la versión:";
        public const string LabelSettingsTip =
            "Tip: pasá el mouse por encima de cada campo para ver una explicación detallada\r\n";
        public const string LabelRimworldVersion = "Versión base de RimWorld:";
        public const string BtnAutoDetect = "Detectar solo";
        public const string LabelOriginalLanguage = "Idioma original:";
        public const string LabelTranslationLanguage = "Idioma de traducción:";
        public const string LabelExtractionFormat = "Formato de extracción:";
        public const string BtnSaveAndClose = "Guardar y cerrar";
        public const string BtnCancel = "Cancelar";
        public const string BtnReset = "Restablecer\r\nvalores por defecto";
        public const string LabelExtractableTags = "Etiquetas extraíbles:";
        public const string LabelTranslationHandleTags = "Etiquetas de Translation Handle:";
        public const string LabelDuplicatePolicy = "Si al guardar hay archivos duplicados:";
        public const string GroupRimworldSettings = "Ajustes de RimWorld";
        public const string GroupBasicSettings = "Ajustes básicos de extracción y guardado";
        public const string LabelBaseRefListPath = "Ruta de la lista de mods de referencia por defecto:";
        public const string GroupAdvancedSettings = "Ajustes avanzados de extracción y guardado";
        public const string LabelFullListTags = "Etiquetas de Full-list Translation:";
        public const string LabelNodeReplacement = "Palabras clave de reemplazo de nodos:";
        public const string CheckBoxTkey = "(provisorio) Usar extracción de TKey";

        // --- FormSelectMod ---
        public const string BtnSelectionDone = "Listo";
        public const string LabelSelectModControls =
            "Controles: 'clic izquierdo' = elegir como mod a extraer, 'clic derecho' = abrir el menú, 'A' = abrir en el explorador, 'S' = elegir como mod de referencia, 'D' = ver solo los elegidos\r\n";
        public const string LabelSelectExtractionMode = "Elegí el mod que querés extraer";
        public const string LabelSelectFolder = "Elegí la carpeta que querés extraer";
        public const string CheckBoxFilterSelected = "Ver solo los mods elegidos";

        // --- FormImageFileCombiner ---
        public const string BtnDone = "Listo";
        public const string LabelSelectFileToCombine = "Elegí la ruta del archivo o carpeta a unir";
        public const string LabelSelectImagePath = "Elegí la ruta de la imagen (opcional)";
        public const string BtnSelectFile = "Elegir archivo";
        public const string BtnSelectFolder = "Elegir carpeta";
        public const string LabelCombinerHelp =
            "Herramienta que une un archivo de imagen con un archivo comprimido (.zip).\r\n" +
            "Cambiando la extensión de la imagen a '.zip' se puede ver el contenido del comprimido.\r\n" +
            "Si no se elige una imagen, se usa la imagen por defecto.\r\n" +
            "Si se elige una carpeta, se comprime automáticamente antes de unirla.";

        // --- FormTranslationAnalyzer ---
        public const string ColumnSelect = "Elegir";
        public const string ColumnModInfo = "Datos del mod";
        public const string ColumnFileName = "Nombre del archivo";
        public const string ColumnOriginalCount = "Cantidad de originales";
        public const string ColumnChanges = "Cambios";
        public const string ColumnReextractMethod = "Método de re-extracción";
        public const string ColumnSaveMethod = "Método de guardado";
        public const string BtnSelectModManually = "Elegir a mano el mod a re-extraer";
        public const string BtnFixSelectedFiles = "Corregir los archivos elegidos";
        public const string BtnDeselectAll = "Deseleccionar todo";
        public const string BtnSelectAllPossible = "Elegir todos los posibles";
        public const string LabelSaveMethod = "Método de guardado:";

        public const string SaveMethodAppend = "Agregar al final";
        public const string SaveMethodRebuildOverwrite = "Reconstruir (sobrescribir)";
        public const string SaveMethodRebuildNew = "Reconstruir (crear nuevo)";
        public const string SaveMethodOnlyNewNodes = "Crear nuevo solo con los nodos agregados";

        // --- FormTranslationAnalyzerPathSelect ---
        public const string BtnSelectSingleXlsx = "Elegir archivo Excel";
        public const string BtnSelectXlsxDir = "Elegir la carpeta con los archivos Excel";
        public const string LabelAnalyzerFaq =
            "P. ¿Qué es el analizador de traducciones?\r\n" +
            "R. Lee y analiza los archivos Excel que extrajiste antes,\r\n" +
            "y te avisa si el texto original cambió por una actualización del mod\r\n" +
            "o si se agregaron nodos nuevos que hay que traducir.\r\n" +
            "Si hay algo que corregir, actualiza automáticamente el Excel existente.\r\n";

        // --- FormInitialPathSelect ---
        public const string LabelRimworldPathShort = "Ruta de RimWorld";
        public const string LabelWorkshopPathShort = "Ruta del workshop";

        // --- FormStopCallback ---
        public const string LabelDuplicateFileFound = "Se encontró un archivo duplicado al guardar.";
        public const string BtnOverwrite = "Sobrescribir";
        public const string BtnSkip = "Omitir";

        // --- FormXmlister ---
        public const string LabelSelectLanguagesRoot =
            "Elegí la carpeta raíz que contiene la carpeta Languages. (se puede elegir más de una)";

        #endregion

        #region Titulos de ventana

        public const string TitleImageFileCombiner = "Unir imagen + archivo";
        public const string TitleInitialPathSelect = "Indicá las rutas de RimWorld y del workshop";
        public const string TitleSelectMod = "Elegí el mod que querés extraer";
        public const string TitleSettings = "Ajustes";
        public const string TitleStopCallback = "Archivo duplicado";
        public const string TitleTranslationAnalyzer = "Analizador de traducciones";
        public const string TitleAnalyzerPathSelect = "Elegí la ruta con los archivos Excel (se puede elegir más de una)";
        public const string TitleXmlister = "Herramienta de extracción XML -> XLSX";

        #endregion

        #region Chequeo de version

        public static string VersionUpToDate(string current) => $"{current} — es la última versión";

        public static string VersionUpdateAvailable(string current, string latest)
            => $"{current} < {latest} — hay una versión nueva";

        public static string VersionCheckFailed(string message)
            => $"No se pudo comprobar si hay una versión nueva: {message}";

        public const string BtnReportProblem = "Si aparece una advertencia o un error";

        #endregion

        #region PatchOperations

        public static string UnsupportedPatchOperation(string operation)
            => $"Tipo de PatchOperation no soportado: {operation}";

        public const string MissingXpathOrValue =
            "Falta el valor de xpath o de value. Formato XML de RimWorld inválido.";

        public static string SelectedNodeHasNoParent(string nodeName)
            => $"El nodo elegido {nodeName} no tiene nodo padre.";

        public static string PatchWithoutDefNameOrClassName(string xpath)
            => $"Patch sin defName ni className: xpath:{xpath}";

        public static string PatchWithoutDefNameUnsupported(string xpath, string value)
            => $"No se admiten los casos sin defName. xpath={xpath}, value={value}";

        #endregion




    }
}
