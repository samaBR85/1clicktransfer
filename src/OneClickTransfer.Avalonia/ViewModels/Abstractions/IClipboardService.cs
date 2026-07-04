using System.Threading.Tasks;

namespace OneClickTransfer.Avalonia.ViewModels.Abstractions;

/// <summary>Área de transferência do SO (copiar caminho no menu de contexto do DESTINATION).</summary>
public interface IClipboardService
{
    Task SetTextAsync(string text);
}
