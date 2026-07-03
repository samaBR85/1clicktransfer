using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneClickTransfer.Avalonia.ViewModels.Abstractions;
using OneClickTransfer.I18n;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.ViewModels;

/// <summary>Aviso de atualização. Windows: baixa+troca+reinicia. Linux/mac: abre a página do release.</summary>
public sealed partial class UpdateViewModel : ViewModelBase
{
    private readonly UpdateInfo _info;
    private readonly IAppControl _app;
    private bool _busy;

    public event Action<bool>? CloseRequested;

    public UpdateViewModel(UpdateInfo info, IAppControl app)
    {
        _info = info;
        _app = app;
        Notes = string.IsNullOrWhiteSpace(info.Notes)
            ? "—"
            : info.Notes.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
    }

    public string WindowTitle => L.T("updateTitle");
    public string Head => L.T("updateAvailable", _info.Tag);
    public string CurrentText => L.T("updateCurrentVersion", UpdateService.Current.ToString());
    public string WhatsNewLabel => L.T("whatsNew");
    public string Notes { get; }
    public string LaterLabel => L.T("updateLater");
    public string PrimaryLabel => OperatingSystem.IsWindows() ? L.T("updateNow") : L.T("openReleasePage");

    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private bool _progressVisible;
    [ObservableProperty] private string _statusText = "";

    [RelayCommand]
    private async Task UpdateNowAsync()
    {
        if (_busy) return;

        // Fora do Windows: sem self-swap — abre a página do release no navegador.
        if (!OperatingSystem.IsWindows())
        {
            OpenUrl(_info.Url);
            CloseRequested?.Invoke(false);
            return;
        }

        _busy = true;
        ProgressVisible = true;
        StatusText = L.T("updateDownloading");
        try
        {
            var progress = new Progress<double>(p => ProgressValue = p);
            await UpdateService.DownloadAndSwapAsync(_info, progress);
            StatusText = L.T("updateRestarting");
            UpdateService.Restart();
            _app.Shutdown();
        }
        catch (Exception ex)
        {
            _busy = false;
            ProgressVisible = false;
            StatusText = L.T("updateFailed", ex.Message);
        }
    }

    [RelayCommand]
    private void Later() { if (!_busy) CloseRequested?.Invoke(false); }

    private static void OpenUrl(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
        }
        catch { /* sem navegador disponível: ignora */ }
    }
}
