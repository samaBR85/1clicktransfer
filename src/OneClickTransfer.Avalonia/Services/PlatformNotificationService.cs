using System;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.Services;

/// <summary>Escolhe a implementação certa por SO (mesmo padrão de ISecretProtector); no-op se
/// nenhuma bater ou se a chamada falhar.</summary>
public sealed class PlatformNotificationService : INotificationService
{
    private readonly INotificationService _impl;

    public PlatformNotificationService(Func<IntPtr> windowsHwnd)
    {
        _impl = OperatingSystem.IsWindows() ? new WindowsToastNotificationService(windowsHwnd)
            : OperatingSystem.IsLinux() ? new LinuxNotifySendService()
            : OperatingSystem.IsMacOS() ? new MacNotificationService()
            : new NullNotificationService();
    }

    public void Notify(string title, string message, bool error)
    {
        try { _impl.Notify(title, message, error); } catch { }
    }
}
