using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using OneClickTransfer.Avalonia.ViewModels.Abstractions;

namespace OneClickTransfer.Avalonia.Services;

public sealed class AvaloniaClipboardService : IClipboardService
{
    private readonly Func<TopLevel?> _topLevel;
    public AvaloniaClipboardService(Func<TopLevel?> topLevel) => _topLevel = topLevel;

    public async Task SetTextAsync(string text)
    {
        var top = _topLevel();
        if (top?.Clipboard != null) await top.Clipboard.SetTextAsync(text);
    }
}
