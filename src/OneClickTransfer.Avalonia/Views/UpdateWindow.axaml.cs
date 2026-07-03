using Avalonia.Controls;
using OneClickTransfer.Avalonia.Services;
using OneClickTransfer.Avalonia.ViewModels;

namespace OneClickTransfer.Avalonia.Views;

public partial class UpdateWindow : Window
{
    public UpdateWindow() => InitializeComponent();

    public UpdateWindow(UpdateViewModel vm) : this()
    {
        DataContext = vm;
        vm.CloseRequested += _ => Close();
        Opened += (_, _) => WindowsDarkTitleBar.Apply(this, App.Settings.Theme != "light");
    }
}
