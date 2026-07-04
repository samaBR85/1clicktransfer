using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using OneClickTransfer.Avalonia.Services;
using OneClickTransfer.Avalonia.ViewModels;
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
            RequestedThemeVariant = Settings.Theme == "light" ? ThemeVariant.Light : ThemeVariant.Dark;

            var window = new MainWindow();

            // Composition root manual (sem container DI).
            AppServices.Dispatcher = new AvaloniaUiDispatcher();
            AppServices.App = new AppControl();
            AppServices.Files = new AvaloniaFilePickerService(() => window);
            AppServices.Clipboard = new AvaloniaClipboardService(() => window);
            AppServices.Notifications = new PlatformNotificationService(() => window.TryGetPlatformHandle()?.Handle ?? System.IntPtr.Zero);
            // Owner = janela ativa (p/ um diálogo aberto de dentro de outro ser modal ao pai correto).
            AppServices.Dialogs = new AvaloniaDialogService(
                () => desktop.Windows.LastOrDefault(w => w.IsActive) ?? desktop.MainWindow);

            var vm = new MainViewModel(Settings, AppServices.Dialogs, AppServices.Dispatcher, AppServices.Clipboard, AppServices.Notifications);
            // Ao salvar Configurar (E9): troca App.Settings, tema e a barra de título.
            vm.SettingsReloaded += s =>
            {
                Settings = s;
                RequestedThemeVariant = s.Theme == "light" ? ThemeVariant.Light : ThemeVariant.Dark;
                WindowsDarkTitleBar.Apply(window, s.Theme != "light");
            };
            window.DataContext = vm;

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
