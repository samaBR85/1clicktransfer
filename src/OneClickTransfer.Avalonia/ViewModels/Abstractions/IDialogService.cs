using System.Threading.Tasks;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.ViewModels.Abstractions;

/// <summary>Diálogos modais (retornam tipos do domínio — sem tipos Avalonia aqui).</summary>
public interface IDialogService
{
    Task<string?> PromptAsync(string title, string label, string initial = "");
    Task<bool> ConfirmAsync(string title, string message);
    Task ShowMessageAsync(string title, string message, bool error = false);

    Task<Destination?> EditDestinationAsync(Destination? existing);
    Task<string?> BrowseRemoteFolderAsync(Destination d, string startPath);
    Task<bool> ShowTaskEditorAsync();
    Task<bool> ShowSettingsAsync();
    Task ShowUpdateAsync(UpdateInfo info);
}
