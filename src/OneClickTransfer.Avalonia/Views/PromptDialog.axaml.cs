using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OneClickTransfer.Avalonia.Services;
using OneClickTransfer.I18n;

namespace OneClickTransfer.Avalonia.Views;

/// <summary>Diálogo de entrada de texto. ShowDialog&lt;string?&gt; -> texto ou null (cancelado).</summary>
public partial class PromptDialog : Window
{
    public PromptDialog() => InitializeComponent();

    public PromptDialog(string title, string label, string initial) : this()
    {
        Title = title;
        LabelText.Text = label;
        Input.Text = initial;
        CancelButton.Content = L.T("cancel");
        Opened += (_, _) =>
        {
            WindowsDarkTitleBar.Apply(this, App.Settings.Theme != "light");
            Input.SelectAll();
            Input.Focus();
        };
        KeyDown += (_, e) => { if (e.Key == Key.Enter) Close(Input.Text ?? ""); else if (e.Key == Key.Escape) Close(null); };
    }

    private void Ok_Click(object? sender, RoutedEventArgs e) => Close(Input.Text ?? "");
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
