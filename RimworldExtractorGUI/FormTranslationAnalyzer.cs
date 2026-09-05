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

        public IEnumerable<TranslationAnalyzerEntry> Entries
        {
            get
            {
                return _items.Where(x => x.Checked && ((TranslationAnalyzerEntry)x.Tag).HasChanges)
                    .Select(x => (TranslationAnalyzerEntry)x.Tag);
            }
        }
        public FormTranslationAnalyzer(string[] paths)
        {
            InitializeComponent();
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
                var tag = (TranslationAnalyzerEntry)item.Tag;
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
                MessageBox.Show(Strings.SomeFilesFailedToAnalyze);
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
            var analyzerEntry = (TranslationAnalyzerEntry)selected.Tag;
            labelModTitle.Text = analyzerEntry.Metadata?.ToString() ?? Strings.OriginalModNotFound;
            comboBox1.SelectedIndex = (int)analyzerEntry.SaveMethod;

        }

        private void buttonOpenSelectMod_Click(object sender, EventArgs e)
        {
            var curSelected = listViewResults.SelectedItems[0];
            var curEntry = (TranslationAnalyzerEntry)curSelected.Tag;
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
            if (e.Item.Checked && ((TranslationAnalyzerEntry)e.Item.Tag).Metadata == null)
            {
                MessageBox.Show(Strings.CannotReextractUnknownMod);
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
                var entry = ((TranslationAnalyzerEntry)listViewItem.Tag);
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
            var entry = (TranslationAnalyzerEntry)item.Tag;
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
            var curOption = (string)comboBox1.SelectedItem;
            var curOptionIdx = comboBox1.SelectedIndex;
            var curSelected = listViewResults.SelectedItems[0];
            var curEntry = (TranslationAnalyzerEntry)curSelected.Tag;
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
            label1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            comboBox1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
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
        }
    }
}
