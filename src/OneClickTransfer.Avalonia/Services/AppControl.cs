using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using OneClickTransfer.Avalonia.ViewModels.Abstractions;

namespace OneClickTransfer.Avalonia.Services;

public sealed class AppControl : IAppControl
{
    public void Shutdown(int code = 0)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown(code);
    }
}
