using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using OneClickTransfer.Avalonia.Services;
using OneClickTransfer.Avalonia.ViewModels;
using OneClickTransfer.Avalonia.Views;
using OneClickTransfer.I18n;
using OneClickTransfer.Models;

namespace OneClickTransfer.Avalonia;

public partial class App : Application
{
    /// <summary>Settings carregados no Program.Main (antes da UI).</summary>
    public static AppSettings Settings { get; set; } = new();

    /// <summary>Setada pelo item "Sair" da bandeja antes do Shutdown — bypassa o minimizar-ao-fechar.</summary>
    public static bool IsReallyExiting { get; set; }

    /// <summary>Setado pelo Program.Main quando esta é a instância primária (mesmo caminho de exe).</summary>
    public static SingleInstanceGuard? InstanceGuard { get; set; }

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
            SetupTrayIcon(window, vm, desktop);

            InstanceGuard?.StartListening(() =>
                Dispatcher.UIThread.Post(() => ShowMainWindow(window)));
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Ícone na bandeja: Abrir / Enviar todas as tarefas / Sair (Sair bypassa minimizar-ao-fechar).
    private static void SetupTrayIcon(MainWindow window, MainViewModel vm, IClassicDesktopStyleApplicationLifetime desktop)
    {
        var openItem = new NativeMenuItem(L.T("trayOpen"));
        openItem.Click += (_, _) => ShowMainWindow(window);

        var sendAllItem = new NativeMenuItem(L.T("traySendAll"));
        sendAllItem.Click += (_, _) => vm.TransferCommand.Execute(null);

        var exitItem = new NativeMenuItem(L.T("trayExit"));
        exitItem.Click += (_, _) =>
        {
            IsReallyExiting = true;
            desktop.Shutdown();
        };

        var menu = new NativeMenu { openItem, sendAllItem, exitItem };

        var tray = new TrayIcon
        {
            Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://1clickTransfer/Assets/app.ico"))),
            ToolTipText = L.T("appTitle"),
            Menu = menu,
        };
        tray.Clicked += (_, _) => ShowMainWindow(window);

        TrayIcon.SetIcons(Current!, new TrayIcons { tray });
    }

    private static void ShowMainWindow(MainWindow window) => window.RestoreFromTray();
}
