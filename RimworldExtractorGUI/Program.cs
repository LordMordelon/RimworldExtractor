using System.Diagnostics;
using System.Reflection;
using RimworldExtractorInternal;

namespace RimworldExtractorGUI
{
    /*
    * TODO: https://github.com/RimWorldKorea/RMK/discussions/496#discussioncomment-8651025
    */
    internal static class Program
    {
        /// <summary>
        /// Lo genera automaticamente la GitHub Action antes de publicar
        /// </summary>
        internal const string VERSION = "";
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            // La configuracion se lee antes de la primera ventana para arrancar ya con el
            // tema elegido. FormMain la vuelve a leer enseguida: si el archivo no sirve,
            // el que avisa es el, asi que aca el error se ignora a proposito.
            try
            {
                Prefabs.Load();
            }
            catch
            {
                // Queda el tema por defecto, que es el de Windows.
            }

            Tema.Aplicar();
            if (!File.Exists("Prefabs.dat"))
            {
                var formInitialPathSelect = new FormInitialPathSelect();
                formInitialPathSelect.StartPosition = FormStartPosition.CenterScreen;
                if (formInitialPathSelect.ShowDialog() != DialogResult.OK)
                {
                    Aviso.Mostrar(Strings.CompleteFolderSelection);
                    return;
                }
                // Application.Run();
            }

            var formMain = new FormMain();
            formMain.StartPosition = FormStartPosition.CenterScreen;
            Application.Run(formMain);
        }

        private static Assembly? CurrentDomainOnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            // Ignore missing resources
            if (args.Name.Contains(".resources"))
                return null;

            // check for assemblies already loaded
            Assembly? assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
            if (assembly != null)
                return assembly;

            string filename = args.Name.Split(',')[0] + ".dll";
            // Relativo al ejecutable, no al directorio de trabajo: si no, lanzar la app
            // desde otra carpeta (un acceso directo, por ejemplo) no encuentra el "bin"
            // donde el PostBuild deja las dependencias, y revienta al arrancar.
            var assemblyFilePath = Path.Combine(AppContext.BaseDirectory, "bin", filename);

            if (File.Exists(assemblyFilePath))
            {
                try
                {
                    return Assembly.LoadFrom(assemblyFilePath);
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }
    }
}
