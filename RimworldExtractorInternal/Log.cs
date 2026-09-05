using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    public readonly record struct LogEntry(
        LogLevel Level,
        string Message,
        string CallSite,
        DateTime Timestamp);

    public static class Log
    {
        public static TextWriter Out { private get; set; } = Console.Out;

        /// <summary>Las entradas tal cual, para quien necesite el nivel sin re-parsear.</summary>
        public static IEnumerable<LogEntry> Entries => _logQueue;

        /// <summary>Se conserva por compatibilidad: devuelve solo el texto de cada entrada.</summary>
        public static IEnumerable<string> Messages => _logQueue.Select(x => x.Message);

        public const string Separator = "::";
        public const string PrefixError = Strings.PrefixError;
        public const string PrefixWarning = Strings.PrefixWarning;
        public const string PrefixMessage = Strings.PrefixMessage;

        private static readonly Queue<LogEntry> _logQueue = new();
        private static readonly HashSet<int> _hashes = new();
        private const int MAX_COUNT = 999;

        public static void Err(string message) => Write(LogLevel.Error, message);

        public static void Wrn(string message) => Write(LogLevel.Warning, message);

        public static void Msg(string message) => Write(LogLevel.Message, message);

        public static void ErrOnce(string message, int hash)
        {
            if (!_hashes.Add(hash))
                return;
            Err(message);
        }

        public static void WrnOnce(string message, int hash)
        {
            if (!_hashes.Add(hash))
                return;
            Wrn(message);
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
            var entries = _logQueue.ToList();
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

        private static void Write(LogLevel level, string message)
        {
            // El origen solo se muestra en advertencias y errores, asi que en los mensajes
            // normales se saltea el StackTrace, que no es gratis y se armaba en cada linea.
            var callSite = level == LogLevel.Message ? string.Empty : GetCallSite();
            var entry = new LogEntry(level, message, callSite, DateTime.Now);

            Out.WriteLine(Format(entry));
            StoreEntry(entry);
        }

        /// <summary>Arma la linea visible. Es el unico lugar donde se decide el formato.</summary>
        public static string Format(LogEntry entry)
        {
            var symbol = entry.Level switch
            {
                LogLevel.Error => Strings.LogSymbolError,
                LogLevel.Warning => Strings.LogSymbolWarning,
                _ => Strings.LogSymbolMessage
            };

            var line = $"[{entry.Timestamp:HH:mm:ss}] {symbol} {entry.Message}";
            return string.IsNullOrEmpty(entry.CallSite) ? line : $"{line}  ({entry.CallSite})";
        }

        /// <summary>Longitud del prefijo de hora, para poder pintarlo aparte.</summary>
        public static int TimestampLength => "[00:00:00]".Length;

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
            if (_logQueue.Count > MAX_COUNT)
            {
                _logQueue.Dequeue();
            }
            _logQueue.Enqueue(entry);
        }
    }
}
