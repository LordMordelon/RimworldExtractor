using RimworldExtractorInternal;

namespace RimworldExtractorGUI
{
    /// <summary>
    /// Lo que la ventana muestra del log: el resultado de cada mod en el panel de log, y los
    /// avisos y errores en su propio recuadro, que aparece solo si hay alguno. Lo tecnico no se
    /// muestra: va al log detallado, y aca solo se cuenta.
    /// </summary>
    internal class PanelDeLog
    {
        private readonly RichTextBox _log;
        private readonly RichTextBox _avisos;
        private readonly Label _tituloAvisos;
        private readonly object _lock = new object();

        /// <summary>
        /// Lo escrito que todavia no se pinto.
        ///
        /// Antes cada linea se pintaba en el momento, con un Invoke sincrono: el hilo que
        /// extrae quedaba esperando a que la ventana lo atendiera, y en una corrida de
        /// doscientos mods eso avanza al ritmo del repintado. Con la cola, quien escribe deja
        /// la linea y sigue; la ventana pinta de a tandas cuando puede.
        /// </summary>
        private readonly Queue<LogEntry> _pendientes = new();

        /// <summary>Si ya hay un volcado en camino. Sin esto se encolaria uno por linea.</summary>
        private bool _volcadoPedido;

        /// <summary>
        /// Cuanto texto lleva pintado el panel de log. Se lleva aparte porque preguntarle
        /// Text.Length recorre el contenido entero, y son hasta 327.670 caracteres por linea.
        /// </summary>
        private int _largo;

        /// <summary>Los avisos y errores que se ven en el recuadro, desde <see cref="Empezar"/>.</summary>
        private int _visibles;

        private int _tecnicos;

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

        public PanelDeLog(RichTextBox log, RichTextBox avisos, Label tituloAvisos)
        {
            _log = log;
            _avisos = avisos;
            _tituloAvisos = tituloAvisos;
        }

        /// <summary>Los avisos tecnicos que se dejaron afuera desde <see cref="Empezar"/>.</summary>
        public int AvisosTecnicos
        {
            get { lock (_lock) return _tecnicos; }
        }

        /// <summary>
        /// Lo llama Log en cada entrada, desde el hilo que sea.
        /// </summary>
        public void Agregar(LogEntry entrada)
        {
            lock (_lock)
            {
                if (entrada.SoloArchivo)
                {
                    if (entrada.Level != LogLevel.Message)
                        _tecnicos++;
                    return;
                }

                _pendientes.Enqueue(entrada);
                if (_volcadoPedido)
                    return;
                _volcadoPedido = true;
            }

            // Fuera del lock y con BeginInvoke, que no espera a la ventana. Mientras esta
            // ocupada, las lineas siguientes se suman a la cola y salen todas en la misma
            // tanda: cuanto mas cargada la ventana, menos tandas y mas grandes.
            if (_log.IsHandleCreated && _log.InvokeRequired)
                _log.BeginInvoke(Volcar);
            else
                Volcar();
        }

        /// <summary>
        /// Arranca una extraccion: el recuadro de avisos y la cuenta de los tecnicos son de la
        /// ultima, igual que el log detallado. El panel de log conserva lo anterior. Corre en
        /// el hilo de la ventana.
        /// </summary>
        public void Empezar()
        {
            Vaciar();
            lock (_lock)
            {
                _tecnicos = 0;
                _visibles = 0;
            }

            _avisos.Clear();
            _avisos.Visible = false;
            _tituloAvisos.Visible = false;
        }

        /// <summary>
        /// Deja la ventana al dia con lo que se escribio hasta aca.
        ///
        /// Se llama al terminar algo largo, antes de mostrar un resumen: si no, el resumen
        /// aparece con las ultimas lineas de la corrida todavia sin pintar.
        /// </summary>
        public void Vaciar()
        {
            if (_log.IsHandleCreated && _log.InvokeRequired)
                _log.Invoke(Volcar);
            else
                Volcar();
        }

        /// <summary>
        /// Pinta todo lo que haya pendiente, de una. Corre en el hilo de la ventana.
        /// </summary>
        private void Volcar()
        {
            LogEntry[] tanda;
            lock (_lock)
            {
                _volcadoPedido = false;
                if (_pendientes.Count == 0)
                    return;

                tanda = _pendientes.ToArray();
                _pendientes.Clear();
            }

            var huboAvisos = false;
            foreach (var entrada in tanda)
            {
                if (entrada.Level == LogLevel.Message)
                {
                    AgregarAlLog(entrada);
                }
                else
                {
                    Pintar(_avisos, entrada);
                    _visibles++;
                    huboAvisos = true;
                }
            }

            // Una sola vez por tanda: es lo que fuerza el scroll y el repintado.
            _log.ScrollToCaret();
            if (huboAvisos)
            {
                _tituloAvisos.Text = Strings.WarningsTitle(_visibles);
                _tituloAvisos.Visible = true;
                _avisos.Visible = true;
                _avisos.ScrollToCaret();
            }
        }

        private void AgregarAlLog(LogEntry entrada)
        {
            var largo = Hora(entrada).Length + entrada.Message.Length + Environment.NewLine.Length;
            if (_largo + largo > 327670)
            {
                _log.Clear();
                _log.SelectionColor = ColorTimestamp;
                var aviso = Strings.LogCleanedUp + Environment.NewLine;
                _log.AppendText(aviso);
                _largo = aviso.Length;
            }

            _largo += largo;
            Pintar(_log, entrada);
        }

        /// <summary>La hora va en gris y el texto en el color de su nivel.</summary>
        private static void Pintar(RichTextBox caja, LogEntry entrada)
        {
            caja.SelectionColor = ColorTimestamp;
            caja.AppendText(Hora(entrada));
            caja.SelectionColor = entrada.Level switch
            {
                LogLevel.Error => ColorError,
                LogLevel.Warning => ColorWarning,
                _ => ColorMessage
            };
            caja.AppendText(entrada.Message + Environment.NewLine);
        }

        private static string Hora(LogEntry entrada) => $"[{entrada.Timestamp:HH:mm:ss}] ";
    }
}
