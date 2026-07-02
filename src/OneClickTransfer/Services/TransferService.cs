using System;
using System.Collections.Generic;
using System.IO;
using FluentFTP;
using Renci.SshNet;
using OneClickTransfer.Models;

namespace OneClickTransfer.Services;

public record RemoteEntry(string Name, bool IsDir, long Size, DateTime Modified);

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

    public static void FtpUpload(Destination d, string srcFile, Action<double>? onProgress)
    {
        using var c = MakeClient(d);
        c.Connect();
        var remote = RemoteFilePath(d, Path.GetFileName(srcFile));
        Action<FtpProgress> p = fp =>
        {
            if (fp.Progress >= 0) onProgress?.Invoke(fp.Progress);
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

    public static void SftpUpload(Destination d, string srcFile, Action<double>? onProgress)
    {
        using var c = MakeSftp(d);
        c.Connect();
        var folder = string.IsNullOrWhiteSpace(d.Folder) ? "/" : d.Folder;
        EnsureSftpDir(c, folder);
        var remote = folder.TrimEnd('/') + "/" + Path.GetFileName(srcFile);
        using var fs = File.OpenRead(srcFile);
        double total = fs.Length;
        c.UploadFile(fs, remote, true, uploaded =>
        {
            if (total > 0) onProgress?.Invoke(uploaded / total * 100.0);
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
    public static void Send(Destination d, string srcFile, Action<double>? onProgress)
    {
        switch (d.Type)
        {
            case DestType.Local: LocalCopy(srcFile, d.Folder); onProgress?.Invoke(100); break;
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
}
