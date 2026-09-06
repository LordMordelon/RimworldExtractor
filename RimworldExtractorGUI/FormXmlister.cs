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
        /// <summary>Margen contra los bordes de la ventana, el mismo del diseño original.</summary>
        private const int Margen = 12;

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
            // Anclajes primero, para que lo que se ensanche despues acompanie a la
            // ventana en vez de dejar un hueco a la derecha.
            label1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBox1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            button1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button2.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Text = Strings.TitleXmlister;
            button2.Text = Strings.BtnDone;
            label1.Text = Strings.LabelSelectLanguagesRoot;

            // Los textos en espanol son mas largos que los originales y los
            // formularios tienen medidas fijas: se ensancha lo que no entra.
            AutoAjuste.Ajustar(button2, label1);

            // El campo llega hasta el boton de examinar, y el rotulo y el de terminar
            // cruzan la ventana de lado a lado. Es la misma disposicion que la del
            // combinador, que es un dialogo de la misma forma: un campo y un boton que
            // confirma. Antes el de terminar media 75 px sueltos contra el borde.
            Rejilla.FilaPegadaALaDerecha(this, textBox1, button1);

            var anchoUtil = ClientSize.Width - Margen * 2;
            label1.Width = anchoUtil;
            button2.SetBounds(Margen, button2.Top, anchoUtil, button2.Height);
        }
    }
}
