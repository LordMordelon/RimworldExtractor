using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using RimworldExtractorInternal;

namespace RimworldExtractorGUI
{
    public partial class FormTranslationAnalyzerPathSelect : Form
    {
        public string[] Paths = Array.Empty<string>();
        public FormTranslationAnalyzerPathSelect()
        {
            InitializeComponent();
            ApplyStrings();
        }

        private void buttonSelectSingleFile_Click(object sender, EventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.Title = Strings.SelectXlsxFile;
            dialog.Filters.Add(new CommonFileDialogFilter(Strings.FilterTranslationXlsx, "*.xlsx"));
            dialog.Multiselect = true;


            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                textBox1.Text = string.Join('|', dialog.FileNames);
            }
        }

        private void buttonSelectDir_Click(object sender, EventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.Multiselect = true;
            dialog.Title = Strings.SelectXlsxRootFolder;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                textBox1.Text = string.Join('|', dialog.FileNames);
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            button1.Enabled = textBox1.Text.Length > 0;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            var tokens = textBox1.Text.Split('|');
            if (tokens.Length > 0 && Directory.Exists(tokens[0]))
            {
                tokens = tokens.SelectMany(TranslationAnalyzerTool.GetXlsxPaths).ToArray();
            }
            Paths = tokens;
            Close();
        }
    
        /// <summary>
        /// Traduce los controles en tiempo de ejecucion, para no tocar el .Designer.cs
        /// y mantener limpios los merges con upstream.
        /// </summary>
        private void ApplyStrings()
        {
            Text = Strings.TitleAnalyzerPathSelect;
            buttonSelectSingleFile.Text = Strings.BtnSelectSingleXlsx;
            buttonSelectDir.Text = Strings.BtnSelectXlsxDir;
            label1.Text = Strings.LabelAnalyzerFaq;
            button1.Text = Strings.BtnSelectionDone;
        }
    }
}
