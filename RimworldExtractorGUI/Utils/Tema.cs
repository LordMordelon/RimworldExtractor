using RimworldExtractorInternal;
using static RimworldExtractorInternal.Prefabs;

namespace RimworldExtractorGUI
{
    /// <summary>
    /// Elige entre el tema claro, el oscuro y el que tenga configurado Windows.
    ///
    /// La API de color de WinForms es de .NET 9 y sigue marcada como experimental, de ahi
    /// el pragma; se acota a las lineas que la usan para no tapar otros avisos. La
    /// documentacion pide llamarla antes de crear ventanas, pero cambiarla despues
    /// funciona: se comprobo que las ventanas ya abiertas se repintan con el tema nuevo.
    /// </summary>
    internal static class Tema
    {
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
        /// Pasa al siguiente tema, lo aplica y lo deja guardado. Devuelve el que quedo.
        /// </summary>
        internal static ColorTheme Alternar()
        {
            Prefabs.Theme = Prefabs.Theme switch
            {
                ColorTheme.System => ColorTheme.Light,
                ColorTheme.Light => ColorTheme.Dark,
                _ => ColorTheme.System
            };

            Aplicar(Prefabs.Theme);
            Prefabs.Save();
            return Prefabs.Theme;
        }

        /// <summary>Lo que dice el boton: el tema que esta puesto ahora.</summary>
        internal static string Rotulo(ColorTheme tema) => tema switch
        {
            ColorTheme.Light => Strings.BtnThemeLight,
            ColorTheme.Dark => Strings.BtnThemeDark,
            _ => Strings.BtnThemeSystem
        };
    }
}
