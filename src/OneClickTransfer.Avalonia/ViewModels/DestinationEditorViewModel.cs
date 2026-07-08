using System;
using System.Collections.ObjectModel;
using System.Linq;
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
    private readonly AppSettings _s;
    private readonly IDialogService _dialogs;
    private readonly IFilePickerService _files;
    private bool _typeSync;
    private bool _svrSync;

    /// <summary>View fecha com este payload (Destination = salvar, null = cancelar).</summary>
    public event Action<Destination?>? CloseRequested;

    public DestinationEditorViewModel(Destination? existing, AppSettings s, IDialogService dialogs, IFilePickerService files)
    {
        _s = s;
        _dialogs = dialogs;
        _files = files;
        LoadFields(existing ?? new Destination());
        ReloadSavedServers();
    }

    // ---------------- Tipo (rádios) ----------------
    [ObservableProperty] private bool _isLocal;
    [ObservableProperty] private bool _isFtp;
    [ObservableProperty] private bool _isSftp;

    partial void OnIsLocalChanged(bool value) { if (_typeSync) return; if (value) ClearExcept(0); ApplyTypeChange(); }
    partial void OnIsFtpChanged(bool value) { if (_typeSync) return; if (value) ClearExcept(1); ApplyTypeChange(); }
    partial void OnIsSftpChanged(bool value) { if (_typeSync) return; if (value) ClearExcept(2); ApplyTypeChange(); }

    // Exclusividade mútua sem depender do GroupName da UI (VM auto-consistente/testável).
    private void ClearExcept(int keep)
    {
        _typeSync = true;
        if (keep != 0) IsLocal = false;
        if (keep != 1) IsFtp = false;
        if (keep != 2) IsSftp = false;
        _typeSync = false;
    }

    private void ApplyTypeChange()
    {
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
    [ObservableProperty] private bool _forceLegacyPasv;
    [ObservableProperty] private bool _verifyAfterTransfer;

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
    public string PasvLabel => L.T("ftpLegacyPasv");
    public string VerifyLabel => L.T("verifyAfterTransfer");
    public string TestLabel => L.T("testConn");
    public string SaveLabel => L.T("save");
    public string CancelLabel => L.T("cancel");
    public string SavedServerLabel => L.T("savedServerLabel");
    public string SaveServerLabel => L.T("saveServer");
    public string DeleteServerLabel => L.T("deleteServer");

    // ---------------- Servidores FTP/SFTP salvos ----------------
    public ObservableCollection<string> SavedServerOptions { get; } = new();
    [ObservableProperty] private int _selectedSavedServerIndex;

    private void ReloadSavedServers()
    {
        _svrSync = true;
        SavedServerOptions.Clear();
        SavedServerOptions.Add(L.T("selectItem"));
        foreach (var srv in _s.SavedServers) SavedServerOptions.Add(srv.Name);
        SelectedSavedServerIndex = 0;
        _svrSync = false;
    }

    partial void OnSelectedSavedServerIndexChanged(int value)
    {
        if (_svrSync || value <= 0 || value >= SavedServerOptions.Count) return;
        var name = SavedServerOptions[value];
        var srv = _s.SavedServers.FirstOrDefault(x => x.Name == name);
        if (srv == null) return;
        _typeSync = true;
        if (srv.Type == DestType.Sftp) { IsSftp = true; IsFtp = false; IsLocal = false; }
        else { IsFtp = true; IsSftp = false; IsLocal = false; }
        Host = srv.Host;
        Port = srv.Port.ToString();
        Username = srv.Username;
        Password = SecretProtector.Unprotect(srv.Password);
        UseTls = srv.UseTls;
        ForceLegacyPasv = srv.ForceLegacyPasv;
        VerifyAfterTransfer = srv.VerifyAfterTransfer;
        _typeSync = false;
        ApplyTypeChange();
    }

    [RelayCommand]
    private async Task SavedServerSaveAsync()
    {
        if (!IsFtp && !IsSftp) return;
        var suggest = SelectedSavedServerIndex > 0 ? SavedServerOptions[SelectedSavedServerIndex] : "";
        var name = await _dialogs.PromptAsync(L.T("saveServer"), L.T("serverNamePrompt"), suggest);
        if (string.IsNullOrWhiteSpace(name)) return;
        var srv = new SavedServer
        {
            Name = name.Trim(),
            Type = IsSftp ? DestType.Sftp : DestType.Ftp,
            Host = Host.Trim(),
            Port = int.TryParse(Port, out var p) && p > 0 ? p : (IsSftp ? 22 : 21),
            Username = Username.Trim(),
            Password = SecretProtector.Protect(Password),
            UseTls = IsFtp && UseTls,
            ForceLegacyPasv = IsFtp && ForceLegacyPasv,
            VerifyAfterTransfer = VerifyAfterTransfer
        };
        var idx = _s.SavedServers.FindIndex(x => x.Name == srv.Name);
        if (idx >= 0) _s.SavedServers[idx] = srv; else _s.SavedServers.Add(srv);
        SettingsService.Save(_s);
        ReloadSavedServers();
        var i = SavedServerOptions.IndexOf(srv.Name);
        if (i >= 0) { _svrSync = true; SelectedSavedServerIndex = i; _svrSync = false; }
    }

    [RelayCommand]
    private async Task SavedServerDeleteAsync()
    {
        if (SelectedSavedServerIndex <= 0) { await _dialogs.ShowMessageAsync(L.T("savedServerLabel"), L.T("selectItem")); return; }
        var name = SavedServerOptions[SelectedSavedServerIndex];
        if (!await _dialogs.ConfirmAsync(L.T("deleteServer"), name)) return;
        _s.SavedServers.RemoveAll(x => x.Name == name);
        SettingsService.Save(_s);
        ReloadSavedServers();
    }

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
        ForceLegacyPasv = d.ForceLegacyPasv;
        VerifyAfterTransfer = d.VerifyAfterTransfer;
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
                UseTls = !sftp && UseTls,
                ForceLegacyPasv = !sftp && ForceLegacyPasv,
                VerifyAfterTransfer = VerifyAfterTransfer
            };
        }
        return new Destination { Type = DestType.Local, Folder = LocalFolder.Trim(), VerifyAfterTransfer = VerifyAfterTransfer };
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
            UseTls = !sftp && UseTls,
            ForceLegacyPasv = !sftp && ForceLegacyPasv
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
