using OneClickTransfer.Avalonia.ViewModels;
using OneClickTransfer.I18n;
using Xunit;

namespace OneClickTransfer.Avalonia.Tests;

public class TransferQueueItemTests
{
    [Fact]
    public void Success_verificado_mostra_done_verified()
    {
        var it = new TransferQueueItem { State = QueueItemState.Success, Verified = true };
        Assert.Equal(L.T("queueDoneVerified"), it.ResultText);
    }

    [Fact]
    public void Success_nao_verificado_mostra_done()
    {
        var it = new TransferQueueItem { State = QueueItemState.Success, Verified = false };
        Assert.Equal(L.T("queueDone"), it.ResultText);
    }

    [Fact]
    public void Skipped_mapeia_como_pulado_nem_ativo_nem_falha()
    {
        var it = new TransferQueueItem { State = QueueItemState.Skipped };
        Assert.False(it.IsActive);
        Assert.False(it.IsFailed);
        Assert.False(it.IsQueued);
        Assert.Equal(L.T("queueSkipped"), it.ResultText);
    }

    [Fact]
    public void Queued_habilita_toggle_e_vem_incluido_por_padrao()
    {
        var it = new TransferQueueItem { State = QueueItemState.Queued };
        Assert.True(it.IsQueued);
        Assert.True(it.IsActive);
        Assert.True(it.Included);
    }
}
