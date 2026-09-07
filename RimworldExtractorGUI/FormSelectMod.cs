using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using Microsoft.Win32;
using RimworldExtractorInternal;
using RimworldExtractorInternal.DataTypes;
using ListBox = System.Windows.Controls.ListBox;
using OpenFileDialog = System.Windows.Forms.OpenFileDialog;
using ToolTip = System.Windows.Forms.ToolTip;

namespace RimworldExtractorGUI
{
    public partial class FormSelectMod : Form
    {
        public ModMetadata? SelectedMod { get; private set; }
        public List<ExtractableFolder> SelectedFolders { get; private set; }
        // Es estado de ejecucion, no del disenador. Sin este atributo el analizador
        // WFO1000 de WinForms lo trata como error a partir de .NET 8.
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public List<ModMetadata> ReferenceMods { get; init; }

        /// <summary>
        /// Casilla de la traduccion rapida. Se crea en codigo, como todo lo que agrega el
        /// fork, para no tocar el .Designer.cs.
        /// </summary>
        private readonly System.Windows.Forms.CheckBox _checkBoxQuickUpdate =
            new() { Name = "checkBoxQuickUpdate", AutoSize = true, Checked = true };

        /// <summary>Si hay que actualizar sobre RML en vez de dejar una carpeta suelta.</summary>
        public bool QuickUpdate { get; private set; }

        /// <summary>
        /// Casilla de la extraccion completa: carga el contenido oficial como referencia.
        /// </summary>
        private readonly System.Windows.Forms.CheckBox _checkBoxFullExtraction =
            new() { Name = "checkBoxFullExtraction", AutoSize = true, Checked = true };

        private readonly List<ModMetadata> _officialModsCached;
        private readonly List<ModMetadata> _localModsCached;
        private readonly List<ModMetadata> _workshopModsCached;
        private readonly List<ModMetadata> _allModsCached;

        public FormSelectMod()
        {
            InitializeComponent();
            ApplyStrings();
            ModLister.ResetCache();
            _officialModsCached = ModLister.OfficialMods.ToList();
            _localModsCached = ModLister.LocalMods.ToList();
            _workshopModsCached = ModLister.WorkshopMods.ToList();
            _allModsCached = _officialModsCached.Concat(_localModsCached).Concat(_workshopModsCached).ToList();

            SelectedFolders = new List<ExtractableFolder>();
            ReferenceMods = new List<ModMetadata>();

            if (!string.IsNullOrEmpty(Prefabs.PathBaseRefList))
            {
                var lines = File.ReadAllLines(Prefabs.PathBaseRefList);
                foreach (var mod in _allModsCached)
                {
                    if (lines.Any(x => mod.Identifier == x))
                    {
                        ReferenceMods.Add(mod);
                    }
                }
            }

            ResetListBoxMods();
        }

        public FormSelectMod(ModMetadata? initSelectedMod) : this()
        {
            if (initSelectedMod == null)
                return;
            SelectedMod = initSelectedMod;
            listBoxMods.SelectedItem = initSelectedMod;
            listBoxMods.TopIndex = listBoxMods.SelectedIndex;
            var autoSelected = listBoxExtractableFolders.Items.OfType<ExtractableFolder>().Where(x => x.IsAutoSelectable()).ToList();

            foreach (ExtractableFolder extractableFolder in autoSelected)
            {
                listBoxExtractableFolders.SelectedItems.Add(extractableFolder);
            }
        }

        private void ResetListBoxMods(bool filterSelected = false)
        {
            listBoxMods.Items.Clear();
            var keyword = textBoxSearch.Text.ToLower();

            listBoxMods.Items.Add(Separador(Strings.ModListSectionOfficial));
            foreach (var officialContent in _officialModsCached)
            {
                if (string.IsNullOrEmpty(keyword) || officialContent.Identifier.ToLower().Contains(keyword))
                {
                    if (filterSelected && !ReferenceMods.Contains(officialContent) && SelectedMod != officialContent)
                        continue;
                    listBoxMods.Items.Add(officialContent);
                }
            }
            listBoxMods.Items.Add(Separador(Strings.ModListSectionLocal));
            foreach (var localMod in _localModsCached)
            {
                if (string.IsNullOrEmpty(keyword) || localMod.Identifier.ToLower().Contains(keyword))
                {
                    if (filterSelected && !ReferenceMods.Contains(localMod) && SelectedMod != localMod)
                        continue;
                    listBoxMods.Items.Add(localMod);
                }
            }
            listBoxMods.Items.Add(Separador(Strings.ModListSectionWorkshop));
            foreach (var workshopMod in _workshopModsCached)
            {
                if (string.IsNullOrEmpty(keyword) || workshopMod.Identifier.ToLower().Contains(keyword))
                {
                    if (filterSelected && !ReferenceMods.Contains(workshopMod) && SelectedMod != workshopMod)
                        continue;

                    listBoxMods.Items.Add(workshopMod);
                }
            }

            if (SelectedMod != null)
            {
                listBoxMods.SelectedItem = SelectedMod;
            }
        }

        private ModMetadata? SelectCurrentMod()
        {
            listBoxExtractableFolders.Items.Clear();
            var item = listBoxMods.SelectedItem as ModMetadata;
            if (item != null)
            {
                foreach (var extractableFolder in ModLister.GetExtractableFolders(item))
                {
                    listBoxExtractableFolders.Items.Add(extractableFolder);
                }
                var autoSelected = listBoxExtractableFolders.Items.OfType<ExtractableFolder>().Where(x => x.IsAutoSelectable()).ToList();

                foreach (ExtractableFolder extractableFolder in autoSelected)
                {
                    listBoxExtractableFolders.SelectedItems.Add(extractableFolder);
                }
            }

            return item;
        }

        /// <summary>
        /// El renglon que separa una seccion de la siguiente.
        ///
        /// Antes se centraba rellenando con espacios hasta un largo fijo en caracteres,
        /// que solo funciona si el titulo mide lo que se supuso: los titulos en español
        /// son mas largos y se pasaban de ese largo, con lo cual dejaban de centrarse.
        /// Ahora se dibuja alineado a la izquierda, igual que el resto de la lista, y el
        /// texto puede medir lo que sea.
        /// </summary>
        private static string Separador(string titulo) => $"{Guion} {titulo} {Guion}";

        private const string Guion = "====================";
        /// <summary>Las dos columnas de cada renglon: el identificador y el nombre.</summary>
        private static (string Id, string Nombre) ColumnasDelMod(ModMetadata metadata)
            => (metadata.IsOfficialContent ? Strings.ModListOfficialTag : metadata.Id, metadata.ModName);

        /// <summary>
        /// Referencia para medir el ancho de la columna del identificador: los diez
        /// digitos que usa el workshop.
        /// </summary>
        private const string PlantillaColumnaId = "0000000000";

        /// <summary>Marca visual entre las dos columnas.</summary>
        private const string SeparadorDeColumnas = "::";

        /// <summary>Aire entre una columna y la siguiente.</summary>
        private const int MargenDeColumna = 8;
        private static void OpenWithExplorer(ModMetadata item)
        {
            Process.Start("explorer.exe", item.RootDir);
        }

        private IEnumerable<ToolStripMenuItem> BaseMenus
        {
            get
            {
                var menuSaveRefModsList = new ToolStripMenuItem(Strings.MenuSaveRefModsList);
                menuSaveRefModsList.Click += (sender, args) =>
                {
                    var saveFileDialog = new System.Windows.Forms.SaveFileDialog();
                    saveFileDialog.Title = Strings.SaveRefModsList;
                    saveFileDialog.InitialDirectory = Assembly.GetExecutingAssembly().Location;
                    saveFileDialog.Filter = Strings.FilterRefModsList;
                    saveFileDialog.DefaultExt = "refMods";
                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        File.WriteAllLines(saveFileDialog.FileName, ReferenceMods.Select(x => x.Identifier));
                    }
                };
                yield return menuSaveRefModsList;
                var menuLoadRefModsList = new ToolStripMenuItem(Strings.MenuLoadRefModsList);
                menuLoadRefModsList.Click += (sender, args) =>
                {
                    var openFileDialog = new OpenFileDialog();
                    openFileDialog.Title = Strings.LoadRefModsFromFile;
                    openFileDialog.InitialDirectory = Assembly.GetExecutingAssembly().Location;
                    openFileDialog.Filter = Strings.FilterRefModsList;
                    openFileDialog.DefaultExt = "refMods";
                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        ReferenceMods.Clear();
                        var lines = File.ReadAllLines(openFileDialog.FileName);
                        foreach (var mod in _allModsCached)
                        {
                            if (lines.Any(x => mod.Identifier == x))
                            {
                                ReferenceMods.Add(mod);
                            }
                        }

                        ResetListBoxMods();
                    }
                };
                yield return menuLoadRefModsList;
            }
        }

        private void textBoxSearch_TextChanged(object sender, EventArgs e)
        {
            ResetListBoxMods();
        }
        private void listBoxMods_SelectedIndexChanged(object sender, EventArgs e)
        {
            var currentMod = SelectCurrentMod();
            labelSelectedMod.Text = Strings.SelectModToExtract;
            buttonDone.Enabled = true;
            if (currentMod == null)
                return;
            SelectedMod = currentMod;
            labelSelectedMod.Text = currentMod.ModName;
            if (currentMod.ModDependencies is { Count: > 0 })
            {
                labelSelectedMod.Text += Strings.ModDependenciesSuffix(string.Join(';', currentMod.ModDependencies));
            }
        }
        private void buttonDone_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            QuickUpdate = _checkBoxQuickUpdate.Checked;

            foreach (ExtractableFolder extractableFolder in listBoxExtractableFolders.SelectedItems)
            {
                SelectedFolders.Add(extractableFolder);
            }

            // Los mods de los que el mod elegido necesita defs entran como referencia: sus
            // defs se cargan para resolver los patches, pero no se traducen. Son dos
            // fuentes, las dos declaradas por el propio mod: el contenido oficial mas sus
            // dependencias, y los mods que sus patches nombran en PatchOperationFindMod.
            if (_checkBoxFullExtraction.Checked && SelectedMod?.IsOfficialContent != true && SelectedMod != null)
            {
                var declarados = ModLister.FindAllReferenceMods(SelectedMod)
                    .Concat(ModLister.FindModsNamedInPatches(SelectedFolders));

                foreach (var mod in declarados)
                {
                    if (mod != SelectedMod && !ReferenceMods.Contains(mod))
                        ReferenceMods.Add(mod);
                }
            }

            Close();
        }
        private void listBoxMods_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            var idx = listBoxMods.IndexFromPoint(e.Location);
            var item = listBoxMods.Items[idx] as ModMetadata;
            if (item == null) return;
            var contextMenu = new ContextMenuStrip();

            var menuItem1 = new ToolStripMenuItem(Strings.MenuSelectAsExtractionTarget);
            menuItem1.Click += (o, args) =>
            {
                listBoxMods.SelectedIndex = idx;
                SelectedMod = SelectCurrentMod();
            };
            contextMenu.Items.Add(menuItem1);

            var menuItem2 = new ToolStripMenuItem(Strings.MenuOpenInFileExplorer);
            menuItem2.Click += (o, args) => { OpenWithExplorer(item); };
            contextMenu.Items.Add(menuItem2);

            if (ReferenceMods.Contains(item))
            {
                var menuItem3 = new ToolStripMenuItem(Strings.MenuUnselectAsReference);
                menuItem3.Click += (o, args) =>
                {
                    ReferenceMods.Remove(item);
                    listBoxMods.Invalidate(listBoxMods.GetItemRectangle(idx));
                };
                contextMenu.Items.Add(menuItem3);
            }
            else
            {
                var menuItem3 = new ToolStripMenuItem(Strings.MenuSelectAsReference);
                menuItem3.Click += (o, args) =>
                {
                    ReferenceMods.Add(item);
                    listBoxMods.Invalidate(listBoxMods.GetItemRectangle(idx));
                };
                contextMenu.Items.Add(menuItem3);
            }

            var menuItem4 = new ToolStripMenuItem(Strings.MenuSelectAllRelatedAsReference);
            menuItem4.Click += (o, args) =>
            {
                var requiredMods = item.ModDependencies?.Select(x => _allModsCached.Find(y => y.PackageId == x))
                    .ToList();
                if (requiredMods == null)
                {
                    Aviso.Mostrar(Strings.NoDependencies);
                    return;
                }

                foreach (var modMetadata in ModLister.FindAllReferenceMods(item))
                {
                    if (ReferenceMods.Contains(modMetadata))
                        continue;
                    ReferenceMods.Add(modMetadata);
                }

                listBoxMods.Refresh();
            };
            contextMenu.Items.Add(menuItem4);

            foreach (var toolStripMenuItem in BaseMenus)
            {
                contextMenu.Items.Add(toolStripMenuItem);
            }

            contextMenu.Show(MousePosition);
        }

        private void listBoxMods_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.A:
                    {
                        var item = SelectCurrentMod();
                        if (item == null) return;
                        OpenWithExplorer(item);
                        break;
                    }
                case Keys.S:
                    {
                        var mod = SelectCurrentMod();
                        var selectedItem = listBoxMods.SelectedItem as ModMetadata;
                        var selectedIdx = listBoxMods.SelectedIndex;
                        if (mod == null || selectedItem == null) return;
                        if (ReferenceMods.Contains(mod))
                        {
                            ReferenceMods.Remove(mod);
                            listBoxMods.Invalidate(listBoxMods.GetItemRectangle(selectedIdx));
                        }
                        else
                        {
                            ReferenceMods.Add(mod);
                            listBoxMods.Invalidate(listBoxMods.GetItemRectangle(selectedIdx));
                        }

                        break;
                    }
                case Keys.D:
                    checkBoxFilterSelected.Checked = !checkBoxFilterSelected.Checked;
                    break;
            }
        }
        private void listBoxMods_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index == -1)
                return;

            // e.ForeColor sale del tema y ya contempla si el renglon esta seleccionado.
            // Antes era Brushes.Black fijo, y sobre fondo oscuro no se leia.
            using var pincel = new SolidBrush(e.ForeColor);
            using var formato = new StringFormat(StringFormatFlags.NoWrap);

            // e.Font solo viene nulo si el evento se dispara sin renglon; la fuente de la
            // lista es la misma que se usaria igual.
            var fuente = e.Font ?? listBoxMods.Font;

            if (listBoxMods.Items[e.Index] is string sep)
            {
                e.DrawBackground();

                // Arranca donde arranca la columna del identificador, para que quede a
                // plomo con los renglones de mod que tiene debajo.
                e.Graphics.DrawString(sep, fuente, pincel,
                    new Rectangle(e.Bounds.Left + MargenDeColumna, e.Bounds.Top,
                        Math.Max(0, e.Bounds.Width - MargenDeColumna), e.Bounds.Height),
                    formato);
                e.DrawFocusRectangle();
                return;
            }
            var curMod = (ModMetadata)listBoxMods.Items[e.Index];
            var (id, nombre) = ColumnasDelMod(curMod);
            if (ReferenceMods.Any(x => curMod.RootDir == x.RootDir))
                nombre = Strings.ReferencePrefix + nombre;

            e.DrawBackground();

            // Las columnas se ubican midiendo la fuente, no rellenando con espacios como
            // hacia el diseño original: la fuente es proporcional, asi que un espacio no
            // mide lo mismo que un digito y los nombres terminaban desalineados entre si,
            // cada uno centrado dentro de su propio relleno.
            var anchoId = Math.Max(
                Medir(e.Graphics, fuente, PlantillaColumnaId),
                Medir(e.Graphics, fuente, Strings.ModListOfficialTag));

            var xId = e.Bounds.Left + MargenDeColumna;
            var xSeparador = xId + anchoId + MargenDeColumna;
            var xNombre = xSeparador + Medir(e.Graphics, fuente, SeparadorDeColumnas) + MargenDeColumna;

            e.Graphics.DrawString(id, fuente, pincel,
                new Rectangle(xId, e.Bounds.Top, anchoId, e.Bounds.Height), formato);
            e.Graphics.DrawString(SeparadorDeColumnas, fuente, pincel,
                new PointF(xSeparador, e.Bounds.Top), formato);

            // El nombre se dibuja dentro de lo que queda hasta el borde: si no entra se
            // corta ahi en vez de desbordar sobre la barra de desplazamiento.
            e.Graphics.DrawString(nombre, fuente, pincel,
                new Rectangle(xNombre, e.Bounds.Top, Math.Max(0, e.Bounds.Right - xNombre), e.Bounds.Height),
                formato);

            e.DrawFocusRectangle();
        }

        /// <summary>Ancho que ocupa un texto con la fuente del renglon que se esta dibujando.</summary>
        private static int Medir(Graphics grafico, Font fuente, string texto)
            => (int)Math.Ceiling(grafico.MeasureString(texto, fuente).Width);
        private void listBoxExtractableFolders_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index == -1)
                return;
            e.DrawBackground();
            using var pincel = new SolidBrush(e.ForeColor);
            e.Graphics.DrawString(listBoxExtractableFolders.Items[e.Index].ToString(),
                e.Font ?? listBoxExtractableFolders.Font, pincel, e.Bounds, StringFormat.GenericDefault);
            e.DrawFocusRectangle();
        }
        private void checkBoxFilterSelected_CheckedChanged(object sender, EventArgs e)
        {
            ResetListBoxMods(checkBoxFilterSelected.Checked);
        }
        private void FormSelectMod_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            var contextMenu = new ContextMenuStrip();
            foreach (var toolStripMenuItem in BaseMenus)
            {
                contextMenu.Items.Add(toolStripMenuItem);
            }

            contextMenu.Show(MousePosition);
        }



        private void listBoxExtractableFolders_MouseMove(object sender, MouseEventArgs e)
        {
            var idx = listBoxExtractableFolders.IndexFromPoint(listBoxExtractableFolders.PointToClient(MousePosition));
            if (idx == -1)
            {
                toolTip1.Active = false;
                return;
            }
            if (listBoxExtractableFolders.Items[idx] is not ExtractableFolder item)
                return;

            toolTip1.Active = true;
            var prevToolTip = toolTip1.GetToolTip(listBoxExtractableFolders);
            var curToolTip = item.ToString();

            if (prevToolTip == curToolTip)
                return;
            toolTip1.SetToolTip(listBoxExtractableFolders, curToolTip);
        }
    
        /// <summary>
        /// Traduce los controles en tiempo de ejecucion, para no tocar el .Designer.cs
        /// y mantener limpios los merges con upstream.
        /// </summary>
        private void ApplyStrings()
        {
            // La lista de mods conserva su ancho y la de carpetas crece: es la angosta
            // y la que tiene los nombres largos. Los anclajes no reparten espacio entre
            // dos controles, asi que hay que elegir cual de los dos se estira.
            listBoxMods.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            listBoxExtractableFolders.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            buttonDone.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            label1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            // El panel del mod elegido acompaña a la lista de carpetas, que es la que se
            // estira. Con el anclaje original —arriba y a la derecha, con ancho fijo— la
            // lista se ensanchaba y el panel no, asi que el nombre del mod terminaba
            // arrancando mucho mas a la derecha que el rotulo de la carpeta.
            panel1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Text = Strings.TitleSelectMod;
            buttonDone.Text = Strings.BtnSelectionDone;
            label1.Text = Strings.LabelSelectModControls;
            label2.Text = Strings.LabelSelectExtractionMode;
            label3.Text = Strings.LabelSelectFolder;
            labelSelectedMod.Text = Strings.LabelSelectExtractionMode;
            checkBoxFilterSelected.Text = Strings.CheckBoxFilterSelected;

            // Sin esto el buscador es un recuadro vacio sin ninguna pista de para que
            // sirve. El texto lo dibuja el propio TextBox y desaparece al escribir, asi
            // que no ensucia lo que se busca ni hay que limpiarlo a mano.
            textBoxSearch.PlaceholderText = Strings.SearchModsPlaceholder;

            // Los textos en espanol son mas largos que los originales y los
            // formularios tienen medidas fijas: se ensancha lo que no entra.
            AutoAjuste.Ajustar(buttonDone, label2, label3, checkBoxFilterSelected);

            // La casilla arranca la columna de la izquierda, asi que se alinea con el
            // buscador y con la lista que tiene debajo. En el diseño original quedaba
            // suelta contra el borde derecho de esa columna.
            checkBoxFilterSelected.Left = textBoxSearch.Left;

            // Lo mismo del otro lado: el rotulo de la carpeta venia indentado dentro de su
            // columna, con lo cual se leia como si estuviera centrado. Se alinea con la
            // lista que tiene debajo.
            label3.TextAlign = ContentAlignment.MiddleLeft;
            label3.Left = listBoxExtractableFolders.Left;

            // Y el de la columna izquierda, que venia igual de indentado.
            label2.TextAlign = ContentAlignment.MiddleLeft;
            label2.Left = listBoxMods.Left;

            // El panel arranca y termina donde la lista que tiene debajo, para que el
            // nombre del mod quede sobre la misma vertical que el rotulo de la carpeta.
            panel1.SetBounds(
                listBoxExtractableFolders.Left, panel1.Top,
                listBoxExtractableFolders.Width, panel1.Height);

            // Comparte fila con la otra casilla: las dos cambian que hace el boton de
            // aceptar, y esa fila tenia lugar de sobra.
            _checkBoxQuickUpdate.Text = Strings.CheckBoxQuickUpdate;
            _checkBoxQuickUpdate.Top = checkBoxFilterSelected.Top;
            _checkBoxQuickUpdate.Left = checkBoxFilterSelected.Right + 24;
            Controls.Add(_checkBoxQuickUpdate);
            toolTip1.SetToolTip(_checkBoxQuickUpdate, Strings.TooltipQuickUpdate);

            _checkBoxFullExtraction.Text = Strings.CheckBoxFullExtraction;
            _checkBoxFullExtraction.Top = checkBoxFilterSelected.Top;
            _checkBoxFullExtraction.Left = _checkBoxQuickUpdate.Right + 24;
            Controls.Add(_checkBoxFullExtraction);
            toolTip1.SetToolTip(_checkBoxFullExtraction, Strings.TooltipFullExtraction);

            AcomodarModElegido();
            Shown += (_, _) => AcomodarModElegido();
            panel1.SizeChanged += (_, _) => AcomodarModElegido();
        }

        /// <summary>
        /// Deja que el nombre del mod elegido ocupe el panel entero y se parta en varias
        /// lineas, alineado a la izquierda.
        ///
        /// Venia con AutoSize, o sea creciendo en una sola linea: un nombre largo —y mas
        /// con la lista de mods previos— se pasaba del panel, y como el panel tiene
        /// AutoScroll respondia con una barra horizontal y el texto quedaba corrido.
        /// </summary>
        private void AcomodarModElegido()
        {
            // Con un ancho maximo el rotulo parte el texto en varias lineas y crece hacia
            // abajo; si no entra, el panel tiene AutoScroll y se desplaza. Fijarle la
            // altura al panel seria peor: cortaria el final del texto sin avisar.
            labelSelectedMod.TextAlign = ContentAlignment.TopLeft;
            labelSelectedMod.MaximumSize = new Size(
                Math.Max(0, panel1.ClientSize.Width - labelSelectedMod.Left * 2), 0);
        }
    }
}
