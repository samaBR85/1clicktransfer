using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OneClickTransfer.Avalonia.Views;
using OneClickTransfer.Models;

namespace OneClickTransfer.Avalonia;

public partial class App : Application
{
    /// <summary>Settings carregados no Program.Main (antes da UI).</summary>
    public static AppSettings Settings { get; set; } = new();

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
