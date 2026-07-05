using CommunityToolkit.Mvvm.ComponentModel;

namespace OneClickTransfer.Avalonia.ViewModels;

/// <summary>Um item do nível raiz da pasta de origem, com checkbox de inclusão.
/// Desmarcar vira um padrão de exclusão (pasta → "nome/", arquivo → "nome").</summary>
public sealed partial class FolderExcludeItem : ObservableObject
{
    public string RealName { get; init; } = "";
    public bool IsDir { get; init; }

    /// <summary>"_" dobrado -- Content de CheckBox interpreta "_" simples como tecla de acesso
    /// (mnemônico) e engole o caractere na exibição; nomes de arquivo/pasta reais não devem sumir.</summary>
    public string DisplayName => (IsDir ? "📁 " : "") + RealName.Replace("_", "__");

    [ObservableProperty] private bool _isIncluded = true;
}
