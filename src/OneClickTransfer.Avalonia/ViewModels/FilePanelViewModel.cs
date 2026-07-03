using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OneClickTransfer.Avalonia.ViewModels;

/// <summary>Painel de arquivos (origem ou destino): linhas + caminho + comandos de refresh/navegar.
/// O comportamento (o que refrescar/navegar) é injetado pelo MainViewModel.</summary>
public sealed partial class FilePanelViewModel : ViewModelBase
{
    [ObservableProperty] private string _pathText = "";

    public ObservableCollection<FileRow> Rows { get; } = new();
    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand<FileRow?> NavigateCommand { get; }

    public FilePanelViewModel(Action onRefresh, Action<FileRow?> onNavigate)
    {
        RefreshCommand = new RelayCommand(onRefresh);
        NavigateCommand = new RelayCommand<FileRow?>(onNavigate);
    }
}
