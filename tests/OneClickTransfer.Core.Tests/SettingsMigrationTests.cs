using System;
using System.IO;
using OneClickTransfer.Models;
using OneClickTransfer.Services;
using Xunit;

namespace OneClickTransfer.Core.Tests;

public class SettingsMigrationTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public SettingsMigrationTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "octest_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "settings.json");
    }

    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private AppSettings LoadJson(string json)
    {
        File.WriteAllText(_path, json);
        return SettingsService.Load(_path);
    }

    [Fact]
    public void ConfigAntiga_SourceDestinationsNoTopo_ViraJob0()
    {
        var s = LoadJson("""
        { "source": { "path": "C:\\x\\plugin.3gx" },
          "destinations": [ { "type": "local", "folder": "D:\\out", "enabled": true } ],
          "overwriteMode": "ifNewer" }
        """);
        Assert.Single(s.Jobs);
        Assert.Equal("C:\\x\\plugin.3gx", s.Jobs[0].Source.All[0]);
        Assert.Single(s.Jobs[0].Destinations);
        Assert.Equal(OverwriteMode.IfNewer, s.Jobs[0].Overwrite);
        Assert.False(string.IsNullOrWhiteSpace(s.Jobs[0].Name)); // nome automatico
    }

    [Fact]
    public void OrigemAntiga_Path_ViraFiles()
    {
        var s = LoadJson("""{ "jobs": [ { "name": "A", "source": { "path": "C:\\a.txt" }, "destinations": [] } ] }""");
        Assert.Single(s.Jobs[0].Source.Files);
        Assert.Equal("C:\\a.txt", s.Jobs[0].Source.All[0]);
    }

    [Fact]
    public void OrigemPasta_NaoMigraPathParaFiles()
    {
        var s = LoadJson("""{ "jobs": [ { "name": "A", "source": { "kind": "folder", "path": "C:\\alguma\\pasta" }, "destinations": [] } ] }""");
        Assert.Empty(s.Jobs[0].Source.Files);   // Path e a pasta, nao um arquivo -- nao pode virar item de Files
        Assert.Equal(SourceKind.Folder, s.Jobs[0].Source.Kind);
    }

    [Theory]
    [InlineData(10, 150)]     // abaixo do minimo -> padrao
    [InlineData(9999, 600)]   // acima do maximo -> teto
    [InlineData(220, 220)]    // valido -> preservado
    public void TasksHeight_Clamp(double input, double expected)
    {
        var s = LoadJson($"{{ \"tasksHeight\": {input}, \"jobs\": [ {{ \"name\": \"A\", \"source\": {{ \"files\": [] }}, \"destinations\": [] }} ] }}");
        Assert.Equal(expected, s.TasksHeight);
    }

    [Theory]
    [InlineData(10, 160)]     // abaixo do minimo -> padrao
    [InlineData(9999, 500)]   // acima do maximo -> teto
    [InlineData(220, 220)]    // valido -> preservado
    public void QueueHeight_Clamp(double input, double expected)
    {
        var s = LoadJson($"{{ \"queueHeight\": {input}, \"jobs\": [ {{ \"name\": \"A\", \"source\": {{ \"files\": [] }}, \"destinations\": [] }} ] }}");
        Assert.Equal(expected, s.QueueHeight);
    }

    [Fact]
    public void ShortcutF5_ViraF4()
    {
        var s = LoadJson("""{ "shortcut": "F5", "jobs": [ { "name": "A", "source": { "files": [] }, "destinations": [] } ] }""");
        Assert.Equal("F4", s.Shortcut);
    }

    [Fact]
    public void SelectedJob_ForaDoRange_ViraZero()
    {
        var s = LoadJson("""{ "selectedJob": 9, "jobs": [ { "name": "A", "source": { "files": [] }, "destinations": [] } ] }""");
        Assert.Equal(0, s.SelectedJob);
    }

    [Fact]
    public void WatchEnabledGlobalAntigo_EhIgnorado_TarefasNascemSemWatch()
    {
        var s = LoadJson("""{ "watchEnabled": true, "jobs": [ { "name": "A", "source": { "files": [] }, "destinations": [] } ] }""");
        Assert.False(s.Jobs[0].Watch);
    }

    [Fact]
    public void SemArquivo_RetornaNormalizadoComUmaTarefa()
    {
        var s = SettingsService.Load(Path.Combine(_dir, "naoexiste.json"));
        Assert.Single(s.Jobs);
    }

    [Theory]
    [InlineData(0, 3)]     // abaixo do minimo -> padrao
    [InlineData(99, 3)]    // acima do maximo -> padrao
    [InlineData(5, 5)]     // valido -> preservado
    public void MaxParallelDestinations_ForaDoRange_ViraPadrao(int input, int expected)
    {
        var s = LoadJson($"{{ \"maxParallelDestinations\": {input}, \"jobs\": [ {{ \"name\": \"A\", \"source\": {{ \"files\": [] }}, \"destinations\": [] }} ] }}");
        Assert.Equal(expected, s.MaxParallelDestinations);
    }

    [Fact]
    public void SavedServers_AusenteNoJson_ViraListaVazia()
    {
        var s = LoadJson("""{ "jobs": [ { "name": "A", "source": { "files": [] }, "destinations": [] } ] }""");
        Assert.NotNull(s.SavedServers);
        Assert.Empty(s.SavedServers);
    }

    [Fact]
    public void ExcludePatterns_AusenteNoJson_ViraListaVazia()
    {
        var s = LoadJson("""{ "jobs": [ { "name": "A", "source": { "files": [] }, "destinations": [] } ] }""");
        Assert.NotNull(s.Jobs[0].Source.ExcludePatterns);
        Assert.Empty(s.Jobs[0].Source.ExcludePatterns);
    }
}
