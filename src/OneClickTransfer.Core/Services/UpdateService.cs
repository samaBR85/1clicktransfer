using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace OneClickTransfer.Services;

/// <summary>Info da release mais nova encontrada no GitHub.</summary>
public record UpdateInfo(Version Version, string Tag, string Url, string Notes, long Size);

/// <summary>
/// Auto-update via GitHub Releases. Consulta a última release, compara com a versão
/// atual do exe e, se mais nova, baixa e troca o exe em execução (portátil), reiniciando.
/// </summary>
public static class UpdateService
{
    public const string Repo = "samaBR85/1clicktransfer";
    private const string ExeName = "1clickTransfer.exe";
    private const string WinZipMarker = "win-x64";

    public static Version Current
    {
        get
        {
            // GetEntryAssembly: a versao vem do csproj do APP (exe), nao do Core.dll.
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var v = asm.GetName().Version ?? new Version(0, 0, 0);
            return Norm(v);
        }
    }

    private static Version Norm(Version v) => new(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));

    public static string ExePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, ExeName);

    private static HttpClient MakeHttp()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("1clickTransfer-updater");
        h.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return h;
    }

    /// <summary>Consulta a última release. Retorna info se houver versão MAIS NOVA (com o .zip do win-x64 anexado), senão null.</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        using var http = MakeHttp();
        var json = await http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest");
        return ParseLatest(json);
    }

    /// <summary>Separado p/ teste: interpreta o JSON da API e decide se há novidade.</summary>
    public static UpdateInfo? ParseLatest(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? "") : "";
        var notes = root.TryGetProperty("body", out var b) ? (b.GetString() ?? "") : "";
        var ver = ParseVersion(tag);
        if (ver == null || Norm(ver) <= Current) return null;

        string url = ""; long size = 0;
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                    name.Contains(WinZipMarker, StringComparison.OrdinalIgnoreCase))
                {
                    url = a.TryGetProperty("browser_download_url", out var u) ? (u.GetString() ?? "") : "";
                    size = a.TryGetProperty("size", out var sz) && sz.TryGetInt64(out var s) ? s : 0;
                    break;
                }
            }
        if (string.IsNullOrEmpty(url)) return null;   // release sem zip do win-x64 anexado
        return new UpdateInfo(Norm(ver), tag, url, notes, size);
    }

    /// <summary>"v2.1.0" / "2.1.0-beta" -> Version(2,1,0). null se não parsear.</summary>
    public static Version? ParseVersion(string tag)
    {
        var t = (tag ?? "").TrimStart('v', 'V').Trim();
        var main = new string(t.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray());
        return Version.TryParse(main, out var v) ? v : null;
    }

    public static async Task DownloadAsync(UpdateInfo info, string destPath, IProgress<double>? progress)
    {
        using var http = MakeHttp();
        using var resp = await http.GetAsync(info.Url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? info.Size;
        using var src = await resp.Content.ReadAsStreamAsync();
        using var dst = File.Create(destPath);
        var buffer = new byte[81920];
        long read = 0; int n;
        while ((n = await src.ReadAsync(buffer)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n));
            read += n;
            if (total > 0) progress?.Report(read / (double)total * 100.0);
        }
    }

    /// <summary>Baixa o zip, valida o tamanho, extrai o exe de dentro e troca o exe em execução; deixa pronto p/ reiniciar.</summary>
    public static async Task DownloadAndSwapAsync(UpdateInfo info, IProgress<double>? progress)
    {
        var zipPath = ExePath + ".zip.tmp";
        try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
        await DownloadAsync(info, zipPath, progress);

        // Sanidade: se a release informou o tamanho, o zip baixado precisa bater.
        var len = new FileInfo(zipPath).Length;
        if (info.Size > 0 && len != info.Size)
            throw new IOException($"Download incompleto ({len}/{info.Size} bytes).");

        var newp = ExePath + ".new";
        try { if (File.Exists(newp)) File.Delete(newp); } catch { }
        ExtractExeFromZip(zipPath, newp);
        try { File.Delete(zipPath); } catch { }

        SwapExe(newp);
    }

    /// <summary>Extrai o 1clickTransfer.exe de dentro do zip da release para destExePath.</summary>
    private static void ExtractExeFromZip(string zipPath, string destExePath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.Entries.FirstOrDefault(e => e.Name.Equals(ExeName, StringComparison.OrdinalIgnoreCase))
            ?? throw new IOException($"Zip da release não contém {ExeName}.");
        entry.ExtractToFile(destExePath, overwrite: true);
    }

    /// <summary>Renomeia o exe em execução para .old e coloca o novo no lugar (permitido no Windows).</summary>
    public static void SwapExe(string newExe)
    {
        var len = new FileInfo(newExe).Length;
        if (len < 1_000_000)   // um exe self-contained tem dezenas de MB; algo muito pequeno é suspeito
            throw new IOException("Arquivo baixado inválido (muito pequeno).");
        var exe = ExePath;
        var oldp = exe + ".old";
        try { if (File.Exists(oldp)) File.Delete(oldp); } catch { }
        File.Move(exe, oldp);      // renomear o exe em execução é permitido no Windows
        File.Move(newExe, exe);    // novo exe no lugar
    }

    /// <summary>Reinicia o app (novo exe já está no lugar).</summary>
    public static void Restart()
        => Process.Start(new ProcessStartInfo(ExePath) { UseShellExecute = true });

    /// <summary>Na inicialização: remove o exe antigo/baixado deixado por uma atualização.</summary>
    public static void CleanupLeftovers()
    {
        try
        {
            var exe = ExePath;
            foreach (var p in new[] { exe + ".old", exe + ".new", exe + ".zip.tmp" })
                if (File.Exists(p)) { try { File.Delete(p); } catch { } }
        }
        catch { }
    }
}
