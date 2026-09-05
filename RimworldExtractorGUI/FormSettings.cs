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
        public FormSettings()
        {
            InitializeComponent();
            ApplyStrings();
            if (File.Exists("Prefabs.dat"))
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
            comboBoxExtractionMethod.Items.AddRange(new object[]
            {
                Strings.ExtractionMethodExcel,
                Strings.ExtractionMethodXml,
                Strings.ExtractionMethodXmlComments
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
            comboBoxExtractionMethod.SelectedIndex = (int)Prefabs.Method;
            comboBoxFileDuplication.SelectedIndex = (int)Prefabs.Policy;
            textBoxBaseRefList.Text = Prefabs.PathBaseRefList;

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

            Prefabs.OriginalLanguage = (string)comboBoxOriginalLanguage.SelectedItem;
            Prefabs.TranslationLanguage = (string)comboBoxTranslationLanguage.SelectedItem;
            Prefabs.Method = Enum.GetValues<Prefabs.ExtractionMethod>()[comboBoxExtractionMethod.SelectedIndex];
            Prefabs.Policy = Enum.GetValues<Prefabs.DuplicatesPolicy>()[comboBoxFileDuplication.SelectedIndex];
            Prefabs.PathBaseRefList = textBoxBaseRefList.Text;

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
            MessageBox.Show(Strings.HelpExtractableTags);
        }

        private void buttonHelp2_Click(object sender, EventArgs e)
        {
            MessageBox.Show(Strings.HelpTranslationHandle);
        }

        private void buttonHelp3_Click(object sender, EventArgs e)
        {
            MessageBox.Show(Strings.HelpNodeReplacement);
        }

        private void buttonHelp4_Click(object sender, EventArgs e)
        {
            MessageBox.Show(Strings.HelpFullListTranslation);
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
            Text = Strings.TitleSettings;
            label1.Text = Strings.LabelRimworldPath;
            label2.Text = Strings.LabelWorkshopPath;
            label3.Text = Strings.LabelVersionPattern;
            label4.Text = Strings.LabelSettingsTip;
            label5.Text = Strings.LabelRimworldVersion;
            buttonAutoDetect.Text = Strings.BtnAutoDetect;
            label6.Text = Strings.LabelOriginalLanguage;
            label7.Text = Strings.LabelTranslationLanguage;
            label8.Text = Strings.LabelExtractionFormat;
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
        }
    }
}
