using System;
using System.IO;
using OneClickTransfer.Models;
using OneClickTransfer.Services;
using Xunit;

namespace OneClickTransfer.Core.Tests;

public class TransferServiceTests : IDisposable
{
    private readonly string _root;
    public TransferServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "octs_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
    }
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    private string MakeFile(string name, string content)
    {
        var p = Path.Combine(_root, name);
        File.WriteAllText(p, content);
        return p;
    }
    private Destination LocalDest(string sub)
    {
        var d = Path.Combine(_root, sub);
        Directory.CreateDirectory(d);
        return new Destination { Type = DestType.Local, Folder = d, Enabled = true };
    }

    [Fact]
    public void Send_Local_ReportaProgresso100()
    {
        var src = MakeFile("big.bin", new string('x', 5000));
        var dst = LocalDest("d");
        TransferProgress? got = null;
        TransferService.Send(dst, src, p => got = p);
        Assert.True(File.Exists(Path.Combine(dst.Folder, "big.bin")));
        Assert.NotNull(got);
        Assert.Equal(100, got!.Value.Percent);
        Assert.Equal(got.Value.Total, got.Value.Transferred);
        Assert.Equal(0, got.Value.BytesPerSec);   // local = instantaneo, sem taxa
    }

    [Fact]
    public void Send_NxM_LocalCopiaTudo()
    {
        var f1 = MakeFile("a.txt", "A");
        var f2 = MakeFile("b.txt", "B");
        var dA = LocalDest("A");
        var dB = LocalDest("B");
        int sent = 0;
        foreach (var f in new[] { f1, f2 })
            foreach (var d in new[] { dA, dB })
            { TransferService.Send(d, f, null); sent++; }
        Assert.Equal(4, sent);
        Assert.True(File.Exists(Path.Combine(dA.Folder, "a.txt")));
        Assert.True(File.Exists(Path.Combine(dB.Folder, "b.txt")));
        Assert.Equal("B", File.ReadAllText(Path.Combine(dB.Folder, "b.txt")));
    }

    [Fact]
    public void IsSourceNewer_DestinoInexistente_True()
    {
        var src = MakeFile("s.txt", "s");
        Assert.True(TransferService.IsSourceNewer(LocalDest("z"), src, "s.txt"));
    }

    [Fact]
    public void IsSourceNewer_DestinoMaisNovo_False()
    {
        var src = MakeFile("s.txt", "s");
        var dst = LocalDest("z");
        TransferService.Send(dst, src, null);                 // copia
        File.SetLastWriteTime(Path.Combine(dst.Folder, "s.txt"), DateTime.Now.AddHours(1)); // dest mais novo
        Assert.False(TransferService.IsSourceNewer(dst, src, "s.txt"));
    }
}
