using System;
using OneClickTransfer.Models;

namespace OneClickTransfer.Avalonia.ViewModels;

/// <summary>Wrapper observável de <see cref="TransferJob"/> para a lista de tarefas.
/// Substitui os LstJobs.Items.Refresh() do WPF (inexistente em Avalonia): notifica Name/Enabled/Summary/WatchIcon.</summary>
public sealed class JobItemViewModel : ViewModelBase
{
    private readonly Action _onEnabledChanged;

    public JobItemViewModel(TransferJob model, Action onEnabledChanged)
    {
        Model = model;
        _onEnabledChanged = onEnabledChanged;
    }

    public TransferJob Model { get; }

    public string Name
    {
        get => Model.Name;
        set { if (Model.Name != value) { Model.Name = value; OnPropertyChanged(); } }
    }

    public bool Enabled
    {
        get => Model.Enabled;
        set
        {
            if (Model.Enabled == value) return;
            Model.Enabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Summary));
            _onEnabledChanged();
        }
    }

    public string Summary => Model.Summary;
    public string WatchIcon => Model.WatchIcon;

    /// <summary>Re-notifica campos derivados após mudanças no model (origem/destino/watch).</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(WatchIcon));
    }
}
