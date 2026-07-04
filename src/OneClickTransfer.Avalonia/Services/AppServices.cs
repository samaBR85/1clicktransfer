using OneClickTransfer.Avalonia.ViewModels.Abstractions;

namespace OneClickTransfer.Avalonia.Services;

/// <summary>
/// Composition root manual (app pequeno, sem container DI). Populado em
/// App.OnFrameworkInitializationCompleted, injetado nos ViewModels.
/// </summary>
public static class AppServices
{
    public static IUiDispatcher Dispatcher { get; set; } = null!;
    public static IFilePickerService Files { get; set; } = null!;
    public static IAppControl App { get; set; } = null!;
    public static IDialogService Dialogs { get; set; } = null!;   // atribuido na E7 (diálogos)
    public static IClipboardService Clipboard { get; set; } = null!;
}
