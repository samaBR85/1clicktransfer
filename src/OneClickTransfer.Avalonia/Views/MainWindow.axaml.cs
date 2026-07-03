using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace OneClickTransfer.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    // DEMO E5 (temporario): prova a troca de ThemeVariant em runtime.
    private void ToggleTheme_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant =
                app.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    // DEMO E7 (temporario): smoke do PromptDialog escuro.
    private async void Dialog_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
        => await Services.AppServices.Dialogs.PromptAsync("Renomear", "Nome da tarefa:", "Tarefa 1");
}
