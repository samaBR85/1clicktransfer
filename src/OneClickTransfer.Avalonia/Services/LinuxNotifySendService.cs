using System.Diagnostics;
using System.Runtime.Versioning;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.Services;

/// <summary>Notificação no Linux via `notify-send` (freedesktop.org, presente na maioria das
/// distros desktop com libnotify) -- sem pacote NuGet novo. No-op silencioso se o binário
/// não existir (distros minimalistas/headless).</summary>
[SupportedOSPlatform("linux")]
public sealed class LinuxNotifySendService : INotificationService
{
    public void Notify(string title, string message, bool error)
    {
        try
        {
            var psi = new ProcessStartInfo("notify-send")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-u");
            psi.ArgumentList.Add(error ? "critical" : "normal");
            psi.ArgumentList.Add(title);
            psi.ArgumentList.Add(message);
            using var p = Process.Start(psi);
        }
        catch { }
    }
}
