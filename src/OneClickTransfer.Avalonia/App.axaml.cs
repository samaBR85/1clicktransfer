using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OneClickTransfer.Avalonia.Services;
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
        {
            var window = new MainWindow();

            // Composition root manual (sem container DI).
            AppServices.Dispatcher = new AvaloniaUiDispatcher();
            AppServices.App = new AppControl();
            AppServices.Files = new AvaloniaFilePickerService(() => window);
            AppServices.Dialogs = new AvaloniaDialogService(() => desktop.MainWindow);

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
