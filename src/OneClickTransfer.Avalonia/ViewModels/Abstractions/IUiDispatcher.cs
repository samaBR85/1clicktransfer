using System;
using System.Threading.Tasks;

namespace OneClickTransfer.Avalonia.ViewModels.Abstractions;

/// <summary>Marshaling para a thread de UI (progresso vindo de Task.Run/FileSystemWatcher).</summary>
public interface IUiDispatcher
{
    void Post(Action action);
    Task InvokeAsync(Action action);
}
