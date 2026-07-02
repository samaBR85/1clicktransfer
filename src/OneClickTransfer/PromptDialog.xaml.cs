using System.Windows;

namespace OneClickTransfer;

public partial class PromptDialog : Window
{
    public string Value => TxtInput.Text.Trim();

    public PromptDialog(string title, string message, string initial = "")
    {
        InitializeComponent();
        Title = title;
        TxtMsg.Text = message;
        TxtInput.Text = initial;
        Loaded += (_, _) => { TxtInput.SelectAll(); TxtInput.Focus(); };
    }

    private void Ok_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    /// <summary>Retorna o texto ou null se cancelado/vazio.</summary>
    public static string? Ask(Window owner, string title, string message, string initial = "")
    {
        var d = new PromptDialog(title, message, initial) { Owner = owner };
        if (d.ShowDialog() == true && !string.IsNullOrWhiteSpace(d.Value)) return d.Value;
        return null;
    }
}
