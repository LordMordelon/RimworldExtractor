using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.WindowsAPICodePack.Dialogs;
using RimworldExtractorInternal;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;

namespace RimworldExtractorGUI
{
    public partial class FormSettings : Form
    {
        // La fila de la carpeta de RML se arma en codigo, como todo lo que agrega el fork,
        // para no tocar el .Designer.cs.
        private readonly Label _labelPathRml = new() { Name = "labelPathRml", TextAlign = ContentAlignment.MiddleLeft };
        private readonly TextBox _textBoxPathRml = new() { Name = "textBoxPathRml" };
        private readonly Button _buttonPathRml = new() { Name = "buttonPathRml" };

        public FormSettings()
        {
            InitializeComponent();
            Icon = Logo.Icono;
            ApplyStrings();
            if (File.Exists(Prefabs.RutaPorDefecto))
            {
                Prefabs.Load();
            }
            else
            {
                Prefabs.Save();
            }

            comboBoxOriginalLanguage.Items.AddRange(new object[]
            {
                "English", "Korean (한국어)", "Catalan (Català)", "ChineseSimplified (简体中文)", "ChineseTraditional (繁體中文)",
                "Czech (Čeština)", "Danish (Dansk)", "Dutch (Nederlands)", "Estonian (Eesti)",
                "Finnish (Suomi)", "French (Français)", "German (Deutsch)", "Greek (Ελληνικά)",
                "Hungarian (Magyar)", "Italian (Italiano)", "Japanese (日本語)", "Norwegian (Norsk Bokmål)",
                "Polish (Polski)", "Portuguese (Português)", "PortugueseBrazilian (Português Brasileiro)",
                "Romanian (Română)", "Russian (Русский)", "Slovak (Slovenčina)", "Spanish (Español(Castellano))",
                "SpanishLatin (Español(Latinoamérica))", "Swedish (Svenska)", "Turkish (Türkçe)",
                "Ukrainian (Українська)"
            });
            comboBoxTranslationLanguage.Items.AddRange(new object[]
            {
                "English", "Korean (한국어)", "Catalan (Català)", "ChineseSimplified (简体中文)", "ChineseTraditional (繁體中文)",
                "Czech (Čeština)", "Danish (Dansk)", "Dutch (Nederlands)", "Estonian (Eesti)",
                "Finnish (Suomi)", "French (Français)", "German (Deutsch)", "Greek (Ελληνικά)",
                "Hungarian (Magyar)", "Italian (Italiano)", "Japanese (日本語)", "Norwegian (Norsk Bokmål)",
                "Polish (Polski)", "Portuguese (Português)", "PortugueseBrazilian (Português Brasileiro)",
                "Romanian (Română)", "Russian (Русский)", "Slovak (Slovenčina)", "Spanish (Español(Castellano))",
                "SpanishLatin (Español(Latinoamérica))", "Swedish (Svenska)", "Turkish (Türkçe)",
                "Ukrainian (Українська)"
            });
            comboBoxFileDuplication.Items.AddRange(new object[]
            {
                Strings.DuplicatePolicyAsk, Strings.DuplicatePolicyOverwrite, Strings.DuplicatePolicySkip
            });

            FromPrefabs();
        }

        public void FromPrefabs()
        {
            checkBox1.Checked = Prefabs.EnableTkey; // TODO: REMOVE THIS AFTER

            textBoxPathRimworld.Text = Prefabs.PathRimworld;
            textBoxPathWorkshop.Text = Prefabs.PathWorkshop;
            textBoxVersionPattern.Text = Prefabs.PatternVersion;
            textBoxRimworldVersion.Text = Prefabs.CurrentVersion;

            comboBoxOriginalLanguage.SelectedItem = Prefabs.OriginalLanguage;
            comboBoxTranslationLanguage.SelectedItem = Prefabs.TranslationLanguage;
            comboBoxFileDuplication.SelectedIndex = (int)Prefabs.Policy;
            textBoxBaseRefList.Text = Prefabs.PathBaseRefList;
            _textBoxPathRml.Text = Prefabs.PathRml;

            textBoxExtractableTags.Text = string.Join('/', Prefabs.ExtractableTags);
            textBoxTranslationHandles.Text = string.Join('/', Prefabs.TranslationHandles);
            textBoxNodeReplacement.Text = string.Join("/", Prefabs.NodeReplacement.Select(x => $"{x.Key}|{x.Value}"));
            textBoxFullListTranslation.Text = string.Join('/', Prefabs.FullListTranslationTags);
        }

        public void ToPrefabs()
        {
            Prefabs.EnableTkey = checkBox1.Checked; // TODO: REMOVE THIS AFTER

            Prefabs.PathRimworld = textBoxPathRimworld.Text;
            Prefabs.PathWorkshop = textBoxPathWorkshop.Text;
            Prefabs.PatternVersion = textBoxVersionPattern.Text;
            Prefabs.CurrentVersion = textBoxRimworldVersion.Text;

            // Sin nada elegido el combo devuelve null, y guardar null dejaria el idioma
            // vacio en Prefabs.dat: se conserva el que ya estaba.
            Prefabs.OriginalLanguage = comboBoxOriginalLanguage.SelectedItem as string ?? Prefabs.OriginalLanguage;
            Prefabs.TranslationLanguage = comboBoxTranslationLanguage.SelectedItem as string ?? Prefabs.TranslationLanguage;
            Prefabs.Policy = Enum.GetValues<Prefabs.DuplicatesPolicy>()[comboBoxFileDuplication.SelectedIndex];
            Prefabs.PathBaseRefList = textBoxBaseRefList.Text;
            Prefabs.PathRml = _textBoxPathRml.Text;

            Prefabs.ExtractableTags = new HashSet<string>(RemoveSep(textBoxExtractableTags.Text).Split('/'));
            Prefabs.TranslationHandles = new List<string>(RemoveSep(textBoxTranslationHandles.Text).Split('/'));
            Prefabs.NodeReplacement = new Dictionary<string, string>(RemoveSep(textBoxNodeReplacement.Text).Split("/").Select(x =>
            {
                var token = x.Split('|');
                return new KeyValuePair<string, string>(token[0], token[1]);
            }));
            Prefabs.FullListTranslationTags = new HashSet<string>(RemoveSep(textBoxFullListTranslation.Text).Split('/'));
        }

        private void buttonSaveAndClose_Click(object sender, EventArgs e)
        {
            ToPrefabs();
            Prefabs.Save();
            Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            Prefabs.Init();
            Prefabs.Save();
            FromPrefabs();
        }

        private void buttonAutoDetect_Click(object sender, EventArgs e)
        {
            var version = Prefabs.AutoDetectRimworldVersion();
            textBoxRimworldVersion.Text = version;
        }

        private void buttonHelp1_Click(object sender, EventArgs e)
        {
            Aviso.Mostrar(Strings.HelpExtractableTags);
        }

        private void buttonHelp2_Click(object sender, EventArgs e)
        {
            Aviso.Mostrar(Strings.HelpTranslationHandle);
        }

        private void buttonHelp3_Click(object sender, EventArgs e)
        {
            Aviso.Mostrar(Strings.HelpNodeReplacement);
        }

        private void buttonHelp4_Click(object sender, EventArgs e)
        {
            Aviso.Mostrar(Strings.HelpFullListTranslation);
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

        private void buttonBaseRefList_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.Title = Strings.LoadRefModsFromFile;
            openFileDialog.InitialDirectory = Assembly.GetExecutingAssembly().Location;
            openFileDialog.Filter = Strings.FilterRefModsList;
            openFileDialog.DefaultExt = "refMods";
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                textBoxBaseRefList.Text = openFileDialog.FileName;
            }
        }


        private static string RemoveSep(string s) => s.Replace(" ", "").Replace("\r", "").Replace("\n", "");
    
        /// <summary>
        /// Traduce los controles en tiempo de ejecucion, para no tocar el .Designer.cs
        /// y mantener limpios los merges con upstream.
        /// </summary>
        private void ApplyStrings()
        {
            // La columna izquierda queda fija y la derecha ocupa lo que sobra. No pueden
            // crecer las dos: los anclajes no reparten espacio entre controles vecinos.
            groupBox3.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            buttonSaveAndClose.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonReset.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            // Los cuatro cuadros de etiquetas forman una rejilla de dos por dos, que los
            // anclajes no saben repartir: se acomoda por codigo al cambiar el tamano.
            Rejilla.EnDosColumnas(groupBox3,
                new Rejilla.Fila(
                    new Rejilla.Celda(label9, textBoxExtractableTags, buttonHelp1),
                    new Rejilla.Celda(label10, textBoxTranslationHandles, buttonHelp2)),
                new Rejilla.Fila(
                    new Rejilla.Celda(label12, textBoxNodeReplacement, buttonHelp3),
                    new Rejilla.Celda(label13, textBoxFullListTranslation, buttonHelp4),
                    checkBox1));

            // La carpeta de RML va con las otras dos rutas: es una ruta mas, y desde que
            // escribir en RML es el modo normal de extraccion dejo de ser el ajuste de un
            // modo alterno. Se mide contra la fila de la ruta de RimWorld, que es la misma
            // forma —rotulo, campo y boton de examinar— y ocupa el ancho entero.
            _labelPathRml.Text = Strings.LabelPathRml;
            _buttonPathRml.Text = buttonSelectPathRimworld.Text;
            _labelPathRml.SetBounds(label1.Left, textBoxVersionPattern.Bottom + 6,
                label1.Width, label1.Height);
            _textBoxPathRml.SetBounds(textBoxPathRimworld.Left, _labelPathRml.Bottom + 2,
                textBoxPathRimworld.Width, textBoxPathRimworld.Height);
            _buttonPathRml.SetBounds(buttonSelectPathRimworld.Left, _textBoxPathRml.Top,
                buttonSelectPathRimworld.Width, buttonSelectPathRimworld.Height);
            _buttonPathRml.Click += (_, _) =>
            {
                var dialog = new CommonOpenFileDialog { IsFolderPicker = true, Title = Strings.SelectRmlPath };
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                    _textBoxPathRml.Text = dialog.FileName;
            };
            groupBox1.Controls.Add(_labelPathRml);
            groupBox1.Controls.Add(_textBoxPathRml);
            groupBox1.Controls.Add(_buttonPathRml);

            Text = Strings.TitleSettings;
            label1.Text = Strings.LabelRimworldPath;
            label2.Text = Strings.LabelWorkshopPath;
            label3.Text = Strings.LabelVersionPattern;
            label4.Text = Strings.LabelSettingsTip;
            label5.Text = Strings.LabelRimworldVersion;
            buttonAutoDetect.Text = Strings.BtnAutoDetect;
            label6.Text = Strings.LabelOriginalLanguage;
            label7.Text = Strings.LabelTranslationLanguage;
            buttonSaveAndClose.Text = Strings.BtnSaveAndClose;
            buttonCancel.Text = Strings.BtnCancel;
            buttonReset.Text = Strings.BtnReset;
            label9.Text = Strings.LabelExtractableTags;
            label10.Text = Strings.LabelTranslationHandleTags;
            label11.Text = Strings.LabelDuplicatePolicy;
            label12.Text = Strings.LabelNodeReplacement;
            label13.Text = Strings.LabelFullListTags;
            label14.Text = Strings.LabelBaseRefListPath;
            groupBox1.Text = Strings.GroupRimworldSettings;
            groupBox2.Text = Strings.GroupBasicSettings;
            groupBox3.Text = Strings.GroupAdvancedSettings;
            checkBox1.Text = Strings.CheckBoxTkey;

            // Los textos en espanol son mas largos que los originales y los
            // formularios tienen medidas fijas: se ensancha lo que no entra.
            AutoAjuste.Ajustar(label1, label2, label3, label5, label6, label7, label9, label10, label11, label12, label13, label14,
                buttonAutoDetect, buttonSaveAndClose, buttonCancel, buttonReset, checkBox1);

            // Los tres botones del pie estan uno al lado del otro: al ensancharse
            // para que entre su texto se pisaban entre si.
            Rejilla.FilaPegadaALaDerecha(this, buttonSaveAndClose, buttonReset, buttonCancel);

            // La columna izquierda se parte en dos secciones que se reparten el alto.
            Rejilla.EnDosFilas(this, groupBox1, groupBox2);

            // La fila de RML no entra en el alto original de su seccion —probado sacando
            // esto: el rotulo queda cortado por el borde del grupo y el campo no se ve—,
            // asi que la ventana crece. Va despues de repartir y no antes: EnDosFilas se
            // queda con los margenes que encuentra al llamarla, asi que crecer primero solo
            // agranda el margen de abajo y la seccion se queda igual. Y va el doble de lo
            // que hace falta, porque el alto se parte en dos mitades.
            var altoDeLaFila = _textBoxPathRml.Bottom - textBoxVersionPattern.Bottom + 6;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + altoDeLaFila * 2);
            MinimumSize = Size;

            // El contenido de cada seccion se estira para cubrir su ancho.
            Rejilla.EstirarAlAncho(groupBox1,
                Rejilla.Linea(label1),
                Rejilla.Linea(new Rejilla.Campo(textBoxPathRimworld, buttonSelectPathRimworld)),
                Rejilla.Linea(label2),
                Rejilla.Linea(new Rejilla.Campo(textBoxPathWorkshop, buttonSelectPathWorkshop)),
                Rejilla.Linea(label3, label5),
                Rejilla.Linea(textBoxVersionPattern, new Rejilla.Campo(textBoxRimworldVersion, buttonAutoDetect)),
                Rejilla.Linea(_labelPathRml),
                Rejilla.Linea(new Rejilla.Campo(_textBoxPathRml, _buttonPathRml)));

            Rejilla.EstirarAlAncho(groupBox2,
                Rejilla.Linea(label6, label7),
                Rejilla.Linea(comboBoxOriginalLanguage, comboBoxTranslationLanguage),
                Rejilla.Linea(label11),
                Rejilla.Linea(comboBoxFileDuplication),
                Rejilla.Linea(label14),
                Rejilla.Linea(new Rejilla.Campo(textBoxBaseRefList, buttonBaseRefList)));
        }
    }
}
