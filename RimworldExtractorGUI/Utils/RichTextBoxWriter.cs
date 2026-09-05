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
        public override Encoding Encoding { get; } = Encoding.UTF8;

        // La paleta depende del tema: sobre fondo oscuro los colores puros pierden
        // contraste, y sobre fondo claro los tonos claros se vuelven ilegibles.
        private static bool Oscuro =>
#pragma warning disable WFO5003
            Application.IsDarkModeEnabled;
#pragma warning restore WFO5003

        internal static Color ColorFondo => Oscuro ? Color.FromArgb(32, 32, 32) : Color.FromArgb(250, 250, 250);
        private static Color ColorError => Oscuro ? Color.FromArgb(255, 110, 110) : Color.FromArgb(180, 30, 30);
        private static Color ColorWarning => Oscuro ? Color.FromArgb(255, 190, 90) : Color.FromArgb(150, 95, 0);
        private static Color ColorMessage => Oscuro ? Color.FromArgb(220, 220, 220) : Color.FromArgb(30, 30, 30);
        private static Color ColorTimestamp => Oscuro ? Color.FromArgb(130, 130, 130) : Color.FromArgb(140, 140, 140);

        public RichTextBoxWriter(RichTextBox richTextBox)
        {
            this._richTextBox = richTextBox;
            this._logFileWriter = File.CreateText("log.txt");
        }

        public override void WriteLine(string? value)
        {
            if (value == null)
            {
                return;
            }

            lock (_lock)
            {
                _logFileWriter.WriteLine(value);
                _logFileWriter.Flush();

                var line = value + Environment.NewLine;
                if (_richTextBox.InvokeRequired)
                {
                    _richTextBox.Invoke(() => AppendToRichTextBox(line));
                }
                else
                {
                    AppendToRichTextBox(line);
                }
            }
        }

        private void AppendToRichTextBox(string line)
        {
            if (_richTextBox.Text.Length + line.Length > 327670)
            {
                _richTextBox.Clear();
                _richTextBox.SelectionColor = ColorTimestamp;
                _richTextBox.AppendText(Strings.LogCleanedUp + Environment.NewLine);
            }

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

            _richTextBox.ScrollToCaret();
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
                _logFileWriter.Flush();
                _logFileWriter.Close();
                _logFileWriter.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
