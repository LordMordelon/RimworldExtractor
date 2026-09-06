using System;
using System.Linq;

namespace RimworldExtractorGUI
{
    /// <summary>
    /// Reparte un contenedor en columnas iguales.
    ///
    /// Los anclajes de WinForms no sirven para esto: solo saben mantener la distancia a un
    /// borde, asi que en una rejilla de dos por dos terminan agrandando el cuadro de abajo
    /// a la derecha y dejando los otros tres como estaban. Repartir el espacio hay que
    /// hacerlo a mano.
    /// </summary>
    internal static class Rejilla
    {
        private const int Padding = 6;

        /// <summary>
        /// Acomoda filas de controles en dos columnas iguales, ocupando todo el contenedor.
        ///
        /// Cada fila es un encabezado —etiqueta mas un boton de ayuda opcional apoyado en
        /// su borde derecho— y debajo el control que se estira para llenar lo que sobre.
        /// </summary>
        internal static void EnDosColumnas(Control contenedor, params Fila[] filas)
        {
            contenedor.Resize += (_, _) => Acomodar(contenedor, filas);
            Acomodar(contenedor, filas);
        }

        private static void Acomodar(Control contenedor, Fila[] filas)
        {
            if (filas.Length == 0 || contenedor.ClientSize.Width <= 0)
                return;

            var anchoCol = (contenedor.ClientSize.Width - Padding * 3) / 2;
            if (anchoCol <= 0)
                return;

            var altoEncabezado = filas.Max(f => f.Izquierda.Encabezado.Height);
            var altoTitulo = 13;    // el titulo del GroupBox ocupa la primera franja
            var pie = filas.Select(f => f.Pie).FirstOrDefault(p => p != null);
            var altoPie = pie is null ? 0 : pie.Height + Padding;

            // Lo que sobra despues de los encabezados, los margenes y el pie se reparte
            // entre las filas: eso es lo que hace que los cuadros crezcan con la ventana.
            var libre = contenedor.ClientSize.Height
                        - altoTitulo
                        - Padding
                        - filas.Length * (altoEncabezado + Padding)
                        - altoPie;
            var altoCampo = Math.Max(40, libre / filas.Length);

            var y = altoTitulo + Padding;
            foreach (var fila in filas)
            {
                AcomodarCelda(fila.Izquierda, Padding, y, anchoCol, altoEncabezado, altoCampo);
                AcomodarCelda(fila.Derecha, Padding * 2 + anchoCol, y, anchoCol, altoEncabezado, altoCampo);
                y += altoEncabezado + altoCampo + Padding;
            }

            if (pie != null)
                pie.Top = contenedor.ClientSize.Height - pie.Height - Padding;
        }

        private static void AcomodarCelda(Celda celda, int x, int y, int ancho, int altoEncabezado, int altoCampo)
        {
            if (celda.Ayuda is { } ayuda)
            {
                ayuda.Left = x + ancho - ayuda.Width;
                ayuda.Top = y;
                celda.Encabezado.Width = ancho - ayuda.Width - Padding;
            }
            else
            {
                celda.Encabezado.Width = ancho;
            }

            celda.Encabezado.Left = x;
            celda.Encabezado.Top = y;

            celda.Campo.SetBounds(x, y + altoEncabezado, ancho, altoCampo);
        }

        /// <summary>
        /// Reparte la altura de una columna entre dos controles, uno arriba y otro abajo.
        ///
        /// Con anclajes solo se puede estirar uno de los dos: el de arriba quedaria con su
        /// alto original y el de abajo se llevaria todo el espacio nuevo.
        /// </summary>
        internal static void EnDosFilas(Control contenedor, Control arriba, Control abajo)
        {
            // Se toman los margenes del diseño original, antes de mover nada.
            var margenSuperior = arriba.Top;
            var margenInferior = Math.Max(Padding, contenedor.ClientSize.Height - abajo.Bottom);
            var separacion = Math.Max(Padding, abajo.Top - arriba.Bottom);

            void Acomodar()
            {
                var disponible = contenedor.ClientSize.Height - margenSuperior - margenInferior - separacion;
                if (disponible <= 0)
                    return;

                var alto = disponible / 2;
                // Mismo ancho para los dos: son dos secciones de una misma columna, y si
                // una quedo mas ancha al ensancharse por su contenido se nota el desnivel.
                var ancho = Math.Max(arriba.Width, abajo.Width);

                arriba.SetBounds(arriba.Left, margenSuperior, ancho, alto);
                abajo.SetBounds(abajo.Left, margenSuperior + alto + separacion, ancho, disponible - alto);
            }

            contenedor.Resize += (_, _) => Acomodar();
            Acomodar();
        }

        /// <summary>
        /// Acomoda una fila de botones pegada al borde derecho, de derecha a izquierda.
        /// El primero de la lista se estira para ocupar lo que sobra a la izquierda.
        ///
        /// Hace falta porque los botones tienen posicion fija y al ensancharse para que
        /// entre su texto se terminan pisando entre si.
        /// </summary>
        internal static void FilaPegadaALaDerecha(Control contenedor, Control estirable, params Control[] fijos)
        {
            var derecha = contenedor.ClientSize.Width - Padding;
            foreach (var boton in fijos)
            {
                boton.Left = derecha - boton.Width;
                derecha = boton.Left - Padding;
            }

            // Se asigna, no se toma el maximo: si conservara un ancho anterior mayor
            // seguiria pisando al boton de su derecha.
            estirable.Width = Math.Max(0, derecha - estirable.Left);
        }

        /// <summary>Una fila de la rejilla: dos celdas y, en la ultima, un pie opcional.</summary>
        internal readonly record struct Fila(Celda Izquierda, Celda Derecha, Control? Pie = null);

        /// <summary>Un encabezado con su boton de ayuda opcional, y el campo que va debajo.</summary>
        internal readonly record struct Celda(Control Encabezado, Control Campo, Control? Ayuda = null);
    }
}
