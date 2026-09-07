using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;

namespace AvaloniaSpike;

internal static class Program
{
    /// <summary>
    ///   AvaloniaSpike                     abre la ventana
    ///   AvaloniaSpike --render CARPETA    la dibuja a PNG en claro y en oscuro
    ///
    /// El render sirve para comparar contra el de WinForms sin depender de que la ventana
    /// este visible. Del lado de Avalonia lo hace Avalonia.Headless, que reemplaza al
    /// Control.DrawToBitmap de WinForms.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--render")
            return Dibujar(args[1]);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    /// <summary>El ancho de cada control que interesa, por nombre.</summary>
    private static Dictionary<string, double> Medir(VentanaRutas ventana)
        => new[] { "rotuloRimworld", "rotuloWorkshop", "rutaRimworld", "rutaWorkshop", "botonListo" }
            .ToDictionary(n => n, n => ventana.FindControl<Control>(n)!.Bounds.Width);

    private static int Dibujar(string carpeta)
    {
        Directory.CreateDirectory(carpeta);

        // UseHeadlessDrawing en false es lo que hace que dibuje pixeles de verdad con
        // Skia; en true la ventana existe pero no pinta nada.
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        foreach (var (nombre, variante) in new[]
                 {
                     ("claro", ThemeVariant.Light),
                     ("oscuro", ThemeVariant.Dark)
                 })
        {
            Application.Current!.RequestedThemeVariant = variante;

            var ventana = new VentanaRutas();
            ventana.Show();
            Dispatcher.UIThread.RunJobs();

            var destino = Path.Combine(carpeta, $"Avalonia-{nombre}.png");
            using (var marco = ventana.CaptureRenderedFrame())
            {
                if (marco == null)
                {
                    Console.WriteLine($"{nombre}: no se pudo obtener el marco dibujado");
                    return 1;
                }
                marco.Save(destino);
            }

            Console.WriteLine($"{nombre}: {ventana.Width:0}x{ventana.Height:0} -> {destino}");

            // Lo que en WinForms hay que programar en AutoAjuste: al agrandar, los rotulos
            // se quedan en su columna y los campos se llevan el espacio nuevo. Se mide en
            // vez de mirarlo, que es lo unico que lo convierte en una comprobacion.
            var antes = Medir(ventana);
            ventana.Width += 300;
            Dispatcher.UIThread.RunJobs();
            var despues = Medir(ventana);

            foreach (var (control, ancho) in despues)
            {
                var previo = antes[control];
                var esperado = control.StartsWith("ruta") || control == "botonListo";
                var crecio = ancho > previo + 1;
                var estado = crecio == esperado ? "OK" : "PROBLEMA";
                Console.WriteLine($"    {estado}  {control,-16} {previo:0} -> {ancho:0}"
                                  + (esperado ? "  (tiene que crecer)" : "  (tiene que quedarse)"));
            }

            using (var ancha = ventana.CaptureRenderedFrame())
                ancha?.Save(Path.Combine(carpeta, $"Avalonia-{nombre}-ancha.png"));

            ventana.Close();
        }

        return 0;
    }
}
