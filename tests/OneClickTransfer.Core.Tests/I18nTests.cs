using OneClickTransfer.I18n;
using Xunit;

namespace OneClickTransfer.Core.Tests;

public class I18nTests
{
    [Fact]
    public void T_ChaveConhecida_PtEEn()
    {
        L.Lang = "pt";
        Assert.Equal("Configurações", L.T("settingsTitle"));
        L.Lang = "en";
        Assert.Equal("Settings", L.T("settingsTitle"));
    }

    [Fact]
    public void T_ChaveInexistente_RetornaAPropriaChave()
    {
        Assert.Equal("nao_existe_essa_chave", L.T("nao_existe_essa_chave"));
    }

    [Fact]
    public void T_ComArgumentos_Formata()
    {
        L.Lang = "en";
        Assert.Equal("Task 3", L.T("taskDefault", 3));
    }
}
