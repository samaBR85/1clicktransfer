using Avalonia.Controls;
using Avalonia.Input;
using OneClickTransfer.Avalonia.Services;
using OneClickTransfer.Avalonia.ViewModels;

namespace OneClickTransfer.Avalonia.Views;

public partial class TaskEditorWindow : Window
{
    public TaskEditorWindow() => InitializeComponent();

    public TaskEditorWindow(TaskEditorViewModel vm) : this()
    {
        DataContext = vm;
        vm.CloseRequested += ok => Close(ok);
        Opened += (_, _) => WindowsDarkTitleBar.Apply(this, App.Settings.Theme != "light");
    }

    private void Dests_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is TaskEditorViewModel vm) vm.EditDestCommand.Execute(null);
    }
}
