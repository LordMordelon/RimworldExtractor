namespace RimworldExtractorGUI
{
    partial class FormInitialPathSelect
    {
        private System.ComponentModel.IContainer components = null;

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
        /// La maquetacion la resuelve la tabla, no hay medidas calculadas a mano.
        ///
        /// Las tres columnas son todo: la primera se ancha sola hasta el rotulo mas largo y
        /// los alinea, la del medio se queda con el resto y estira los campos, la tercera se
        /// ajusta al boton. Antes eso lo hacia AutoAjuste midiendo cada texto en tiempo de
        /// ejecucion, porque no se podia tocar este archivo.
        ///
        /// El formulario crece solo hasta lo que la tabla necesita (AutoSize con GrowOnly),
        /// que es lo que reemplaza al ajuste final de ancho y al MinimumSize que ponia
        /// AutoAjuste. Hace falta porque los textos se asignan en ApplyStrings y en espanol
        /// ocupan mas que los coreanos originales.
        /// </summary>
        private void InitializeComponent()
        {
            tabla = new TableLayoutPanel();
            textBoxPathRimworld = new TextBox();
            textBoxPathWorkshop = new TextBox();
            buttonSelectPathRimworld = new Button();
            buttonSelectPathWorkshop = new Button();
            label1 = new Label();
            label2 = new Label();
            buttonDone = new Button();
            tabla.SuspendLayout();
            SuspendLayout();
            //
            // tabla
            //
            tabla.ColumnCount = 3;
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tabla.RowCount = 3;
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            tabla.Dock = DockStyle.Fill;
            tabla.AutoSize = true;
            tabla.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            tabla.Padding = new Padding(12);
            tabla.Name = "tabla";
            tabla.Controls.Add(label1, 0, 0);
            tabla.Controls.Add(textBoxPathRimworld, 1, 0);
            tabla.Controls.Add(buttonSelectPathRimworld, 2, 0);
            tabla.Controls.Add(label2, 0, 1);
            tabla.Controls.Add(textBoxPathWorkshop, 1, 1);
            tabla.Controls.Add(buttonSelectPathWorkshop, 2, 1);
            tabla.Controls.Add(buttonDone, 0, 2);
            tabla.SetColumnSpan(buttonDone, 3);
            //
            // label1
            //
            label1.AutoSize = true;
            label1.Anchor = AnchorStyles.Left;
            label1.Margin = new Padding(3, 3, 9, 3);
            label1.Name = "label1";
            label1.TabIndex = 4;
            //
            // textBoxPathRimworld
            //
            textBoxPathRimworld.Dock = DockStyle.Fill;
            textBoxPathRimworld.Margin = new Padding(3, 6, 3, 6);
            textBoxPathRimworld.Name = "textBoxPathRimworld";
            textBoxPathRimworld.TabIndex = 0;
            //
            // buttonSelectPathRimworld
            //
            buttonSelectPathRimworld.Anchor = AnchorStyles.Left;
            buttonSelectPathRimworld.Size = new Size(75, 25);
            buttonSelectPathRimworld.Margin = new Padding(6, 3, 3, 3);
            buttonSelectPathRimworld.Name = "buttonSelectPathRimworld";
            buttonSelectPathRimworld.TabIndex = 2;
            buttonSelectPathRimworld.Text = "...";
            buttonSelectPathRimworld.UseVisualStyleBackColor = true;
            buttonSelectPathRimworld.Click += buttonSelectPathRimworld_Click;
            //
            // label2
            //
            label2.AutoSize = true;
            label2.Anchor = AnchorStyles.Left;
            label2.Margin = new Padding(3, 3, 9, 3);
            label2.Name = "label2";
            label2.TabIndex = 5;
            //
            // textBoxPathWorkshop
            //
            textBoxPathWorkshop.Dock = DockStyle.Fill;
            textBoxPathWorkshop.Margin = new Padding(3, 6, 3, 6);
            textBoxPathWorkshop.Name = "textBoxPathWorkshop";
            textBoxPathWorkshop.TabIndex = 1;
            //
            // buttonSelectPathWorkshop
            //
            buttonSelectPathWorkshop.Anchor = AnchorStyles.Left;
            buttonSelectPathWorkshop.Size = new Size(75, 25);
            buttonSelectPathWorkshop.Margin = new Padding(6, 3, 3, 3);
            buttonSelectPathWorkshop.Name = "buttonSelectPathWorkshop";
            buttonSelectPathWorkshop.TabIndex = 3;
            buttonSelectPathWorkshop.Text = "...";
            buttonSelectPathWorkshop.UseVisualStyleBackColor = true;
            buttonSelectPathWorkshop.Click += buttonSelectPathWorkshop_Click;
            //
            // buttonDone
            //
            buttonDone.SetBounds(0, 0, 0, 30);
            buttonDone.Dock = DockStyle.Fill;
            buttonDone.Margin = new Padding(3, 12, 3, 3);
            buttonDone.Name = "buttonDone";
            buttonDone.TabIndex = 6;
            buttonDone.UseVisualStyleBackColor = true;
            buttonDone.Click += buttonDone_Click;
            //
            // FormInitialPathSelect
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowOnly;
            ClientSize = new Size(587, 110);
            ControlBox = false;
            Controls.Add(tabla);
            Name = "FormInitialPathSelect";
            tabla.ResumeLayout(false);
            tabla.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private TableLayoutPanel tabla;
        private TextBox textBoxPathRimworld;
        private TextBox textBoxPathWorkshop;
        private Button buttonSelectPathRimworld;
        private Button buttonSelectPathWorkshop;
        private Label label1;
        private Label label2;
        private Button buttonDone;
    }
}
