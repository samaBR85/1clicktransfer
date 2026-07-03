using System;
using OneClickTransfer.Services;
using Xunit;

namespace OneClickTransfer.Core.Tests;

public class UpdateServiceTests
{
    // Current = versao do ENTRY assembly (host de teste) -> tags relativas p/ determinismo.
    private static string NewerTag => $"v{UpdateService.Current.Major + 1}.0.0";
    private const string OlderTag = "v0.0.1";

    private static string ReleaseJson(string tag, bool withExe, string notes = "notas")
    {
        var assets = withExe
            ? "[{\"name\":\"1clickTransfer.exe\",\"browser_download_url\":\"http://x/e.exe\",\"size\":12345}]"
            : "[{\"name\":\"notas.zip\",\"browser_download_url\":\"http://x/z.zip\",\"size\":1}]";
        return $"{{\"tag_name\":\"{tag}\",\"body\":\"{notes}\",\"assets\":{assets}}}";
    }

    [Fact] public void ParseVersion_ComPrefixoV() => Assert.Equal(new Version(2, 1, 0), UpdateService.ParseVersion("v2.1.0"));
    [Fact] public void ParseVersion_SemPrefixo() => Assert.Equal(new Version(2, 0), UpdateService.ParseVersion("2.0"));
    [Fact] public void ParseVersion_ComSufixoBeta() => Assert.Equal(new Version(3, 0, 0), UpdateService.ParseVersion("v3.0.0-beta"));
    [Fact] public void ParseVersion_Lixo_RetornaNull() => Assert.Null(UpdateService.ParseVersion("abc"));

    [Fact]
    public void ParseLatest_VersaoMaisNova_RetornaInfo()
    {
        var info = UpdateService.ParseLatest(ReleaseJson(NewerTag, withExe: true));
        Assert.NotNull(info);
        Assert.Equal(NewerTag, info!.Tag);
        Assert.Equal("http://x/e.exe", info.Url);
        Assert.Equal(12345, info.Size);
        Assert.Equal("notas", info.Notes);
    }

    [Fact]
    public void ParseLatest_VersaoIgualOuMaisVelha_RetornaNull()
        => Assert.Null(UpdateService.ParseLatest(ReleaseJson(OlderTag, withExe: true)));

    [Fact]
    public void ParseLatest_SemAssetExe_RetornaNull()
        => Assert.Null(UpdateService.ParseLatest(ReleaseJson(NewerTag, withExe: false)));
}
