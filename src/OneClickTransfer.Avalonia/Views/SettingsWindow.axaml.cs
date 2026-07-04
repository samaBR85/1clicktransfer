using Avalonia.Controls;
using OneClickTransfer.Avalonia.Services;
using OneClickTransfer.Avalonia.ViewModels;

namespace OneClickTransfer.Avalonia.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow() => InitializeComponent();

    public SettingsWindow(SettingsViewModel vm) : this()
    {
        DataContext = vm;
        vm.CloseRequested += ok => Close(ok);
        Opened += (_, _) => WindowsDarkTitleBar.Apply(this, App.Settings.Theme != "light");
    }
}
