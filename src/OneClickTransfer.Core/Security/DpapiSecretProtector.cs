using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace OneClickTransfer.Security;

/// <summary>
/// DPAPI (CurrentUser) — Windows. Formato do valor: base64 puro, SEM prefixo
/// (retrocompatível com todos os settings.json existentes do v1/v2).
/// </summary>
public sealed class DpapiSecretProtector : ISecretProtector
{
    public static readonly DpapiSecretProtector Instance = new();

    public string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        if (!OperatingSystem.IsWindows()) return "";
        try
        {
            var bytes = Encoding.UTF8.GetBytes(plain);
            var enc = Protect(bytes);
            return Convert.ToBase64String(enc);
        }
        catch { return ""; }
    }

    public string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored)) return "";
        if (!OperatingSystem.IsWindows()) return "";
        try
        {
            var bytes = Convert.FromBase64String(stored);
            var dec = Unprotect(bytes);
            return Encoding.UTF8.GetString(dec);
        }
        catch { return ""; }
    }

    [SupportedOSPlatform("windows")]
    private static byte[] Protect(byte[] data) => ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

    [SupportedOSPlatform("windows")]
    private static byte[] Unprotect(byte[] data) => ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
}
