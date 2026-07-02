using System;
using System.Windows;
using OneClickTransfer.I18n;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer;

public partial class App : Application
{
    public static AppSettings Settings { get; set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Settings = SettingsService.Load();
        L.Lang = Settings.Language == "en" ? "en" : "pt";
        ThemeManager.Apply(Settings.Theme);
        new MainWindow().Show();
    }
}

public static class ThemeManager
{
    public static void Apply(string theme)
    {
        var file = theme == "light" ? "Light.xaml" : "Dark.xaml";
        var dict = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{file}", UriKind.Absolute)
        };
        var merged = Application.Current.Resources.MergedDictionaries;
        // Indice 0 = tema (Dark/Light); indice 1 = Controls
        if (merged.Count > 0) merged[0] = dict;
        else merged.Add(dict);
    }
}
