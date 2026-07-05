using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.Services;

/// <summary>Toast do Windows via balão de bandeja transitório (Shell_NotifyIcon) -- sem WinRT,
/// sem pacote novo, funciona em app não empacotado (single-file). O ícone é adicionado, mostra
/// o balão e é removido pouco depois (não fica residente na bandeja).</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsToastNotificationService : INotificationService
{
    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_MODIFY = 0x00000001;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_INFO = 0x00000010;
    private const uint NIIF_USER = 0x00000004;
    private const uint NIIF_LARGE_ICON = 0x00000020;
    private const int IDI_APPLICATION = 32512;
    private const int IconId = 0x1C71C;   // arbitrário, único o bastante p/ não colidir

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uVersionOrTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

    [DllImport("user32.dll")]
    private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractIconW(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    private static readonly Lazy<IntPtr> _appIcon = new(ExtractAppIcon);

    private static IntPtr ExtractAppIcon()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                var icon = ExtractIconW(IntPtr.Zero, exePath, 0);
                if (icon != IntPtr.Zero && icon != new IntPtr(1)) return icon;
            }
        }
        catch { }
        return LoadIcon(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
    }

    private readonly Func<IntPtr> _hwnd;
    public WindowsToastNotificationService(Func<IntPtr> hwnd) => _hwnd = hwnd;

    public void Notify(string title, string message, bool error)
    {
        try
        {
            var hwnd = _hwnd();
            if (hwnd == IntPtr.Zero) return;
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = hwnd,
                uID = IconId,
                uFlags = NIF_ICON | NIF_MESSAGE | NIF_INFO,
                hIcon = _appIcon.Value,
                szTip = "1-Click Transfer",
                szInfo = message.Length > 255 ? message[..255] : message,
                uVersionOrTimeout = 8000,
                szInfoTitle = title.Length > 63 ? title[..63] : title,
                dwInfoFlags = NIIF_USER | NIIF_LARGE_ICON
            };
            Shell_NotifyIcon(NIM_ADD, ref data);
            Shell_NotifyIcon(NIM_MODIFY, ref data);
            var removeData = data;
            _ = new Timer(_ =>
            {
                try { Shell_NotifyIcon(NIM_DELETE, ref removeData); } catch { }
            }, null, 12000, Timeout.Infinite);
        }
        catch { }
    }
}
