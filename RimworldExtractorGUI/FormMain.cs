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

        /// <summary>
        /// Boton para elegir el tema. Se crea en codigo y no en el Designer, como todo lo
        /// que agrega el fork, para no tocar los archivos generados.
        /// </summary>
        private readonly Button _buttonTema = new() { Name = "buttonTema" };

        /// <summary>Si la ultima seleccion pidio actualizar sobre RML.</summary>
        private bool _quickUpdate;

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
                Aviso.Mostrar(Strings.PrefabsDatOutdated + Strings.ErrorMessagePrefix(e.Message));
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
                        linkLabelLatestVersion.Text = string.IsNullOrEmpty(current)
                            ? Strings.VersionDevBuild(latest)
                            : latest == current
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
                _quickUpdate = formSelectMod.QuickUpdate;
                buttonExtract.Enabled = true;

                labelSelectedMods.Text = Strings.SelectedMod(SelectedMod.ModName);
                if (ReferenceMods?.Count > 0)
                {
                    var concatText = string.Join(", ", ReferenceMods.Select(x => x.ModName));
                    var stripedText = concatText.Substring(0, Math.Min(concatText.Length, 200));
                    if (concatText.Length > 200)
                        stripedText += "...";
                    labelSelectedMods.Text += Strings.SelectedReferenceMods(stripedText);
                }
            }
        }

        private void buttonExtract_Click(object sender, EventArgs e)
        {
            if (ReferenceMods is null || SelectedFolders is null || SelectedMod is null)
            {
                return;
            }

            // La ruta se comprueba antes de extraer: si falta, no tiene sentido hacer el
            // trabajo para despues no poder guardarlo donde se pidio.
            if (_quickUpdate && string.IsNullOrWhiteSpace(Prefabs.PathRml))
            {
                Aviso.Mostrar(Strings.QuickUpdateNoRmlPath, Strings.DialogTitleNotice);
                return;
            }

            Log.Msg(Strings.ExtractionStarted);

            var extraction = Extractor.ExtractTranslationData(SelectedMod, SelectedFolders, ReferenceMods);

            var outPath = SelectedMod.Identifier.StripInvaildChars();

            if (_quickUpdate)
            {
                outPath = ActualizarSobreRml(extraction, outPath);
                TerminarExtraccion(extraction, outPath);
                return;
            }

            switch (Prefabs.Method)
            {
                case Prefabs.ExtractionMethod.Excel:
                    IO.ToExcel(extraction, Path.Combine(outPath, outPath));
                    break;
                case Prefabs.ExtractionMethod.Languages:
                    IO.ToLanguageXml(extraction, false, XmlCommentStyle.None, outPath, outPath);
                    LoadFoldersBuild.Write(SelectedMod, outPath);
                    break;
                case Prefabs.ExtractionMethod.LanguagesWithComments:
                    IO.ToLanguageXml(extraction, false, XmlCommentStyle.Original, outPath, outPath);
                    LoadFoldersBuild.Write(SelectedMod, outPath);
                    break;
                case Prefabs.ExtractionMethod.LanguagesToTranslate:
                    IO.ToLanguageXml(extraction, false, XmlCommentStyle.TranslationTemplate, outPath, outPath);
                    LoadFoldersBuild.Write(SelectedMod, outPath);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            TerminarExtraccion(extraction, outPath);
        }

        /// <summary>
        /// El cierre comun de las dos formas de extraer: el resumen en el log y la
        /// pregunta de si abrir la carpeta.
        /// </summary>
        private void TerminarExtraccion(List<TranslationEntry> extraction, string outPath)
        {
            var (cntDefs, cntKeyed, cntStrings, cntPatches) = extraction.Count();
            Log.Msg(Strings.ExtractionSummary(extraction.Count, cntDefs, cntKeyed, cntStrings, cntPatches));

            var hasError = Log.HasErrorSince(Strings.ExtractionStarted);
            var pregunta = hasError ? Strings.DoneWithErrorsOpenFolder : Strings.DoneOpenFolder;
            var titulo = hasError ? Strings.DialogTitleDoneQuestion : Strings.DialogTitleDone;

            if (Aviso.Preguntar(pregunta, titulo) == DialogResult.Yes)
            {
                Process.Start("explorer.exe", outPath);
            }
        }

        /// <summary>
        /// Cruza la extraccion con lo que ya esta traducido en RML y escribe el resultado
        /// ahi mismo. Devuelve la carpeta donde quedo.
        ///
        /// El arbol de destino se borra antes de escribir: la mezcla ya es el contenido
        /// completo, y si no, los archivos de una version anterior del mod quedarian ahi
        /// con claves que el juego intentaria cargar.
        /// </summary>
        private string ActualizarSobreRml(List<TranslationEntry> extraction, string nombreDeArchivos)
        {
            var r = ActualizacionRml.Escribir(SelectedMod!, extraction, Prefabs.PathRml);

            // Acomoda las traducciones sueltas de un autor que ya tiene su carpeta. Va antes
            // de regenerar y no despues: mover una carpeta le cambia la ruta, y el indice
            // tiene que salir con la de despues del movimiento.
            Agrupador.Reagrupar(Prefabs.PathRml);

            // El yaml de la carpeta no alcanza: RimWorld lee el LoadFolders.xml, que lo arma
            // el builder de RML a partir de todos los yaml. Sin esto un mod recien agregado no
            // carga, y no hay ningun sintoma que lo explique. Va aca y no dentro de Escribir
            // porque en una corrida por lotes se hace una sola vez, al final.
            LoadFoldersBuild.Regenerar(Prefabs.PathRml);

            // Si la que se acaba de escribir era una de las sueltas, ya no esta donde la dejo
            // Escribir. Se vuelve a preguntar en vez de confiar en la ruta vieja: si no, el
            // log manda a una carpeta que ya no existe y el boton de abrirla no abre nada.
            var destino = LoadFoldersBuild.CarpetaDe(SelectedMod!, Prefabs.PathRml);

            Log.Msg(Strings.QuickUpdateSummary(r.Conservadas, r.Pendientes, r.SinUso));
            Log.Msg(Strings.QuickUpdateWrittenTo(destino));

            return destino;
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
                    var carpeta = Path.GetDirectoryName(path) ?? "";
                    IO.ToLanguageXml(translations, true,
                        Prefabs.CommentOriginal ? XmlCommentStyle.Original : XmlCommentStyle.None,
                        Path.GetFileName(path), carpeta);

                    // De que mod es se deduce del nombre del archivo, que es para lo que la
                    // planilla tiene que volver con el nombre con el que salio.
                    LoadFoldersBuild.Write(TranslationAnalyzerTool.GetModMetadataFromFilePath(path), carpeta);
                    if (Aviso.Preguntar(Strings.DoneOpenConvertedFolder, Strings.DialogTitleDone) == DialogResult.Yes)
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

                Aviso.Mostrar(Strings.ConversionDone);
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

                    Aviso.Mostrar(Strings.FilesFixed(analyzerEntries.Count));
                }
            }
        }
    
        /// <summary>
        /// Traduce los controles en tiempo de ejecucion, para no tocar el .Designer.cs
        /// y mantener limpios los merges con upstream.
        /// </summary>
        private void ApplyStrings()
        {
            // Anclajes: van antes de todo porque AutoAjuste ensancha la ventana, y con
            // esto el panel de log se estira con ella en vez de dejar un hueco.
            richTextBoxLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            labelSelectedMods.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label2.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            linkLabelLatestVersion.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label2.TextAlign = ContentAlignment.MiddleLeft;
            button1.Anchor = AnchorStyles.Top | AnchorStyles.Right;

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

            // LinkLabel trae un azul fijo que sobre fondo oscuro casi no se lee.
            linkLabelLatestVersion.LinkColor = Tema.ColorDeEnlace;
            linkLabelLatestVersion.ActiveLinkColor = Tema.ColorDeEnlace;
            linkLabelLatestVersion.VisitedLinkColor = Tema.ColorDeEnlace;

            button1.Text = Strings.BtnReportProblem;

            // Comparte fila con el de opciones: los dos hacen a como se comporta la
            // aplicacion, y ahi habia una fila entera para un solo boton.
            _buttonTema.SetBounds(button2.Left, button2.Top, button2.Width, button2.Height);
            _buttonTema.Text = Tema.Rotulo(Prefabs.Theme);
            _buttonTema.Click += (_, _) => CambiarDeTema();
            Controls.Add(_buttonTema);

            // La columna de botones queda pareja: las filas de un boton toman el ancho
            // entero y las de dos se lo reparten a la mitad. Va antes del autoajuste,
            // que despues corre las etiquetas de la derecha para que no queden tapadas.
            Rejilla.Columna(buttonSelectMod.Left,
                Rejilla.Linea(buttonSelectMod),
                Rejilla.Linea(buttonExtract),
                Rejilla.Linea(buttonConvertXlsx, buttonConvertXml),
                Rejilla.Linea(buttonOpenTranslationAnalyzer, buttonJpgPackager),
                Rejilla.Linea(button2, _buttonTema));

            // Los textos en espanol son mas largos que los originales y los
            // formularios tienen medidas fijas: se ensancha lo que no entra.
            AutoAjuste.Ajustar(buttonSelectMod, buttonExtract, button2, _buttonTema, buttonJpgPackager,
                buttonOpenTranslationAnalyzer, buttonConvertXlsx, buttonConvertXml, button1,
                labelSelectedMods, label1);

            // La franja del log la comparten tres cosas: el rotulo, el enlace de version
            // y el boton de reportar problemas. En el diseño original las tres arrancaban
            // en la misma columna, superpuestas, y solo se salvaba porque el rotulo estaba
            // centrado; al alinearlo a la izquierda el enlace lo tapaba, que va delante en
            // el orden Z. Se reparte a mano: el rotulo se queda con lo que mide su palabra
            // y el enlace ocupa el resto, que ademas le viene bien porque el aviso de
            // "no se pudo comprobar la version" no entraba en su ancho original.
            // Va despues del autoajuste, que es lo que fija el ancho final de la ventana.
            const int separacion = 6;
            label2.Width = AutoAjuste.AnchoNecesario(label2);
            linkLabelLatestVersion.Left = label2.Right + separacion;
            linkLabelLatestVersion.Width = Math.Max(
                0, button1.Left - separacion - linkLabelLatestVersion.Left);
        }

        /// <summary>
        /// Pasa al siguiente tema y ofrece reiniciar, que es lo unico que lo aplica del
        /// todo: cambiarlo en caliente deja la ventana a medias —el fondo hace caso pero
        /// los botones conservan el tema con el que se crearon—, asi que se guarda y no se
        /// toca nada hasta el proximo arranque.
        /// </summary>
        private void CambiarDeTema()
        {
            _buttonTema.Text = Tema.Rotulo(Tema.Alternar());

            if (Aviso.Preguntar(Strings.ThemeRestartQuestion, Strings.DialogTitleTheme) != DialogResult.Yes)
                return;

            if (!Tema.Reiniciar())
                Aviso.Mostrar(Strings.ThemeRestartManually, Strings.DialogTitleTheme);
        }
    }
}