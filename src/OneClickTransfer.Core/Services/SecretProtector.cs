using System;
using System.Security.Cryptography;
using System.Text;

namespace OneClickTransfer.Services;

/// <summary>Criptografia de senha via DPAPI (por usuario do Windows), igual ao v1.</summary>
public static class SecretProtector
{
    public static string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        if (!OperatingSystem.IsWindows()) return "";   // DPAPI e Windows-only (fachada multiplataforma na E2)
        try
        {
            var bytes = Encoding.UTF8.GetBytes(plain);
            var enc = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(enc);
        }
        catch { return ""; }
    }

    public static string Unprotect(string enc)
    {
        if (string.IsNullOrEmpty(enc)) return "";
        if (!OperatingSystem.IsWindows()) return "";   // DPAPI e Windows-only (fachada multiplataforma na E2)
        try
        {
            var bytes = Convert.FromBase64String(enc);
            var dec = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(dec);
        }
        catch { return ""; }
    }
}
