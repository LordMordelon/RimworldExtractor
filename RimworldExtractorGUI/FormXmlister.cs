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
    public partial class FormXmlister : Form
    {
        public string[] FileNames = Array.Empty<string>();
        public FormXmlister()
        {
            InitializeComponent();
            ApplyStrings();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.Multiselect = true;
            dialog.Title = Strings.SelectLanguagesRootFolder;

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                textBox1.Text = string.Join('|', dialog.FileNames);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            FileNames = textBox1.Text.Split('|');
            Close();
        }
    
        /// <summary>
        /// Traduce los controles en tiempo de ejecucion, para no tocar el .Designer.cs
        /// y mantener limpios los merges con upstream.
        /// </summary>
        private void ApplyStrings()
        {
            button2.Text = Strings.BtnDone;
            label1.Text = Strings.LabelSelectLanguagesRoot;
        }
    }
}
