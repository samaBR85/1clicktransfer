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
    private static string? _settingsPath;

    /// <summary>
    /// Windows: sempre ao lado do exe (portátil). Linux/macOS: ao lado do binário SE a pasta
    /// for gravável; senão ~/.config/1clicktransfer/ (o .app do macOS bloqueia escrita local).
    /// </summary>
    public static string SettingsPath => _settingsPath ??= ResolveSettingsPath();

    private static string ResolveSettingsPath()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "settings.json");
        if (OperatingSystem.IsWindows()) return local;
        try
        {
            var probe = Path.Combine(AppContext.BaseDirectory, ".writeprobe");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return local;
        }
        catch
        {
            var cfg = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "1clicktransfer");
            Directory.CreateDirectory(cfg);
            return Path.Combine(cfg, "settings.json");
        }
    }

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static AppSettings Load() => Load(SettingsPath);

    /// <summary>Carrega+normaliza de um caminho específico (usado em testes).</summary>
    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var s = JsonSerializer.Deserialize<AppSettings>(json, _opts);
                if (s != null) return Normalize(s);
            }
        }
        catch { /* ignora e usa padrao */ }
        return Normalize(new AppSettings());
    }

    public static void Save(AppSettings s) => Save(s, SettingsPath);

    /// <summary>Serializa para um caminho específico (usado em testes).</summary>
    public static void Save(AppSettings s, string path)
    {
        try
        {
            var json = JsonSerializer.Serialize(s, _opts);
            File.WriteAllText(path, json);
        }
        catch { /* silencioso */ }
    }

    /// <summary>Serializa o objeto para JSON (mesmas opções do arquivo). Para testes/diff.</summary>
    public static string Serialize(AppSettings s) => JsonSerializer.Serialize(s, _opts);

    private static AppSettings Normalize(AppSettings s)
    {
        s.Theme ??= "dark";
        s.Language ??= "en";
        L.Lang = s.Language == "en" ? "en" : "pt";   // p/ nomear tarefas no idioma certo
        s.Shortcut ??= "F4";
        if (s.Shortcut == "F5") s.Shortcut = "F4";   // F5 agora e fixo p/ Atualizar
        s.Source ??= new SourceSpec();
        s.Destinations ??= new();
        s.Profiles ??= new();
        s.ActiveProfile ??= "";
        s.DestGroups ??= new();
        s.Jobs ??= new();
        s.SavedServers ??= new();
        if (s.MaxParallelDestinations < 1 || s.MaxParallelDestinations > 8) s.MaxParallelDestinations = 3;

        if (double.IsNaN(s.TasksHeight) || s.TasksHeight < 140) s.TasksHeight = 150;
        if (s.TasksHeight > 600) s.TasksHeight = 600;

        if (double.IsNaN(s.QueueHeight) || s.QueueHeight < 90) s.QueueHeight = 160;
        if (s.QueueHeight > 500) s.QueueHeight = 500;

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
            j.Source.Files ??= new();
            j.Source.ExcludePatterns ??= new();
            // Migração: origem antiga de 1 arquivo (Path) vira a lista Files.
            // So p/ Kind==File -- em Kind==Folder, Path e a pasta em si, nao um arquivo.
            if (j.Source.Kind == SourceKind.File && j.Source.Files.Count == 0 && !string.IsNullOrEmpty(j.Source.Path))
                j.Source.Files.Add(j.Source.Path);
            j.Destinations ??= new();
            if (string.IsNullOrWhiteSpace(j.Name)) j.Name = DefaultJobName(i);
        }
        if (s.SelectedJob < 0 || s.SelectedJob >= s.Jobs.Count) s.SelectedJob = 0;
        return s;
    }

    /// <summary>Nome padrão "Tarefa N" / "Task N" (1-based).</summary>
    public static string DefaultJobName(int index) => L.T("taskDefault", index + 1);
}
