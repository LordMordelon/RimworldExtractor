using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using RimworldExtractorInternal;

namespace AvaloniaSpike;

/// <summary>
/// El equivalente de FormInitialPathSelect. Solo maquetacion: los botones no hacen nada,
/// porque lo que se compara es como se escribe y se mantiene la interfaz.
/// </summary>
public partial class VentanaRutas : Window
{
    public VentanaRutas()
    {
        InitializeComponent();
        AplicarTextos();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Mismo Strings.cs que usa la aplicacion de verdad. Y a diferencia de WinForms, aca
    /// termina donde empezo: se asignan los textos y nada mas. No hay que medir nada ni
    /// correr controles de lugar, porque de eso se encarga la Grid.
    /// </summary>
    private void AplicarTextos()
    {
        Title = Strings.TitleInitialPathSelect;
        this.FindControl<TextBlock>("rotuloRimworld")!.Text = Strings.LabelRimworldPathShort;
        this.FindControl<TextBlock>("rotuloWorkshop")!.Text = Strings.LabelWorkshopPathShort;
        this.FindControl<Button>("botonListo")!.Content = Strings.BtnDone;

        // Rutas de ejemplo, para que el render se parezca al de la aplicacion real.
        this.FindControl<TextBox>("rutaRimworld")!.Text = @"D:\SteamLibrary\steamapps\common\RimWorld";
        this.FindControl<TextBox>("rutaWorkshop")!.Text = @"D:\SteamLibrary\steamapps\workshop\content\294100";
    }
}
