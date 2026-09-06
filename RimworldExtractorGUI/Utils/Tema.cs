using System.Diagnostics;
using RimworldExtractorInternal;
using static RimworldExtractorInternal.Prefabs;

namespace RimworldExtractorGUI
{
    /// <summary>
    /// Elige entre el tema claro, el oscuro y el que tenga configurado Windows.
    ///
    /// La API de color de WinForms es de .NET 9 y sigue marcada como experimental, de ahi
    /// el pragma; se acota a las lineas que la usan para no tapar otros avisos.
    ///
    /// Tiene que llamarse antes de crear ventanas, como pide la documentacion. Cambiarla
    /// con la aplicacion abierta deja el cambio a medias: el fondo de la ventana y el panel
    /// de log siguen al tema nuevo, pero los botones conservan el anterior, porque su
    /// aspecto queda fijado cuando se crea su handle. Por eso el tema se guarda y se aplica
    /// al arrancar, y cambiarlo ofrece reiniciar.
    /// </summary>
    internal static class Tema
    {
        /// <summary>Queda en true cuando se acepto reiniciar para ver el tema nuevo.</summary>
        private static bool _seVaARelanzar;

        /// <summary>Aplica el tema guardado. Se llama al arrancar, antes de la primera ventana.</summary>
        internal static void Aplicar() => Aplicar(Prefabs.Theme);

        internal static void Aplicar(ColorTheme tema)
        {
#pragma warning disable WFO5003
            Application.SetColorMode(tema switch
            {
                ColorTheme.Light => SystemColorMode.Classic,
                ColorTheme.Dark => SystemColorMode.Dark,
                _ => SystemColorMode.System
            });
#pragma warning restore WFO5003
        }

        /// <summary>
        /// Pasa al siguiente tema y lo deja guardado. Devuelve el que quedo elegido.
        ///
        /// A proposito no lo aplica: hacerlo con la ventana abierta la deja mitad clara y
        /// mitad oscura, que se ve peor que no cambiar nada hasta el proximo arranque.
        /// </summary>
        internal static ColorTheme Alternar()
        {
            Prefabs.Theme = Prefabs.Theme switch
            {
                ColorTheme.System => ColorTheme.Light,
                ColorTheme.Light => ColorTheme.Dark,
                _ => ColorTheme.System
            };

            Prefabs.Save();
            return Prefabs.Theme;
        }

        /// <summary>
        /// Cierra la aplicacion para volver a abrirla con el tema nuevo. Devuelve false si
        /// no se puede relanzar sola, y entonces hay que cerrarla y abrirla a mano.
        ///
        /// El relanzamiento no ocurre aca: primero tiene que terminar de cerrarse esta
        /// instancia, porque mientras viva tiene tomado log.txt y la nueva no podria
        /// crearlo. Lo hace <see cref="RelanzarSiHaceFalta"/> cuando Application.Run vuelve.
        /// </summary>
        internal static bool Reiniciar()
        {
            if (RutaDelEjecutable is null)
                return false;

            _seVaARelanzar = true;
            Application.Exit();
            return true;
        }

        /// <summary>Abre la nueva instancia. Se llama despues de que Application.Run vuelve.</summary>
        internal static void RelanzarSiHaceFalta()
        {
            if (!_seVaARelanzar || RutaDelEjecutable is null)
                return;

            // Suelta log.txt antes de que la instancia nueva intente crearlo.
            Log.CerrarSalida();

            Process.Start(new ProcessStartInfo(RutaDelEjecutable) { UseShellExecute = true });
        }

        /// <summary>
        /// El ejecutable que hay que volver a abrir, o null si no se puede saber.
        ///
        /// Sirve tanto para el ejecutable normal como para el portable, que es un unico
        /// archivo autoextraible. Se descarta el caso de estar corriendo bajo "dotnet",
        /// donde el proceso es dotnet.exe y relanzarlo no abriria la aplicacion.
        /// </summary>
        private static string? RutaDelEjecutable
        {
            get
            {
                var ruta = Environment.ProcessPath;
                if (string.IsNullOrEmpty(ruta))
                    return null;

                var nombre = Path.GetFileNameWithoutExtension(ruta);
                return nombre.Equals("dotnet", StringComparison.OrdinalIgnoreCase) ? null : ruta;
            }
        }

        /// <summary>Lo que dice el boton: el tema que esta elegido.</summary>
        internal static string Rotulo(ColorTheme tema) => tema switch
        {
            ColorTheme.Light => Strings.BtnThemeLight,
            ColorTheme.Dark => Strings.BtnThemeDark,
            _ => Strings.BtnThemeSystem
        };
    }
}
