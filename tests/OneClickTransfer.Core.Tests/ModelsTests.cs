using OneClickTransfer.Models;
using Xunit;

namespace OneClickTransfer.Core.Tests;

public class ModelsTests
{
    [Fact]
    public void SourceSpec_All_UsaFiles()
    {
        var s = new SourceSpec { Files = { "a", "b" } };
        Assert.Equal(2, s.Count);
        Assert.Equal("a", s.First);
    }

    [Fact]
    public void SourceSpec_All_CaiPraPathQuandoFilesVazio()
    {
        var s = new SourceSpec { Path = "legacy.txt" };
        Assert.Single(s.All);
        Assert.Equal("legacy.txt", s.All[0]);
    }

    [Fact]
    public void SourceSpec_Vazio_CountZero() => Assert.Equal(0, new SourceSpec().Count);

    [Fact]
    public void TransferJob_SourceFile_UmArquivo()
    {
        var j = new TransferJob { Source = new SourceSpec { Files = { @"C:\dir\plugin.3gx" } } };
        Assert.Equal("plugin.3gx", j.SourceFile);
    }

    [Fact]
    public void TransferJob_SourceFile_VariosMostraMaisN()
    {
        var j = new TransferJob { Source = new SourceSpec { Files = { @"C:\a.txt", @"C:\b.txt", @"C:\c.txt" } } };
        Assert.Equal("a.txt +2", j.SourceFile);
    }

    [Fact]
    public void TransferJob_Summary_TemSetaEDestino()
    {
        var j = new TransferJob
        {
            Source = new SourceSpec { Files = { @"C:\a.txt" } },
            Destinations = { new Destination { Type = DestType.Local, Folder = @"D:\out", Enabled = true } }
        };
        Assert.Contains("a.txt", j.Summary);
        Assert.Contains("→", j.Summary);
    }

    [Fact]
    public void TransferJob_WatchIcon_SoQuandoWatch()
    {
        Assert.Equal("", new TransferJob { Watch = false }.WatchIcon);
        Assert.Equal("\U0001F441", new TransferJob { Watch = true }.WatchIcon);
    }

    [Fact]
    public void TransferJob_Clone_Independente()
    {
        var j = new TransferJob { Name = "A", Watch = true, Source = new SourceSpec { Files = { "x" } } };
        var c = j.Clone();
        c.Source.Files.Add("y");
        c.Name = "B";
        Assert.Equal("A", j.Name);
        Assert.Single(j.Source.Files);
        Assert.True(c.Watch);
    }

    [Fact]
    public void Destination_Clone_Independente()
    {
        var d = new Destination { Type = DestType.Ftp, Host = "h", Port = 21, Enabled = true };
        var c = d.Clone();
        c.Host = "outro";
        c.Enabled = false;
        Assert.Equal("h", d.Host);
        Assert.True(d.Enabled);
    }
}
