using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneClickTransfer.I18n;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.ViewModels;

/// <summary>Item de listagem remota (pasta ou "voltar").</summary>
public sealed class FtpBrowserItem
{
    public string Display { get; set; } = "";
    public string? Name { get; set; }
    public bool IsUp { get; set; }
}

/// <summary>Navegador de pastas remotas (FTP/SFTP). ShowDialog&lt;string?&gt; via CloseRequested.</summary>
public sealed partial class FtpBrowserViewModel : ViewModelBase
{
    private readonly Destination _dest;
    private string _cur;

    /// <summary>View fecha com este payload (caminho escolhido, ou null se cancelou).</summary>
    public event Action<string?>? CloseRequested;

    public FtpBrowserViewModel(Destination dest, string startPath)
    {
        _dest = dest;
        _cur = string.IsNullOrWhiteSpace(startPath) ? "/" : startPath;
    }

    public ObservableCollection<FtpBrowserItem> Items { get; } = new();

    [ObservableProperty] private string _currentText = "";

    public string WindowTitle => L.T("ftpBrowserTitle");
    public string HintText => L.T("dblClickEnter");
    public string SelectLabel => L.T("selectThisFolder");
    public string CancelLabel => L.T("cancel");

    /// <summary>Chamado pela View no Opened.</summary>
    public Task InitAsync() => LoadDirAsync(_cur);

    private async Task LoadDirAsync(string path)
    {
        _cur = path;
        CurrentText = L.T("currentFolder", _cur);
        Items.Clear();
        if (_cur.TrimEnd('/').Length > 0) Items.Add(new FtpBrowserItem { Display = L.T("upFolder"), IsUp = true });
        Items.Add(new FtpBrowserItem { Display = L.T("ftpConnecting") });
        try
        {
            var list = await Task.Run(() => TransferService.ListPath(_dest, _cur));
            Items.Clear();
            if (_cur.TrimEnd('/').Length > 0) Items.Add(new FtpBrowserItem { Display = L.T("upFolder"), IsUp = true });
            foreach (var e in list.Where(x => x.IsDir).OrderBy(x => x.Name))
                Items.Add(new FtpBrowserItem { Display = "\U0001F4C1  " + e.Name, Name = e.Name });
        }
        catch (Exception ex)
        {
            Items.Clear();
            if (_cur.TrimEnd('/').Length > 0) Items.Add(new FtpBrowserItem { Display = L.T("upFolder"), IsUp = true });
            Items.Add(new FtpBrowserItem { Display = L.T("listErrorPrefix", ex.Message) });
        }
    }

    [RelayCommand]
    private async Task EnterAsync(FtpBrowserItem? it)
    {
        if (it is null) return;
        if (it.IsUp)
        {
            var c = _cur.TrimEnd('/');
            var idx = c.LastIndexOf('/');
            await LoadDirAsync(idx <= 0 ? "/" : c.Substring(0, idx));
        }
        else if (!string.IsNullOrEmpty(it.Name))
        {
            await LoadDirAsync(_cur.TrimEnd('/') + "/" + it.Name);
        }
    }

    [RelayCommand]
    private void Select() => CloseRequested?.Invoke(_cur);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(null);
}
