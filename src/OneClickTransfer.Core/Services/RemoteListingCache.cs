using System;
using System.Collections.Generic;
using System.Linq;

namespace OneClickTransfer.Services;

/// <summary>Cache em memória de uma listagem remota (FTP/SFTP), sem I/O de rede -- construído uma
/// vez a partir de RemoteEntry já coletados, respondendo exists/modified-time por caminho relativo
/// sem reconectar. Comparação de "é mais novo" (normalização UTC) fica em TransferService, não
/// aqui, pra não duplicar essa lógica em dois lugares.</summary>
public sealed class RemoteListingCache
{
    private readonly Dictionary<string, RemoteEntry> _byRelPath;

    public RemoteListingCache(IEnumerable<(string relDir, RemoteEntry entry)> entries)
    {
        _byRelPath = new Dictionary<string, RemoteEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var (relDir, entry) in entries)
        {
            var key = Normalize(string.IsNullOrEmpty(relDir) ? entry.Name : relDir.TrimEnd('/') + "/" + entry.Name);
            _byRelPath[key] = entry;
        }
    }

    public bool Exists(string relPath) => _byRelPath.ContainsKey(Normalize(relPath));

    /// <summary>Kind cru do RemoteEntry (FTP: Utc via MLSD; SFTP: Local via SSH.NET) -- normalizar
    /// pra UTC é responsabilidade de quem compara (mesmo padrão de TransferService.IsSourceNewer).</summary>
    public DateTime? Modified(string relPath)
        => _byRelPath.TryGetValue(Normalize(relPath), out var e) ? e.Modified : null;

    private static string Normalize(string p) => p.Replace('\\', '/').TrimStart('/');
}
