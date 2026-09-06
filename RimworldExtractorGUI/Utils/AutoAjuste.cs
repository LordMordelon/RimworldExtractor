using System;
using System.Collections.Generic;
using System.Linq;

namespace RimworldExtractorGUI
{
    /// <summary>
    /// Ensancha los controles cuyo texto no entra.
    ///
    /// Hace falta porque los formularios vienen de upstream con posicionamiento absoluto
    /// y casi ningun AutoSize: cada control tiene el ancho que le servia al texto coreano,
    /// y el español ocupa bastante mas. Se mide con la fuente real en vez de aplicar un
    /// porcentaje fijo, porque los casos son muy dispares —hay botones que necesitan casi
    /// el doble y etiquetas a las que les sobra— y porque asi se adapta solo si mañana
    /// cambia una traduccion.
    ///
    /// Se llama desde ApplyStrings(), despues de asignar los textos, para no tener que
    /// tocar los .Designer.cs y poder seguir sincronizando con upstream sin conflictos.
    /// </summary>
    internal static class AutoAjuste
    {
        /// <summary>Aire a los lados del texto, para que no quede pegado al borde.</summary>
        private const int Margen = 10;

        /// <summary>Separacion minima que se deja al correr un control.</summary>
        private const int Separacion = 6;

        /// <summary>Tope de pasadas, por si alguna disposicion no llegara a estabilizarse.</summary>
        private const int MaxPasadas = 10;

        /// <summary>
        /// Ensancha lo que no entra y corre lo que quedaria tapado.
        ///
        /// El trabajo se hace en dos etapas y no control por control. Hacerlo de una sola
        /// pasada dependia del orden: al agrandar un control se movia otro que ya se habia
        /// procesado, y la decision tomada para ese quedaba invalidada.
        /// </summary>
        internal static void Ajustar(params Control[] controles)
        {
            // Se distinguen dos papeles: los que pueden crecer —hace falta texto para
            // medirlos— y los que pueden correrse, que incluye a los vacios. Un cuadro de
            // texto no se mide, pero si hay que apartarlo cuando su etiqueta se agranda.
            var movibles = controles.Where(c => c is not null).ToList();
            var medibles = movibles.Where(c => !string.IsNullOrEmpty(c.Text)).ToList();
            if (medibles.Count == 0)
                return;

            // 1. Cada control toma el ancho que necesita su texto.
            foreach (var control in medibles)
            {
                var necesario = AnchoNecesario(control);
                if (necesario > control.Width)
                    control.Width = necesario;
            }

            // 2. Se resuelven los solapamientos hasta que no quede ninguno. Es un punto
            //    fijo, asi que el resultado no depende del orden de la lista.
            for (var pasada = 0; pasada < MaxPasadas && CorrerTapados(medibles, movibles); pasada++)
            {
            }

            // 3. Recien al final se agranda lo que haga falta para que todo entre.
            foreach (var padre in movibles.Select(c => c.Parent).Where(p => p != null).Distinct())
                AjustarContenedor(padre!);

            // 4. Lo que quedo es el tamaño minimo con el que todo entra. Sin esto se
            //    puede encoger la ventana hasta tapar los propios controles que se
            //    acaban de acomodar.
            var forma = movibles.Select(c => c.FindForm()).FirstOrDefault(f => f != null);
            if (forma != null)
                forma.MinimumSize = forma.Size;
        }

        /// <summary>
        /// Corre los controles que quedarian tapados por el texto de otro. Devuelve si
        /// movio alguno, para saber si hace falta otra pasada.
        /// </summary>
        private static bool CorrerTapados(List<Control> medibles, List<Control> movibles)
        {
            var movio = false;

            foreach (var control in medibles)
            {
                var padre = control.Parent;
                if (padre == null)
                    continue;

                // Hasta donde llega el texto. Un control puede ser legitimamente mas ancho
                // que su texto —hay etiquetas holgadas con un boton de ayuda apoyado en su
                // borde derecho—, y eso no molesta a nadie.
                var finDelTexto = control.Left + Math.Max(control.Width, AnchoNecesario(control));

                // Solo se mueven los controles que la llamada declara. Antes se movia
                // cualquier vecino, y eso desacomodaba cosas que estaban puestas a
                // proposito: los botones de ayuda de Ajustes se apoyan sobre el borde
                // derecho de su etiqueta, y terminaban empujandose entre si.
                foreach (var vecino in movibles.Where(v => ReferenceEquals(v.Parent, padre)))
                {
                    if (ReferenceEquals(vecino, control))
                        continue;
                    if (vecino.Left < control.Left || vecino.Left >= finDelTexto)
                        continue;
                    if (!SeSolapanEnVertical(vecino, control))
                        continue;

                    vecino.Left = finDelTexto + Separacion;
                    movio = true;
                }
            }

            return movio;
        }

        /// <summary>
        /// Ancho que necesita el texto con la fuente real del control. Contempla los
        /// textos de varias lineas, que los hay, midiendo la linea mas larga.
        /// </summary>
        internal static int AnchoNecesario(Control control)
        {
            var lineas = control.Text.Split('\n');
            var ancho = lineas.Max(l => TextRenderer.MeasureText(l.Trim(), control.Font).Width);

            // Un boton reserva espacio para su borde y el foco.
            if (control is ButtonBase)
                ancho += 8;

            return ancho + Margen;
        }

        /// <summary>
        /// Dos controles se estorban solo si comparten franja vertical. Sin esto, agrandar
        /// una etiqueta correria controles de otras filas que no molestaban.
        /// </summary>
        private static bool SeSolapanEnVertical(Control a, Control b)
            => a.Top < b.Bottom && b.Top < a.Bottom;

        /// <summary>
        /// Agranda el contenedor si algo quedo fuera, y sube hasta la ventana, que es lo
        /// unico que puede crecer de verdad.
        /// </summary>
        private static void AjustarContenedor(Control contenedor)
        {
            while (contenedor != null)
            {
                var borde = contenedor.Controls.OfType<Control>().DefaultIfEmpty().Max(c => c?.Right ?? 0);
                var desborde = borde + Margen - contenedor.ClientSize.Width;
                if (desborde <= 0)
                    return;

                if (contenedor is Form form)
                {
                    form.ClientSize = new Size(form.ClientSize.Width + desborde, form.ClientSize.Height);
                    return;
                }

                contenedor.Width += desborde;
                contenedor = contenedor.Parent!;
            }
        }
    }
}
