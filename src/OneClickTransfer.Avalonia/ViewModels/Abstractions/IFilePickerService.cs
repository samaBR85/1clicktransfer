using System.Collections.Generic;
using System.Threading.Tasks;

namespace OneClickTransfer.Avalonia.ViewModels.Abstractions;

/// <summary>Seleção de arquivos/pastas (Avalonia StorageProvider por baixo).</summary>
public interface IFilePickerService
{
    Task<IReadOnlyList<string>> PickFilesAsync(bool multiple);
    Task<string?> PickFolderAsync(string? start);
}
