namespace RimworldExtractorGUI
{
    partial class FormMain
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            buttonSelectMod = new Button();
            button2 = new Button();
            label1 = new Label();
            label2 = new Label();
            richTextBoxLog = new RichTextBox();
            labelSelectedMods = new Label();
            button1 = new Button();
            linkLabelLatestVersion = new LinkLabel();
            buttonUpdateAllRml = new Button();
            buttonAbrirLog = new Button();
            tablaLog = new TableLayoutPanel();
            labelProgreso = new Label();
            marcoProgreso = new Panel();
            progressBar = new ProgressBar();
            labelAvisos = new Label();
            richTextBoxAvisos = new RichTextBox();
            tablaLog.SuspendLayout();
            SuspendLayout();
            // 
            // buttonSelectMod
            // 
            buttonSelectMod.Location = new Point(12, 12);
            buttonSelectMod.Name = "buttonSelectMod";
            buttonSelectMod.Size = new Size(200, 46);
            buttonSelectMod.TabIndex = 0;
            buttonSelectMod.Text = "1. 추출할 모드 선택";
            buttonSelectMod.UseVisualStyleBackColor = true;
            buttonSelectMod.Click += buttonSelectMod_Click;
            // 
            // button2
            // 
            button2.Location = new Point(12, 116);
            button2.Name = "button2";
            button2.Size = new Size(200, 46);
            button2.TabIndex = 2;
            button2.Text = "옵션";
            button2.UseVisualStyleBackColor = true;
            button2.Click += button2_Click;
            // 
            // label1
            // 
            label1.Location = new Point(218, 9);
            label1.Name = "label1";
            label1.Size = new Size(570, 23);
            label1.TabIndex = 5;
            label1.Text = "림월드의 공식 콘텐츠나 모드의 번역 데이터를 추출하는 프로그램입니다.\r\n";
            label1.TextAlign = ContentAlignment.TopCenter;
            // 
            // label2
            // 
            label2.Location = new Point(12, 219);
            label2.Name = "label2";
            label2.Size = new Size(776, 23);
            label2.TabIndex = 7;
            label2.Text = "Log";
            label2.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // richTextBoxLog
            // 
            richTextBoxLog.BackColor = SystemColors.ControlText;
            richTextBoxLog.Dock = DockStyle.Fill;
            richTextBoxLog.Margin = new Padding(0, 0, 0, 6);
            richTextBoxLog.Name = "richTextBoxLog";
            richTextBoxLog.ReadOnly = true;
            richTextBoxLog.ScrollBars = RichTextBoxScrollBars.ForcedVertical;
            richTextBoxLog.TabIndex = 8;
            richTextBoxLog.Text = "";
            // 
            // labelSelectedMods
            // 
            // Su alto es el que fija donde puede empezar la franja del log: llega hasta
            // y=214 y el boton de reportar problemas vive en esa franja, del lado derecho.
            // Subirla mas lo deja tapado por este rotulo, que va delante en el orden Z y no
            // se nota hasta que alguien busca el boton.
            labelSelectedMods.Location = new Point(218, 32);
            labelSelectedMods.Name = "labelSelectedMods";
            labelSelectedMods.Size = new Size(570, 182);
            labelSelectedMods.TabIndex = 9;
            labelSelectedMods.Text = resources.GetString("labelSelectedMods.Text");
            labelSelectedMods.TextAlign = ContentAlignment.TopCenter;
            // 
            // button1
            // 
            button1.Location = new Point(650, 216);
            button1.Name = "button1";
            button1.Size = new Size(138, 23);
            button1.TabIndex = 10;
            button1.Text = "경고나 에러 발생 시";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // linkLabelLatestVersion
            // 
            linkLabelLatestVersion.Location = new Point(12, 217);
            linkLabelLatestVersion.Name = "linkLabelLatestVersion";
            linkLabelLatestVersion.Size = new Size(200, 23);
            linkLabelLatestVersion.TabIndex = 11;
            linkLabelLatestVersion.TextAlign = ContentAlignment.MiddleLeft;
            linkLabelLatestVersion.LinkClicked += linkLabelLatestVersion_LinkClicked;
            // 
            // buttonUpdateAllRml
            // 
            buttonUpdateAllRml.Location = new Point(12, 64);
            buttonUpdateAllRml.Name = "buttonUpdateAllRml";
            buttonUpdateAllRml.Size = new Size(97, 46);
            buttonUpdateAllRml.TabIndex = 12;
            buttonUpdateAllRml.Text = "Actualizar todo RML";
            buttonUpdateAllRml.UseVisualStyleBackColor = true;
            buttonUpdateAllRml.Click += buttonUpdateAllRml_Click;
            // 
            // buttonAbrirLog
            // 
            // Su posicion final la calcula ApplyStrings, pegado a la izquierda de button1.
            buttonAbrirLog.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonAbrirLog.Location = new Point(506, 216);
            buttonAbrirLog.Name = "buttonAbrirLog";
            buttonAbrirLog.Size = new Size(138, 23);
            buttonAbrirLog.TabIndex = 13;
            buttonAbrirLog.UseVisualStyleBackColor = true;
            buttonAbrirLog.Click += buttonAbrirLog_Click;
            // 
            // tablaLog
            // 
            // Avance, log y avisos, uno debajo de otro. Los avisos van en filas AutoSize: con
            // sus controles ocultos miden cero y el log se queda con todo el alto.
            tablaLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tablaLog.ColumnCount = 1;
            tablaLog.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tablaLog.Controls.Add(labelProgreso, 0, 0);
            tablaLog.Controls.Add(marcoProgreso, 0, 1);
            tablaLog.Controls.Add(richTextBoxLog, 0, 2);
            tablaLog.Controls.Add(labelAvisos, 0, 3);
            tablaLog.Controls.Add(richTextBoxAvisos, 0, 4);
            tablaLog.Location = new Point(12, 245);
            tablaLog.Name = "tablaLog";
            tablaLog.RowCount = 5;
            tablaLog.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tablaLog.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tablaLog.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            tablaLog.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tablaLog.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tablaLog.Size = new Size(776, 348);
            tablaLog.TabIndex = 14;
            // 
            // labelProgreso
            // 
            labelProgreso.AutoSize = true;
            labelProgreso.Margin = new Padding(0, 0, 0, 4);
            labelProgreso.Name = "labelProgreso";
            labelProgreso.TabIndex = 0;
            // 
            // marcoProgreso
            // 
            // En oscuro la barra no dibuja el tramo vacio. ApplyStrings le da a este panel un
            // borde de un pixel, para que se vea cuanto falta.
            marcoProgreso.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            marcoProgreso.Controls.Add(progressBar);
            marcoProgreso.Margin = new Padding(0, 0, 0, 8);
            marcoProgreso.Name = "marcoProgreso";
            marcoProgreso.Size = new Size(776, 18);
            marcoProgreso.TabIndex = 1;
            // 
            // progressBar
            // 
            progressBar.Dock = DockStyle.Fill;
            progressBar.Name = "progressBar";
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.TabIndex = 1;
            // 
            // labelAvisos
            // 
            labelAvisos.AutoSize = true;
            labelAvisos.Margin = new Padding(0, 0, 0, 4);
            labelAvisos.Name = "labelAvisos";
            labelAvisos.TabIndex = 3;
            labelAvisos.Visible = false;
            // 
            // richTextBoxAvisos
            // 
            richTextBoxAvisos.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            richTextBoxAvisos.Margin = new Padding(0);
            richTextBoxAvisos.Name = "richTextBoxAvisos";
            richTextBoxAvisos.DetectUrls = false;
            richTextBoxAvisos.ReadOnly = true;
            richTextBoxAvisos.ScrollBars = RichTextBoxScrollBars.Vertical;
            richTextBoxAvisos.Size = new Size(776, 110);
            richTextBoxAvisos.TabIndex = 4;
            richTextBoxAvisos.Text = "";
            richTextBoxAvisos.Visible = false;
            // 
            // FormMain
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 600);
            Controls.Add(tablaLog);
            Controls.Add(buttonAbrirLog);
            Controls.Add(buttonUpdateAllRml);
            Controls.Add(linkLabelLatestVersion);
            Controls.Add(button1);
            Controls.Add(labelSelectedMods);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(button2);
            Controls.Add(buttonSelectMod);
            Name = "FormMain";
            Text = "Rimworld Extractor GUI (림추출기)";
            tablaLog.ResumeLayout(false);
            tablaLog.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Button buttonSelectMod;
        private Button button2;
        private Label label1;
        private Label label2;
        private RichTextBox richTextBoxLog;
        private Label labelSelectedMods;
        private Button button1;
        private LinkLabel linkLabelLatestVersion;
        private Button buttonUpdateAllRml;
        private Button buttonAbrirLog;
        private TableLayoutPanel tablaLog;
        private Label labelProgreso;
        private Panel marcoProgreso;
        private ProgressBar progressBar;
        private Label labelAvisos;
        private RichTextBox richTextBoxAvisos;
    }
}