using System;
using System.Collections.Generic;
using System.IO;
using FluentFTP;
using Renci.SshNet;
using OneClickTransfer.Models;

namespace OneClickTransfer.Services;

public record RemoteEntry(string Name, bool IsDir, long Size, DateTime Modified);

/// <summary>Progresso de uma transferência: % , bytes enviados, total e velocidade (bytes/seg).</summary>
public readonly record struct TransferProgress(double Percent, long Transferred, long Total, double BytesPerSec);

/// <summary>Transferencias local e FTP/FTPS. SFTP entra no v2.0 (SSH.NET).</summary>
public static class TransferService
{
    // ---------------- Local ----------------
    public static void LocalCopy(string srcFile, string destFolder)
    {
        Directory.CreateDirectory(destFolder);
        var dst = Path.Combine(destFolder, Path.GetFileName(srcFile));
        File.Copy(srcFile, dst, true);
    }

    public static bool LocalExists(string destFolder, string fileName)
        => File.Exists(Path.Combine(destFolder, fileName));

    public static IEnumerable<RemoteEntry> LocalList(string folder)
    {
        var list = new List<RemoteEntry>();
        if (!Directory.Exists(folder)) return list;
        foreach (var d in Directory.GetDirectories(folder))
        {
            var di = new DirectoryInfo(d);
            list.Add(new RemoteEntry(di.Name, true, 0, di.LastWriteTime));
        }
        foreach (var f in Directory.GetFiles(folder))
        {
            var fi = new FileInfo(f);
            list.Add(new RemoteEntry(fi.Name, false, fi.Length, fi.LastWriteTime));
        }
        return list;
    }

    // ---------------- FTP ----------------
    private static FtpClient MakeClient(Destination d)
    {
        // FTP anonimo: se usuario vazio, usa "anonymous" (o FluentFTP nao aceita usuario vazio,
        // e servidores sem autenticacao -- ex.: ftpd do Luma/3DS -- aceitam anonymous).
        var user = string.IsNullOrWhiteSpace(d.Username) ? "anonymous" : d.Username;
        var pass = SecretProtector.Unprotect(d.Password);
        if (string.IsNullOrEmpty(pass) && user == "anonymous") pass = "anonymous@";
        var c = new FtpClient(d.Host, user, pass, d.Port <= 0 ? 21 : d.Port);
        c.Config.ConnectTimeout = 8000;
        c.Config.DataConnectionConnectTimeout = 8000;
        c.Config.ReadTimeout = 15000;
        if (d.UseTls)
        {
            c.Config.EncryptionMode = FtpEncryptionMode.Explicit;
            c.Config.ValidateAnyCertificate = true;
        }
        return c;
    }

    private static string RemoteFilePath(Destination d, string fileName)
    {
        var baseP = string.IsNullOrWhiteSpace(d.Folder) ? "/" : d.Folder;
        return baseP.TrimEnd('/') + "/" + fileName;
    }

    public static void FtpUpload(Destination d, string srcFile, Action<TransferProgress>? onProgress)
    {
        long total = new FileInfo(srcFile).Length;
        using var c = MakeClient(d);
        c.Connect();
        var remote = RemoteFilePath(d, Path.GetFileName(srcFile));
        Action<FtpProgress> p = fp =>
        {
            if (fp.Progress >= 0)
                onProgress?.Invoke(new TransferProgress(fp.Progress, fp.TransferredBytes, total, fp.TransferSpeed));
        };
        c.UploadFile(srcFile, remote, FtpRemoteExists.Overwrite, true, FtpVerify.None, p);
        c.Disconnect();
    }

    public static bool FtpExists(Destination d, string fileName)
    {
        using var c = MakeClient(d);
        c.Connect();
        var ok = c.FileExists(RemoteFilePath(d, fileName));
        c.Disconnect();
        return ok;
    }

    public static DateTime? FtpModified(Destination d, string fileName)
    {
        try
        {
            using var c = MakeClient(d);
            c.Connect();
            var t = c.GetModifiedTime(RemoteFilePath(d, fileName));
            c.Disconnect();
            return t == DateTime.MinValue ? (DateTime?)null : t;
        }
        catch { return null; }
    }

    public static List<RemoteEntry> FtpList(Destination d) => FtpListPath(d, d.Folder);

    public static List<RemoteEntry> FtpListPath(Destination d, string path)
    {
        var list = new List<RemoteEntry>();
        using var c = MakeClient(d);
        c.Connect();
        var p = string.IsNullOrWhiteSpace(path) ? "/" : path;
        foreach (var item in c.GetListing(p))
        {
            list.Add(new RemoteEntry(
                item.Name,
                item.Type == FtpObjectType.Directory,
                item.Size < 0 ? 0 : item.Size,
                item.Modified));
        }
        c.Disconnect();
        return list;
    }

    public static void FtpTestConnection(Destination d)
    {
        using var c = MakeClient(d);
        c.Connect();
        c.GetListing(string.IsNullOrWhiteSpace(d.Folder) ? "/" : d.Folder);
        c.Disconnect();
    }

    // ---------------- SFTP (SSH.NET) ----------------
    private static SftpClient MakeSftp(Destination d)
    {
        var user = string.IsNullOrWhiteSpace(d.Username) ? "anonymous" : d.Username;
        var pass = SecretProtector.Unprotect(d.Password);
        var c = new SftpClient(d.Host, d.Port <= 0 ? 22 : d.Port, user, pass);
        c.ConnectionInfo.Timeout = TimeSpan.FromSeconds(15);
        return c;
    }

    private static void EnsureSftpDir(SftpClient c, string dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || dir == "/") return;
        var cur = "";
        foreach (var part in dir.Trim('/').Split('/'))
        {
            if (part.Length == 0) continue;
            cur += "/" + part;
            try { if (!c.Exists(cur)) c.CreateDirectory(cur); } catch { }
        }
    }

    public static void SftpUpload(Destination d, string srcFile, Action<TransferProgress>? onProgress)
    {
        using var c = MakeSftp(d);
        c.Connect();
        var folder = string.IsNullOrWhiteSpace(d.Folder) ? "/" : d.Folder;
        EnsureSftpDir(c, folder);
        var remote = folder.TrimEnd('/') + "/" + Path.GetFileName(srcFile);
        using var fs = File.OpenRead(srcFile);
        long total = fs.Length;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        long lastBytes = 0; double lastMs = 0;
        c.UploadFile(fs, remote, true, uploaded =>
        {
            long done = (long)uploaded;
            double nowMs = sw.Elapsed.TotalMilliseconds;
            double dt = nowMs - lastMs;
            double bps = 0;
            if (dt >= 250) { bps = (done - lastBytes) / (dt / 1000.0); lastBytes = done; lastMs = nowMs; }
            double pct = total > 0 ? done / (double)total * 100.0 : 0;
            onProgress?.Invoke(new TransferProgress(pct, done, total, bps));
        });
        c.Disconnect();
    }

    public static List<RemoteEntry> SftpListPath(Destination d, string path)
    {
        var list = new List<RemoteEntry>();
        using var c = MakeSftp(d);
        c.Connect();
        var p = string.IsNullOrWhiteSpace(path) ? "/" : path;
        foreach (var f in c.ListDirectory(p))
        {
            if (f.Name is "." or "..") continue;
            list.Add(new RemoteEntry(f.Name, f.IsDirectory, f.Length < 0 ? 0 : f.Length, f.LastWriteTime));
        }
        c.Disconnect();
        return list;
    }

    public static bool SftpExists(Destination d, string fileName)
    {
        using var c = MakeSftp(d);
        c.Connect();
        var folder = string.IsNullOrWhiteSpace(d.Folder) ? "/" : d.Folder;
        var ok = c.Exists(folder.TrimEnd('/') + "/" + fileName);
        c.Disconnect();
        return ok;
    }

    public static DateTime? SftpModified(Destination d, string fileName)
    {
        try
        {
            using var c = MakeSftp(d);
            c.Connect();
            var folder = string.IsNullOrWhiteSpace(d.Folder) ? "/" : d.Folder;
            var full = folder.TrimEnd('/') + "/" + fileName;
            DateTime? t = c.Exists(full) ? c.GetLastWriteTime(full) : null;
            c.Disconnect();
            return t;
        }
        catch { return null; }
    }

    public static void SftpTest(Destination d)
    {
        using var c = MakeSftp(d);
        c.Connect();
        c.ListDirectory(string.IsNullOrWhiteSpace(d.Folder) ? "/" : d.Folder);
        c.Disconnect();
    }

    // ---------------- Dispatchers por tipo ----------------
    public static void Send(Destination d, string srcFile, Action<TransferProgress>? onProgress)
    {
        switch (d.Type)
        {
            case DestType.Local:
            {
                long size = new FileInfo(srcFile).Length;
                LocalCopy(srcFile, d.Folder);
                onProgress?.Invoke(new TransferProgress(100, size, size, 0));
                break;
            }
            case DestType.Ftp: FtpUpload(d, srcFile, onProgress); break;
            case DestType.Sftp: SftpUpload(d, srcFile, onProgress); break;
        }
    }

    public static bool DestExists(Destination d, string fileName) => d.Type switch
    {
        DestType.Local => LocalExists(d.Folder, fileName),
        DestType.Ftp => FtpExists(d, fileName),
        DestType.Sftp => SftpExists(d, fileName),
        _ => false
    };

    public static DateTime? DestModified(Destination d, string fileName) => d.Type switch
    {
        DestType.Ftp => FtpModified(d, fileName),
        DestType.Sftp => SftpModified(d, fileName),
        _ => null
    };

    /// <summary>A origem é mais nova que o arquivo homônimo no destino? (modo "Substituir se for mais recente")</summary>
    public static bool IsSourceNewer(Destination d, string sourcePath, string fileName)
    {
        var srcT = File.GetLastWriteTime(sourcePath);
        if (d.Type == DestType.Local)
        {
            var dst = Path.Combine(d.Folder, fileName);
            if (!File.Exists(dst)) return true;
            return srcT > File.GetLastWriteTime(dst);
        }
        var rt = DestModified(d, fileName);
        return rt == null || srcT > rt.Value;
    }

    public static List<RemoteEntry> ListPath(Destination d, string path) => d.Type switch
    {
        DestType.Local => new List<RemoteEntry>(LocalList(path)),
        DestType.Ftp => FtpListPath(d, path),
        DestType.Sftp => SftpListPath(d, path),
        _ => new List<RemoteEntry>()
    };

    public static List<RemoteEntry> ListDest(Destination d) => ListPath(d, d.Folder);

    public static void TestConnection(Destination d)
    {
        if (d.Type == DestType.Ftp) FtpTestConnection(d);
        else if (d.Type == DestType.Sftp) SftpTest(d);
    }

    // ---------------- Operações de arquivo/pasta (menu de contexto do DESTINATION) ----------------
    public static void CreateFolder(Destination d, string parentPath, string name)
    {
        switch (d.Type)
        {
            case DestType.Local:
                Directory.CreateDirectory(Path.Combine(parentPath, name));
                break;
            case DestType.Ftp:
            {
                using var c = MakeClient(d);
                c.Connect();
                c.CreateDirectory(parentPath.TrimEnd('/') + "/" + name);
                c.Disconnect();
                break;
            }
            case DestType.Sftp:
            {
                using var c = MakeSftp(d);
                c.Connect();
                c.CreateDirectory(parentPath.TrimEnd('/') + "/" + name);
                c.Disconnect();
                break;
            }
        }
    }

    public static void Delete(Destination d, string path, bool isDir)
    {
        switch (d.Type)
        {
            case DestType.Local:
                if (isDir) Directory.Delete(path, true); else File.Delete(path);
                break;
            case DestType.Ftp:
            {
                using var c = MakeClient(d);
                c.Connect();
                if (isDir) c.DeleteDirectory(path); else c.DeleteFile(path);
                c.Disconnect();
                break;
            }
            case DestType.Sftp:
            {
                using var c = MakeSftp(d);
                c.Connect();
                if (isDir) c.DeleteDirectory(path); else c.DeleteFile(path);
                c.Disconnect();
                break;
            }
        }
    }

    public static void Rename(Destination d, string oldPath, string newPath)
    {
        switch (d.Type)
        {
            case DestType.Local:
                if (Directory.Exists(oldPath)) Directory.Move(oldPath, newPath);
                else File.Move(oldPath, newPath);
                break;
            case DestType.Ftp:
            {
                using var c = MakeClient(d);
                c.Connect();
                c.Rename(oldPath, newPath);
                c.Disconnect();
                break;
            }
            case DestType.Sftp:
            {
                using var c = MakeSftp(d);
                c.Connect();
                c.RenameFile(oldPath, newPath);
                c.Disconnect();
                break;
            }
        }
    }
}
