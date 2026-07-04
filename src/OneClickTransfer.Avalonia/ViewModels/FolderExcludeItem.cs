using CommunityToolkit.Mvvm.ComponentModel;

namespace OneClickTransfer.Avalonia.ViewModels;

/// <summary>Um item do nível raiz da pasta de origem, com checkbox de inclusão.
/// Desmarcar vira um padrão de exclusão (pasta → "nome/", arquivo → "nome").</summary>
public sealed partial class FolderExcludeItem : ObservableObject
{
    public string RealName { get; init; } = "";
    public bool IsDir { get; init; }
    public string DisplayName => (IsDir ? "📁 " : "") + RealName;

    [ObservableProperty] private bool _isIncluded = true;
}
