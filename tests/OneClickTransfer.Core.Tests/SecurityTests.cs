using System;
using System.IO;
using OneClickTransfer.Security;
using Xunit;

namespace OneClickTransfer.Core.Tests;

public class SecurityTests
{
    [Fact]
    public void Aes_Roundtrip_ComPrefixo()
    {
        var key = Path.Combine(Path.GetTempPath(), "octk_" + Guid.NewGuid().ToString("N")[..8] + ".key");
        try
        {
            var p = new AesFileKeyProtector(key);
            var enc = p.Protect("senha-secreta");
            Assert.StartsWith("aes1:", enc);
            Assert.NotEqual("senha-secreta", enc);
            Assert.Equal("senha-secreta", p.Unprotect(enc));
        }
        finally { try { File.Delete(key); } catch { } }
    }

    [Fact]
    public void Aes_Vazio_RetornaVazio()
    {
        var p = new AesFileKeyProtector(Path.Combine(Path.GetTempPath(), "nope.key"));
        Assert.Equal("", p.Protect(""));
        Assert.Equal("", p.Unprotect(""));
    }

    [Fact]
    public void Dpapi_Roundtrip_SemPrefixo()
    {
        if (!OperatingSystem.IsWindows()) return;   // DPAPI so no Windows
        var enc = DpapiSecretProtector.Instance.Protect("abc123");
        Assert.False(enc.StartsWith("aes1:"));
        Assert.Equal("abc123", DpapiSecretProtector.Instance.Unprotect(enc));
    }
}
