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
        /// Ahora el renglon se dibuja centrado de verdad y el texto puede medir lo que sea.
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
            foreach (ExtractableFolder extractableFolder in listBoxExtractableFolders.SelectedItems)
            {
                SelectedFolders.Add(extractableFolder);
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
                    MessageBox.Show(Strings.NoDependencies);
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
                using var centrado = new StringFormat(StringFormatFlags.NoWrap)
                {
                    Alignment = StringAlignment.Center
                };
                e.Graphics.DrawString(sep, fuente, pincel, e.Bounds, centrado);
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
            panel1.Anchor = AnchorStyles.Top | AnchorStyles.Right;

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
        }
    }
}
