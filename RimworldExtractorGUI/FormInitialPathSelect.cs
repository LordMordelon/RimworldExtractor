using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;
using RimworldExtractorInternal;

namespace RimworldExtractorGUI
{
    public partial class FormInitialPathSelect : Form
    {
        public FormInitialPathSelect()
        {
            InitializeComponent();
            Icon = Logo.Icono;
            ApplyStrings();
            Prefabs.Init();
            textBoxPathRimworld.Text = Prefabs.PathRimworld;
            textBoxPathWorkshop.Text = Prefabs.PathWorkshop;
            textBoxPathRml.Text = Prefabs.PathRml;

            // La tabla ya sabe cuanto necesita; se fija como minimo para que la ventana no se
            // pueda encoger hasta romper lo que acaba de acomodar. Va aca y no en el diseñador
            // porque depende de los textos, que se asignan recien en ApplyStrings.
            PerformLayout();
            MinimumSize = Size;
        }

        private void buttonSelectPathRimworld_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = Strings.SelectRimworldExe;
            dialog.FileName = "";
            dialog.Filter = Strings.FilterRimworldExe;
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = false;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                textBoxPathRimworld.Text = Path.GetDirectoryName(dialog.FileName);
            }
        }

        private void buttonSelectPathWorkshop_Click(object sender, EventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.Title = Strings.SelectWorkshopPath;

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                textBoxPathWorkshop.Text = dialog.FileName;
            }
        }

        /// <summary>
        /// La carpeta del clon de RML, que es donde escribe el modo normal de extraccion.
        /// Se pide aca y no solo en Opciones porque dejo de ser un ajuste de un modo
        /// alterno: sin ella, extraer avisa y no hace nada.
        /// </summary>
        private void buttonSelectPathRml_Click(object sender, EventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.Title = Strings.SelectRmlPath;

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                textBoxPathRml.Text = dialog.FileName;
            }
        }

        private void buttonDone_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            Prefabs.PathRimworld = textBoxPathRimworld.Text;
            Prefabs.PathWorkshop = textBoxPathWorkshop.Text;

            // Vacia es una respuesta valida: quien solo quiera extraer a una carpeta no
            // tiene por que tener un clon de RML. Las otras dos rutas tampoco se validan.
            Prefabs.PathRml = textBoxPathRml.Text;
            Prefabs.Save();
            Close();
        }
    
        /// <summary>
        /// Traduce los controles en tiempo de ejecucion, para no tocar el .Designer.cs
        /// y mantener limpios los merges con upstream.
        /// </summary>
        private void ApplyStrings()
        {
            Text = Strings.TitleInitialPathSelect;
            label1.Text = Strings.LabelRimworldPathShort;
            label2.Text = Strings.LabelWorkshopPathShort;
            label3.Text = Strings.LabelPathRmlShort;
            buttonDone.Text = Strings.BtnDone;
        }
    }
}
