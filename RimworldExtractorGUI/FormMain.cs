using DocumentFormat.OpenXml.Spreadsheet;
using RimworldExtractorInternal;
using System.Diagnostics;
using System.Xml;
using RimworldExtractorInternal.Compats;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorGUI
{
    public partial class FormMain : Form
    {
        public ModMetadata? SelectedMod { get; private set; }
        public List<ExtractableFolder>? SelectedFolders { get; private set; }
        public List<ModMetadata>? ReferenceMods { get; private set; }

        public FormMain()
        {
            InitializeComponent();
            ApplyStrings();
            Log.Out = new RichTextBoxWriter(richTextBoxLog);
            Prefabs.StopCallbackXlsx = FormStopCallback.StopCallbackXlsx;
            Prefabs.StopCallbackXml = FormStopCallback.StopCallbackXml;
            Prefabs.StopCallbackTxt = FormStopCallback.StopCallbackTxt;
            try
            {
                Prefabs.Load();
            }
            catch (Exception e)
            {
                MessageBox.Show(Strings.PrefabsDatOutdated + Strings.ErrorMessagePrefix(e.Message));
                // Prefabs ya quedo con los valores por defecto de Init(), asi que se puede
                // seguir. Upstream hacia Close() y relanzaba, y la app terminaba igual en el
                // dialogo de excepcion no controlada.
                Prefabs.Save();
            }

            Task.Factory.StartNew(() =>
            {
                try
                {
                    var latest = GithubVersionCheker.GetLatest();
                    var current = Program.VERSION;

                    void UpdateVersionText()
                    {
                        linkLabelLatestVersion.Text = latest == current
                            ? Strings.VersionUpToDate(current)
                            : Strings.VersionUpdateAvailable(current, latest);
                    }

                    if (linkLabelLatestVersion.InvokeRequired)
                    {
                        linkLabelLatestVersion.Invoke(UpdateVersionText);
                    }
                    else
                    {
                        UpdateVersionText();
                    }
                }
                catch (Exception e)
                {
                    Log.Wrn(Strings.VersionCheckFailed(e.Message));
                }
            });
        }


        private void buttonSelectMod_Click(object sender, EventArgs e)
        {
            var formSelectMod = new FormSelectMod();
            formSelectMod.StartPosition = FormStartPosition.CenterParent;
            if (formSelectMod.ShowDialog(this) == DialogResult.OK)
            {
                SelectedMod = formSelectMod.SelectedMod!;
                ReferenceMods = formSelectMod.ReferenceMods.Except(Enumerable.Repeat(SelectedMod, 1)).ToList();
                SelectedFolders = formSelectMod.SelectedFolders;
                buttonExtract.Enabled = true;

                labelSelectedMods.Text = Strings.SelectedMod(SelectedMod.ModName);
                if (ReferenceMods?.Count > 0)
                {
                    var concatText = string.Join(", ", ReferenceMods.Select(x => x.ModName));
                    var stripedText = concatText.Substring(0, Math.Min(concatText.Length, 200));
                    if (concatText.Length > 200)
                        stripedText += "...";
                    labelSelectedMods.Text += Strings.SelectedReferenceMods(concatText);
                }
            }
        }

        private void buttonExtract_Click(object sender, EventArgs e)
        {
            if (ReferenceMods is null || SelectedFolders is null || SelectedMod is null)
            {
                return;
            }

            Log.Msg(Strings.ExtractionStarted);

            var extraction = Extractor.ExtractTranslationData(SelectedMod, SelectedFolders, ReferenceMods);

            var outPath = SelectedMod.Identifier.StripInvaildChars();
            switch (Prefabs.Method)
            {
                case Prefabs.ExtractionMethod.Excel:
                    IO.ToExcel(extraction, Path.Combine(outPath, outPath));
                    break;
                case Prefabs.ExtractionMethod.Languages:
                    IO.ToLanguageXml(extraction, false, XmlCommentStyle.None, outPath, outPath);
                    break;
                case Prefabs.ExtractionMethod.LanguagesWithComments:
                    IO.ToLanguageXml(extraction, false, XmlCommentStyle.Original, outPath, outPath);
                    break;
                case Prefabs.ExtractionMethod.LanguagesToTranslate:
                    IO.ToLanguageXml(extraction, false, XmlCommentStyle.TranslationTemplate, outPath, outPath);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }


            var (cntDefs, cntKeyed, cntStrings, cntPatches) = extraction.Count();
            Log.Msg(Strings.ExtractionSummary(extraction.Count, cntDefs, cntKeyed, cntStrings, cntPatches));

            var hasError = Log.HasErrorSince(Strings.ExtractionStarted);

            if (hasError)
            {
                if (MessageBox.Show(Strings.DoneWithErrorsOpenFolder, Strings.DialogTitleDoneQuestion, MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    Process.Start("explorer.exe", outPath);
                }
            }
            else
            {
                if (MessageBox.Show(Strings.DoneOpenFolder, Strings.DialogTitleDone, MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    Process.Start("explorer.exe", outPath);
                }
            }
        }



        private void buttonConvertXml_Click(object sender, EventArgs e)
        {
            var openfileDialog = new OpenFileDialog();
            openfileDialog.Title = Strings.SelectExtractorXlsx;
            openfileDialog.FileName = "";
            openfileDialog.Filter = Strings.FilterTranslationData;

            if (openfileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var path = openfileDialog.FileName;
                    var translations = IO.FromExcel(path);
                    IO.ToLanguageXml(translations, true,
                        Prefabs.CommentOriginal ? XmlCommentStyle.Original : XmlCommentStyle.None,
                        Path.GetFileName(path), Path.GetDirectoryName(path) ?? "");
                    if (MessageBox.Show(Strings.DoneOpenConvertedFolder, Strings.DialogTitleDone, MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        Process.Start("explorer.exe", Path.GetDirectoryName(path) ?? "");
                    }
                }
                catch (Exception exception)
                {
                    Console.WriteLine(exception);
                    throw;
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var form = new FormSettings();
            form.StartPosition = FormStartPosition.CenterParent;
            form.ShowDialog(this);

        }

        private void linkLabelLatestVersion_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("explorer.exe", GithubVersionCheker.ReleasesUrl);
        }

        private void buttonConvertXlsx_Click(object sender, EventArgs e)
        {
            var form = new FormXmlister();
            form.StartPosition = FormStartPosition.CenterParent;
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                var roots = form.FileNames;
                for (var i = 0; i < roots.Length; i++)
                {
                    var root = roots[i];
                    var translations = IO.FromLanguageXml(root);
                    IO.ToExcel(translations, Path.Combine(root, Path.GetFileNameWithoutExtension(root)));
                    Log.Msg(Strings.ProgressFixed(i + 1, roots.Length, root));
                }

                MessageBox.Show(Strings.ConversionDone);
            }
        }

        private void buttonJpgPackager_Click(object sender, EventArgs e)
        {
            var form = new FormImageFileCombiner();
            form.StartPosition = FormStartPosition.CenterParent;
            if (form.ShowDialog(this) == DialogResult.OK)
            {

            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", GithubVersionCheker.IssueUrl);
        }

        private void buttonOpenTranslationAnalyzer_Click(object sender, EventArgs e)
        {
            var form = new FormTranslationAnalyzerPathSelect();
            form.StartPosition = FormStartPosition.CenterParent;
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                var paths = form.Paths;
                var analyzer = new FormTranslationAnalyzer(paths);
                analyzer.StartPosition = FormStartPosition.CenterParent;
                if (analyzer.ShowDialog(this) == DialogResult.OK)
                {
                    var analyzerEntries = analyzer.Entries.ToList();
                    for (var i = 0; i < analyzerEntries.Count; i++)
                    {
                        string newPath;
                        var analyzerEntry = analyzerEntries[i];
                        switch (analyzerEntry.SaveMethod)
                        {
                            case TranslationAnalyzerEntry.SaveMethodEnum.Append:
                                IO.ModifyExcel(analyzerEntry.Changes.ToList(), analyzerEntry.FilePath);
                                break;
                            case TranslationAnalyzerEntry.SaveMethodEnum.Overwrite:
                                analyzerEntry.MergeTranslation();
                                IO.ToExcel(analyzerEntry.NewTranslations!,
                                    Path.Combine(Path.GetDirectoryName(analyzerEntry.FilePath),
                                        Path.GetFileNameWithoutExtension(analyzerEntry.FilePath)));
                                break;
                            case TranslationAnalyzerEntry.SaveMethodEnum.RewriteNewFile:
                                analyzerEntry.MergeTranslation();
                                newPath = Path.Combine(
                                    Path.GetDirectoryName(analyzerEntry.FilePath),
                                    Path.GetFileNameWithoutExtension(analyzerEntry.FilePath) + Strings.EditedFileSuffix);
                                IO.ToExcel(analyzerEntry.NewTranslations, newPath, true);
                                Log.Msg(Strings.ProgressFixed(i + 1, analyzerEntries.Count, newPath));
                                continue;
                            case TranslationAnalyzerEntry.SaveMethodEnum.New:
                                newPath = Path.Combine(
                                    Path.GetDirectoryName(analyzerEntry.FilePath),
                                    Path.GetFileNameWithoutExtension(analyzerEntry.FilePath) + Strings.EditedFileSuffix);
                                IO.ToExcel(
                                    analyzerEntry.Changes
                                        .Where(x => x.Reason == TranslationAnalyzerEntry.ChangeReason.AddedNewly)
                                        .Select(x => x.New).ToList(), newPath);
                                Log.Msg(Strings.ProgressFixed(i + 1, analyzerEntries.Count, newPath));
                                continue;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        Log.Msg(Strings.ProgressFixed(i + 1, analyzerEntries.Count, analyzerEntry.FilePath));
                    }

                    MessageBox.Show(Strings.FilesFixed(analyzerEntries.Count));
                }
            }
        }
    
        /// <summary>
        /// Traduce los controles en tiempo de ejecucion, para no tocar el .Designer.cs
        /// y mantener limpios los merges con upstream.
        /// </summary>
        private void ApplyStrings()
        {
            Text = Strings.FormMainTitle;
            buttonSelectMod.Text = Strings.BtnSelectMod;
            buttonExtract.Text = Strings.BtnExtract;
            button2.Text = Strings.BtnOptions;
            label1.Text = Strings.LabelMainDescription;
            buttonJpgPackager.Text = Strings.BtnJpgPackager;
            buttonOpenTranslationAnalyzer.Text = Strings.BtnOpenTranslationAnalyzer;
            labelSelectedMods.Text = Strings.LabelNoModSelected;

            // El Designer lo deja negro fijo; que acompanie al tema.
            richTextBoxLog.BackColor = RichTextBoxWriter.ColorFondo;

            button1.Text = Strings.BtnReportProblem;

            // Los textos en espanol son mas largos que los originales y los
            // formularios tienen medidas fijas: se ensancha lo que no entra.
            AutoAjuste.Ajustar(buttonSelectMod, buttonExtract, button2, buttonJpgPackager,
                buttonOpenTranslationAnalyzer, buttonConvertXlsx, buttonConvertXml, button1,
                labelSelectedMods, label1);
        }
    }
}