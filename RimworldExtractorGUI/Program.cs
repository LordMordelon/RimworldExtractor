using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
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

            try
            {
                Arrancar();
            }
            catch (Exception e)
            {
                // Ultimo recurso: la aplicacion no tiene consola, asi que una excepcion
                // aca la cierra sin decir absolutamente nada. Se usa MessageBox y no el
                // Aviso propio porque este es el camino para cuando no se puede dar por
                // sentado que el resto del programa funcione.
                MessageBox.Show(e.ToString(), Strings.TitleStartupError);
            }
        }

        /// <summary>
        /// El arranque de verdad. Vive en su propio metodo porque <see cref="Main"/> no puede
        /// nombrar nada de RimworldExtractorInternal: el JIT resuelve las referencias de un
        /// metodo justo antes de ejecutarlo, y esa DLL no esta al lado del ejecutable —el
        /// PostBuild la mueve a "bin"—, asi que se carga gracias al manejador que registra
        /// Main en su primera linea. Nombrarla dentro de Main hace que se intente cargar
        /// antes de que ese manejador exista, y la aplicacion muere antes de abrir nada.
        ///
        /// El atributo no es decorativo: sin el, el JIT puede incorporar este metodo dentro
        /// de Main y el problema vuelve tal cual.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Arrancar()
        {
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
