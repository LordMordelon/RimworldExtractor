using ClosedXML.Excel;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using RimworldExtractorInternal.Compats;
using RimworldExtractorInternal.DataTypes;
using RimworldExtractorInternal.Exceptions;

namespace RimworldExtractorInternal
{
    /// <summary>Que se escribe en el XML junto a cada entrada.</summary>
    public enum XmlCommentStyle
    {
        /// <summary>Solo el valor, sin comentario.</summary>
        None,

        /// <summary>El original como comentario, y el original tambien como valor.</summary>
        Original,

        /// <summary>
        /// El original como comentario y un marcador como valor, para traducir sobre el
        /// propio XML y poder distinguir de un vistazo lo que falta.
        /// </summary>
        TranslationTemplate
    }

    public static class IO
    {
        private static readonly string HeaderClassNode = "Class+Node [(Identifier (Key)]";
        private static readonly string HeaderClass = "Class [Not chosen]";
        private static readonly string HeaderNode = "Node [Not chosen]";
        private static readonly string HeaderRequiredMods = "Required Mods [Not chosen]";
        private const string HeaderSuffixOriginal = "[Source string]";
        private const string HeaderSuffixTranslated = "[Translation]";
        private static string HeaderOriginal => $"{Prefabs.OriginalLanguage} [Source string]";
        private static string HeaderTranslated => $"{Prefabs.TranslationLanguage} [Translation]";

        /// <summary>
        /// Comentario que conserva el texto original. Un comentario XML no puede contener
        /// la secuencia "--", de eso se encarga EscapeXmlCommentDashes.
        /// </summary>
        private static string OriginalComment(XmlCommentStyle style, string original)
        {
            var texto = SecurityElement.Escape(original).EscapeXmlCommentDashes();
            return style == XmlCommentStyle.TranslationTemplate
                ? $" {Strings.OriginalCommentPrefix} {texto} "
                : $"{Prefabs.OriginalLanguage}={texto}";
        }

        /// <summary>
        /// Valor de la entrada. En el modo para traducir a mano, lo que no esta traducido
        /// lleva un marcador en vez del original: si quedara el ingles no habria forma de
        /// distinguir lo traducido de lo que falta.
        /// </summary>
        private static string ValueFor(XmlCommentStyle style, TranslationEntry translation)
        {
            if (style == XmlCommentStyle.TranslationTemplate && string.IsNullOrEmpty(translation.Translated))
                return Strings.UntranslatedPlaceholder;

            return translation.Translated ?? translation.Original;
        }
        /// <summary>
        /// Descarta lo que no tiene ni texto original ni traduccion.
        ///
        /// Hay mods que traen claves vacias en su propio archivo en ingles
        /// —&lt;Defaults_EmptyString&gt;&lt;/Defaults_EmptyString&gt;—, y de ahi salian entradas que le
        /// piden al traductor que traduzca la nada. La extraccion no se toca: es fiel a lo que
        /// trae el mod. El descarte es una decision de que se escribe.
        ///
        /// La condicion mira las dos cosas y no solo el original a proposito. Con el original
        /// vacio igual puede haber una traduccion hecha, que ademas es la que pone texto donde
        /// el mod no muestra nada: en RML son 16 de Simple Sidearms, del estilo
        /// Preset1_label = "Solo equipamiento". Filtrar solo por original vacio las sacaria.
        /// </summary>
        private static bool HayAlgoQueEscribir(TranslationEntry entrada)
            => !string.IsNullOrWhiteSpace(entrada.Original)
               || !string.IsNullOrWhiteSpace(entrada.Translated);

        public static void ToExcel(List<TranslationEntry> translations, string outPath = "result",
            bool markNoTranslation = false)
        {
            translations = translations.Where(HayAlgoQueEscribir).ToList();

            var xlsx = new XLWorkbook();
            var sheet = xlsx.AddWorksheet();
            sheet.Cell(1, 1).Value = HeaderClassNode;
            sheet.Cell(1, 2).Value = HeaderClass;
            sheet.Cell(1, 3).Value = HeaderNode;
            sheet.Cell(1, 4).Value = HeaderRequiredMods;
            sheet.Cell(1, 5).Value = HeaderOriginal;
            sheet.Cell(1, 6).Value = HeaderTranslated;
            for (int i = 0; i < translations.Count; i++)
            {
                var entry = translations[i];
                sheet.Cell(2 + i, 1).Value = $"{entry.ClassName}+{entry.Node}";
                sheet.Cell(2 + i, 2).Value = entry.ClassName;
                sheet.Cell(2 + i, 3).Value = entry.Node;
                if (entry.RequiredMods != null)
                {
                    var combinedRequiredMods = entry.RequiredMods.ToString();
                    var cellRequiredMods = sheet.Cell(2 + i, 4);
                    cellRequiredMods.Value = combinedRequiredMods;
                    if (SinNombreEnNomatch(entry.RequiredMods) && entry.ClassName.StartsWith("Patches."))
                    {
                        Log.WrnOnce(Strings.InvalidRequiredModsValue(combinedRequiredMods),
                            $"RequiredMods-warn-{combinedRequiredMods}".GetHashCode());
                        var comment = cellRequiredMods.GetComment();
                        comment.AddText(
                            Strings.PackageIdInsteadOfModName(RequiredMods.PACKAGE_ID_PREFIX));
                        comment.Visible = true;
                    }
                }
                sheet.Cell(2 + i, 5).Value = entry.Original;
                if (entry.Translated != null)
                {
                    sheet.Cell(2 + i, 6).Value = entry.Translated;
                }
                else if (markNoTranslation)
                {
                    sheet.Cell(2 + i, 6).Style.Fill.SetBackgroundColor(XLColor.SkyBlue);
                }

                if (entry.TryGetExtension(Prefabs.ExtensionKeyExtraCommentTranslated, out object? extension) &&
                    extension is string extensionStr)
                {
                    var comment = sheet.Cell(2 + i, 6).CreateComment();
                    comment.AddText(extensionStr);
                    comment.Visible = true;
                }
            }

            sheet.Style.Font.FontName = Strings.ExcelFontName;
            xlsx.SaveSafely(outPath + ".xlsx");
        }
        public static void ModifyExcel(List<TranslationAnalyzerEntry.ChangeRecord> changes, string targetPath)
        {
            var xlsx = new XLWorkbook(targetPath);
            var mainSheet = xlsx.Worksheets.Worksheet(1);
            var rows = mainSheet.RowsUsed().ToList();
            var offset = rows.Count;
            var headers = rows.First().Cells();

            var colClassNode =
                headers.FirstOrDefault(x => x.StrVal() == HeaderClassNode)?.WorksheetColumn().ColumnNumber() ??
                throw new XlsxHeaderReadingException(HeaderClass);
            var colClass = headers.FirstOrDefault(x => x.StrVal() == HeaderClass)
                               ?.WorksheetColumn().ColumnNumber() ??
                           throw new XlsxHeaderReadingException(HeaderClass);
            var colNode = headers.FirstOrDefault(x => x.StrVal() == HeaderNode)
                              ?.WorksheetColumn().ColumnNumber() ??
                          throw new XlsxHeaderReadingException(HeaderNode);
            var colRequiredMods = headers
                .FirstOrDefault(x => x.StrVal() == HeaderRequiredMods)
                ?.WorksheetColumn().ColumnNumber() ?? -1;
            var colOriginal = headers.FirstOrDefault(x => x.StrVal() == HeaderOriginal)
                                  ?.WorksheetColumn().ColumnNumber() ??
                              headers.FirstOrDefault(x => x.StrVal().EndsWith(HeaderSuffixOriginal, StringComparison.Ordinal))
                                  ?.WorksheetColumn().ColumnNumber() ??
                              throw new XlsxHeaderReadingException(HeaderOriginal);
            var colTranslated = headers.FirstOrDefault(x => x.StrVal() == HeaderTranslated)
                                    ?.WorksheetColumn().ColumnNumber() ??
                                headers.FirstOrDefault(x => x.StrVal().EndsWith(HeaderSuffixTranslated, StringComparison.Ordinal))
                                    ?.WorksheetColumn().ColumnNumber() ??
                                throw new XlsxHeaderReadingException(HeaderTranslated);
            
            var changedOriginals = new List<TranslationAnalyzerEntry.ChangeRecord>();
            var fillOriginals = new List<TranslationAnalyzerEntry.ChangeRecord>();
            var removeNodes = new List<TranslationAnalyzerEntry.ChangeRecord>();
            var addedNewlys = new List<TranslationAnalyzerEntry.ChangeRecord>();
            var dateString = DateTime.Today.ToString("yyyy-MM-dd");

            foreach (var changeRecord in changes)
            {
                switch (changeRecord.Reason)
                {
                    case TranslationAnalyzerEntry.ChangeReason.ChangedOriginal:
                        changedOriginals.Add(changeRecord);
                        break;
                    case TranslationAnalyzerEntry.ChangeReason.FillOriginal:
                        fillOriginals.Add(changeRecord);
                        break;
                    case TranslationAnalyzerEntry.ChangeReason.RemoveNode:
                        removeNodes.Add(changeRecord);
                        break;
                    case TranslationAnalyzerEntry.ChangeReason.AddedNewly:
                        addedNewlys.Add(changeRecord);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (removeNodes.Count > 0)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var curRow = rows[i];
                    var curClassNode = curRow.Cell(colClassNode).StrVal();
                    var pairEntry =
                        removeNodes.FirstOrDefault(x => $"{x.Orig!.ClassName}+{x.Orig!.Node}" == curClassNode);
                    if (pairEntry != null)
                    {
                        var origCell = curRow.Cell(colOriginal);
                        origCell.GetComment().AddText(Strings.CommentDeleted(dateString, curRow.Cell(colTranslated).StrVal()));
                        origCell.GetComment().Visible = true;
                        origCell.Style.Fill.SetBackgroundColor(XLColor.Red);
                        curRow.Cell(colTranslated).Clear();
                    }
                }
                //var subSheet = xlsx.AddWorksheet($"{dateString}_nodos eliminados");
                //subSheet.Cell(1, 1).Value = HeaderClassNode;
                //subSheet.Cell(1, 2).Value = HeaderClass;
                //subSheet.Cell(1, 3).Value = HeaderNode;
                //subSheet.Cell(1, 4).Value = HeaderRequiredMods;
                //subSheet.Cell(1, 5).Value = HeaderOriginal;
                //subSheet.Cell(1, 6).Value = HeaderTranslated;
                //var nxtRow = subSheet.Row(2);
                //for (int i = rows.Count - 1; i >= 0; i--)
                //{
                //    var curRow = rows[i];
                //    var curClassNode = curRow.Cell(colClassNode).StrVal();
                //    if (removeNodes.Any(x => $"{x.Orig!.ClassName}+{x.Orig!.Node}" == curClassNode))
                //    {
                //        curRow.Cell(colClassNode).CopyTo(nxtRow.Cell(1));
                //        curRow.Cell(colClass).CopyTo(nxtRow.Cell(2));
                //        curRow.Cell(colNode).CopyTo(nxtRow.Cell(3));
                //        if (colRequiredMods != -1)
                //            curRow.Cell(colRequiredMods).CopyTo(nxtRow.Cell(4));
                //        curRow.Cell(colOriginal).CopyTo(nxtRow.Cell(5));
                //        curRow.Cell(colTranslated).CopyTo(nxtRow.Cell(6));
                //        for (int j = 0; j < mainSheet.ColumnsUsed().Count() - colTranslated + 1; j++)
                //        {
                //            curRow.Cell(j + colTranslated + 1).CopyTo(nxtRow.Cell(j + 6 + 1));
                //        }
                //        curRow.Cells(colTranslated + 1, mainSheet.ColumnsUsed().Count());

                //        nxtRow = nxtRow.RowBelow();
                //        curRow.Delete();
                //    }
                //}

                //rows = mainSheet.RowsUsed().ToList();
            }

            if (changedOriginals.Count > 0)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var curRow = rows[i];
                    var curClassNode = curRow.Cell(colClassNode).StrVal();
                    var pairEntry =
                        changedOriginals.FirstOrDefault(x => $"{x.Orig!.ClassName}+{x.Orig!.Node}" == curClassNode);
                    if (pairEntry != null)
                    {
                        var origCell = curRow.Cell(colOriginal);
                        origCell.GetComment().AddText(Strings.CommentPreviousOriginal(dateString, curRow.Cell(colOriginal).StrVal()));
                        origCell.Value = pairEntry.New!.Original;
                        origCell.GetComment().Visible = true;
                        origCell.Style.Fill.SetBackgroundColor(XLColor.Orange);
                    }
                }
            }

            if (fillOriginals.Count > 0)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    var curRow = rows[i];
                    var curClassNode = curRow.Cell(colClassNode).StrVal();
                    var pairEntry =
                        fillOriginals.FirstOrDefault(x => $"{x.Orig!.ClassName}+{x.Orig!.Node}" == curClassNode);
                    if (pairEntry != null)
                    {
                        var origCell = curRow.Cell(colOriginal);
                        origCell.GetComment()
                            .AddText(Strings.CommentOriginalRestored(dateString));
                        origCell.Value = pairEntry.New!.Original;
                        origCell.GetComment().Visible = true;
                        origCell.Style.Fill.SetBackgroundColor(XLColor.Orange);
                    }
                }
            }

            if (addedNewlys.Count > 0)
            {
                for (int i = 0; i < addedNewlys.Count; i++)
                {
                    var entry = addedNewlys[i].New!;
                    mainSheet.Cell(2 + i + rows.Count, colClassNode).Value = $"{entry.ClassName}+{entry.Node}";
                    mainSheet.Cell(2 + i + rows.Count, colClass).Value = entry.ClassName;
                    mainSheet.Cell(2 + i + rows.Count, colNode).Value = entry.Node;
                    if (colRequiredMods != -1 && entry.RequiredMods != null)
                    {
                        var combinedRequiredMods = entry.RequiredMods.ToString();
                        mainSheet.Cell(2 + i + rows.Count, colRequiredMods).Value = combinedRequiredMods;
                        if (SinNombreEnNomatch(entry.RequiredMods) && entry.ClassName.StartsWith("Patches."))
                        {
                            Log.WrnOnce(Strings.InvalidRequiredModsValue(combinedRequiredMods),
                                $"RequiredMods-warn-{combinedRequiredMods}".GetHashCode());
                        }
                    }
                    mainSheet.Cell(2 + i + rows.Count, colOriginal).Value = entry.Original;
                    if (entry.Translated != null)
                    {
                        mainSheet.Cell(2 + i + rows.Count, colTranslated).Value = entry.Translated;
                    }

                    if (entry.TryGetExtension(Prefabs.ExtensionKeyExtraCommentTranslated, out object? extension) &&
                        extension is string extensionStr)
                    {
                        var comment = mainSheet.Cell(2 + i + rows.Count, 6).GetComment();
                        comment.AddText(extensionStr);
                        comment.Visible = true;
                    }
                    mainSheet.Row(2 + i + rows.Count).Select();

                    if (i == 0)
                    {
                        mainSheet.Cell(2 + i + rows.Count, colOriginal).Style.Fill.SetBackgroundColor(XLColor.SkyBlue);
                        var comment = mainSheet.Cell(2 + i + rows.Count, colOriginal).GetComment();
                        comment.AddText(Strings.CommentNewlyAdded(dateString, addedNewlys.Count));
                        comment.Visible = true;
                    }
                }
            }

            mainSheet.Style.Font.FontName = Strings.ExcelFontName;
            foreach (var cell in mainSheet.CellsUsed().Where(x => x.HasComment))
            {
                var comment = cell.GetComment();
                comment.Position.ColumnOffset = 5d;
                comment.Position.RowOffset = 5d;
                comment.Position.Row = cell.Address.RowNumber + 1;
                comment.Position.Column = cell.Address.ColumnNumber + 1;
                comment.Style.Alignment.SetAutomaticSize();
            }
            xlsx.SaveSafely(targetPath);
        }

        public static List<TranslationEntry> FromExcel(string inputPath)
        {
            using var libFixer = new LibreExcelFixer(inputPath);
            inputPath = libFixer.DoFix();

            var xlsx = new XLWorkbook(inputPath);
            var sheet = xlsx.Worksheets.Worksheet(1);
            var translations = new List<TranslationEntry>();
            var rows = sheet.RowsUsed().ToList();
            var headers = rows.First().Cells();

            var colClass = headers.FirstOrDefault(x => x.StrVal() == HeaderClass)
                               ?.WorksheetColumn().ColumnNumber() ??
                           throw new XlsxHeaderReadingException(HeaderClass);
            var colNode = headers.FirstOrDefault(x => x.StrVal() == HeaderNode)
                ?.WorksheetColumn().ColumnNumber() ??
                          throw new XlsxHeaderReadingException(HeaderNode);
            var colRequiredMods = headers
                .FirstOrDefault(x => x.StrVal() == HeaderRequiredMods)
                ?.WorksheetColumn().ColumnNumber() ?? -1;
            var colOriginal = headers.FirstOrDefault(x => x.StrVal() == HeaderOriginal)
                                  ?.WorksheetColumn().ColumnNumber() ??
                              headers.FirstOrDefault(x => x.StrVal().EndsWith(HeaderSuffixOriginal, StringComparison.Ordinal))
                                  ?.WorksheetColumn().ColumnNumber() ??
                              throw new XlsxHeaderReadingException(HeaderOriginal);
            var colTranslated = headers.FirstOrDefault(x => x.StrVal() == HeaderTranslated)
                                    ?.WorksheetColumn().ColumnNumber() ??
                                headers.FirstOrDefault(x => x.StrVal().EndsWith(HeaderSuffixTranslated, StringComparison.Ordinal))
                                    ?.WorksheetColumn().ColumnNumber() ??
                                throw new XlsxHeaderReadingException(HeaderTranslated);



            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var className = row.Cell(colClass).StrVal();
                var node = row.Cell(colNode).StrVal();
                if (className.Length == 0 || node.Length == 0)
                    continue;
                RequiredMods? requiredMods = null;
                if (colRequiredMods != -1 && row.Cell(colRequiredMods).Value is { IsText: true } cellRequiredMods)
                {
                    var textRequiredMods = cellRequiredMods.GetText();
                    // Compatibilidad hacia atras
                    if (textRequiredMods != null && textRequiredMods.Contains('\n'))
                    {
                        requiredMods = new RequiredMods();
                        foreach (var s in textRequiredMods.Split('\n'))
                        {
                            requiredMods.AddAllowedByModName(s);
                        }
                    }
                    else if (textRequiredMods != null)
                    {
                        requiredMods = RequiredMods.FromStringByModNames(textRequiredMods);
                    }
                }
                var original = row.Cell(colOriginal).Value.IsBlank ? "" : row.Cell(colOriginal).StrVal();
                var cellTranslated = row.Cell(colTranslated).Value;
                var translated = cellTranslated.IsText ? (cellTranslated.GetText() == "" ? null : cellTranslated.GetText()) : null;

                var translation = new TranslationEntry(className, node, original,
                    translated, requiredMods, null);
                translations.Add(translation);
            }
            return translations;
        }
        
        public static void ToLanguageXml(List<TranslationEntry> translations, bool skipNoTranslation, XmlCommentStyle commentStyle, string ModName, string rootDirPath)
        {
            translations = translations.Where(HayAlgoQueEscribir).ToList();

            var languagesDir = PathCombineCreateDir(rootDirPath, "Languages");
            var translationDir = PathCombineCreateDir(languagesDir, Prefabs.TranslationLanguage);
            var defInjected = new List<TranslationEntry>();
            var defInjectedFullListTranslations = new List<TranslationEntry>();
            var keyed = new List<TranslationEntry>();
            var strings = new List<TranslationEntry>();
            var patches = new List<TranslationEntry>();
            var patchedNodeSet = new HashSet<string>();

            var isOfficial = translations.Any(x => x.SourceFile != null);
            if (isOfficial)
            {
                Log.Msg(Strings.OfficialContentKeepsFileNames);
            }

            var reencaminadas = PatchesQueElJuegoPisa(translations);

            foreach (var entrada in translations)
            {
                var translation = reencaminadas.Contains(entrada.ClassNode)
                    ? entrada with { ClassName = SinPrefijoDePatches(entrada.ClassName) }
                    : entrada;
                var className = translation.ClassName;

                if (skipNoTranslation && className != "Strings" && string.IsNullOrEmpty(translation.Translated))
                {
                    continue;
                }

                switch (className)
                {
                    case "Keyed":
                        keyed.Add(translation);
                        break;
                    case "Strings":
                        strings.Add(translation);
                        break;
                    default:
                        {
                            if (className.StartsWith("Patches."))
                                patches.Add(translation);
                            else if (!isOfficial && Prefabs.FullListTranslationTags.Any(translation.Node.Contains))
                                defInjectedFullListTranslations.Add(translation);
                            else
                                defInjected.Add(translation);
                            break;
                        }
                }
            }

            if (skipNoTranslation && patches.Count == 0 && defInjected.Count == 0 &&
                keyed.Count == 0 && translations.Count > 0 && defInjectedFullListTranslations.Count == 0)
            {
                Log.Wrn(Strings.NothingToExtract);
            }

            // Un archivo por mod parcheado, para ver de un vistazo a que le aplica cada cosa
            // y poder manejarlos por separado. El dueño de cada def se anota al cargar los
            // defs de referencia; lo que no figure ahi es del propio mod.
            foreach (var grupoDePatches in patches
                         .GroupBy(x => Extractor.DuenioPorDefName.TryGetValue(x.Node.Split('.').First(), out var duenio)
                             ? duenio
                             : ModName))
            {
                var patchesDelMod = grupoDePatches.ToList();
                var outputPath = PathCombineCreateDir(rootDirPath, "Patches");

                var docPatch = new XmlDocument();
                docPatch.AppendElement("Patch");
                var root = docPatch.DocumentElement ?? throw new InvalidOperationException();

                var entryDict = new Dictionary<string, XmlElement>();

                // Arma el diccionario base segun RequiredMods
                foreach (var translation in CompatManager.DoPostProcessing(patchesDelMod))
                {
                    var requiredMods = translation.RequiredMods;
                    if (requiredMods == null || entryDict.ContainsKey(requiredMods.ToString()))
                        continue;

                    var aboveNode = root.AppendElement("Operation");

                    // Los grupos que nombran un mod por packageId sin nombre conocido no pueden
                    // ir por FindMod, que compara nombres: se juntan aca y van por MayRequire,
                    // mas abajo, dentro de la secuencia.
                    var porPackageId = new List<string[]>();
                    foreach (var allowedMod in requiredMods.AllowedMods)
                    {
                        var packageIds = PackageIdsParaMayRequire(allowedMod.Split(RequiredMods.OR_IDENTIFIER));
                        if (packageIds != null)
                        {
                            porPackageId.Add(packageIds);
                            continue;
                        }

                        aboveNode.Append(operationFindMod =>
                        {
                            operationFindMod.AppendAttribute("Class", "PatchOperationFindMod");
                            operationFindMod.AppendElement("mods", mods =>
                            {
                                var allowedModSplited = allowedMod.Split(RequiredMods.OR_IDENTIFIER);
                                foreach (var allowedModToken in allowedModSplited)
                                {
                                    if (allowedModToken.Contains("##packageId##"))
                                    {
                                        Log.ErrOnce(
                                            Strings.InvalidRequiredModsValue(allowedModToken),
                                            $"RequiredMods-err-{allowedModToken}".GetHashCode());
                                    }

                                    mods.AppendElement("li", allowedModToken);
                                }
                            });
                            aboveNode = operationFindMod.AppendElement("match");
                        });
                    }

                    foreach (var disallowedMod in requiredMods.DisallowedMods)
                    {
                        aboveNode.Append(operationFindMod =>
                        {
                            operationFindMod.AppendAttribute("Class", "PatchOperationFindMod");
                            operationFindMod.AppendElement("mods", mods =>
                            {
                                var disallowedModSplited = disallowedMod.Split(RequiredMods.OR_IDENTIFIER);
                                foreach (var disallowedModToken in disallowedModSplited)
                                {
                                    if (disallowedModToken.Contains("##packageId##"))
                                    {
                                        Log.ErrOnce(
                                            Strings.InvalidRequiredModsValue(disallowedModToken),
                                            $"RequiredMods-err-{disallowedModToken}".GetHashCode());
                                    }

                                    mods.AppendElement("li", disallowedModToken);
                                }
                            });
                            aboveNode = operationFindMod.AppendElement("nomatch");
                        });
                    }

                    aboveNode.Append(operationSequence =>
                    {
                        operationSequence.AppendAttribute("Class", "PatchOperationSequence");
                        operationSequence.AppendElement("success", "Always");
                        var operations = operationSequence.AppendElement("operations");

                        // MayRequire en un li de lista es lo que RimWorld resuelve siempre, en
                        // cualquier nivel. Un nivel por grupo: cada li lleva un solo atributo, y
                        // anidados quedan en AND como los FindMod de arriba.
                        foreach (var packageIds in porPackageId)
                        {
                            var condicion = operations.AppendElement("li");
                            condicion.AppendAttribute("Class", "PatchOperationSequence");
                            condicion.AppendAttribute(packageIds.Length == 1 ? "MayRequire" : "MayRequireAnyOf",
                                string.Join(",", packageIds));
                            condicion.AppendElement("success", "Always");
                            operations = condicion.AppendElement("operations");
                        }

                        entryDict[requiredMods.ToString()] = operations;
                    });
                }

                foreach (var translation in patchesDelMod)
                {
                    var requiredMods = translation.RequiredMods;
                    XmlElement operation;
                    if (requiredMods != null)
                    {
                        operation = entryDict[requiredMods.ToString()].AppendElement("li");
                    }
                    else
                    {
                        operation = root.AppendElement("Operation");
                    }

                    operation.Append(li =>
                    {
                        li.AppendAttribute("Class", "PatchOperationReplace");
                        li.AppendElement("success", "Always");
                        li.AppendElement("xpath", Utils.GetXpath(translation.ClassName[(translation.ClassName.IndexOf('.') + 1)..], translation.Node));
                        li.AppendElement("value", value =>
                        {
                            // El original va pegado al nodo que hay que traducir, no arriba
                            // de todo: es lo que uno mira mientras traduce. Al releer no
                            // molesta porque ReadXml descarta los comentarios.
                            if (commentStyle != XmlCommentStyle.None)
                                value.AppendComment(OriginalComment(commentStyle, translation.Original));

                            var lastNode = translation.Node.Split('.').Last();
                            if (int.TryParse(lastNode, out _)) lastNode = "li";
                            value.AppendElement(lastNode, ValueFor(commentStyle, translation));
                        });
                    });

                }

                // Con el nombre del mod y no codificado: es lo que hace legible la carpeta, y
                // sigue siendo estable —mismo mod, mismo archivo—, que es la propiedad por la
                // que se codificaban, para que una re-extraccion sobrescriba en vez de duplicar.
                docPatch.SaveSafely(Path.Combine(outputPath, grupoDePatches.Key.StripInvaildChars() + ".xml"));
            }

            if (defInjected.Count > 0)
            {
                var defInjectedDir = PathCombineCreateDir(translationDir, "DefInjected");
                var xmls = new Dictionary<string, XmlDocument>();
                foreach (var translation in CompatManager.DoPostProcessing(defInjected))
                {
                    if (patchedNodeSet.Contains(translation.Node))
                        continue;
                    PathCombineCreateDir(defInjectedDir, translation.ClassName);
                    if (!xmls.TryGetValue($"{translation.ClassName}|{translation.SourceFile}", out var doc))
                    {
                        doc = new XmlDocument();
                        xmls[$"{translation.ClassName}|{translation.SourceFile}"] = doc;
                        doc.AppendElement("LanguageData");
                    }

                    doc.DocumentElement!.Append(languageData =>
                    {
                        if (commentStyle != XmlCommentStyle.None)
                            languageData.AppendComment(OriginalComment(commentStyle, translation.Original));
                        languageData.AppendElement(translation.Node, t =>
                        {
                            t.InnerText = ValueFor(commentStyle, translation);
                            if (!t.InnerText.Contains("{*")) return;
                            t.InnerText = Regex.Replace(t.InnerText, "\\{\\*(.*?)\\}", match =>
                            {
                                var targetIdentifier = match.Groups[1].Value;
                                var replacement = translations.FirstOrDefault(x => $"{x.ClassName}+{x.Node}" == targetIdentifier);
                                if (replacement != null)
                                    return replacement.Translated ?? replacement.Original;
                                Log.Err(Strings.OriginalIdentifierNotFound(targetIdentifier));
                                return "ERR";
                            });
                        });
                    });

                }

                foreach (var (key, doc) in xmls)
                {
                    var tokens = key.Split('|');
                    var className = tokens[0];
                    var outputPath = isOfficial
                        ? Path.Combine(defInjectedDir, className, tokens[1] + ".xml")
                        : Path.Combine(defInjectedDir, className, Utils.GenerateFileName(ModName, key) + ".xml");

                    doc.DoFullListTranslation();
                    doc.SaveSafely(outputPath);
                }
            }

            if (defInjectedFullListTranslations.Count > 0)
            {
                var defInjectedDir = PathCombineCreateDir(translationDir, "DefInjected");
                var xmls = new Dictionary<(string, string), XmlDocument>();
                foreach (var translation in CompatManager.DoPostProcessing(defInjectedFullListTranslations))
                {
                    if (patchedNodeSet.Contains(translation.Node))
                        continue;
                    PathCombineCreateDir(defInjectedDir, translation.ClassName);
                    var nodeParent = translation.Node[..translation.Node.LastIndexOf('.')];
                    if (!xmls.TryGetValue((translation.ClassName, nodeParent), out var doc))
                    {
                        doc = new XmlDocument();
                        xmls[(translation.ClassName, nodeParent)] = doc;
                        doc.AppendElement("LanguageData");
                    }

                    doc.DocumentElement!.Append(languageData =>
                    {
                        if (commentStyle != XmlCommentStyle.None)
                            languageData.AppendComment(OriginalComment(commentStyle, translation.Original));
                        languageData.AppendElement(translation.Node, t =>
                        {
                            t.InnerText = ValueFor(commentStyle, translation);
                            if (!t.InnerText.Contains("{*")) return;
                            t.InnerText = Regex.Replace(t.InnerText, "\\{\\*(.*?)\\}", match =>
                            {
                                var targetIdentifier = match.Groups[1].Value;
                                var replacement = translations.FirstOrDefault(x => $"{x.ClassName}+{x.Node}" == targetIdentifier);
                                if (replacement != null)
                                    return replacement.Translated ?? replacement.Original;
                                Log.Err(Strings.OriginalIdentifierNotFound(targetIdentifier));
                                return "ERR";
                            });
                        });
                    });

                }
                
                foreach (var ((className, nodeParent), doc) in xmls)
                {
                    var tokens = nodeParent.Split('.');
                    var outputPath = Path.Combine(defInjectedDir, className,
                        Utils.GenerateFileName(ModName, className, nodeParent) + ".xml");

                    doc.DoFullListTranslation();
                    doc.SaveSafely(outputPath);
                }
            }

            if (keyed.Count > 0)
            {
                var keyedDir = PathCombineCreateDir(translationDir, "Keyed");
                var xmls = new Dictionary<string, XmlDocument>();
                foreach (var translation in keyed)
                {
                    var key = isOfficial ? translation.SourceFile! : "default";

                    if (!xmls.TryGetValue(key, out var doc))
                    {
                        doc = new XmlDocument();
                        xmls[key] = doc;
                        doc.AppendElement("LanguageData");
                    }

                    doc.DocumentElement!.Append(languageData =>
                    {
                        if (commentStyle != XmlCommentStyle.None)
                            languageData.AppendComment(OriginalComment(commentStyle, translation.Original));
                        languageData.AppendElement(translation.Node, ValueFor(commentStyle, translation));
                    });
                }

                foreach (var (key, doc) in xmls)
                {
                    var outputPath = isOfficial ? Path.Combine(keyedDir, $"{key}.xml"): Path.Combine(keyedDir, Utils.GenerateFileName(ModName, "Keyed") + ".xml");
                    doc.SaveSafely(outputPath);
                }
            }

            if (strings.Count > 0)
            {
                var stringDir = PathCombineCreateDir(translationDir, "Strings");
                var txts = new Dictionary<string, List<string>>();
                foreach (var translation in strings)
                {
                    var className = translation.Node[..translation.Node.LastIndexOf('.')];
                    if (!txts.TryGetValue(className, out var lines))
                    {
                        lines = new List<string>();
                        txts[className] = lines;
                    }

                    lines.Add(translation.Translated ?? translation.Original);
                }

                foreach (var (className, lines) in txts)
                {
                    var key = className[..className.LastIndexOf('.')].Replace('.', '\\');
                    var outputPath = PathCombineCreateDir(stringDir, key);
                    var fileNameTxt = Path.Combine(outputPath, $"{className.Split('.').Last()}") + ".txt";
                    lines.SaveSafely(fileNameTxt);
                }
            }
        }

        /// <summary>
        /// El marcador significa "todavia sin traducir". Sin esto, reimportar un XML del
        /// modo para traducir a mano metería la palabra TODO como traduccion en la planilla.
        /// </summary>
        private static string? TranslatedFromXml(string innerText)
            => innerText == Strings.UntranslatedPlaceholder ? null : innerText;

        /// <summary>
        /// Recupera el texto original del comentario que lo precede, si lo hay. Antes ese
        /// dato se perdia al reimportar y la columna del original quedaba vacia.
        /// </summary>
        private static string OriginalFromComment(XmlNode node)
        {
            if (node.PreviousSibling is not XmlComment comment)
                return string.Empty;

            var texto = comment.Value?.Trim() ?? string.Empty;
            return texto.StartsWith(Strings.OriginalCommentPrefix, StringComparison.Ordinal)
                ? texto[Strings.OriginalCommentPrefix.Length..].Trim()
                : string.Empty;
        }

        public static List<TranslationEntry> FromLanguageXml(string rootPath)
        {
            var translationsDir = Path.Combine(rootPath, "Languages", Prefabs.TranslationLanguage);
            if (!Directory.Exists(translationsDir))
                translationsDir = Path.Combine(rootPath, "Languages", Prefabs.TranslationLanguage.Split(' ').First());

            var defInjectedDir = Path.Combine(translationsDir, "DefInjected");
            var keyedDir = Path.Combine(translationsDir, "Keyed");
            var stringsDir = Path.Combine(translationsDir, "Strings");
            var patchesDir = Path.Combine(rootPath, "Patches");

            var translations = new List<TranslationEntry>();

            // Los Patches se leen primero a proposito, aunque vivan fuera de Languages.
            //
            // Un mismo nodo puede estar traducido de las dos formas a la vez, y TranslationMerge
            // las unifica —le saca el prefijo "Patches."— asi que una tiene que ganar. Gana la
            // que se lee despues, y tiene que ser la de DefInjected: es la que el juego aplica
            // ultima, y en la practica es la que esta al dia. En tres mods de AobaKuma la de
            // Patches habia quedado de una version anterior del mod, con la lista de tools en
            // otro orden, y al releer daba vuelta veinte traducciones correctas.
            if (Directory.Exists(patchesDir))
            {
                // Con la base de defs completa: estos xpath apuntan a defs del juego o de otros
                // mods, y para cuando se llega aca la extraccion ya reemplazo CombinedDefs por
                // el documento reducido de sus propios patches. Contra ese, no encuentran nada
                // y la traduccion que ya estaba hecha se pierde sin avisar.
                var patches = new ExtractableFolder(ModMetadata.Emptry, patchesDir, null);
                translations.AddRange(Extractor.ConLaBaseCompleta(
                    () => Extractor.ExtractPatches(patches)
                        .Select(x => x with { Translated = TranslatedFromXml(x.Original), Original = "" })
                        .ToList()));

                // Y las que ni asi aparecieron, parseando el archivo. Ver LeerPatchesLiteral.
                var vistas = new HashSet<(string, string)>(
                    translations.Select(x => (SinPrefijoDePatches(x.ClassName), x.Node)));
                foreach (var suelta in LeerPatchesLiteral(patchesDir))
                {
                    if (vistas.Add((SinPrefijoDePatches(suelta.ClassName), suelta.Node)))
                        translations.Add(suelta);
                }
            }



            foreach (var filePath in DescendantFiles(defInjectedDir).Where(x => x.ToLower().EndsWith(".xml")))
            {
                var className = Path.GetRelativePath(defInjectedDir, filePath).Split(Path.DirectorySeparatorChar).First();
                try
                {
                    var doc = ReadXmlKeepingComments(filePath);
                    foreach (var node in doc.DocumentElement!.ChildNodes.OfType<XmlElement>())
                    {
                        var name = node.Name;
                        var original = OriginalFromComment(node);
                        // Si es FullTranslation
                        if (node.ChildNodes.OfType<XmlNode>().All(x => x.NodeType == XmlNodeType.Element))
                        {
                            for (int i = 0; i < node.ChildNodes.Count; i++)
                            {
                                translations.Add(new TranslationEntry(className, $"{name}.{i}", original,
                                    TranslatedFromXml(node.ChildNodes[i]!.InnerText), null, null));
                            }
                        }
                        else
                        {
                            translations.Add(new TranslationEntry(className, name, original,
                                TranslatedFromXml(node.InnerText), null, null));
                        }
                    }
                }
                catch (Exception e)
                {
                    Log.Err(Strings.ErrorReadingFileColon(filePath, e.Message));
                    throw;
                }
            }

            // En estas tres el texto del nodo es la traduccion, no el original: se leen con
            // el extractor y se da vuelta el par. TranslatedFromXml es lo que evita que el
            // marcador TODO entre como si fuera una traduccion de verdad, que lo dejaria
            // "traducido" para siempre y nunca volveria a aparecer como pendiente.
            var keyed = new ExtractableFolder(ModMetadata.Emptry, keyedDir, null);
            translations.AddRange(Extractor.ExtractKeyed(keyed)
                .Select(x => x with { Translated = TranslatedFromXml(x.Original), Original = "" }));

            var strings = new ExtractableFolder(ModMetadata.Emptry, stringsDir, null);
            translations.AddRange(Extractor.ExtractStrings(strings)
                .Select(x => x with { Translated = TranslatedFromXml(x.Original), Original = "" }));

            return translations;
        }


        /// <summary>
        /// Los packageId con los que condicionar por MayRequire un grupo de RequiredMods (los
        /// mods de un "||"), o null si el grupo se escribe como PatchOperationFindMod.
        ///
        /// PatchOperationFindMod compara nombres de mod. Cuando el mod no esta instalado en esta
        /// maquina el nombre no se conoce y queda el packageId con su prefijo, y escrito asi el
        /// FindMod no coincide nunca: la traduccion no se aplica ni teniendo el mod. Paso con
        /// Cerebrex en GravTech. MayRequire, en cambio, toma packageIds, que es justo lo que hay.
        ///
        /// Los grupos que se resuelven del todo por nombre siguen como FindMod, para no cambiar
        /// lo que ya estaba bien escrito. Y vuelve a FindMod el grupo en el que algun nombre no
        /// tiene packageId conocido: no hay forma de armarlo entero, y el FindMod lo avisa.
        /// </summary>
        private static string[]? PackageIdsParaMayRequire(string[] tokens)
        {
            if (!tokens.Any(x => x.StartsWith(RequiredMods.PACKAGE_ID_PREFIX, StringComparison.Ordinal)))
                return null;

            var packageIds = new string[tokens.Length];
            for (var i = 0; i < tokens.Length; i++)
            {
                var packageId = tokens[i].StartsWith(RequiredMods.PACKAGE_ID_PREFIX, StringComparison.Ordinal)
                    ? tokens[i][RequiredMods.PACKAGE_ID_PREFIX.Length..]
                    : ModLister.GetModMetadataByModName(tokens[i])?.PackageId;
                if (packageId == null)
                    return null;
                packageIds[i] = packageId;
            }

            return packageIds;
        }

        /// <summary>
        /// Si hay un packageId sin nombre en la parte que exige que un mod NO este. Es lo unico
        /// que sigue sin poder escribirse: no hay un MayRequire al reves, y un nomatch de
        /// FindMod necesita el nombre. Lo demas lo resuelve PackageIdsParaMayRequire.
        /// </summary>
        private static bool SinNombreEnNomatch(RequiredMods requiredMods)
            => requiredMods.DisallowedMods.Any(x => x.Contains(RequiredMods.PACKAGE_ID_PREFIX, StringComparison.Ordinal));

        /// <summary>El prefijo con el que el extractor marca lo que sale por un patch.</summary>
        private static string SinPrefijoDePatches(string className) =>
            className.StartsWith("Patches.", StringComparison.Ordinal) ? className["Patches.".Length..] : className;

        /// <summary>
        /// Cuales de las traducciones que saldrian como Patches tienen que salir como
        /// DefInjected, porque el juego ya traduce ese nodo y un patch no le puede ganar.
        ///
        /// RimWorld aplica las PatchOperation antes de inyectar los DefInjected, asi que sobre
        /// un def de Core o de un DLC que la traduccion oficial ya cubre, el patch se aplica y
        /// se pisa un paso despues. En pantalla queda el texto oficial y nada avisa. Lo unico
        /// que gana es otro DefInjected, y el nuestro gana porque los DLC cargan siempre antes
        /// que cualquier mod.
        ///
        /// Devuelve las claves ClassName+Node a reencaminar, y avisa por las que no se pueden.
        /// </summary>
        private static HashSet<string> PatchesQueElJuegoPisa(List<TranslationEntry> translations)
        {
            var reencaminadas = new HashSet<string>();
            var sinConvertir = new List<TranslationEntry>();

            // Los elementos de una lista se deciden en conjunto, no de a uno: una inyeccion de
            // lista reemplaza la lista entera, asi que emitirla incompleta es peor que dejar
            // el patch. (clase, nodo de la lista) -> indices que tenemos.
            var listas = new Dictionary<(string, string), List<TranslationEntry>>();

            foreach (var entrada in translations)
            {
                if (!entrada.ClassName.StartsWith("Patches.", StringComparison.Ordinal))
                    continue;

                // Un PatchOperationFindMod es condicional y un DefInjected no puede serlo:
                // inyectarlo sin condicion aplicaria la traduccion de un texto que sin ese mod
                // no existe.
                var esCondicional = entrada.RequiredMods != null
                                    && (entrada.RequiredMods.CountAllowed > 0
                                        || entrada.RequiredMods.CountDisallowed > 0);

                if (!Extractor.DefsDeContenidoOficial.Contains(entrada.DefName))
                    continue;

                var clase = SinPrefijoDePatches(entrada.ClassName);

                if (TraduccionOficial.Cubre(clase, entrada.Node))
                {
                    if (esCondicional)
                        sinConvertir.Add(entrada);
                    else
                        reencaminadas.Add(entrada.ClassNode);
                    continue;
                }

                // El nodo puede ser un elemento de una lista que el oficial inyecta entera.
                var corte = entrada.Node.LastIndexOf('.');
                if (corte < 0 || !int.TryParse(entrada.Node[(corte + 1)..], out _))
                    continue;

                var nodoLista = entrada.Node[..corte];
                if (TraduccionOficial.CantidadDeLista(clase, nodoLista) == null)
                    continue;

                if (esCondicional)
                {
                    sinConvertir.Add(entrada);
                    continue;
                }

                if (!listas.TryGetValue((clase, nodoLista), out var elementos))
                    listas[(clase, nodoLista)] = elementos = new List<TranslationEntry>();
                elementos.Add(entrada);
            }

            foreach (var ((clase, nodoLista), elementos) in listas)
            {
                // Solo si tenemos la lista completa: los indices 0..n-1, con n el que declara
                // el oficial. Con cualquier otra cosa RimWorld avisa por conteo y no aplica ni
                // eso, asi que conviene mas el patch de hoy.
                var declarados = TraduccionOficial.CantidadDeLista(clase, nodoLista)!.Value;
                var indices = elementos
                    .Select(x => int.Parse(x.Node[(x.Node.LastIndexOf('.') + 1)..]))
                    .ToHashSet();

                if (indices.Count == declarados && Enumerable.Range(0, declarados).All(indices.Contains))
                {
                    foreach (var elemento in elementos)
                        reencaminadas.Add(elemento.ClassNode);
                }
                else
                {
                    sinConvertir.AddRange(elementos);
                }
            }

            if (reencaminadas.Count > 0)
                Log.Msg(Strings.OfficialTranslationBeatsPatch(reencaminadas.Count));

            // El defecto era invisible; dejar invisible lo que queda sin arreglar seria
            // repetirlo. Estas se siguen escribiendo como patch y el juego las va a pisar.
            foreach (var entrada in sinConvertir)
                Log.Wrn(Strings.OfficialTranslationBeatsPatchUnfixed(
                    SinPrefijoDePatches(entrada.ClassName), entrada.Node));

            return reencaminadas;
        }

        /// <summary>
        /// Lee los Patches de RML parseando el archivo, sin evaluar los xpath.
        ///
        /// Es la red de seguridad de la lectura normal, que corre ExtractPatches y por lo tanto
        /// solo ve las traducciones cuyo xpath encuentra su objetivo. Cuando no lo encuentra, la
        /// traduccion hecha se vuelve invisible: no entra al cruce, no queda apartada en UNUSED y
        /// se pierde sin que nada avise. Pasa cuando el def lo agrega otro mod, o cuando vive en
        /// una carpeta condicional que esta corrida no cargo. Con Alpha Mechs fueron 39
        /// traducciones que volvieron a TODO de una corrida a la otra.
        ///
        /// Parsear no reemplaza a evaluar, porque el xpath es el que resuelve de verdad a que
        /// nodo apunta cada operacion. Por eso lo de aca se agrega despues y solo para las claves
        /// que no vinieron por el camino normal.
        /// </summary>
        private static List<TranslationEntry> LeerPatchesLiteral(string patchesDir)
        {
            var sueltas = new List<TranslationEntry>();

            foreach (var filePath in DescendantFiles(patchesDir).Where(x => x.ToLower().EndsWith(".xml")))
            {
                XmlDocument doc;
                try
                {
                    doc = ReadXmlKeepingComments(filePath);
                }
                catch (Exception e)
                {
                    // Un Patches roto ya lo reporta la lectura normal; aca solo se saltea.
                    Log.Wrn(Strings.ErrorReadingFile(filePath, e.Message));
                    continue;
                }

                foreach (var operacion in doc.SelectNodes("//*[xpath][value]")!.OfType<XmlElement>())
                {
                    var xpath = operacion["xpath"]?.InnerText.Trim();
                    var valor = operacion["value"];
                    if (string.IsNullOrEmpty(xpath) || valor == null)
                        continue;

                    // Solo el caso simple, que es el que se puede perder: una operacion que
                    // reemplaza un campo. Las que traen varios hijos son de otra forma y no se
                    // reconstruyen asi.
                    var hijos = valor.ChildNodes.OfType<XmlElement>().ToList();
                    if (hijos.Count != 1)
                        continue;

                    var clave = DesarmarXpath(xpath);
                    if (clave == null)
                        continue;

                    sueltas.Add(new TranslationEntry($"Patches.{clave.Value.Clase}", clave.Value.Nodo,
                        "", TranslatedFromXml(hijos[0].InnerText), null, null));
                }
            }

            return sueltas;
        }

        /// <summary>
        /// La vuelta de <see cref="Utils.GetXpath"/>: del xpath saca la clase y el nodo.
        ///
        /// Invierte las tres formas que GetXpath genera —el nombre de clase tal cual, li[N] para
        /// un indice de lista, y el predicado de texto para un TranslationHandle— y devuelve null
        /// para cualquier otra. Ese null es a proposito: reconstruir mal una clave es peor que no
        /// reconstruirla, porque pondria una traduccion en un nodo que no le corresponde.
        ///
        /// Las dos primeras formas no se invertian y por eso se perdio trabajo: la clase quedaba
        /// afuera si traia el ensamblado ("ZoologyMod.LifeStagePenetrationDef, ZoologyMod"), y el
        /// predicado quedaba afuera siempre. Como <see cref="LeerPatchesLiteral"/> se apoya en
        /// esto, esas traducciones no se releian, no entraban al cruce, no quedaban apartadas en
        /// UNUSED y volvian a TODO sin que nada avisara. Fueron 21 de tres mods en una sola
        /// corrida.
        /// </summary>
        private static (string Clase, string Nodo)? DesarmarXpath(string xpath)
        {
            // La clase va tal cual la escribio GetXpath, asi que puede traer el ensamblado
            // detras de una coma. Se acepta todo menos corchetes, que son los del defName.
            var m = Regex.Match(xpath, @"^/Defs/([^\[\]/]+)\[defName=""([^""]+)""\]/(.+)$");
            if (!m.Success)
                return null;

            // No se puede partir por '/' a secas: el predicado de un TranslationHandle trae
            // ".//" adentro y quedaria cortado en pedazos.
            var tokens = PartirRuta(m.Groups[3].Value);
            for (var i = 0; i < tokens.Count; i++)
            {
                // li[N] es 1-based en el xpath y 0-based en el nodo.
                var li = Regex.Match(tokens[i], @"^li\[(\d+)\]$");
                if (li.Success)
                {
                    tokens[i] = (int.Parse(li.Groups[1].Value) - 1).ToString();
                    continue;
                }

                // El predicado que GetXpath arma para un TranslationHandle: adentro esta el
                // handle, que es justo el token del nodo.
                var handle = Regex.Match(tokens[i], @"^\*\[\.//\*\[contains\(text\(\), '([^']*)'\)\]\]$");
                if (handle.Success)
                {
                    tokens[i] = handle.Groups[1].Value;
                    continue;
                }

                if (!Regex.IsMatch(tokens[i], "^[A-Za-z0-9_]+$"))
                    return null;
            }

            return (m.Groups[1].Value, $"{m.Groups[2].Value}.{string.Join('.', tokens)}");
        }

        /// <summary>
        /// Parte una ruta de xpath por sus '/' de primer nivel, dejando enteros los predicados.
        /// </summary>
        private static List<string> PartirRuta(string ruta)
        {
            var tramos = new List<string>();
            var actual = new StringBuilder();
            var profundidad = 0;

            foreach (var c in ruta)
            {
                switch (c)
                {
                    case '[':
                        profundidad++;
                        break;
                    case ']':
                        profundidad--;
                        break;
                    case '/' when profundidad == 0:
                        tramos.Add(actual.ToString());
                        actual.Clear();
                        continue;
                }

                actual.Append(c);
            }

            tramos.Add(actual.ToString());
            return tramos;
        }

        /// <summary>
        /// Guarda las traducciones que quedaron sin lugar porque su nodo ya no existe en
        /// el mod, para poder recuperarlas a mano si el mod las vuelve a traer.
        ///
        /// Va fuera de Languages/ a proposito: ahi adentro RimWorld lo cargaria como una
        /// traduccion mas, y las claves muertas de DefInjected le llenan el log de errores
        /// al jugador.
        /// </summary>
        public static void WriteUnused(List<TranslationEntry> sinUso, string rootDirPath)
        {
            var destino = Path.Combine(rootDirPath, "UNUSED.xml");
            if (sinUso.Count == 0)
            {
                // Con el archivo anterior releido y sumado al cruce, llegar aca sin nada
                // significa que se rescato todo lo que habia apartado. Borrarlo es correcto.
                if (File.Exists(destino))
                    File.Delete(destino);
                return;
            }

            var doc = new XmlDocument();
            var root = doc.AppendElement("UnusedTranslations");

            // Ordenado para que el diff de una actualizacion a la siguiente sea legible.
            foreach (var entry in sinUso.OrderBy(x => x.ClassName).ThenBy(x => x.Node))
            {
                var elemento = root.AppendElement("entry", entry.Translated);
                elemento.AppendAttribute("class", entry.ClassName);
                elemento.AppendAttribute("node", entry.Node);
                if (!string.IsNullOrEmpty(entry.Original))
                    elemento.AppendAttribute("original", entry.Original);
            }

            doc.InsertBefore(doc.CreateXmlDeclaration("1.0", "utf-8", null), doc.DocumentElement);
            // Se guarda sin pasar por la politica de duplicados: es un informe que se
            // rehace en cada corrida, y conservar el viejo lo dejaria mintiendo.
            doc.Save(destino);
            Log.Msg(Strings.UnusedTranslationsSaved(sinUso.Count, destino));
        }

        /// <summary>
        /// Lee lo que se aparto en corridas anteriores, para que vuelva a entrar al cruce.
        ///
        /// Sin esto el archivo dura una sola extraccion. La corrida que lo escribe saca esas
        /// traducciones de Languages/, asi que a la siguiente ya no las encuentra por ningun
        /// lado, no le sobra nada, y borra el archivo con todo adentro.
        ///
        /// Releerlo ademas es lo que hace cierto lo que promete WriteUnused: si el mod vuelve
        /// a traer el nodo, la traduccion se recupera sola en vez de a mano.
        ///
        /// Va aparte de FromLanguageXml a proposito. Ese metodo tambien alimenta la conversion
        /// a XLSX, y meterle claves muertas seria llenarle la planilla al traductor de cosas
        /// que el mod ya no tiene.
        /// </summary>
        public static List<TranslationEntry> ReadUnused(string rootDirPath)
        {
            var apartadas = new List<TranslationEntry>();
            var origen = Path.Combine(rootDirPath, "UNUSED.xml");
            if (!File.Exists(origen))
                return apartadas;

            try
            {
                var doc = new XmlDocument();
                doc.Load(origen);

                foreach (var entrada in doc.DocumentElement?.ChildNodes.OfType<XmlElement>()
                                        ?? Enumerable.Empty<XmlElement>())
                {
                    var clase = entrada.GetAttribute("class");
                    var nodo = entrada.GetAttribute("node");
                    if (string.IsNullOrEmpty(clase) || string.IsNullOrEmpty(nodo))
                        continue;

                    apartadas.Add(new TranslationEntry(clase, nodo,
                        entrada.GetAttribute("original"), entrada.InnerText, null, null));
                }
            }
            catch (Exception e)
            {
                // Un UNUSED.xml roto no puede voltear la actualizacion de un mod: es el archivo
                // de descarte, no la traduccion. Se avisa y se sigue sin el.
                Log.Err(Strings.ErrorReadingFileColon(origen, e.Message));
                return new List<TranslationEntry>();
            }

            return apartadas;
        }

        private static void SaveSafely(this XLWorkbook xlsx, string path)
        {
            if (!File.Exists(path))
            {
                xlsx.SaveAs(path);
                return;
            }

            switch (Prefabs.Policy)
            {
                case Prefabs.DuplicatesPolicy.Stop:
                    var stopCallback = Prefabs.StopCallbackXlsx;
                    if (stopCallback != null)
                        stopCallback(xlsx, path);
                    else
                        throw new ArgumentNullException(nameof(stopCallback));
                    return;
                case Prefabs.DuplicatesPolicy.Overwrite:
                    try
                    {
                        xlsx.SaveAs(path);
                    }
                    catch (IOException)
                    {
                        Log.Err(Strings.FileInUse(Path.GetFileName(path)));
                    }
                    return;
                case Prefabs.DuplicatesPolicy.KeepOriginal:
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void SaveSafely(this XmlDocument doc, string path)
        {
            doc.InsertBefore(doc.CreateXmlDeclaration("1.0", "utf-8", null), doc.DocumentElement);
            if (!File.Exists(path))
            {
                doc.Save(path);
                return;
            }

            switch (Prefabs.Policy)
            {
                case Prefabs.DuplicatesPolicy.Stop:
                    var stopCallback = Prefabs.StopCallbackXml;
                    if (stopCallback != null)
                        stopCallback(doc, path);
                    else
                        throw new ArgumentNullException(nameof(stopCallback));
                    return;
                case Prefabs.DuplicatesPolicy.Overwrite:
                    doc.Save(path);
                    return;
                case Prefabs.DuplicatesPolicy.KeepOriginal:
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void SaveSafely(this IEnumerable<string> lines, string path)
        {
            if (!File.Exists(path))
            {
                File.WriteAllLines(path, lines);
                return;
            }

            switch (Prefabs.Policy)
            {
                case Prefabs.DuplicatesPolicy.Stop:
                    var stopCallback = Prefabs.StopCallbackTxt;
                    if (stopCallback != null)
                        stopCallback(lines, path);
                    else
                        throw new ArgumentNullException(nameof(stopCallback));
                    return;
                case Prefabs.DuplicatesPolicy.Overwrite:
                    File.WriteAllLines(path, lines);
                    return;
                case Prefabs.DuplicatesPolicy.KeepOriginal:
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        private static string PathCombineCreateDir(params string[] paths)
        {
            var dir = Path.Combine(paths);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        private static void DoFullListTranslation(this XmlDocument defInjectedDoc)
        {
            var patterns = Prefabs.FullListTranslationTags.Select(x => $".+?\\.{x}\\.\\d+").ToList();

            var fullListdic = new Dictionary<string, XmlNode>();
            var removedNodesDic = new Dictionary<string, List<XmlNode>>();
            foreach (XmlNode childNode in defInjectedDoc.DocumentElement!.ChildNodes)
            {
                var nodeName = childNode.Name;
                if (!patterns.Any(x => Regex.IsMatch(nodeName, x)))
                    continue;
                nodeName = nodeName[..nodeName.LastIndexOf('.')];
                if (!fullListdic.TryGetValue(nodeName, out var fullList))
                {
                    fullList = defInjectedDoc.CreateElement(nodeName);
                    fullListdic[nodeName] = fullList;
                }

                if (!removedNodesDic.TryGetValue(nodeName, out var removedList))
                {
                    removedList = new List<XmlNode>();
                    removedNodesDic[nodeName] = removedList;
                }

                var li = fullList.AppendElement("li");
                li.InnerText = childNode.InnerText;
                removedList.Add(childNode);
            }


            foreach (var (key, fullListNode) in fullListdic)
            {
                var removedList = removedNodesDic[key];

                defInjectedDoc.DocumentElement!.InsertAfter(fullListNode, removedList.Last());
                foreach (var xmlNode in removedList)
                {
                    defInjectedDoc.DocumentElement!.RemoveChild(xmlNode);
                }
            }
        }

        /// <summary>
        /// Como ReadXml pero conservando los comentarios, que es de donde se recupera el
        /// texto original al reimportar. ReadXml los descarta a proposito, y lo usa el
        /// extractor para leer los mods, asi que conviene no tocarlo.
        /// </summary>
        internal static XmlDocument ReadXmlKeepingComments(string filePath)
        {
            var readerSettings = new XmlReaderSettings
            {
                IgnoreComments = false,
                IgnoreWhitespace = true,
                CheckCharacters = false
            };
            using var stringReader = new StringReader(File.ReadAllText(filePath));
            using var xmlReader = XmlReader.Create(stringReader, readerSettings);
            var doc = new XmlDocument();
            doc.Load(xmlReader);
            return doc;
        }

        internal static XmlDocument ReadXml(string filePath)
        {
            var contents = File.ReadAllText(filePath);
            var readerSettings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
                CheckCharacters = false
            };
            using var stringReader = new StringReader(contents);
            using var xmlReader = XmlReader.Create(stringReader, readerSettings);
            var childDoc = new XmlDocument();
            childDoc.Load(xmlReader);
            return childDoc;
        }

        internal static IEnumerable<string> DescendantFiles(string root)
        {
            if (!Directory.Exists(root))
                yield break;

            var q = new Queue<string>();
            q.Enqueue(root);

            while (q.Count > 0)
            {
                var curPath = q.Dequeue();
                foreach (var subDir in Directory.GetDirectories(curPath).OrderBy(x => x))
                {
                    q.Enqueue(subDir);
                }

                foreach (var file in Directory.EnumerateFiles(curPath).OrderBy(x => x))
                {
                    yield return file;
                }
            }
        }
    }
}
