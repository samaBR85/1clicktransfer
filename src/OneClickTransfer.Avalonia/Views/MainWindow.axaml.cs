using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace OneClickTransfer.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    // DEMO E5 (temporario): prova a troca de ThemeVariant em runtime.
    private void ToggleTheme_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Application.Current is { } app)
            app.RequestedThemeVariant =
                app.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark;
    }
}
