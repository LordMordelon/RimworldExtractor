using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimworldExtractorInternal;

namespace RimworldExtractorGUI
{
    internal class RichTextBoxWriter : TextWriter
    {
        private readonly RichTextBox _richTextBox;
        private readonly StreamWriter _logFileWriter;
        private readonly object _lock = new object();

        /// <summary>
        /// Lo escrito que todavia no se pinto.
        ///
        /// Antes cada linea se pintaba en el momento, con un Invoke sincrono: el hilo que
        /// extrae quedaba esperando a que la ventana lo atendiera, y en una corrida de
        /// doscientos mods eso avanza al ritmo del repintado. Con la cola, quien escribe deja
        /// la linea y sigue; la ventana pinta de a tandas cuando puede.
        /// </summary>
        private readonly Queue<string> _pendientes = new();

        /// <summary>Si ya hay un volcado en camino. Sin esto se encolaria uno por linea.</summary>
        private bool _volcadoPedido;

        /// <summary>
        /// Cuanto texto lleva pintado el control. Se lleva aparte porque preguntarle
        /// Text.Length recorre el contenido entero, y son hasta 327.670 caracteres por linea.
        /// </summary>
        private int _largo;

        public override Encoding Encoding { get; } = Encoding.UTF8;

        // La paleta depende del tema: sobre fondo oscuro los colores puros pierden
        // contraste, y sobre fondo claro los tonos claros se vuelven ilegibles.
        //
        // Se deduce del propio color de fondo y no de Application.IsDarkModeEnabled: asi
        // el texto no puede quedar en desacuerdo con el fondo sobre el que se dibuja, sin
        // importar en que momento se consulte ni si la API experimental cambia.
        internal static Color ColorFondo => SystemColors.Window;

        private static bool Oscuro => Tema.Luminancia(ColorFondo) < 128;
        private static Color ColorError => Oscuro ? Color.FromArgb(255, 110, 110) : Color.FromArgb(180, 30, 30);
        private static Color ColorWarning => Oscuro ? Color.FromArgb(255, 190, 90) : Color.FromArgb(150, 95, 0);
        private static Color ColorMessage => Oscuro ? Color.FromArgb(220, 220, 220) : Color.FromArgb(30, 30, 30);
        private static Color ColorTimestamp => Oscuro ? Color.FromArgb(130, 130, 130) : Color.FromArgb(140, 140, 140);

        public RichTextBoxWriter(RichTextBox richTextBox)
        {
            this._richTextBox = richTextBox;
            // Junto al ejecutable y no en el directorio de trabajo, igual que Prefabs.dat.
            this._logFileWriter = File.CreateText(Path.Combine(Prefabs.Carpeta, "log.txt"));
        }

        public override void WriteLine(string? value)
        {
            if (value == null)
            {
                return;
            }

            lock (_lock)
            {
                // Sin Flush por linea: lo hace Volcar, y Dispose se encarga del final. Un
                // flush por linea es una ida al disco por cada mensaje.
                _logFileWriter.WriteLine(value);
                _pendientes.Enqueue(value + Environment.NewLine);

                if (_volcadoPedido)
                    return;
                _volcadoPedido = true;
            }

            // Fuera del lock y con BeginInvoke, que no espera a la ventana. Mientras esta
            // ocupada, las lineas siguientes se suman a la cola y salen todas en la misma
            // tanda: cuanto mas cargada la ventana, menos tandas y mas grandes.
            if (_richTextBox.IsHandleCreated && _richTextBox.InvokeRequired)
                _richTextBox.BeginInvoke(Volcar);
            else
                Volcar();
        }

        /// <summary>
        /// Pinta todo lo que haya pendiente, de una. Corre en el hilo de la ventana.
        /// </summary>
        private void Volcar()
        {
            string[] tanda;
            lock (_lock)
            {
                _volcadoPedido = false;
                if (_pendientes.Count == 0)
                    return;

                tanda = _pendientes.ToArray();
                _pendientes.Clear();
                _logFileWriter.Flush();
            }

            foreach (var line in tanda)
                AppendToRichTextBox(line);

            // Una sola vez por tanda: es lo que fuerza el scroll y el repintado.
            _richTextBox.ScrollToCaret();
        }

        /// <summary>
        /// Deja la ventana al dia con lo que se escribio hasta aca.
        ///
        /// Se llama al terminar algo largo, antes de mostrar un resumen: si no, el resumen
        /// aparece con las ultimas lineas de la corrida todavia sin pintar.
        /// </summary>
        public void Vaciar()
        {
            if (_richTextBox.IsHandleCreated && _richTextBox.InvokeRequired)
                _richTextBox.Invoke(Volcar);
            else
                Volcar();
        }

        private void AppendToRichTextBox(string line)
        {
            if (_largo + line.Length > 327670)
            {
                _richTextBox.Clear();
                _richTextBox.SelectionColor = ColorTimestamp;
                var aviso = Strings.LogCleanedUp + Environment.NewLine;
                _richTextBox.AppendText(aviso);
                _largo = aviso.Length;
            }

            _largo += line.Length;

            // La hora va en gris y el resto en el color del nivel. Se deduce del texto
            // porque TextWriter solo recibe la linea ya armada por Log.Format.
            var split = line.StartsWith('[') ? Log.TimestampLength : 0;
            if (split > 0 && line.Length > split)
            {
                _richTextBox.SelectionColor = ColorTimestamp;
                _richTextBox.AppendText(line[..split]);
                // +1 por el espacio que separa la hora del simbolo.
                _richTextBox.SelectionColor = ColorFor(line, split + 1);
                _richTextBox.AppendText(line[split..]);
            }
            else
            {
                _richTextBox.SelectionColor = ColorMessage;
                _richTextBox.AppendText(line);
            }
        }

        /// <summary>
        /// El simbolo de nivel esta en una posicion fija: "[HH:mm:ss] X ...". Se mira
        /// ahi y no con Contains, para que un mensaje que incluya el simbolo en su
        /// propio texto no se pinte como si fuera un error.
        /// </summary>
        private static Color ColorFor(string line, int symbolIndex)
        {
            if (symbolIndex >= line.Length)
                return ColorMessage;

            var symbol = line[symbolIndex].ToString();
            if (symbol == Strings.LogSymbolError) return ColorError;
            if (symbol == Strings.LogSymbolWarning) return ColorWarning;
            return ColorMessage;
        }

        protected override void Dispose(bool disposing)
        {
            lock (_lock)
            {
                // Lo que quedo en la cola no se pinta —la ventana ya se esta yendo— pero si
                // tiene que quedar en el archivo, que es lo que se mira despues.
                _pendientes.Clear();
                _logFileWriter.Flush();
                _logFileWriter.Close();
                _logFileWriter.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
