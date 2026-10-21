using System.Diagnostics;

namespace RimworldExtractorInternal
{
    public enum LogLevel
    {
        Message,
        Warning,
        Error
    }

    /// <summary>
    /// Una linea de log, guardada como dato en vez de como texto ya armado.
    ///
    /// Que sea estructurada importa: antes la deteccion de errores tenia que buscar
    /// la palabra "ERROR" dentro de la cadena, asi que dependia del formato.
    /// </summary>
    /// <param name="Level">Nivel de la entrada.</param>
    /// <param name="Message">Texto visible para el usuario.</param>
    /// <param name="CallSite">De donde salio. Vacio en los mensajes normales.</param>
    /// <param name="Timestamp">Momento en que se registro.</param>
    /// <param name="SoloArchivo">
    /// Si es un detalle tecnico, que va al log detallado y no a la ventana. La ventana lo
    /// recibe igual, para poder contar los avisos que deja afuera.
    /// </param>
    public readonly record struct LogEntry(
        LogLevel Level,
        string Message,
        string CallSite,
        DateTime Timestamp,
        bool SoloArchivo = false);

    public static class Log
    {
        /// <summary>Donde se escribe cada linea si no hay <see cref="Pantalla"/>: la consola.</summary>
        public static TextWriter Out { private get; set; } = Console.Out;

        /// <summary>
        /// La ventana, si la hay. Recibe las entradas como dato y no como texto: es ella la
        /// que decide que mostrar y donde, y para eso necesita el nivel y si es tecnica.
        /// </summary>
        public static Action<LogEntry>? Pantalla { private get; set; }

        /// <summary>
        /// El log detallado de la ultima extraccion, o null si no hay ninguno abierto. Se
        /// escribe con el candado tomado, porque llegan lineas desde varios hilos a la vez.
        /// </summary>
        private static StreamWriter? _archivo;

        /// <summary>
        /// Empieza el log detallado de cero en esa ruta. Lo llama quien arranca una extraccion,
        /// asi que el archivo trae siempre una sola: con todas una detras de otra, despues de
        /// una corrida de todo RML no habria forma de encontrar la ultima.
        ///
        /// Cada linea va al disco en el momento. Con buffer, lo ultimo antes de que la
        /// aplicacion se caiga no llegaba al archivo, y es justo lo que se busca ahi. Son unos
        /// miles de lineas por corrida: el costo no se nota.
        /// </summary>
        public static void EmpezarArchivo(string ruta)
        {
            lock (_candado)
            {
                // En null antes de abrir: si la ruta esta tomada y el constructor tira, el
                // aviso que sigue no puede ir a parar a un archivo ya cerrado.
                _archivo?.Dispose();
                _archivo = null;
                _archivo = new StreamWriter(ruta, false, new System.Text.UTF8Encoding(false)) { AutoFlush = true };
            }
        }

        public static void CerrarArchivo()
        {
            lock (_candado)
            {
                _archivo?.Dispose();
                _archivo = null;
            }
        }

        /// <summary>
        /// Cierra el archivo y vuelve a la consola.
        ///
        /// Hace falta antes de abrir otra instancia de la aplicacion: mientras el archivo siga
        /// abierto la instancia nueva no puede crearlo.
        /// </summary>
        public static void CerrarSalida()
        {
            CerrarArchivo();
            Pantalla = null;
            Out = Console.Out;
        }

        /// <summary>
        /// Las entradas tal cual, para quien necesite el nivel sin re-parsear.
        ///
        /// Devuelve una copia: si se entregara la cola en vivo, recorrerla mientras otro hilo
        /// escribe tira InvalidOperationException.
        /// </summary>
        public static IEnumerable<LogEntry> Entries
        {
            get { lock (_candado) return _logQueue.ToList(); }
        }

        public const string PrefixError = Strings.PrefixError;
        public const string PrefixWarning = Strings.PrefixWarning;
        public const string PrefixMessage = Strings.PrefixMessage;

        private static readonly Queue<LogEntry> _logQueue = new();
        private static readonly HashSet<int> _hashes = new();
        private const int MAX_COUNT = 999;

        /// <summary>
        /// Protege las dos colecciones de arriba. Desde que la extraccion lee y parsea en
        /// paralelo, Err y Wrn pueden llegar desde varios hilos a la vez, y una Queue no
        /// sincronizada se corrompe o revienta al enumerarla mientras otro escribe.
        /// </summary>
        private static readonly object _candado = new();

        public static void Err(string message) => Write(LogLevel.Error, message);

        public static void Wrn(string message) => Write(LogLevel.Warning, message);

        public static void Msg(string message) => Write(LogLevel.Message, message);

        /// <summary>Un mensaje tecnico: va al log detallado y a la consola, pero no a la ventana.</summary>
        public static void Det(string message) => Write(LogLevel.Message, message, true);

        /// <summary>
        /// Un aviso tecnico, que no le pide nada al usuario: un nodo padre que no aparece, una
        /// operacion de patch sin soporte. La ventana solo los cuenta.
        /// </summary>
        public static void WrnDet(string message) => Write(LogLevel.Warning, message, true);

        public static void ErrOnce(string message, int hash)
        {
            lock (_candado)
            {
                if (!_hashes.Add(hash))
                    return;
            }
            Err(message);
        }


        /// <summary>
        /// Indica si hubo algun error despues de la ultima aparicion del marcador.
        ///
        /// Vive aca y no en la GUI porque antes se resolvia buscando texto dentro de la
        /// linea ya formateada, lo que la ataba al formato y hacia que cualquier mensaje
        /// que contuviera la palabra "ERROR" diera un falso positivo.
        /// </summary>
        public static bool HasErrorSince(string marker)
        {
            var entries = Entries.ToList();
            var idx = entries.FindLastIndex(x => x.Message == marker);
            if (idx == -1)
                return false;

            for (var i = idx; i < entries.Count; i++)
            {
                if (entries[i].Level == LogLevel.Error)
                    return true;
            }

            return false;
        }

        private static void Write(LogLevel level, string message, bool soloArchivo = false)
        {
            // El origen solo se muestra en advertencias y errores, asi que en los mensajes
            // normales se saltea el StackTrace, que no es gratis y se armaba en cada linea.
            var callSite = level == LogLevel.Message ? string.Empty : GetCallSite();
            var entry = new LogEntry(level, message, callSite, DateTime.Now, soloArchivo);

            var pantalla = Pantalla;
            if (pantalla != null)
                pantalla(entry);
            else
                Out.WriteLine(Format(entry));

            StoreEntry(entry);
        }

        /// <summary>
        /// Arma la linea del archivo y de la consola. Es el unico lugar donde se decide ese
        /// formato. El nivel va en palabras: los simbolos que se usaban antes no se leen en
        /// todas las fuentes, y en el archivo se buscan con Ctrl+F.
        /// </summary>
        public static string Format(LogEntry entry)
        {
            var nivel = entry.Level switch
            {
                LogLevel.Error => Strings.PrefixError + " ",
                LogLevel.Warning => Strings.PrefixWarning + " ",
                _ => string.Empty
            };

            var line = $"[{entry.Timestamp:HH:mm:ss}] {nivel}{entry.Message}";
            return string.IsNullOrEmpty(entry.CallSite) ? line : $"{line}  ({entry.CallSite})";
        }

        private static string GetCallSite()
        {
            var trace = new StackTrace();
            // 0 = GetCallSite, 1 = Write, 2 = Err/Wrn, 3 = quien llamo de verdad.
            var method = trace.GetFrame(3)?.GetMethod();
            if (method?.DeclaringType == typeof(Log))
                method = trace.GetFrame(4)?.GetMethod();

            var typeName = CleanTypeName(method?.DeclaringType?.Name);
            var methodName = CleanMethodName(method?.Name);

            if (string.IsNullOrEmpty(methodName))
                return typeName;

            return $"{typeName}.{methodName}()";
        }

        /// <summary>
        /// Los lambdas viven en una clase generada por el compilador, del estilo
        /// "&lt;&gt;c__DisplayClass12_0". En esos casos el tipo util es el que la contiene.
        /// </summary>
        private static string CleanTypeName(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return "?";

            return name.StartsWith("<>") ? "?" : name;
        }

        /// <summary>
        /// Desarma los nombres que genera el compilador. Un lambda aparece como
        /// "&lt;.ctor&gt;b__12_0": el nombre real es lo que va entre los angulos.
        /// Los constructores (".ctor" / ".cctor") se omiten, porque "FormMain" solo
        /// dice mas que "FormMain..ctor()".
        /// </summary>
        private static string CleanMethodName(string? name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            var open = name.IndexOf('<');
            var close = name.IndexOf('>');
            if (open == 0 && close > 1)
                name = name.Substring(1, close - 1);

            return name is ".ctor" or ".cctor" ? string.Empty : name;
        }

        private static void StoreEntry(LogEntry entry)
        {
            lock (_candado)
            {
                _archivo?.WriteLine(Format(entry));

                if (_logQueue.Count > MAX_COUNT)
                {
                    _logQueue.Dequeue();
                }
                _logQueue.Enqueue(entry);
            }
        }
    }
}
