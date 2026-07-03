namespace OneClickTransfer.Avalonia.ViewModels.Abstractions;

/// <summary>Controle do ciclo de vida do app (usado pelo update p/ reiniciar).</summary>
public interface IAppControl
{
    void Shutdown(int code = 0);
}
