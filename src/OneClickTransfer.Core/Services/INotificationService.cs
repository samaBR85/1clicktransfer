namespace OneClickTransfer.Services;

/// <summary>Notificação do sistema (toast do SO), ao concluir/falhar uma transferência.
/// Implementações por plataforma vivem no lado Avalonia (integração de UI/SO); esta
/// interface fica no Core pra permitir um no-op no CLI headless (sem lifetime de UI).</summary>
public interface INotificationService
{
    void Notify(string title, string message, bool error);
}

/// <summary>No-op: usado no CLI headless (sem tray/toast) e como fallback seguro.</summary>
public sealed class NullNotificationService : INotificationService
{
    public void Notify(string title, string message, bool error) { }
}
