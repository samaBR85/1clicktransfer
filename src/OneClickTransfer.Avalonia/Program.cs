using System;
using Avalonia;
using OneClickTransfer.Avalonia.Services;
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

        // 1 instância por caminho de executável -- 2ª tentativa do MESMO exe só ativa a que já
        // está aberta e sai; exe em outra pasta (outra versão) roda livre.
        var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        var guard = new SingleInstanceGuard(exePath);
        if (!guard.IsPrimary)
        {
            guard.NotifyExistingInstance();
            return 0;
        }
        App.InstanceGuard = guard;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
