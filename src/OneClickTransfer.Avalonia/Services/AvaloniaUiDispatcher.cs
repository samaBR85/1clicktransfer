using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using OneClickTransfer.Avalonia.ViewModels.Abstractions;

namespace OneClickTransfer.Avalonia.Services;

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
    public Task InvokeAsync(Action action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();
}
