using System.Diagnostics;
using RimworldExtractorInternal;

namespace RimworldExtractorGUI
{
    /// <summary>
    /// La pregunta de si actualizar, con lo que trae la version nueva.
    ///
    /// Antes iba por <see cref="Aviso"/> con el cuerpo de la release pegado abajo: se veia el
    /// Markdown crudo —los ###, los ** y los enlaces enteros— y las instrucciones para elegir
    /// descarga, que ahi sobran porque la aplicacion se actualiza sola. Ahora se muestra solo la
    /// lista de cambios (<see cref="Actualizador.CambiosDe"/>), en un recuadro con su propia
    /// barra, para que una lista larga no estire la ventana hasta salirse de la pantalla.
    ///
    /// Como <see cref="Aviso"/>, se arma en codigo y toma los colores del tema: nada cableado.
    /// </summary>
    internal static class AvisoDeActualizacion
    {
        private const int Margen = 16;

        /// <summary>Ancho del texto, el mismo al que <see cref="Aviso"/> parte sus renglones.</summary>
        private const int Ancho = 520;

        /// <summary>Hasta cuantos renglones crece la lista antes de usar su barra.</summary>
        private const int RenglonesVisibles = 10;

        private const int AnchoMinimoBoton = 96;

        /// <summary>Devuelve true si eligio actualizar.</summary>
        internal static bool Preguntar(string actual, string ultima, IReadOnlyList<string> cambios)
        {
            using var ventana = Armar(actual, ultima, cambios);

            var padre = Form.ActiveForm;
            ventana.StartPosition = padre is null
                ? FormStartPosition.CenterScreen
                : FormStartPosition.CenterParent;

            return ventana.ShowDialog(padre) == DialogResult.OK;
        }

        /// <summary>La ventana sin mostrar. Aparte para poder renderizarla y revisarla.</summary>
        internal static Form Armar(string actual, string ultima, IReadOnlyList<string> cambios)
        {
            var ventana = new Form
            {
                Text = Strings.DialogTitleUpdate,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                AutoScaleMode = AutoScaleMode.Font,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Icon = Logo.Icono
            };

            var columna = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(Margen - 3)
            };
            ventana.Controls.Add(columna);

            var normal = ventana.Font;
            var titulo = new Font(normal.FontFamily, normal.SizeInPoints * 1.5f, FontStyle.Bold, GraphicsUnit.Point);
            var destacada = new Font(normal, FontStyle.Bold);

            columna.Controls.Add(Texto(Strings.UpdateNewVersion(ultima), titulo, SystemColors.ControlText, 0));
            columna.Controls.Add(Texto(Strings.UpdateYouHave(actual), normal, SystemColors.GrayText, 2));

            if (cambios.Count > 0)
            {
                columna.Controls.Add(Texto(Strings.UpdateWhatChanged, destacada, SystemColors.ControlText, Margen));
                columna.Controls.Add(Lista(cambios, normal));
            }

            var enlace = new LinkLabel
            {
                Text = Strings.UpdateSeeOnGithub,
                AutoSize = true,
                Margin = new Padding(3, 6, 3, 3),
                // El azul por defecto de LinkLabel es fijo y en el tema oscuro casi no se lee.
                // HotTrack es el color de enlace del sistema, y cambia con el tema.
                LinkColor = SystemColors.HotTrack,
                ActiveLinkColor = SystemColors.HotTrack,
                VisitedLinkColor = SystemColors.HotTrack
            };
            enlace.LinkClicked += (_, _) =>
                Process.Start("explorer.exe", $"{GithubVersionCheker.ReleasesUrl}/tag/{ultima}");
            columna.Controls.Add(enlace);

            columna.Controls.Add(Texto(Strings.UpdateAskRestart, normal, SystemColors.GrayText, Margen));

            var actualizar = Boton(Strings.BtnUpdate, DialogResult.OK);
            var ahoraNo = Boton(Strings.BtnNotNow, DialogResult.Cancel);

            // De derecha a izquierda: el que se agrega primero queda en el borde, y la accion
            // principal, a su izquierda, como en los cuadros de Windows.
            var botones = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Anchor = AnchorStyles.Right,
                Margin = new Padding(0, Margen, 0, 0)
            };
            botones.Controls.Add(ahoraNo);
            botones.Controls.Add(actualizar);
            columna.Controls.Add(botones);

            ventana.AcceptButton = actualizar;
            ventana.CancelButton = ahoraNo;
            // El foco en el boton y no en la lista: si no, el recuadro arranca con el texto
            // seleccionado entero.
            ventana.ActiveControl = actualizar;

            ventana.Disposed += (_, _) =>
            {
                titulo.Dispose();
                destacada.Dispose();
            };
            return ventana;
        }

        private static Label Texto(string texto, Font fuente, Color color, int arriba) => new()
        {
            Text = texto,
            Font = fuente,
            ForeColor = color,
            AutoSize = true,
            MaximumSize = new Size(Ancho, 0),
            Margin = new Padding(3, arriba, 3, 3)
        };

        /// <summary>
        /// Los cambios, uno por parrafo y con viñeta: no son una secuencia, asi que sin numerar.
        /// Un RichTextBox de solo lectura y no un Label, porque trae barra propia, deja copiar el
        /// texto y sabe hacer viñetas con sangria colgante: con un TextBox el segundo renglon de
        /// un cambio largo arrancaba debajo de la viñeta y no se veia donde empezaba el otro.
        /// </summary>
        private static RichTextBox Lista(IReadOnlyList<string> cambios, Font fuente)
        {
            var lista = new RichTextBox
            {
                Font = fuente,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                // Solo lectura lo pinta con el color de los controles, igual que el fondo de la
                // ventana; con el de una caja de texto se lee como un recuadro aparte.
                BackColor = SystemColors.Window,
                ForeColor = SystemColors.WindowText,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                DetectUrls = false,
                Width = Ancho,
                Margin = new Padding(3, 4, 3, 3)
            };

            lista.Text = string.Join("\n", cambios);
            lista.SelectAll();
            lista.SelectionBullet = true;
            lista.BulletIndent = fuente.Height;
            lista.SelectionColor = SystemColors.WindowText;
            lista.Select(0, 0);

            // Alto en renglones enteros, para que el borde no corte uno por la mitad. Se miden con
            // el ancho que queda descontando la viñeta y la barra.
            var util = Ancho - fuente.Height - SystemInformation.VerticalScrollBarWidth - 12;
            var renglones = cambios.Sum(x => TextRenderer.MeasureText(x, fuente, new Size(util, 0),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height / fuente.Height);
            var visibles = Math.Min(Math.Max(renglones, 1), RenglonesVisibles);
            lista.Height = visibles * fuente.Height + 8;
            return lista;
        }

        private static Button Boton(string texto, DialogResult resultado) => new()
        {
            Text = texto,
            DialogResult = resultado,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(AnchoMinimoBoton, 0),
            Margin = new Padding(Margen / 2, 0, 0, 0)
        };
    }
}
