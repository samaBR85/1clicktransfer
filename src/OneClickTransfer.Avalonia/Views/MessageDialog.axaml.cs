using Avalonia.Controls;
using Avalonia.Interactivity;
using OneClickTransfer.Avalonia.Services;
using OneClickTransfer.I18n;

namespace OneClickTransfer.Avalonia.Views;

/// <summary>Mensagem/confirmação. ShowDialog&lt;bool&gt; -> true (OK/Sim), false (Cancelar/Não).</summary>
public partial class MessageDialog : Window
{
    public MessageDialog() => InitializeComponent();

    public MessageDialog(string title, string message, bool confirm, bool error = false) : this()
    {
        Title = title;
        MessageText.Text = message;
        if (error) MessageText.Foreground = this.FindResource("ErrorBrush") as global::Avalonia.Media.IBrush;
        if (confirm)
        {
            YesButton.Content = L.T("yes");
            NoButton.Content = L.T("no");
            NoButton.IsVisible = true;
        }
        Opened += (_, _) => WindowsDarkTitleBar.Apply(this, App.Settings.Theme != "light");
    }

    private void Yes_Click(object? sender, RoutedEventArgs e) => Close(true);
    private void No_Click(object? sender, RoutedEventArgs e) => Close(false);
}
