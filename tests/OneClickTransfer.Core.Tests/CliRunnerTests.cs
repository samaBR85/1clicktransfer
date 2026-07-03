using System;
using System.IO;
using OneClickTransfer.Models;
using OneClickTransfer.Services;
using Xunit;

namespace OneClickTransfer.Core.Tests;

public class CliRunnerTests : IDisposable
{
    private readonly string _root;
    private readonly TextWriter _realOut;

    public CliRunnerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "octcli_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
        _realOut = Console.Out;
    }
    public void Dispose() { Console.SetOut(_realOut); try { Directory.Delete(_root, true); } catch { } }

    private (string src, string dstA, string dstB) MakeDirs()
    {
        var src = Path.Combine(_root, "src");
        var dstA = Path.Combine(_root, "dstA");
        var dstB = Path.Combine(_root, "dstB");
        foreach (var d in new[] { src, dstA, dstB }) Directory.CreateDirectory(d);
        File.WriteAllText(Path.Combine(src, "a.txt"), "A");
        File.WriteAllText(Path.Combine(src, "b.txt"), "B");
        return (src, dstA, dstB);
    }

    private AppSettings Settings()
    {
        var (src, dstA, dstB) = MakeDirs();
        return new AppSettings
        {
            Jobs =
            {
                new TransferJob { Name = "Enviar", Enabled = true, Overwrite = OverwriteMode.Always,
                    Source = new SourceSpec { Files = { Path.Combine(src, "a.txt"), Path.Combine(src, "b.txt") } },
                    Destinations = { new Destination { Type = DestType.Local, Folder = dstA, Enabled = true },
                                     new Destination { Type = DestType.Local, Folder = dstB, Enabled = true } } },
                new TransferJob { Name = "Desligada", Enabled = false, Overwrite = OverwriteMode.Always,
                    Source = new SourceSpec { Files = { Path.Combine(src, "a.txt") } },
                    Destinations = { new Destination { Type = DestType.Local, Folder = dstA, Enabled = true } } },
            }
        };
    }

    private string Capture(Func<int> act, out int code)
    {
        var sw = new StringWriter();
        Console.SetOut(sw);
        try { code = act(); } finally { Console.SetOut(_realOut); }
        return sw.ToString();
    }

    [Fact] public void IsCli_DetectaVerbos()
    {
        Assert.True(CliRunner.IsCli(new[] { "--task", "x" }));
        Assert.True(CliRunner.IsCli(new[] { "--all" }));
        Assert.False(CliRunner.IsCli(Array.Empty<string>()));
        Assert.False(CliRunner.IsCli(new[] { "arg-solto" }));
    }

    [Fact] public void Help_Exit0()
    {
        var outp = Capture(() => CliRunner.Run(new[] { "--help" }, new AppSettings()), out var c);
        Assert.Equal(0, c);
        Assert.Contains("--task", outp);
    }

    [Fact] public void List_Exit0_MostraTarefas()
    {
        var outp = Capture(() => CliRunner.Run(new[] { "--list" }, Settings()), out var c);
        Assert.Equal(0, c);
        Assert.Contains("Enviar", outp);
        Assert.Contains("Desligada", outp);
    }

    [Fact] public void Task_Valida_Exit0_Envia4()
    {
        var s = Settings();
        var outp = Capture(() => CliRunner.Run(new[] { "--task", "Enviar" }, s), out var c);
        Assert.Equal(0, c);
        Assert.Equal(4, outp.Split("OK").Length - 1);   // 2 arquivos x 2 destinos
    }

    [Fact] public void Task_Inexistente_Exit2()
    {
        var _ = Capture(() => CliRunner.Run(new[] { "--task", "NaoExiste" }, Settings()), out var c);
        Assert.Equal(2, c);
    }

    [Fact] public void All_SoTarefasMarcadas()
    {
        var outp = Capture(() => CliRunner.Run(new[] { "--all" }, Settings()), out var c);
        Assert.Equal(0, c);
        Assert.Equal(4, outp.Split("OK").Length - 1);   // so a marcada (2x2); a desligada nao envia
    }

    [Fact] public void ArgDesconhecido_Exit2()
    {
        var _ = Capture(() => CliRunner.Run(new[] { "--task", "Enviar", "--zzz" }, Settings()), out var c);
        Assert.Equal(2, c);
    }

    [Fact] public void Silent_SemSaida_MasEnvia()
    {
        var s = Settings();
        var outp = Capture(() => CliRunner.Run(new[] { "--task", "Enviar", "--silent" }, s), out var c);
        Assert.Equal(0, c);
        Assert.Equal("", outp.Trim());
        Assert.True(File.Exists(Path.Combine(s.Jobs[0].Destinations[0].Folder, "a.txt")));
    }

    [Fact] public void Never_ArquivoExistente_Pula()
    {
        var s = Settings();
        s.Jobs[0].Overwrite = OverwriteMode.Never;
        File.WriteAllText(Path.Combine(s.Jobs[0].Destinations[0].Folder, "a.txt"), "old"); // ja existe
        var outp = Capture(() => CliRunner.Run(new[] { "--task", "Enviar" }, s), out var _);
        Assert.Contains("SKIP", outp);
    }
}
