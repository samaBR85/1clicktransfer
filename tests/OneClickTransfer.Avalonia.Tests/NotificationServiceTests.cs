using OneClickTransfer.Avalonia.Services;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.Tests;

public class NotificationServiceTests
{
    [Fact]
    public void NullNotificationService_NuncaLanca()
        => new NullNotificationService().Notify("t", "m", false);

    [Fact]
    public void PlatformNotificationService_ConstrucaoENotifyNuncaLancam_IndependenteDoSO()
    {
        var svc = new PlatformNotificationService(() => System.IntPtr.Zero);
        svc.Notify("Título", "Mensagem", false);
        svc.Notify("Falhou", "Detalhe do erro", true);
    }
}
