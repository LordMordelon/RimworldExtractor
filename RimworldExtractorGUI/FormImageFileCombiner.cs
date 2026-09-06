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
using RimworldExtractorGUI.Utils;
using System.Diagnostics;
using RimworldExtractorInternal;

namespace RimworldExtractorGUI
{
    public partial class FormImageFileCombiner : Form
    {
        public FormImageFileCombiner()
        {
            InitializeComponent();
            ApplyStrings();
        }

        private void buttonSelectPathImage_Click(object sender, EventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.Title = Strings.SelectImageFile;
            dialog.FileName = "";
            dialog.Filter = Strings.FilterImageFile;
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = false;

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                textBoxPathImage.Text = dialog.FileName;
            }
        }

        private void buttonSelectPathFile_Click(object sender, EventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.Title = Strings.SelectZipToPackage;
            dialog.Filters.Add(new CommonFileDialogFilter(Strings.FilterZip, "*.zip"));


            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                textBoxPathFile.Text = dialog.FileName;
            }
        }

        private void buttonDone_Click(object sender, EventArgs e)
        {
            var filePath = textBoxPathFile.Text;
            var imgPath = string.IsNullOrEmpty(textBoxPathImage.Text) ? null : textBoxPathImage.Text;
            var imgExtension = Path.GetExtension(imgPath);
            if (imgExtension == null)
                imgExtension = ".jpg";

            if (imgPath != null && !File.Exists(imgPath))
            {
                MessageBox.Show(Strings.ImageNotFoundOrNoAccess);
                return;
            }


            string? OpenDialogSelectDestPath()
            {
                var dialog = new SaveFileDialog();
                dialog.Title = Strings.SelectSaveLocation;
                dialog.InitialDirectory = Path.GetDirectoryName(filePath) ?? "";
                dialog.FileName = Path.GetFileNameWithoutExtension(filePath) + imgExtension;
                dialog.Filter = Strings.FilterImageFile.Split('|')[0] + "|*" + imgExtension;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    return dialog.FileName;
                }

                return null;
            }

            if (File.Exists(filePath))
            {

                var destPath = OpenDialogSelectDestPath();
                if (destPath == null)
                {
                    MessageBox.Show(Strings.ReselectFileLocation);
                    return;
                }
                ImageFilePackageHelper.Package(filePath, destPath, imgPath);
                if (MessageBox.Show(Strings.DoneOpenPackagedFolder, Strings.DialogTitleDone, MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    Process.Start("explorer.exe", Path.GetDirectoryName(destPath) ?? "");
                }

                if (textBoxPathFile.Text != filePath)
                {
                    File.Delete(filePath);
                }
            }
            else if (Directory.Exists(filePath))
            {
                var newFilePath = Path.Combine(Path.GetDirectoryName(filePath) ?? "",
                    Path.GetFileNameWithoutExtension(filePath) + ".zip");
                ImageFilePackageHelper.ZipDir(filePath, newFilePath);

                var destPath = OpenDialogSelectDestPath();
                if (destPath == null)
                {
                    MessageBox.Show(Strings.ReselectFileLocation);
                    return;
                }
                ImageFilePackageHelper.Package(newFilePath, destPath, imgPath);
                File.Delete(newFilePath);
                if (MessageBox.Show(Strings.DoneOpenPackagedFolder, Strings.DialogTitleDone, MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    Process.Start("explorer.exe", Path.GetDirectoryName(destPath) ?? "");
                }
            }
            else
            {
                MessageBox.Show(Strings.FileNotFoundOrNoAccess);
                                
            }
        }

        private void buttonSelectPathDir_Click(object sender, EventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            dialog.Title = Strings.SelectFolderToPackage;

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                textBoxPathFile.Text = dialog.FileName;
            }
        }
    
        /// <summary>
        /// Traduce los controles en tiempo de ejecucion, para no tocar el .Designer.cs
        /// y mantener limpios los merges con upstream.
        /// </summary>
        private void ApplyStrings()
        {
            // Anclajes primero, para que lo que se ensanche despues acompanie a la
            // ventana en vez de dejar un hueco a la derecha.
            textBoxPathImage.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxPathFile.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            buttonSelectPathImage.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonSelectPathFile.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonSelectPathDir.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            label3.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            buttonDone.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            Text = Strings.TitleImageFileCombiner;
            buttonDone.Text = Strings.BtnDone;
            label1.Text = Strings.LabelSelectImagePath;
            label2.Text = Strings.LabelSelectFileToCombine;
            label3.Text = Strings.LabelCombinerHelp;
            buttonSelectPathFile.Text = Strings.BtnSelectFile;
            buttonSelectPathDir.Text = Strings.BtnSelectFolder;

            // El boton de elegir carpeta estaba una fila mas abajo, apoyado sobre el
            // texto de ayuda. Ya en el diseño original se pisaban; con el texto en
            // español, que es mas largo, el solapamiento se nota. Las dos maneras de
            // elegir el origen van juntas en la fila del campo, que es justamente lo
            // que anuncia su etiqueta: "archivo o carpeta".
            buttonSelectPathDir.Top = buttonSelectPathFile.Top;

            // Los textos en espanol son mas largos que los originales y los
            // formularios tienen medidas fijas: se ensancha lo que no entra.
            AutoAjuste.Ajustar(buttonDone, label1, label2, buttonSelectPathFile, buttonSelectPathDir);

            // Recien aca se conoce el ancho definitivo de la ventana. Los botones se
            // apoyan en el borde derecho y el campo se estira hasta donde empiezan.
            Rejilla.FilaPegadaALaDerecha(this, textBoxPathImage, buttonSelectPathImage);
            Rejilla.FilaPegadaALaDerecha(this, textBoxPathFile, buttonSelectPathDir, buttonSelectPathFile);

            // El texto de ayuda y el boton de terminar cruzan la ventana de lado a lado.
            label3.Width = ClientSize.Width - label3.Left * 2;
            buttonDone.Width = ClientSize.Width - buttonDone.Left * 2;

            // label3 ocupa todo el ancho y se superpone con estos botones desde
            // upstream: sin esto quedan tapados por su fondo.
            buttonSelectPathFile.BringToFront();
            buttonSelectPathDir.BringToFront();
        }
    }
}
