using RimworldExtractorInternal;
using System.Diagnostics;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorGUI
{
    public partial class FormMain : Form
    {
        public ModMetadata? SelectedMod { get; private set; }
        public List<ExtractableFolder>? SelectedFolders { get; private set; }
        public List<ModMetadata>? ReferenceMods { get; private set; }

        /// <summary>
        /// Boton para elegir el tema. Se crea en codigo porque es de cuando no se tocaban los
        /// .Designer.cs; pasa al Designer cuando esta ventana se migre a TableLayoutPanel.
        /// </summary>
        private readonly Button _buttonTema = new() { Name = "buttonTema" };

        /// <summary>Si la ultima seleccion pidio actualizar sobre RML.</summary>
        private bool _quickUpdate;

        /// <summary>Si esta corriendo «Actualizar todo RML».</summary>
        private bool _actualizandoRml;

        /// <summary>
        /// El destino del log, guardado para poder vaciarlo antes de mostrar un resumen: la
        /// ventana pinta de a tandas, asi que al terminar una corrida larga quedan lineas sin
        /// pintar y el resumen apareceria antes que ellas.
        /// </summary>
        private PanelDeLog? _salidaDelLog;

        /// <summary>El log detallado: junto al ejecutable, como Prefabs.dat.</summary>
        private static string RutaDelLogDetallado => Path.Combine(Prefabs.Carpeta, Strings.DetailedLogFileName);

        /// <summary>
        /// La ultima version publicada, o null si todavia no se pudo comprobar. La guarda el
        /// chequeo de arranque para que el clic en el rotulo sepa si hay algo que ofrecer sin
        /// volver a consultar.
        /// </summary>
        private string? _ultimaVersion;

        /// <summary>
        /// Que trae la version nueva, para poder decirlo antes de actualizar. Null si no hay
        /// version nueva o si no se pudo leer, que no es motivo para no ofrecer la actualizacion.
        /// </summary>
        private string? _notasDeLaUltima;

        /// <summary>Si ya se esta actualizando, para no largar dos descargas encima.</summary>
        private bool _actualizandoApp;

        public FormMain()
        {
            InitializeComponent();
            Icon = Logo.Icono;
            ApplyStrings();

            // Cerrar a mitad del lote mataria el hilo que escribe, y el mod de ese momento
            // quedaria con su carpeta borrada y sin reescribir.
            FormClosing += (_, e) =>
            {
                if (!_actualizandoRml)
                    return;

                e.Cancel = true;
                Aviso.Mostrar(Strings.BatchStillRunning, Strings.DialogTitleNotice);
            };
            _salidaDelLog = new PanelDeLog(richTextBoxLog, richTextBoxAvisos, labelAvisos);
            FormClosed += (_, _) => Log.CerrarArchivo();
            Log.Pantalla = _salidaDelLog.Agregar;
            // Hasta la primera extraccion, el log detallado es el del arranque.
            EmpezarLogDetallado();
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
                    _ultimaVersion = latest;

                    void UpdateVersionText()
                    {
                        linkLabelLatestVersion.Text = string.IsNullOrEmpty(current)
                            ? Strings.VersionDevBuild(latest)
                            : EsBeta(current)
                                ? Strings.VersionBeta(current, latest)
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

                    // Las notas se piden aca y no al hacer clic: es una consulta a la API y en
                    // el hilo de la interfaz dejaria la ventana congelada hasta que responda.
                    if (!string.IsNullOrEmpty(current) && latest != current)
                        _notasDeLaUltima = Actualizador.NotasDe(latest);
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
                _quickUpdate = !formSelectMod.ExtraerEnCarpeta;

                labelSelectedMods.Text = Strings.SelectedMod(SelectedMod.ModName);
                if (ReferenceMods?.Count > 0)
                {
                    var concatText = string.Join(", ", ReferenceMods.Select(x => x.ModName));
                    var stripedText = concatText.Substring(0, Math.Min(concatText.Length, 200));
                    if (concatText.Length > 200)
                        stripedText += "...";
                    labelSelectedMods.Text += Strings.SelectedReferenceMods(stripedText);
                }

                // El boton del dialogo ya dice «extraer», asi que se extrae al volver de el.
                // Va despues del rotulo y no antes: la extraccion corre en este mismo hilo y
                // puede tardar minutos, y mientras tanto se ve que mod se esta extrayendo.
                labelSelectedMods.Refresh();
                Extraer();
            }
        }

        /// <summary>
        /// Extrae el mod elegido. Antes era el boton «2. Extraer los datos de traduccion» de
        /// esta ventana; ahora lo dispara el boton del dialogo de seleccion, que es donde ya
        /// estaba toda la decision.
        /// </summary>
        private void Extraer()
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

            // Una ruta que ya no existe —el clon se movio de carpeta— no puede tomarse como
            // destino: Escribir crearia ahi un Data/ nuevo, lejos del repositorio, y el log
            // diria que la traduccion quedo guardada.
            if (_quickUpdate && !Directory.Exists(Path.Combine(Prefabs.PathRml, "Data")))
            {
                Aviso.Mostrar(Strings.RmlPathWithoutData(Prefabs.PathRml), Strings.DialogTitleNotice);
                return;
            }

            Empezar();
            Log.Det(Strings.ExtractionStarted);

            // Tres pasos sobre RML y dos en carpeta. La extraccion corre en este hilo, asi que
            // MostrarAvance repinta la barra a mano: si no, no se moveria hasta terminar.
            var pasos = _quickUpdate ? 3 : 2;
            MostrarAvance(Strings.ProgressStep(1, pasos, Strings.StepExtracting), 0, pasos);
            var extraction = Extractor.ExtractTranslationData(SelectedMod, SelectedFolders, ReferenceMods);

            var outPath = SelectedMod.Identifier.StripInvaildChars();

            if (_quickUpdate)
            {
                outPath = ActualizarSobreRml(extraction);
                TerminarExtraccion(extraction, outPath);
                return;
            }

            MostrarAvance(Strings.ProgressStep(2, pasos, Strings.StepWritingFolder), 1, pasos);

            // Sin traduccion rapida el mod sale a una carpeta propia, en el mismo formato
            // que se escribe sobre RML: el original en un comentario y un TODO por valor.
            // Antes habia cuatro formatos a elegir en Opciones, y los otros tres —la
            // planilla y los dos XML sin marcador— no los usaba nadie.
            IO.ToLanguageXml(extraction, false, XmlCommentStyle.TranslationTemplate, outPath, outPath);
            LoadFoldersBuild.Write(SelectedMod, outPath);

            Log.Msg(Strings.ModExtractedToFolder(SelectedMod.ModName, extraction.Count, Path.GetFullPath(outPath)));
            TerminarExtraccion(extraction, outPath);
        }

        /// <summary>
        /// El cierre comun de las dos formas de extraer: el resumen en el log y la
        /// pregunta de si abrir la carpeta.
        /// </summary>
        private void TerminarExtraccion(List<TranslationEntry> extraction, string outPath)
        {
            var (cntDefs, cntKeyed, cntStrings, cntPatches) = extraction.Count();
            Log.Det(Strings.ExtractionSummary(extraction.Count, cntDefs, cntKeyed, cntStrings, cntPatches));
            Terminar(Strings.ProgressDone(SelectedMod!.ModName));

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
        private string ActualizarSobreRml(List<TranslationEntry> extraction)
        {
            MostrarAvance(Strings.ProgressStep(2, 3, Strings.StepMerging), 1, 3);
            var r = ActualizacionRml.Escribir(SelectedMod!, extraction, Prefabs.PathRml);

            MostrarAvance(Strings.ProgressStep(3, 3, Strings.StepIndex), 2, 3);

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

            Log.Msg(Strings.ModResult(SelectedMod!.ModName, r.Conservadas, r.Pendientes, r.SinUso));
            Log.Det(Strings.QuickUpdateWrittenTo(destino));

            return destino;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var form = new FormSettings();
            form.StartPosition = FormStartPosition.CenterParent;
            form.ShowDialog(this);

        }

        /// <summary>
        /// El mismo enlace de siempre, pero si hay una version nueva ofrece pasarse a ella en
        /// vez de mandar al navegador a elegir la descarga correcta y descomprimir a mano.
        /// </summary>
        private void linkLabelLatestVersion_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (_actualizandoApp)
                return;

            var actual = Program.VERSION;
            var ultima = _ultimaVersion;

            if (ultima == null || ultima == actual)
            {
                Process.Start("explorer.exe", GithubVersionCheker.ReleasesUrl);
                return;
            }

            // Sin version propia es una compilacion local: el numero lo escribe la CI al
            // publicar. Pisarla con una release seria reemplazarle a alguien su propio build.
            if (string.IsNullOrEmpty(actual))
            {
                Aviso.Mostrar(Strings.UpdateSkippedDevBuild, Strings.DialogTitleUpdate);
                Process.Start("explorer.exe", GithubVersionCheker.ReleasesUrl);
                return;
            }

            // Y una beta tampoco se actualiza sola: GetLatest consulta releases/latest, de
            // donde GitHub excluye las prereleases, asi que "la ultima" es siempre la
            // estable anterior y actualizar reemplazaria la beta por algo mas viejo.
            if (EsBeta(actual))
            {
                Aviso.Mostrar(Strings.UpdateSkippedBeta, Strings.DialogTitleUpdate);
                Process.Start("explorer.exe", GithubVersionCheker.ReleasesUrl);
                return;
            }

            // «Ahora no» solo cierra: la pagina de la version queda a un clic, en el enlace del
            // aviso, en vez de abrirse sola en el navegador.
            if (!AvisoDeActualizacion.Preguntar(actual, ultima, Actualizador.CambiosDe(_notasDeLaUltima)))
                return;

            Actualizar(ultima);
        }

        /// <summary>
        /// Si la version que corre es una prerelease. El sufijo con guion lo pone la CI
        /// fuera de master: "1.0.0-beta.1".
        /// </summary>
        private static bool EsBeta(string version) => version.Contains('-');

        /// <summary>
        /// Baja el paquete que corresponde, lo pone en su lugar y reinicia.
        ///
        /// La descarga va en segundo plano —son 78 MB en el Portable— y deja el rastro en el
        /// panel de log, que es donde el usuario ya mira lo que esta pasando. El reemplazo y el
        /// reinicio vuelven al hilo de la interfaz: Tema.Reiniciar cierra la aplicacion, y eso
        /// no se hace desde otro hilo.
        /// </summary>
        private void Actualizar(string etiqueta)
        {
            _actualizandoApp = true;
            linkLabelLatestVersion.Enabled = false;

            var variante = Actualizador.VarianteActual();
            var url = Actualizador.UrlDe(etiqueta, variante);
            var temporal = Path.Combine(Path.GetTempPath(), "RimworldExtractor-" + etiqueta);

            Task.Factory.StartNew(() =>
            {
                try
                {
                    if (Directory.Exists(temporal))
                        Directory.Delete(temporal, true);
                    Directory.CreateDirectory(temporal);

                    Log.Msg(Strings.UpdateDownloading(url));
                    var paquete = Path.Combine(temporal, Path.GetFileName(url));
                    Actualizador.Descargar(url, paquete);
                    Actualizador.VerificarPaquete(paquete, variante);

                    var desplegado = Actualizador.Desplegar(paquete, variante, temporal);

                    var nombreDelEjecutable = Path.GetFileName(Environment.ProcessPath) ?? "";
                    var reemplazos = Actualizador.Reemplazos(desplegado, variante, nombreDelEjecutable);

                    EnLaInterfaz(() =>
                    {
                        try
                        {
                            Log.Msg(Strings.UpdateReplacing(reemplazos.Count));
                            Actualizador.Reemplazar(Prefabs.Carpeta, reemplazos);
                            Aviso.Mostrar(Strings.UpdateDone, Strings.DialogTitleUpdate);
                            Tema.Reiniciar();
                        }
                        catch (Exception e)
                        {
                            Fallo(e);
                        }
                    });
                }
                catch (Exception e)
                {
                    EnLaInterfaz(() => Fallo(e));
                }
            });

            void Fallo(Exception e)
            {
                Log.Err(Strings.UpdateFailed(e.Message));
                Aviso.Mostrar(Strings.UpdateFailed(e.Message), Strings.DialogTitleUpdate);
                _actualizandoApp = false;
                linkLabelLatestVersion.Enabled = true;
            }
        }

        /// <summary>
        /// Corre algo en el hilo de la interfaz, sin romperse si la ventana ya se cerro.
        ///
        /// La descarga de la actualizacion tarda, y si alguien cierra mientras tanto el Invoke
        /// tira ObjectDisposedException desde un hilo de fondo, que no lo ataja nadie y voltea
        /// la aplicacion entera.
        /// </summary>
        private void EnLaInterfaz(Action accion)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated)
                    return;
                Invoke(accion);
            }
            catch (ObjectDisposedException)
            {
                // Se cerro entre el chequeo y el Invoke. No hay nada que actualizar.
            }
        }

        /// <summary>
        /// Vuelve a extraer de una vez todos los mods que RML ya tiene traducidos, que es lo
        /// que hace falta despues de una actualizacion del juego o de una tanda de mods.
        ///
        /// Corre en segundo plano para que la ventana siga respondiendo y el log se vea avanzar:
        /// son cientos de mods. Es seguro porque ActualizacionRml.Escribir fuerza la
        /// sobrescritura, asi que nunca abre el dialogo de archivo duplicado desde otro hilo, y
        /// PanelDeLog ya pasa cada linea al hilo de la interfaz.
        /// </summary>
        private async void buttonUpdateAllRml_Click(object sender, EventArgs e)
        {
            var rml = Prefabs.PathRml;
            if (string.IsNullOrWhiteSpace(rml))
            {
                Aviso.Mostrar(Strings.QuickUpdateNoRmlPath, Strings.DialogTitleNotice);
                return;
            }

            if (!Directory.Exists(Path.Combine(rml, "Data")))
            {
                Aviso.Mostrar(Strings.RmlPathWithoutData(rml), Strings.DialogTitleNotice);
                return;
            }

            var total = ActualizacionPorLotes.Contar(rml);
            if (total == 0)
            {
                Aviso.Mostrar(Strings.UpdateAllRmlNoData, Strings.DialogTitleNotice);
                return;
            }

            if (Aviso.Preguntar(Strings.ConfirmUpdateAllRml(total), Strings.DialogTitleNotice) != DialogResult.Yes)
                return;

            _actualizandoRml = true;
            HabilitarAcciones(false);
            Empezar();
            try
            {
                // Un paso por mod y uno mas para el indice, que tarda lo que varios mods.
                var renglones = await Task.Run(() =>
                {
                    var r = ActualizacionPorLotes.Correr(rml, (i, n, nombre) =>
                    {
                        Log.Det(Strings.BatchProgress(i, n, nombre));
                        MostrarAvance(Strings.ProgressMod(i, n, nombre), i - 1, n + 1);
                    });

                    // Una sola vez al final y en el mismo orden que la traduccion rapida: primero
                    // se acomoda Data/ y despues el indice sale con las rutas definitivas.
                    MostrarAvance(Strings.ProgressIndex, r.Count, r.Count + 1);
                    Agrupador.Reagrupar(rml);
                    LoadFoldersBuild.Regenerar(rml);
                    return r;
                });

                InformarLote(renglones);
                Terminar(Strings.ProgressDoneBatch(renglones.Count));
                Aviso.Mostrar(Strings.BatchDone, Strings.DialogTitleDone);
            }
            catch (Exception ex)
            {
                // Los fallos de un mod ya los ataja Correr. Esto es lo que queda fuera de ese
                // cuidado —leer la lista de mods instalados, por ejemplo—, y en un handler
                // async sin atajar cierra la aplicacion entera.
                Log.Err(Strings.ErrorMessagePrefix(ex.Message));
                Aviso.Mostrar(Strings.ErrorMessagePrefix(ex.Message), Strings.DialogTitleNotice);
            }
            finally
            {
                _actualizandoRml = false;
                HabilitarAcciones(true);
            }
        }

        /// <summary>El resumen de una actualizacion por lotes, con los mods que se saltearon por nombre.</summary>
        private static void InformarLote(List<ActualizacionPorLotes.Renglon> renglones)
        {
            var actualizados = renglones.Where(x => x.Motivo == ActualizacionPorLotes.Motivo.Actualizado).ToList();
            Log.Msg(Strings.BatchSummary(actualizados.Count, renglones.Count,
                actualizados.Sum(x => x.Conservadas), actualizados.Sum(x => x.Pendientes),
                actualizados.Sum(x => x.SinUso), actualizados.Sum(x => x.Rescatadas.Count)));

            void Informar(ActualizacionPorLotes.Motivo motivo, Func<int, string, string> texto)
            {
                var carpetas = renglones.Where(x => x.Motivo == motivo).Select(x => x.Carpeta).ToList();
                if (carpetas.Count > 0)
                    Log.Wrn(texto(carpetas.Count, string.Join(", ", carpetas)));
            }

            Informar(ActualizacionPorLotes.Motivo.NoInstalado, Strings.BatchNotInstalled);
            Informar(ActualizacionPorLotes.Motivo.OtroNumero, Strings.BatchOtroNumero);
            Informar(ActualizacionPorLotes.Motivo.SinPackageId, Strings.BatchNoPackageId);
            Informar(ActualizacionPorLotes.Motivo.NadaQueExtraer, Strings.BatchNothingToExtract);
            Informar(ActualizacionPorLotes.Motivo.Fallo, Strings.BatchFailed);
        }

        /// <summary>
        /// Prende o apaga todos los botones de la ventana. Mientras corre el lote no se puede
        /// extraer, cambiar opciones ni reiniciar por el tema: cualquiera de esas cosas pisaria
        /// el estado que la actualizacion esta usando.
        /// </summary>
        private void HabilitarAcciones(bool habilitar)
        {
            // El log detallado si se puede abrir: es justamente lo que se mira mientras corre.
            foreach (var boton in Controls.OfType<Button>().Where(x => x != buttonAbrirLog))
                boton.Enabled = habilitar;
        }

        /// <summary>
        /// Arranca una extraccion: el log detallado y el recuadro de avisos vuelven a empezar,
        /// para que traigan solo la que esta por correr.
        /// </summary>
        private void Empezar()
        {
            _salidaDelLog?.Empezar();
            EmpezarLogDetallado();
        }

        private static void EmpezarLogDetallado()
        {
            try
            {
                Log.EmpezarArchivo(RutaDelLogDetallado);
            }
            catch (Exception e)
            {
                // Otra instancia abierta lo tiene tomado. Se extrae igual: el archivo es para
                // mirar despues, y la ventana sigue mostrando lo importante.
                Log.Wrn(Strings.DetailedLogUnavailable(e.Message));
            }
        }

        /// <summary>
        /// El cierre comun: cuenta lo tecnico que quedo en el archivo y deja la barra llena.
        /// </summary>
        private void Terminar(string rotulo)
        {
            var tecnicos = _salidaDelLog?.AvisosTecnicos ?? 0;
            if (tecnicos > 0)
                Log.Msg(Strings.TechnicalWarningsInDetailedLog(tecnicos));

            _salidaDelLog?.Vaciar();
            MostrarAvance(rotulo, 1, 1);
        }

        /// <summary>
        /// Pone el rotulo y la barra. Desde otro hilo va por BeginInvoke, sin esperar a la
        /// ventana, por lo mismo que el log. Desde el de la ventana repinta en el momento:
        /// la extraccion de un mod lo tiene ocupado, y sin Refresh no se veria ningun paso.
        /// </summary>
        private void MostrarAvance(string rotulo, int valor, int maximo)
        {
            void Aplicar()
            {
                labelProgreso.Text = rotulo;
                progressBar.Maximum = Math.Max(1, maximo);
                progressBar.Value = Math.Clamp(valor, 0, progressBar.Maximum);
                labelProgreso.Refresh();
                progressBar.Refresh();
            }

            if (InvokeRequired)
                BeginInvoke(Aplicar);
            else
                Aplicar();
        }

        private void buttonAbrirLog_Click(object sender, EventArgs e)
        {
            var ruta = RutaDelLogDetallado;
            if (!File.Exists(ruta))
            {
                Aviso.Mostrar(Strings.DetailedLogMissing(ruta), Strings.DialogTitleNotice);
                return;
            }

            try
            {
                // Con el programa que Windows tenga para los .log, que casi siempre es el
                // Bloc de notas.
                Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
            }
            catch (Exception)
            {
                Process.Start("notepad.exe", ruta);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Process.Start("explorer.exe", GithubVersionCheker.IssueUrl);
        }

        /// <summary>
        /// Pone los textos y acomoda la ventana. Todavia maqueta en tiempo de ejecucion,
        /// con Rejilla y AutoAjuste, hasta que pase a TableLayoutPanel como
        /// FormInitialPathSelect.
        /// </summary>
        private void ApplyStrings()
        {
            // Anclajes: van antes de todo porque AutoAjuste ensancha la ventana. El panel de
            // log va dentro de tablaLog, que se estira con la ventana desde el Designer.
            labelSelectedMods.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label2.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            linkLabelLatestVersion.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label2.TextAlign = ContentAlignment.MiddleLeft;
            button1.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            Text = Strings.FormMainTitle;
            buttonSelectMod.Text = Strings.BtnSelectMod;
            button2.Text = Strings.BtnOptions;
            label1.Text = Strings.LabelMainDescription;
            buttonUpdateAllRml.Text = Strings.BtnUpdateAllRml;
            labelSelectedMods.Text = Strings.LabelNoModSelected;
            labelProgreso.Text = Strings.ProgressIdle;
            buttonAbrirLog.Text = Strings.BtnOpenDetailedLog;

            // El Designer lo deja negro fijo; que acompanie al tema.
            richTextBoxLog.BackColor = PanelDeLog.ColorFondo;
            richTextBoxAvisos.BackColor = PanelDeLog.ColorFondo;

            // En oscuro la barra pinta solo lo avanzado y el resto se confunde con el fondo.
            if (Tema.Luminancia(SystemColors.Control) < 128)
            {
                marcoProgreso.Padding = new Padding(1);
                marcoProgreso.BackColor = SystemColors.ControlDark;
            }

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
                Rejilla.Linea(buttonUpdateAllRml),
                Rejilla.Linea(button2, _buttonTema));

            // Los textos en espanol son mas largos que los originales y los
            // formularios tienen medidas fijas: se ensancha lo que no entra.
            AutoAjuste.Ajustar(buttonSelectMod, button2, _buttonTema, buttonUpdateAllRml,
                button1, labelSelectedMods, label1);

            // La franja del log la comparten tres cosas: el rotulo, el enlace de version
            // y el boton de reportar problemas. En el diseño original las tres arrancaban
            // en la misma columna, superpuestas, y solo se salvaba porque el rotulo estaba
            // centrado; al alinearlo a la izquierda el enlace lo tapaba, que va delante en
            // el orden Z. Se reparte a mano: el rotulo se queda con lo que mide su palabra
            // y el enlace ocupa el resto, que ademas le viene bien porque el aviso de
            // "no se pudo comprobar la version" no entraba en su ancho original.
            // Va despues del autoajuste, que es lo que fija el ancho final de la ventana.
            // El boton del log detallado se suma a la franja pegado al de reportar problemas,
            // con el ancho de su texto, y el enlace se queda con lo que sobra entre los dos.
            const int separacion = 6;
            label2.Width = AutoAjuste.AnchoNecesario(label2);
            buttonAbrirLog.Width = AutoAjuste.AnchoNecesario(buttonAbrirLog);
            buttonAbrirLog.Left = button1.Left - separacion - buttonAbrirLog.Width;
            linkLabelLatestVersion.Left = label2.Right + separacion;
            linkLabelLatestVersion.Width = Math.Max(
                0, buttonAbrirLog.Left - separacion - linkLabelLatestVersion.Left);
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