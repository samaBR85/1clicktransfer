using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using OneClickTransfer.Avalonia.ViewModels.Abstractions;

namespace OneClickTransfer.Avalonia.Services;

public sealed class AvaloniaFilePickerService : IFilePickerService
{
    private readonly Func<TopLevel?> _topLevel;
    public AvaloniaFilePickerService(Func<TopLevel?> topLevel) => _topLevel = topLevel;

    public async Task<IReadOnlyList<string>> PickFilesAsync(bool multiple)
    {
        var top = _topLevel();
        if (top is null) return Array.Empty<string>();
        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = multiple });
        var list = new List<string>();
        foreach (var f in files)
        {
            var p = f.TryGetLocalPath();
            if (!string.IsNullOrEmpty(p)) list.Add(p!);
        }
        return list;
    }

    public async Task<string?> PickFolderAsync(string? start)
    {
        var top = _topLevel();
        if (top is null) return null;
        IStorageFolder? startFolder = null;
        if (!string.IsNullOrEmpty(start))
            try { startFolder = await top.StorageProvider.TryGetFolderFromPathAsync(start); } catch { }
        var folders = await top.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { AllowMultiple = false, SuggestedStartLocation = startFolder });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
