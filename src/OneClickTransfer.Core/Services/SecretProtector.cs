using System;
using System.IO;
using OneClickTransfer.Security;

namespace OneClickTransfer.Services;

/// <summary>
/// Fachada estática de proteção de senha (call sites do TransferService intocados).
/// Windows: DPAPI (base64 sem prefixo — retrocompatível com v1/v2).
/// Linux/macOS: AES com chave local (valores com prefixo "aes1:").
/// Regra do Unprotect: prefixo "aes1:" → AES; sem prefixo → DPAPI (Windows).
/// </summary>
public static class SecretProtector
{
    /// <summary>Provedor ativo (substituível em testes).</summary>
    public static ISecretProtector Provider { get; set; } = CreateDefault();

    public static ISecretProtector CreateDefault()
        => OperatingSystem.IsWindows()
            ? DpapiSecretProtector.Instance
            : new AesFileKeyProtector(DefaultKeyPath);

    /// <summary>Chave AES ao lado do settings.json (unix).</summary>
    public static string DefaultKeyPath
        => Path.Combine(Path.GetDirectoryName(SettingsService.SettingsPath) ?? ".", "secret.key");

    public static string Protect(string plain) => Provider.Protect(plain);

    public static string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored)) return "";
        if (stored.StartsWith(AesFileKeyProtector.Prefix, StringComparison.Ordinal))
            return (Provider as AesFileKeyProtector ?? new AesFileKeyProtector(DefaultKeyPath)).Unprotect(stored);
        // Sem prefixo = DPAPI legado; fora do Windows é indecifrável (vinculado à máquina/usuário).
        return OperatingSystem.IsWindows() ? DpapiSecretProtector.Instance.Unprotect(stored) : "";
    }
}
