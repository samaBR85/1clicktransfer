using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.Services;

/// <summary>Notificação no macOS via NSUserNotification (P/Invoke ao runtime Objective-C --
/// API antiga porém ainda funcional nas versões suportadas). NÃO pode ser testado nesta
/// máquina Windows -- escrito defensivamente (try/catch em toda chamada nativa); o usuário
/// ajusta/valida no Mac dele depois, mesmo fluxo já usado pro .app bundle.</summary>
[SupportedOSPlatform("macos")]
public sealed class MacNotificationService : INotificationService
{
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ret(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ret_arg(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_void_arg(IntPtr receiver, IntPtr selector, IntPtr arg1);

    public void Notify(string title, string message, bool error)
    {
        try
        {
            var notifClass = objc_getClass("NSUserNotification");
            var alloc = objc_msgSend_ret(notifClass, sel_registerName("alloc"));
            var notification = objc_msgSend_ret(alloc, sel_registerName("init"));

            SetString(notification, "setTitle:", title);
            SetString(notification, "setInformativeText:", message);

            var centerClass = objc_getClass("NSUserNotificationCenter");
            var center = objc_msgSend_ret(centerClass, sel_registerName("defaultUserNotificationCenter"));
            objc_msgSend_void_arg(center, sel_registerName("deliverNotification:"), notification);
        }
        catch { /* best-effort -- ajustar/validar no Mac real */ }
    }

    private static void SetString(IntPtr target, string selectorName, string value)
    {
        try
        {
            var nsStringClass = objc_getClass("NSString");
            var utf8Ptr = Marshal.StringToHGlobalAuto(value);
            try
            {
                var nsStr = objc_msgSend_ret_arg(nsStringClass, sel_registerName("stringWithUTF8String:"), utf8Ptr);
                objc_msgSend_void_arg(target, sel_registerName(selectorName), nsStr);
            }
            finally { Marshal.FreeHGlobal(utf8Ptr); }
        }
        catch { }
    }
}
