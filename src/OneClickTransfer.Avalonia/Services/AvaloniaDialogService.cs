using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using OneClickTransfer.Avalonia.ViewModels.Abstractions;
using OneClickTransfer.Avalonia.Views;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.Services;

public sealed class AvaloniaDialogService : IDialogService
{
    private readonly Func<Window?> _owner;
    public AvaloniaDialogService(Func<Window?> owner) => _owner = owner;

    public async Task<string?> PromptAsync(string title, string label, string initial = "")
    {
        var o = _owner();
        return o is null ? null : await new PromptDialog(title, label, initial).ShowDialog<string?>(o);
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var o = _owner();
        return o is not null && await new MessageDialog(title, message, confirm: true).ShowDialog<bool>(o);
    }

    public async Task ShowMessageAsync(string title, string message, bool error = false)
    {
        var o = _owner();
        if (o is not null) await new MessageDialog(title, message, confirm: false, error: error).ShowDialog<bool>(o);
    }

    // Implementados nas etapas seguintes (E9: editor/browser/settings; E10: update).
    public Task<Destination?> EditDestinationAsync(Destination? existing) => throw new NotImplementedException("E9");
    public Task<string?> BrowseRemoteFolderAsync(Destination d, string startPath) => throw new NotImplementedException("E9");
    public Task<bool> ShowSettingsAsync() => throw new NotImplementedException("E9");
    public Task ShowUpdateAsync(UpdateInfo info) => throw new NotImplementedException("E10");
}
