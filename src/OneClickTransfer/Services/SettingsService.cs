using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OneClickTransfer.I18n;
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
        L.Lang = s.Language == "en" ? "en" : "pt";   // p/ nomear tarefas no idioma certo
        s.Shortcut ??= "F4";
        if (s.Shortcut == "F5") s.Shortcut = "F4";   // F5 agora e fixo p/ Atualizar
        s.Source ??= new SourceSpec();
        s.Destinations ??= new();
        s.Profiles ??= new();
        s.ActiveProfile ??= "";
        s.DestGroups ??= new();
        s.Jobs ??= new();

        // Migração: config antiga (Source/Destinations no topo) vira a 1ª tarefa.
        if (s.Jobs.Count == 0)
        {
            s.Jobs.Add(new TransferJob
            {
                Name = "",
                Enabled = true,
                Source = s.Source.Clone(),
                Destinations = s.Destinations.ConvertAll(d => d.Clone()),
                Overwrite = s.OverwriteMode
            });
        }

        // Garante integridade de cada tarefa e nomes automáticos.
        for (int i = 0; i < s.Jobs.Count; i++)
        {
            var j = s.Jobs[i];
            j.Source ??= new SourceSpec();
            j.Destinations ??= new();
            if (string.IsNullOrWhiteSpace(j.Name)) j.Name = DefaultJobName(i);
        }
        if (s.SelectedJob < 0 || s.SelectedJob >= s.Jobs.Count) s.SelectedJob = 0;
        return s;
    }

    /// <summary>Nome padrão "Tarefa N" / "Task N" (1-based).</summary>
    public static string DefaultJobName(int index) => L.T("taskDefault", index + 1);
}
