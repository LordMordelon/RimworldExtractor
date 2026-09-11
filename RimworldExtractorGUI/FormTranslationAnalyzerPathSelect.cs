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

        /// <summary>Margen contra los bordes de la ventana, el mismo del diseño original.</summary>
        private const int Margen = 12;
        public FormTranslationAnalyzerPathSelect()
        {
            InitializeComponent();
            Icon = Logo.Icono;
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
            // Anclajes primero, para que lo que se ensanche despues acompanie a la
            // ventana en vez de dejar un hueco a la derecha.
            textBox1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            label1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            buttonSelectSingleFile.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonSelectDir.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button1.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            Text = Strings.TitleAnalyzerPathSelect;
            buttonSelectSingleFile.Text = Strings.BtnSelectSingleXlsx;
            buttonSelectDir.Text = Strings.BtnSelectXlsxDir;
            label1.Text = Strings.LabelAnalyzerFaq;
            button1.Text = Strings.BtnSelectionDone;

            // Los textos en espanol son mas largos que los originales y los
            // formularios tienen medidas fijas: se ensancha lo que no entra.
            AutoAjuste.Ajustar(buttonSelectSingleFile, buttonSelectDir, button1, label1);

            // Los tres botones forman un bloque parejo apoyado en el borde derecho: los
            // dos de elegir se reparten el ancho y el de terminar lo ocupa entero. Antes
            // cada uno tomaba el ancho de su propio texto, asi que el de terminar quedaba
            // mas corto que los de arriba y desalineado con ellos.
            var filaElegir = Rejilla.Linea(buttonSelectSingleFile, buttonSelectDir);
            var filaTerminar = Rejilla.Linea(button1);
            var izquierdaDeLosBotones = Rejilla.ColumnaALaDerecha(this, Margen, filaElegir, filaTerminar);

            // El texto de ayuda es el que manda el ancho de la ventana: son cinco
            // renglones que no se pueden achicar. Si lo que quedo a su izquierda no le
            // alcanza se ensancha la ventana y se vuelve a apoyar el bloque de botones.
            var falta = AutoAjuste.AnchoNecesario(label1) - (izquierdaDeLosBotones - Margen * 2);
            if (falta > 0)
            {
                ClientSize = new Size(ClientSize.Width + falta, ClientSize.Height);
                MinimumSize = Size;
                izquierdaDeLosBotones = Rejilla.ColumnaALaDerecha(this, Margen, filaElegir, filaTerminar);
            }

            // El campo de la ruta y el texto de ayuda ocupan lo que queda a la izquierda.
            var anchoRestante = izquierdaDeLosBotones - Margen * 2;
            textBox1.Width = anchoRestante;
            label1.Width = anchoRestante;
        }
    }
}
