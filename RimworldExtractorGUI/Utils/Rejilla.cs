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
        /// Estira el contenido de un contenedor para que ocupe todo su ancho.
        ///
        /// Cada fila se reparte en celdas iguales. Una celda puede llevar un boton pegado
        /// a su derecha —el tipico "..." de elegir carpeta—, y entonces el campo ocupa lo
        /// que queda. Solo se tocan las posiciones horizontales: el alto y la altura de
        /// cada fila quedan como estaban.
        /// </summary>
        internal static void EstirarAlAncho(Control contenedor, params FilaAncho[] filas)
        {
            void Acomodar()
            {
                var util = contenedor.ClientSize.Width - Padding * 2;
                if (util <= 0)
                    return;

                foreach (var fila in filas)
                {
                    var celdas = fila.Celdas;
                    var anchoCelda = (util - Padding * (celdas.Length - 1)) / celdas.Length;

                    for (var i = 0; i < celdas.Length; i++)
                    {
                        var x = Padding + i * (anchoCelda + Padding);
                        AcomodarCampo(celdas[i], x, anchoCelda);
                    }
                }
            }

            contenedor.Resize += (_, _) => Acomodar();
            Acomodar();
        }

        /// <summary>
        /// Acomoda filas en una columna de ancho unico, para que todas queden parejas.
        ///
        /// El ancho sale de la fila mas exigente: una fila de un control necesita el ancho
        /// entero, y una de dos necesita el doble del mas ancho. Asi todos los botones
        /// terminan alineados y los pares se reparten la mitad cada uno.
        /// </summary>
        internal static void Columna(int izquierda, params FilaAncho[] filas)
        {
            var ancho = 0;
            foreach (var fila in filas)
            {
                var n = fila.Celdas.Length;
                var mayor = fila.Celdas.Max(c => AutoAjuste.AnchoNecesario(c.Control));
                ancho = Math.Max(ancho, mayor * n + Padding * (n - 1));
            }

            foreach (var fila in filas)
            {
                var n = fila.Celdas.Length;
                var anchoCelda = (ancho - Padding * (n - 1)) / n;
                for (var i = 0; i < n; i++)
                    AcomodarCampo(fila.Celdas[i], izquierda + i * (anchoCelda + Padding), anchoCelda);
            }
        }

        private static void AcomodarCampo(Campo campo, int x, int ancho)
        {
            if (campo.Boton is { } boton)
            {
                boton.Left = x + ancho - boton.Width;
                campo.Control.Left = x;
                campo.Control.Width = Math.Max(20, ancho - boton.Width - Padding);
            }
            else
            {
                campo.Control.Left = x;
                campo.Control.Width = ancho;
            }
        }

        /// <summary>Atajo para armar una fila sin tener que nombrar el tipo.</summary>
        internal static FilaAncho Linea(params Campo[] celdas) => new(celdas);

        /// <summary>Una fila del reparto horizontal: sus celdas se reparten el ancho.</summary>
        internal readonly record struct FilaAncho(Campo[] Celdas);

        /// <summary>Un control, y opcionalmente un boton pegado a su derecha.</summary>
        internal readonly record struct Campo(Control Control, Control? Boton = null)
        {
            /// <summary>Deja escribir el control a secas cuando no lleva boton.</summary>
            public static implicit operator Campo(Control control) => new(control);
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
