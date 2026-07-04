using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OneClickTransfer.Avalonia.ViewModels.Abstractions;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.Tests;

/// <summary>Dispatcher síncrono: executa inline (sem thread de UI).</summary>
public sealed class FakeUiDispatcher : IUiDispatcher
{
    public void Post(Action action) => action();
    public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
}

/// <summary>IAppControl que só registra se Shutdown foi chamado.</summary>
public sealed class FakeAppControl : IAppControl
{
    public bool ShutdownCalled { get; private set; }
    public void Shutdown(int code = 0) => ShutdownCalled = true;
}

/// <summary>File picker configurável.</summary>
public sealed class FakeFilePicker : IFilePickerService
{
    public IReadOnlyList<string> FilesToReturn { get; set; } = Array.Empty<string>();
    public string? FolderToReturn { get; set; }
    public Task<IReadOnlyList<string>> PickFilesAsync(bool multiple) => Task.FromResult(FilesToReturn);
    public Task<string?> PickFolderAsync(string? start) => Task.FromResult(FolderToReturn);
}

/// <summary>Diálogos configuráveis; conta chamadas.</summary>
public sealed class FakeDialogService : IDialogService
{
    public string? PromptResult { get; set; }
    public bool ConfirmResult { get; set; }
    public Destination? EditResult { get; set; }
    public string? BrowseResult { get; set; }
    public bool TaskEditorResult { get; set; }
    public bool SettingsResult { get; set; }
    public int MessageCount { get; private set; }

    public Task<string?> PromptAsync(string title, string label, string initial = "") => Task.FromResult(PromptResult);
    public Task<bool> ConfirmAsync(string title, string message) => Task.FromResult(ConfirmResult);
    public Task ShowMessageAsync(string title, string message, bool error = false) { MessageCount++; return Task.CompletedTask; }
    public Task<Destination?> EditDestinationAsync(Destination? existing) => Task.FromResult(EditResult);
    public Task<string?> BrowseRemoteFolderAsync(Destination d, string startPath) => Task.FromResult(BrowseResult);
    public Task<bool> ShowTaskEditorAsync() => Task.FromResult(TaskEditorResult);
    public Task<bool> ShowSettingsAsync() => Task.FromResult(SettingsResult);
    public Task ShowUpdateAsync(UpdateInfo info) => Task.CompletedTask;
}
