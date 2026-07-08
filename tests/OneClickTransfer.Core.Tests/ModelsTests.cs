using System.IO;
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
    public void SourceSpec_All_Folder_EnumeraRecursivo()
    {
        var tmp = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(tmp, "a.txt"), "x");
            var sub = Directory.CreateDirectory(Path.Combine(tmp, "sub")).FullName;
            File.WriteAllText(Path.Combine(sub, "b.txt"), "y");

            var s = new SourceSpec { Kind = SourceKind.Folder, Path = tmp, Pattern = "*" };

            Assert.Equal(2, s.Count);
            Assert.Contains(s.All, f => f.EndsWith("a.txt"));
            Assert.Contains(s.All, f => f.EndsWith("b.txt"));
        }
        finally { Directory.Delete(tmp, true); }
    }

    [Fact]
    public void SourceSpec_All_Folder_PastaInexistente_RetornaVazio()
    {
        var s = new SourceSpec { Kind = SourceKind.Folder, Path = @"C:\definitely-does-not-exist-xyz" };
        Assert.Empty(s.All);
        Assert.Equal(0, s.Count);
        Assert.Equal("", s.First);
    }

    [Fact]
    public void SourceSpec_All_Folder_RespeitaExcludePatterns()
    {
        var tmp = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var nodeModules = Directory.CreateDirectory(Path.Combine(tmp, "node_modules")).FullName;
            File.WriteAllText(Path.Combine(nodeModules, "x.js"), "x");
            File.WriteAllText(Path.Combine(tmp, "a.txt"), "y");
            File.WriteAllText(Path.Combine(tmp, "b.tmp"), "z");

            var s = new SourceSpec
            {
                Kind = SourceKind.Folder,
                Path = tmp,
                Pattern = "*",
                ExcludePatterns = { "node_modules/", "*.tmp" }
            };

            Assert.Single(s.All);
            Assert.EndsWith("a.txt", s.All[0]);
        }
        finally { Directory.Delete(tmp, true); }
    }

    [Fact]
    public void RelPathFor_Folder_SemKeepRootFolderName_RelativoARaiz()
    {
        var s = new SourceSpec { Kind = SourceKind.Folder, Path = @"C:\src\AppGuide" };
        Assert.Equal(Path.Combine("de", "guide.txt"), s.RelPathFor(@"C:\src\AppGuide\de\guide.txt"));
    }

    [Fact]
    public void RelPathFor_Folder_ComKeepRootFolderName_PrefixaNomeDaPasta()
    {
        var s = new SourceSpec { Kind = SourceKind.Folder, Path = @"C:\src\AppGuide", KeepRootFolderName = true };
        Assert.Equal(Path.Combine("AppGuide", "de", "guide.txt"), s.RelPathFor(@"C:\src\AppGuide\de\guide.txt"));
    }

    [Fact]
    public void RelPathFor_ModoArquivoAvulso_SoONomeDoArquivo()
    {
        var s = new SourceSpec { Kind = SourceKind.File, KeepRootFolderName = true };
        Assert.Equal("guide.txt", s.RelPathFor(@"C:\qualquer\pasta\guide.txt"));
    }

    [Fact]
    public void SavedServer_Clone_Independente()
    {
        var s = new SavedServer { Name = "NAS", Type = DestType.Ftp, Host = "h", Port = 21, Username = "u" };
        var c = s.Clone();
        c.Host = "outro";
        c.Name = "Outro";
        Assert.Equal("h", s.Host);
        Assert.Equal("NAS", s.Name);
    }

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

    [Fact]
    public void Destination_Clone_PreservaForceLegacyPasv()
    {
        var d = new Destination { Type = DestType.Ftp, Host = "h", ForceLegacyPasv = true };
        Assert.True(d.Clone().ForceLegacyPasv);
    }

    [Fact]
    public void SavedServer_Clone_PreservaForceLegacyPasv()
    {
        var s = new SavedServer { Name = "NAS", Type = DestType.Ftp, Host = "h", ForceLegacyPasv = true };
        Assert.True(s.Clone().ForceLegacyPasv);
    }

    [Fact]
    public void Destination_Clone_PreservaVerifyAfterTransfer()
    {
        var d = new Destination { Type = DestType.Ftp, Host = "h", VerifyAfterTransfer = true };
        Assert.True(d.Clone().VerifyAfterTransfer);
    }

    [Fact]
    public void SavedServer_Clone_PreservaVerifyAfterTransfer()
    {
        var s = new SavedServer { Name = "NAS", Type = DestType.Ftp, Host = "h", VerifyAfterTransfer = true };
        Assert.True(s.Clone().VerifyAfterTransfer);
    }
}
