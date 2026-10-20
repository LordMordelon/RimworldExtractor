using System.Data;
using Microsoft.WindowsAPICodePack.Dialogs;
using RimworldExtractorInternal;

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
            FromPrefabs();
        }

        public void FromPrefabs()
        {
            textBoxPathRimworld.Text = Prefabs.PathRimworld;
            textBoxPathWorkshop.Text = Prefabs.PathWorkshop;
            textBoxVersionPattern.Text = Prefabs.PatternVersion;
            textBoxRimworldVersion.Text = Prefabs.CurrentVersion;

            comboBoxOriginalLanguage.SelectedItem = Prefabs.OriginalLanguage;
            comboBoxTranslationLanguage.SelectedItem = Prefabs.TranslationLanguage;
            _textBoxPathRml.Text = Prefabs.PathRml;

            textBoxExtractableTags.Text = string.Join('/', Prefabs.ExtractableTags);
            textBoxTranslationHandles.Text = string.Join('/', Prefabs.TranslationHandles);
            textBoxNodeReplacement.Text = string.Join("/", Prefabs.NodeReplacement.Select(x => $"{x.Key}|{x.Value}"));
            textBoxFullListTranslation.Text = string.Join('/', Prefabs.FullListTranslationTags);
        }

        public void ToPrefabs()
        {
            Prefabs.PathRimworld = textBoxPathRimworld.Text;
            Prefabs.PathWorkshop = textBoxPathWorkshop.Text;
            Prefabs.PatternVersion = textBoxVersionPattern.Text;
            Prefabs.CurrentVersion = textBoxRimworldVersion.Text;

            // Sin nada elegido el combo devuelve null, y guardar null dejaria el idioma
            // vacio en Prefabs.dat: se conserva el que ya estaba.
            Prefabs.OriginalLanguage = comboBoxOriginalLanguage.SelectedItem as string ?? Prefabs.OriginalLanguage;
            Prefabs.TranslationLanguage = comboBoxTranslationLanguage.SelectedItem as string ?? Prefabs.TranslationLanguage;
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
                    new Rejilla.Celda(label13, textBoxFullListTranslation, buttonHelp4)));

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
            label12.Text = Strings.LabelNodeReplacement;
            label13.Text = Strings.LabelFullListTags;
            groupBox1.Text = Strings.GroupRimworldSettings;
            groupBox2.Text = Strings.GroupBasicSettings;
            groupBox3.Text = Strings.GroupAdvancedSettings;

            // Los textos en espanol son mas largos que los originales y los
            // formularios tienen medidas fijas: se ensancha lo que no entra.
            AutoAjuste.Ajustar(label1, label2, label3, label5, label6, label7, label9, label10, label12, label13,
                buttonAutoDetect, buttonSaveAndClose, buttonCancel, buttonReset);

            // Los tres botones del pie estan uno al lado del otro: al ensancharse
            // para que entre su texto se pisaban entre si.
            Rejilla.FilaPegadaALaDerecha(this, buttonSaveAndClose, buttonReset, buttonCancel);

            // La columna izquierda lleva cada seccion con el alto de lo que tiene: las rutas
            // arriba, con la fila de RML que no entraba en su alto original, y los idiomas
            // abajo, que quedaron solos. Antes se repartian el alto en dos mitades, pero los
            // idiomas ocupaban la suya con dos opciones que ya no estan y quedaba un hueco.
            // Los margenes se toman del diseño original, antes de mover nada.
            var margenInterno = groupBox1.Height - textBoxVersionPattern.Bottom;
            var separacion = groupBox2.Top - groupBox1.Bottom;
            var margenInferior = ClientSize.Height - groupBox2.Bottom;
            var ancho = Math.Max(groupBox1.Width, groupBox2.Width);
            groupBox1.Size = new Size(ancho, _buttonPathRml.Bottom + margenInterno);
            groupBox2.SetBounds(groupBox2.Left, groupBox1.Bottom + separacion,
                ancho, comboBoxTranslationLanguage.Bottom + margenInterno);

            // La ventana se acomoda a esa columna: la derecha tiene cuadros de texto con
            // barra de desplazamiento, que se estiran o se encogen con ella. AutoAjuste ya
            // fijo un minimo, y con el no se podria achicar.
            MinimumSize = Size.Empty;
            ClientSize = new Size(ClientSize.Width, groupBox2.Bottom + margenInferior);
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
                Rejilla.Linea(comboBoxOriginalLanguage, comboBoxTranslationLanguage));
        }
    }
}
