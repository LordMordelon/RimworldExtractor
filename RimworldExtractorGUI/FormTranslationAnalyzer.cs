using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using RimworldExtractorInternal;
using RimworldExtractorInternal.DataTypes;

namespace RimworldExtractorGUI
{
    public partial class FormTranslationAnalyzer : Form
    {
        private readonly List<ListViewItem> _items;

        /// <summary>
        /// La entrada que cuelga de una fila de la lista. El Tag es object? para el
        /// compilador, pero las filas las arma ConvertToItem y siempre le pone la suya:
        /// una fila sin entrada seria un error de programacion, no un caso a contemplar.
        /// </summary>
        private static TranslationAnalyzerEntry EntradaDe(ListViewItem fila)
            => (TranslationAnalyzerEntry)fila.Tag!;

        public IEnumerable<TranslationAnalyzerEntry> Entries
        {
            get
            {
                return _items.Where(x => x.Checked && EntradaDe(x).HasChanges)
                    .Select(x => EntradaDe(x));
            }
        }
        public FormTranslationAnalyzer(string[] paths)
        {
            InitializeComponent();
            Icon = Logo.Icono;
            ApplyStrings();
            _items = new List<ListViewItem>();
            Task.Factory.StartNew(() => { AnalyzeTranslation(paths); });
        }

        private void AnalyzeTranslation(string[] paths)
        {
            int invailedCnt = 0;
            for (var i = 0; i < paths.Length; i++)
            {
                if (labelTitle.InvokeRequired)
                {
                    labelTitle.Invoke(() => { labelTitle.Text = Strings.AnalyzingProgress(i, paths.Length); });
                }
                else
                {
                    labelTitle.Text = Strings.AnalyzingProgress(i, paths.Length);
                }

                var path = paths[i];
                var item = ConvertToItem(path);
                var tag = EntradaDe(item);
                if (tag.Invalid)
                    invailedCnt += 1;
                _items.Add(item);
                if (listViewResults.InvokeRequired)
                {
                    listViewResults.Invoke(() => { listViewResults.Items.Add(item); });
                }
                else
                {
                    listViewResults.Items.Add(item);
                }
            }

            var doneText = Strings.AnalysisDone;
            if (labelTitle.InvokeRequired)
            {
                labelTitle.Invoke(() => { labelTitle.Text = doneText; });
            }
            else
            {
                labelTitle.Text = doneText;
            }

            if (invailedCnt > 0)
            {
                Aviso.Mostrar(Strings.SomeFilesFailedToAnalyze);
            }
        }

        private static ListViewItem ConvertToItem(string filePath)
        {
            var item = new ListViewItem();
            var analyzerEntry = new TranslationAnalyzerEntry(filePath);
            if (analyzerEntry.Metadata != null)
            {
                var autoSelectedExtractableFolders = ModLister.GetExtractableFolders(analyzerEntry.Metadata)
                    .Where(x => x.IsAutoSelectable()).ToList();

                var autoSelectedReferenceMods = new List<ModMetadata>();
                foreach (var modMetadata in ModLister.FindAllReferenceMods(analyzerEntry.Metadata))
                {
                    if (autoSelectedReferenceMods.Contains(modMetadata))
                        continue;
                    autoSelectedReferenceMods.Add(modMetadata);
                }
                analyzerEntry.ReExtract(autoSelectedExtractableFolders, autoSelectedReferenceMods);
            }
            item.Tag = analyzerEntry;
            var rowTextData = new[]
            {
                analyzerEntry.Metadata?.Identifier ?? "UNKNOWN",
                "...\\" + Path.Combine(Path.GetFileName(Path.GetDirectoryName(filePath) ?? ""), Path.GetFileName(filePath)), analyzerEntry.OriginalTranslations.Count.ToString(),
                analyzerEntry.ChangesString, analyzerEntry.Metadata == null ? Strings.NeedsAssignment : Strings.Automatic, Strings.Append
            };
            item.SubItems.AddRange(rowTextData);
            item.Checked = analyzerEntry.HasChanges;
            return item;
        }

        private void listViewResults_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewResults.SelectedItems.Count == 0)
            {
                buttonOpenSelectMod.Enabled = false;
                comboBox1.Enabled = false;
                labelModTitle.Text = Strings.SelectModToFix;
                return;
            }

            buttonOpenSelectMod.Enabled = true;
            comboBox1.Enabled = true;
            var selected = listViewResults.SelectedItems[0];
            var analyzerEntry = EntradaDe(selected);
            labelModTitle.Text = analyzerEntry.Metadata?.ToString() ?? Strings.OriginalModNotFound;
            comboBox1.SelectedIndex = (int)analyzerEntry.SaveMethod;

        }

        private void buttonOpenSelectMod_Click(object sender, EventArgs e)
        {
            var curSelected = listViewResults.SelectedItems[0];
            var curEntry = EntradaDe(curSelected);
            var curMetaData = curEntry.Metadata;
            var form = new FormSelectMod(curMetaData);
            form.StartPosition = FormStartPosition.CenterParent;
            if (form.ShowDialog() == DialogResult.OK)
            {
                curEntry.Metadata = form.SelectedMod;
                curEntry.ResetChanges();
                curEntry.ReExtract(form.SelectedFolders, form.ReferenceMods);
                curSelected.SubItems[(int)Column.ModName] = new ListViewItem.ListViewSubItem()
                { Text = curEntry.Metadata?.Identifier ?? "UNKNOWN" };
                curSelected.SubItems[(int)Column.OriginalCount] = new ListViewItem.ListViewSubItem()
                { Text = curEntry.OriginalTranslations.Count.ToString() };
                curSelected.SubItems[(int)Column.ChangesCount] = new ListViewItem.ListViewSubItem()
                { Text = curEntry.ChangesString };
                curSelected.SubItems[(int)Column.ExtractionMethod] = new ListViewItem.ListViewSubItem()
                { Text = Strings.Manual };
                curSelected.Selected = true;
            }
        }

        private void listViewResults_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            if (e.Item.Checked && EntradaDe(e.Item).Metadata == null)
            {
                Aviso.Mostrar(Strings.CannotReextractUnknownMod);
                e.Item.Checked = false;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Close();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem listViewItem in listViewResults.Items)
            {
                var entry = EntradaDe(listViewItem);
                if (entry.Metadata == null || !entry.HasChanges)
                {
                    continue;
                }
                else
                {
                    listViewItem.Checked = true;
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            foreach (ListViewItem listViewItem in listViewResults.Items)
            {
                listViewItem.Checked = false;
            }

        }

        private void listViewResults_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            var item = listViewResults.FocusedItem;
            if (item == null || !item.Bounds.Contains(e.Location))
                return;
            var entry = EntradaDe(item);
            var contextMenu = new ContextMenuStrip();

            var menuItem1 = new ToolStripMenuItem(Strings.MenuOpenXlsxInExplorer);
            menuItem1.Click += (o, args) =>
            {
                Process.Start("explorer.exe", Path.GetDirectoryName(entry.FilePath) ?? "");
            };
            contextMenu.Items.Add(menuItem1);

            if (entry.Metadata != null)
            {
                var menuItem2 = new ToolStripMenuItem(Strings.MenuOpenModRootInExplorer);
                menuItem2.Click += (o, args) =>
                {
                    Process.Start("explorer.exe", Path.GetDirectoryName(entry.Metadata!.RootDir) ?? "");
                };
                contextMenu.Items.Add(menuItem2);
            }

            contextMenu.Show(MousePosition);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            var curOption = comboBox1.SelectedItem?.ToString() ?? string.Empty;
            var curOptionIdx = comboBox1.SelectedIndex;
            var curSelected = listViewResults.SelectedItems[0];
            var curEntry = EntradaDe(curSelected);
            var curMetaData = curEntry.Metadata;
            curSelected.SubItems[(int)Column.SaveMethod] = new ListViewItem.ListViewSubItem()
                { Text = curOption };
            curEntry.SaveMethod = (TranslationAnalyzerEntry.SaveMethodEnum)curOptionIdx;
        }

        private enum Column
        {
            ModName = 1, FilePath, OriginalCount, ChangesCount, ExtractionMethod, SaveMethod
        }
    
        /// <summary>
        /// Traduce los controles en tiempo de ejecucion, para no tocar el .Designer.cs
        /// y mantener limpios los merges con upstream.
        /// </summary>
        private void ApplyStrings()
        {
            // La columna de la derecha queda pegada al borde y la tabla ocupa lo que sobra.
            listViewResults.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panel1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonOpenSelectMod.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            // La fila del metodo de guardado la ubica AcomodarFilaDeGuardado, asi que no
            // lleva anclaje a la derecha: con el, WinForms volvia a colocar el desplegable
            // en una pasada de layout posterior y deshacia lo calculado.
            label1.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            comboBox1.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            button1.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            button2.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button3.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            Text = Strings.TitleTranslationAnalyzer;
            columnHeader1.Text = Strings.ColumnSelect;
            columnHeader2.Text = Strings.ColumnModInfo;
            columnHeader4.Text = Strings.ColumnFileName;
            columnHeader5.Text = Strings.ColumnOriginalCount;
            columnHeader6.Text = Strings.ColumnChanges;
            columnHeader7.Text = Strings.ColumnReextractMethod;
            columnHeader8.Text = Strings.ColumnSaveMethod;
            labelModTitle.Text = Strings.SelectModToFix;
            buttonOpenSelectMod.Text = Strings.BtnSelectModManually;
            button1.Text = Strings.BtnFixSelectedFiles;
            button2.Text = Strings.BtnDeselectAll;
            button3.Text = Strings.BtnSelectAllPossible;
            label1.Text = Strings.LabelSaveMethod;

            // El combo se lee por indice, asi que hay que conservar el orden.
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(new object[]
            {
                Strings.SaveMethodAppend,
                Strings.SaveMethodRebuildOverwrite,
                Strings.SaveMethodRebuildNew,
                Strings.SaveMethodOnlyNewNodes
            });

            // Los textos en espanol son mas largos que los originales y los
            // formularios tienen medidas fijas: se ensancha lo que no entra.
            AutoAjuste.Ajustar(buttonOpenSelectMod, button1, button2, button3, label1);

            // Los anchos de columna que vienen del diseño son la medida minima; a partir
            // de ahi se recalcula todo, asi que hay que guardarlos antes de tocarlos.
            _anchosDeColumna = listViewResults.Columns.Cast<ColumnHeader>().Select(c => c.Width).ToArray();

            Acomodar();

            // Cada parte se recalcula cuando cambia aquello de lo que depende, y no en un
            // momento fijo del arranque: los anclajes acomodan los controles en un orden
            // que no se puede dar por sabido, y ademas la escala por DPI se aplica despues
            // de construir la ventana.
            Shown += (_, _) => Acomodar();

            listViewResults.SizeChanged += (_, _) =>
            {
                AcomodarColumnas();
                AcomodarBotonesDeSeleccion();
            };

            panel1.LocationChanged += (_, _) => AcomodarColumnaDerecha();
            panel1.SizeChanged += (_, _) => AcomodarColumnaDerecha();
            label1.SizeChanged += (_, _) => AcomodarFilaDeGuardado();
        }

        /// <summary>Anchos de columna del diseño original, la base del reparto.</summary>
        private int[] _anchosDeColumna = Array.Empty<int>();

        /// <summary>Columna que se queda con el espacio sobrante: la de los nombres largos.</summary>
        private const int ColumnaElastica = 2;

        /// <summary>Aire a los lados del titulo de una columna.</summary>
        private const int MargenDeColumna = 24;

        private void Acomodar()
        {
            AcomodarBotonesDeSeleccion();
            AcomodarColumnas();
            AcomodarColumnaDerecha();
        }

        /// <summary>Lo que cuelga del panel del mod elegido, que es la referencia de esa columna.</summary>
        private void AcomodarColumnaDerecha()
        {
            AcomodarFilaDeGuardado();
            AcomodarTituloDelMod();
        }

        /// <summary>
        /// Reparte la fila del metodo de guardado entre la etiqueta y el desplegable.
        ///
        /// Los dos venian con posicion fija y en español se pisaban: el desplegable va por
        /// delante en el orden Z y tapaba el final de la etiqueta ("Método de guar").
        /// </summary>
        private void AcomodarFilaDeGuardado()
        {
            const int separacion = 6;
            label1.Left = panel1.Left;

            var izquierda = label1.Right + separacion;
            comboBox1.SetBounds(izquierda, comboBox1.Top,
                Math.Max(60, panel1.Right - izquierda), comboBox1.Height);
        }

        /// <summary>
        /// Deja que el nombre del mod ocupe el panel entero y se parta en varias lineas.
        ///
        /// Venia con AutoSize, o sea creciendo en una sola linea: los textos en español se
        /// pasaban del panel, y como el panel tiene AutoScroll respondia con una barra de
        /// desplazamiento horizontal en vez de mostrar el texto.
        /// </summary>
        private void AcomodarTituloDelMod()
        {
            // Con un ancho maximo el rotulo parte el texto en varias lineas y crece hacia
            // abajo; si no entra, el panel tiene AutoScroll y se desplaza. Fijarle la
            // altura al panel seria peor: cortaria el final del texto sin avisar.
            labelModTitle.TextAlign = ContentAlignment.TopLeft;
            labelModTitle.MaximumSize = new Size(
                Math.Max(0, panel1.ClientSize.Width - labelModTitle.Left * 2), 0);
        }

        /// <summary>
        /// Apoya los dos botones de seleccion en el borde derecho de la tabla, no en el de
        /// la ventana: a la derecha de la tabla esta el panel del mod elegido, que va por
        /// delante en el orden Z, y con los textos en español los botones terminaban
        /// metidos debajo.
        /// </summary>
        private void AcomodarBotonesDeSeleccion()
        {
            const int separacion = 6;
            button2.Left = listViewResults.Right - button2.Width;
            button3.Left = button2.Left - separacion - button3.Width;
        }

        /// <summary>
        /// Reparte el ancho de la tabla entre sus columnas.
        ///
        /// Los anchos venian medidos para los titulos en coreano, mas cortos, asi que en
        /// español se cortaban ("Cantid...", "Método ..."). Cada columna toma al menos lo
        /// que mide su titulo, y lo que sobra va a la del nombre del archivo, que es la que
        /// tiene los textos mas largos; asi ademas no queda una columna vacia al final.
        /// </summary>
        private void AcomodarColumnas()
        {
            if (_anchosDeColumna.Length != listViewResults.Columns.Count)
                return;

            var anchos = new int[_anchosDeColumna.Length];
            var total = 0;
            for (var i = 0; i < anchos.Length; i++)
            {
                var titulo = listViewResults.Columns[i].Text;
                var necesario = TextRenderer.MeasureText(titulo, listViewResults.Font).Width + MargenDeColumna;
                anchos[i] = Math.Max(_anchosDeColumna[i], necesario);
                total += anchos[i];
            }

            // ClientSize y no Width: descuenta el borde y la barra de desplazamiento, que
            // es lo que haria aparecer una barra horizontal de mas.
            //
            // Si sobra, se lo queda la columna elastica. Si falta, tambien se lo saca a
            // ella, pero sin bajar de lo que mide su propio titulo: mas alla de eso
            // aparece la barra horizontal, que para eso esta.
            var sobra = listViewResults.ClientSize.Width - total;
            var minimoElastica = TextRenderer.MeasureText(
                listViewResults.Columns[ColumnaElastica].Text, listViewResults.Font).Width + MargenDeColumna;
            anchos[ColumnaElastica] = Math.Max(minimoElastica, anchos[ColumnaElastica] + sobra);

            for (var i = 0; i < anchos.Length; i++)
                listViewResults.Columns[i].Width = anchos[i];
        }
    }
}
