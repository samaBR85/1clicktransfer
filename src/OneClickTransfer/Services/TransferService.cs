using System;
using System.Collections.Generic;
using System.IO;
using FluentFTP;
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
        var pass = SecretProtector.Unprotect(d.Password);
        var c = new FtpClient(d.Host, d.Username, pass, d.Port <= 0 ? 21 : d.Port);
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

    public static List<RemoteEntry> FtpList(Destination d)
    {
        var list = new List<RemoteEntry>();
        using var c = MakeClient(d);
        c.Connect();
        var path = string.IsNullOrWhiteSpace(d.Folder) ? "/" : d.Folder;
        foreach (var item in c.GetListing(path))
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
}
