using System;
using CommunityToolkit.Mvvm.ComponentModel;
using OneClickTransfer.I18n;

namespace OneClickTransfer.Avalonia.ViewModels;

public enum QueueItemState { Queued, Running, Success, Failed }

/// <summary>Um item da fila de transferência: um arquivo indo para um destino específico.
/// POCO de UI (não persiste em settings.json).</summary>
public sealed partial class TransferQueueItem : ObservableObject
{
    public string FileName { get; init; } = "";
    public string DestSummary { get; init; } = "";
    public long SizeBytes { get; init; }

    private DateTime? _finishedAt;
    public DateTime? FinishedAt
    {
        get => _finishedAt;
        set { _finishedAt = value; OnPropertyChanged(nameof(TimeText)); }
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(nameof(TooltipText)); OnPropertyChanged(nameof(ResultText)); }
    }

    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private QueueItemState _state;

    public string DisplayName => string.IsNullOrEmpty(DestSummary) ? FileName : FileName + "  →  " + DestSummary;
    public string TooltipText => ErrorMessage ?? DisplayName;
    public string TimeText => FinishedAt?.ToString("dd/MM HH:mm") ?? "-";

    public bool IsActive => State is QueueItemState.Queued or QueueItemState.Running;
    public bool IsFailed => State == QueueItemState.Failed;

    /// <summary>Texto exibido na coluna Progress quando o item não está mais ativo:
    /// "Done" pra sucesso, "Failed" (+ motivo, se houver) pra falha.</summary>
    public string ResultText => State switch
    {
        QueueItemState.Success => L.T("queueDone"),
        QueueItemState.Failed => string.IsNullOrEmpty(ErrorMessage)
            ? L.T("queueFailed")
            : L.T("queueFailedReason", ErrorMessage),
        _ => "",
    };

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

    partial void OnStateChanged(QueueItemState value)
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(ResultText));
    }
}
