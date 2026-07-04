using System;
using System.Diagnostics;

namespace OneClickTransfer.Avalonia.ViewModels;

/// <summary>Abre uma URL no navegador padrão (cross-platform). BCL puro (sem Avalonia).</summary>
internal static class LinkOpener
{
    public static void Open(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
        }
        catch { /* sem navegador disponível: ignora */ }
    }
}
