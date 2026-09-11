using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RimworldExtractorInternal;

namespace RimworldExtractorGUI
{
    /// <summary>
    /// Los cuadros de mensaje de la aplicacion.
    ///
    /// Reemplazan a MessageBox, que es un dialogo del propio Windows y no obedece al tema
    /// que elige la aplicacion: con la interfaz en oscuro seguia saliendo en blanco. Al
    /// ser una ventana normal de WinForms, esta si se pinta con el tema puesto.
    ///
    /// La ventana se arma en codigo y se mide sola, asi que no hay medidas fijas que
    /// queden cortas cuando el texto en español es mas largo que el original.
    /// </summary>
    internal static class Aviso
    {
        private const int Margen = 16;

        /// <summary>Ancho al que se parte el texto en varios renglones.</summary>
        private const int AnchoMaximo = 520;

        /// <summary>Ancho minimo de un boton, para que no quede un cuadrito con "No".</summary>
        private const int AnchoMinimoBoton = 96;

        /// <summary>Un mensaje que solo se acepta.</summary>
        internal static void Mostrar(string mensaje) => Mostrar(mensaje, Strings.DialogTitleNotice);

        internal static void Mostrar(string mensaje, string titulo)
            => Armar(mensaje, titulo, (Strings.BtnAccept, DialogResult.OK));

        /// <summary>Una pregunta de si o no. Devuelve DialogResult.Yes si contesto que si.</summary>
        internal static DialogResult Preguntar(string mensaje, string titulo)
            => Armar(mensaje, titulo,
                (Strings.BtnYes, DialogResult.Yes),
                (Strings.BtnNo, DialogResult.No));

        private static DialogResult Armar(string mensaje, string titulo,
            params (string Texto, DialogResult Resultado)[] opciones)
        {
            using var ventana = new Form
            {
                Text = titulo,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                AutoScaleMode = AutoScaleMode.Font,
                Icon = Logo.Icono
            };

            var texto = new Label
            {
                Text = mensaje,
                AutoSize = true,
                MaximumSize = new Size(AnchoMaximo, 0),
                Location = new Point(Margen, Margen)
            };
            ventana.Controls.Add(texto);

            var botones = opciones.Select(opcion => new Button
            {
                Text = opcion.Texto,
                DialogResult = opcion.Resultado,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(AnchoMinimoBoton, 0)
            }).ToArray();

            foreach (var boton in botones)
                ventana.Controls.Add(boton);

            // Enter acepta y Escape cancela. Con un solo boton los dos hacen lo mismo,
            // que es lo que uno espera de un aviso.
            ventana.AcceptButton = botones[0];
            ventana.CancelButton = botones[^1];

            var altoBotones = botones.Max(b => b.Height);
            var anchoBotones = botones.Sum(b => b.Width) + Margen * (botones.Length - 1);

            ventana.ClientSize = new Size(
                Math.Max(texto.Width, anchoBotones) + Margen * 2,
                texto.Height + altoBotones + Margen * 3);

            // De derecha a izquierda, para que el primero de la lista quede a la izquierda
            // sin tener que sumar anchos por adelantado.
            var derecha = ventana.ClientSize.Width - Margen;
            foreach (var boton in botones.Reverse())
            {
                boton.Location = new Point(derecha - boton.Width, texto.Bottom + Margen);
                derecha = boton.Left - Margen;
            }

            // Sin ventana activa —el aviso de la seleccion inicial de rutas sale antes de
            // que haya uno— centrar en el padre dejaria el cuadro en un rincon.
            var padre = Form.ActiveForm;
            ventana.StartPosition = padre is null
                ? FormStartPosition.CenterScreen
                : FormStartPosition.CenterParent;

            return ventana.ShowDialog(padre);
        }
    }
}
