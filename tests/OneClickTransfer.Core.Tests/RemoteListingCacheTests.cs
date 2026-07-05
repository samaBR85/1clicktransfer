using System;
using OneClickTransfer.Services;
using Xunit;

namespace OneClickTransfer.Core.Tests;

public class RemoteListingCacheTests
{
    private static RemoteEntry File(string name, DateTime modified) => new(name, false, 10, modified);

    [Fact]
    public void Exists_ArquivoNaRaiz_True()
    {
        var cache = new RemoteListingCache(new[] { ("", File("a.txt", DateTime.UtcNow)) });
        Assert.True(cache.Exists("a.txt"));
        Assert.False(cache.Exists("b.txt"));
    }

    [Fact]
    public void Exists_ArquivoEmSubpasta_UsaPrefixoDaSubpasta()
    {
        var cache = new RemoteListingCache(new[] { ("de", File("guide.txt", DateTime.UtcNow)) });
        Assert.True(cache.Exists("de/guide.txt"));
        Assert.True(cache.Exists(@"de\guide.txt"));   // barra invertida (relPath local) tem que casar
        Assert.False(cache.Exists("guide.txt"));      // sem o prefixo da subpasta não bate
    }

    [Fact]
    public void Exists_CaseInsensitive()
    {
        var cache = new RemoteListingCache(new[] { ("", File("Report.PDF", DateTime.UtcNow)) });
        Assert.True(cache.Exists("report.pdf"));
    }

    [Fact]
    public void Modified_RetornaHorarioDoEntry()
    {
        var t = new DateTime(2026, 7, 4, 23, 27, 42, DateTimeKind.Utc);
        var cache = new RemoteListingCache(new[] { ("de", File("guide.txt", t)) });
        Assert.Equal(t, cache.Modified("de/guide.txt"));
    }

    [Fact]
    public void Modified_ArquivoInexistente_Null()
    {
        var cache = new RemoteListingCache(Array.Empty<(string, RemoteEntry)>());
        Assert.Null(cache.Modified("nada.txt"));
    }
}
