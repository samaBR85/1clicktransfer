using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneClickTransfer.Avalonia.ViewModels.Abstractions;
using OneClickTransfer.I18n;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.ViewModels;

/// <summary>Editor de um destino (local / FTP / SFTP). ShowDialog&lt;Destination?&gt; via CloseRequested.</summary>
public sealed partial class DestinationEditorViewModel : ViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly IFilePickerService _files;
    private bool _typeSync;

    /// <summary>View fecha com este payload (Destination = salvar, null = cancelar).</summary>
    public event Action<Destination?>? CloseRequested;

    public DestinationEditorViewModel(Destination? existing, IDialogService dialogs, IFilePickerService files)
    {
        _dialogs = dialogs;
        _files = files;
        LoadFields(existing ?? new Destination());
    }

    // ---------------- Tipo (rádios) ----------------
    [ObservableProperty] private bool _isLocal;
    [ObservableProperty] private bool _isFtp;
    [ObservableProperty] private bool _isSftp;

    partial void OnIsLocalChanged(bool value) => ApplyTypeChange();
    partial void OnIsFtpChanged(bool value) => ApplyTypeChange();
    partial void OnIsSftpChanged(bool value) => ApplyTypeChange();

    private void ApplyTypeChange()
    {
        if (_typeSync) return;
        if (IsSftp && (Port == "21" || string.IsNullOrWhiteSpace(Port))) Port = "22";
        else if (IsFtp && (Port == "22" || string.IsNullOrWhiteSpace(Port))) Port = "21";
        OnPropertyChanged(nameof(IsServer));
        OnPropertyChanged(nameof(ShowTls));
        OnPropertyChanged(nameof(LocalOpacity));
        OnPropertyChanged(nameof(ServerOpacity));
    }

    public bool IsServer => IsFtp || IsSftp;
    public bool ShowTls => IsFtp;
    public double LocalOpacity => IsLocal ? 1.0 : 0.5;
    public double ServerOpacity => IsServer ? 1.0 : 0.5;

    // ---------------- Campos ----------------
    [ObservableProperty] private string _localFolder = "";
    [ObservableProperty] private string _host = "";
    [ObservableProperty] private string _port = "21";
    [ObservableProperty] private string _remoteFolder = "/";
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private bool _useTls;

    [ObservableProperty] private string _testResult = "";
    [ObservableProperty] private bool _testOk;
    [ObservableProperty] private bool _testError;
    [ObservableProperty] private bool _testing;

    // ---------------- Textos ----------------
    public string WindowTitle => L.T("destEditorTitle");
    public string LocalLabel => L.T("localFolder");
    public string FtpLabel => L.T("ftpServer");
    public string SftpLabel => L.T("sftpServer");
    public string DstFolderLabel => L.T("destFolderLabel");
    public string BrowseLabel => L.T("browse");
    public string HostLabel => L.T("ftpHost");
    public string PortLabel => L.T("ftpPort");
    public string RemoteLabel => L.T("ftpRemote");
    public string BrowseRemoteLabel => L.T("ftpSearch");
    public string UserLabel => L.T("ftpUser");
    public string PassLabel => L.T("ftpPass");
    public string TlsLabel => L.T("ftpTls");
    public string TestLabel => L.T("testConn");
    public string SaveLabel => L.T("save");
    public string CancelLabel => L.T("cancel");

    private void LoadFields(Destination d)
    {
        _typeSync = true;
        if (d.Type == DestType.Ftp || d.Type == DestType.Sftp)
        {
            if (d.Type == DestType.Sftp) IsSftp = true; else IsFtp = true;
            RemoteFolder = string.IsNullOrEmpty(d.Folder) ? "/" : d.Folder;
            LocalFolder = "";
        }
        else
        {
            IsLocal = true;
            LocalFolder = d.Folder;
            RemoteFolder = "/";
        }
        Host = d.Host;
        Port = (d.Port <= 0 ? (d.Type == DestType.Sftp ? 22 : 21) : d.Port).ToString();
        Username = d.Username;
        Password = SecretProtector.Unprotect(d.Password);
        UseTls = d.UseTls;
        _typeSync = false;
        ApplyTypeChange();
    }

    private Destination ReadDest()
    {
        if (IsFtp || IsSftp)
        {
            bool sftp = IsSftp;
            int.TryParse(Port, out var port); if (port <= 0) port = sftp ? 22 : 21;
            return new Destination
            {
                Type = sftp ? DestType.Sftp : DestType.Ftp,
                Host = Host.Trim(),
                Port = port,
                Folder = string.IsNullOrWhiteSpace(RemoteFolder) ? "/" : RemoteFolder.Trim(),
                Username = Username.Trim(),
                Password = SecretProtector.Protect(Password),
                UseTls = !sftp && UseTls
            };
        }
        return new Destination { Type = DestType.Local, Folder = LocalFolder.Trim() };
    }

    private void SetTest(string text, bool ok, bool error)
    {
        TestResult = text; TestOk = ok; TestError = error;
    }

    // ---------------- Comandos ----------------
    [RelayCommand]
    private async Task BrowseLocalAsync()
    {
        var f = await _files.PickFolderAsync(string.IsNullOrWhiteSpace(LocalFolder) ? null : LocalFolder);
        if (!string.IsNullOrEmpty(f)) LocalFolder = f!;
    }

    [RelayCommand]
    private async Task BrowseRemoteAsync()
    {
        if (string.IsNullOrWhiteSpace(Host)) { SetTest("✗ " + L.T("ftpHost"), false, true); return; }
        bool sftp = IsSftp;
        int.TryParse(Port, out var port); if (port <= 0) port = sftp ? 22 : 21;
        var d = new Destination
        {
            Type = sftp ? DestType.Sftp : DestType.Ftp,
            Host = Host.Trim(), Port = port, Folder = "/",
            Username = Username.Trim(),
            Password = SecretProtector.Protect(Password),
            UseTls = !sftp && UseTls
        };
        var start = string.IsNullOrWhiteSpace(RemoteFolder) ? "/" : RemoteFolder.Trim();
        var chosen = await _dialogs.BrowseRemoteFolderAsync(d, start);
        if (chosen != null) RemoteFolder = chosen;
    }

    [RelayCommand]
    private async Task TestAsync()
    {
        var d = ReadDest();
        if ((d.Type != DestType.Ftp && d.Type != DestType.Sftp) || string.IsNullOrWhiteSpace(d.Host))
        {
            SetTest("✗ " + L.T("ftpHost"), false, true);
            return;
        }
        Testing = true;
        SetTest(L.T("testing"), false, false);
        try
        {
            await Task.Run(() => TransferService.TestConnection(d));
            SetTest("✓ " + L.T("connOk"), true, false);
        }
        catch
        {
            SetTest("✗ " + L.T("connFailed"), false, true);
        }
        finally { Testing = false; }
    }

    [RelayCommand]
    private void Ok() => CloseRequested?.Invoke(ReadDest());

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(null);
}
