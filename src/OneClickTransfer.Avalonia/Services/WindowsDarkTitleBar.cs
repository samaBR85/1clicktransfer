using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace OneClickTransfer.Avalonia.Services;

/// <summary>
/// Title bar escura no Windows (DWM), via HWND do Avalonia. No-op fora do Windows —
/// lá o tema da decoração segue o SO/desktop.
/// </summary>
public static class WindowsDarkTitleBar
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public static void Apply(Window window, bool dark)
    {
        if (!OperatingSystem.IsWindows()) return;
        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero) return;
        try
        {
            int v = dark ? 1 : 0;
            SetAttr(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int));
        }
        catch { }
    }

    [SupportedOSPlatform("windows")]
    [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
    private static extern int SetAttr(IntPtr hwnd, int attr, ref int value, int size);
}
