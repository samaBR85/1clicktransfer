using Avalonia.Controls;
using OneClickTransfer.Avalonia.Services;
using OneClickTransfer.Avalonia.ViewModels;
using OneClickTransfer.Models;

namespace OneClickTransfer.Avalonia.Views;

public partial class DestinationEditorWindow : Window
{
    public DestinationEditorWindow() => InitializeComponent();

    public DestinationEditorWindow(DestinationEditorViewModel vm) : this()
    {
        DataContext = vm;
        vm.CloseRequested += result => Close(result);
        Opened += (_, _) => WindowsDarkTitleBar.Apply(this, App.Settings.Theme != "light");
    }
}
