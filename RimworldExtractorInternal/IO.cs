using ClosedXML.Excel;
using System.Security;
using System.Text.RegularExpressions;
using System.Xml;
using RimworldExtractorInternal.Compats;
using RimworldExtractorInternal.DataTypes;
using RimworldExtractorInternal.Exceptions;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Office2016.Excel;

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
        public static void ToExcel(List<TranslationEntry> translations, string outPath = "result",
            bool markNoTranslation = false)
        {
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
                    if (combinedRequiredMods.Contains("##packageId##") && entry.ClassName.StartsWith("Patches."))
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
                    var entry = addedNewlys[i].New;
                    mainSheet.Cell(2 + i + rows.Count, colClassNode).Value = $"{entry.ClassName}+{entry.Node}";
                    mainSheet.Cell(2 + i + rows.Count, colClass).Value = entry.ClassName;
                    mainSheet.Cell(2 + i + rows.Count, colNode).Value = entry.Node;
                    if (colRequiredMods != -1 && entry.RequiredMods != null)
                    {
                        var combinedRequiredMods = entry.RequiredMods.ToString();
                        mainSheet.Cell(2 + i + rows.Count, colRequiredMods).Value = combinedRequiredMods;
                        if (combinedRequiredMods.Contains("##packageId##") && entry.ClassName.StartsWith("Patches."))
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


            foreach (var translation in translations)
            {
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
                    foreach (var allowedMod in requiredMods.AllowedMods)
                    {
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
                        entryDict[requiredMods.ToString()] = operationSequence.AppendElement("operations");
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

            // Los Patches viven fuera de Languages, en la raiz del mod.
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
            }

            return translations;
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
                // Si esta vez no sobro nada, no se deja el archivo de una corrida anterior.
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
