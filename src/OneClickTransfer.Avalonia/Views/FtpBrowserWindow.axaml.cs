using Avalonia.Controls;
using Avalonia.Input;
using OneClickTransfer.Avalonia.Services;
using OneClickTransfer.Avalonia.ViewModels;

namespace OneClickTransfer.Avalonia.Views;

public partial class FtpBrowserWindow : Window
{
    public FtpBrowserWindow() => InitializeComponent();

    public FtpBrowserWindow(FtpBrowserViewModel vm) : this()
    {
        DataContext = vm;
        vm.CloseRequested += result => Close(result);
        Opened += async (_, _) =>
        {
            WindowsDarkTitleBar.Apply(this, App.Settings.Theme != "light");
            await vm.InitAsync();
        };
    }

    private void Lb_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is FtpBrowserViewModel vm)
            vm.EnterCommand.Execute(Lb.SelectedItem as FtpBrowserItem);
    }
}
