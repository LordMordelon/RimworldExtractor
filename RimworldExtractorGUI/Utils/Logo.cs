using System.Drawing;

namespace RimworldExtractorGUI
{
    /// <summary>
    /// El logo de la aplicacion, para la barra de titulo de las ventanas.
    ///
    /// El del ejecutable lo pone ApplicationIcon en el .csproj, pero WinForms no lo usa: cada
    /// Form arranca con su icono generico, asi que hay que asignarlo en cada una. Se carga del
    /// .ico embebido, con todos sus tamaños, y no con Icon.ExtractAssociatedIcon, que devuelve
    /// solo el de 32 px y en la barra de titulo se veria borroso.
    ///
    /// Una sola instancia para todas las ventanas: Form no libera el icono que se le asigna.
    /// </summary>
    internal static class Logo
    {
        internal static Icon Icono { get; } = Cargar();

        private static Icon Cargar()
        {
            using var recurso = typeof(Logo).Assembly.GetManifestResourceStream("RimworldExtractorGUI.logo.ico")!;
            return new Icon(recurso);
        }
    }
}
