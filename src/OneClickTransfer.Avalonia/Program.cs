using System;
using Avalonia;
using OneClickTransfer.I18n;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia;

internal static class Program
{
    /// <summary>Ponto de entrada. Intercepta a CLI ANTES de montar a UI (paridade com o WPF).</summary>
    [STAThread]
    public static int Main(string[] args)
    {
        UpdateService.CleanupLeftovers();               // remove exe antigo de uma atualizacao anterior
        App.Settings = SettingsService.Load();
        L.Lang = App.Settings.Language == "en" ? "en" : "pt";

        // Modo linha de comando (headless): --task/--all/--list/--help. Sem UI.
        if (CliRunner.IsCli(args))
            return CliRunner.Run(args, App.Settings);

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
