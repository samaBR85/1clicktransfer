using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OneClickTransfer.Security;

/// <summary>
/// Fallback multiplataforma (Linux/macOS): AES-256 com chave em arquivo local
/// (permissão 600 no unix). Valores gravados com prefixo "aes1:".
/// Honestidade: protege contra leitura casual do settings.json, não contra outro
/// processo do mesmo usuário (a chave está no disco) — avisado no README.
/// </summary>
public sealed class AesFileKeyProtector : ISecretProtector
{
    public const string Prefix = "aes1:";
    private readonly string _keyPath;

    public AesFileKeyProtector(string keyPath) => _keyPath = keyPath;

    private byte[] GetOrCreateKey()
    {
        if (File.Exists(_keyPath))
            return Convert.FromBase64String(File.ReadAllText(_keyPath).Trim());
        var key = RandomNumberGenerator.GetBytes(32);
        var dir = Path.GetDirectoryName(_keyPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_keyPath, Convert.ToBase64String(key));
        try
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(_keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite); // 600
        }
        catch { }
        return key;
    }

    public string Protect(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        try
        {
            using var aes = Aes.Create();
            aes.Key = GetOrCreateKey();
            aes.GenerateIV();
            using var enc = aes.CreateEncryptor();
            var data = Encoding.UTF8.GetBytes(plain);
            var ct = enc.TransformFinalBlock(data, 0, data.Length);
            var payload = new byte[aes.IV.Length + ct.Length];   // IV (16) + ciphertext
            Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
            Buffer.BlockCopy(ct, 0, payload, aes.IV.Length, ct.Length);
            return Prefix + Convert.ToBase64String(payload);
        }
        catch { return ""; }
    }

    public string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored)) return "";
        try
        {
            var b64 = stored.StartsWith(Prefix, StringComparison.Ordinal)
                ? stored.Substring(Prefix.Length) : stored;
            var payload = Convert.FromBase64String(b64);
            using var aes = Aes.Create();
            aes.Key = GetOrCreateKey();
            var iv = new byte[16];
            Buffer.BlockCopy(payload, 0, iv, 0, 16);
            aes.IV = iv;
            using var dec = aes.CreateDecryptor();
            var pt = dec.TransformFinalBlock(payload, 16, payload.Length - 16);
            return Encoding.UTF8.GetString(pt);
        }
        catch { return ""; }
    }
}
