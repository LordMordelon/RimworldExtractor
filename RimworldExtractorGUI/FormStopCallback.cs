using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using RimworldExtractorInternal;
using System.Windows.Controls;

namespace RimworldExtractorGUI
{
    public partial class FormStopCallback : Form
    {
        public FormStopCallback(string path)
        {
            InitializeComponent();
            ApplyStrings();
            label1.Text = path;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Yes; 
            Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.No;
            Close();
        }

        public static void StopCallbackXlsx(XLWorkbook xlsx, string path)
        {
            var form = new FormStopCallback(path);
            form.StartPosition = FormStartPosition.CenterScreen;
            if (form.ShowDialog() == DialogResult.Yes)
            {
                try
                {
                    xlsx.SaveAs(path);
                }
                catch (IOException io)
                {
                    Log.Err(Strings.CouldNotSaveFileInUse(io.Message));
                }
            }
            else
            {
                return;
            }
        }

        public static void StopCallbackXml(XmlDocument doc, string path)
        {
            var form = new FormStopCallback(path);
            form.StartPosition = FormStartPosition.CenterScreen;
            if (form.ShowDialog() == DialogResult.Yes)
            {
                doc.Save(path);
            }
            else
            {
                return;
            }
        }

        public static void StopCallbackTxt(IEnumerable<string> lines, string path)
        {
            var form = new FormStopCallback(path);
            form.StartPosition = FormStartPosition.CenterScreen;
            if (form.ShowDialog() == DialogResult.Yes)
            {
                File.WriteAllLines(path, lines);
            }
            else
            {
                return;
            }
        }
    
        /// <summary>
        /// Traduce los controles en tiempo de ejecucion, para no tocar el .Designer.cs
        /// y mantener limpios los merges con upstream.
        /// </summary>
        private void ApplyStrings()
        {
            Text = Strings.TitleStopCallback;
            label2.Text = Strings.LabelDuplicateFileFound;
            button1.Text = Strings.BtnOverwrite;
            button2.Text = Strings.BtnSkip;

            // Los textos en espanol son mas largos que los originales y los
            // formularios tienen medidas fijas: se ensancha lo que no entra.
            AutoAjuste.Ajustar(label2, button1, button2);
        }
    }
}
