using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OneClickTransfer.Avalonia.ViewModels;

public enum QueueItemState { Queued, Running, Success, Failed }

/// <summary>Um item da fila de transferência: um arquivo indo para um destino específico.
/// POCO de UI (não persiste em settings.json).</summary>
public sealed partial class TransferQueueItem : ObservableObject
{
    public string FileName { get; init; } = "";
    public string DestSummary { get; init; } = "";
    public long SizeBytes { get; init; }
    public DateTime? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }

    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private QueueItemState _state;

    public string DisplayName => string.IsNullOrEmpty(DestSummary) ? FileName : FileName + "  →  " + DestSummary;

    public bool IsActive => State is QueueItemState.Queued or QueueItemState.Running;

    public string SizeText
    {
        get
        {
            string[] u = { "B", "KB", "MB", "GB", "TB" };
            double v = SizeBytes; int i = 0;
            while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
            return v.ToString(i == 0 ? "0" : "0.0") + " " + u[i];
        }
    }

    partial void OnStateChanged(QueueItemState value) => OnPropertyChanged(nameof(IsActive));
}
