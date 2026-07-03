using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OneClickTransfer.Models;

namespace OneClickTransfer.Services;

/// <summary>Carrega/salva settings.json ao lado do exe (portatil), como no v1.</summary>
public static class SettingsService
{
    public static string SettingsPath =>
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var s = JsonSerializer.Deserialize<AppSettings>(json, _opts);
                if (s != null) return Normalize(s);
            }
        }
        catch { /* ignora e usa padrao */ }
        return new AppSettings();
    }

    public static void Save(AppSettings s)
    {
        try
        {
            var json = JsonSerializer.Serialize(s, _opts);
            File.WriteAllText(SettingsPath, json);
        }
        catch { /* silencioso */ }
    }

    private static AppSettings Normalize(AppSettings s)
    {
        s.Theme ??= "dark";
        s.Language ??= "pt";
        s.Shortcut ??= "F4";
        if (s.Shortcut == "F5") s.Shortcut = "F4";   // F5 agora e fixo p/ Atualizar
        s.Source ??= new SourceSpec();
        s.Destinations ??= new();
        s.Profiles ??= new();
        s.ActiveProfile ??= "";
        s.DestGroups ??= new();
        return s;
    }
}
