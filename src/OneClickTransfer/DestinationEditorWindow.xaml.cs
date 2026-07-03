using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using OneClickTransfer.I18n;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer;

public partial class DestinationEditorWindow : Window
{
    public Destination? Result { get; private set; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public DestinationEditorWindow(Destination? existing)
    {
        InitializeComponent();
        ApplyTexts();
        LoadFields(existing ?? new Destination());
        UpdatePanels();
        Loaded += (_, _) =>
        {
            try { var h = new WindowInteropHelper(this).Handle; int v = App.Settings.Theme != "light" ? 1 : 0; DwmSetWindowAttribute(h, 20, ref v, 4); } catch { }
        };
    }

    private void ApplyTexts()
    {
        Title = L.T("destEditorTitle");
        RbLocal.Content = L.T("localFolder");
        RbFtp.Content = L.T("ftpServer");
        RbSftp.Content = L.T("sftpServer");
        LblDstFolder.Text = L.T("destFolderLabel");
        BtnBrowseDst.Content = L.T("browse");
        LblHost.Text = L.T("ftpHost");
        LblPort.Text = L.T("ftpPort");
        LblRemote.Text = L.T("ftpRemote");
        BtnBrowseRemote.Content = L.T("ftpSearch");
        LblUser.Text = L.T("ftpUser");
        LblPass.Text = L.T("ftpPass");
        ChkTls.Content = L.T("ftpTls");
        BtnTest.Content = L.T("testConn");
        BtnOk.Content = L.T("save");
        BtnCancel.Content = L.T("cancel");
    }

    private void LoadFields(Destination d)
    {
        if (d.Type == DestType.Ftp || d.Type == DestType.Sftp)
        {
            if (d.Type == DestType.Sftp) RbSftp.IsChecked = true; else RbFtp.IsChecked = true;
            TxtRemote.Text = string.IsNullOrEmpty(d.Folder) ? "/" : d.Folder;
            TxtDstFolder.Text = "";
        }
        else
        {
            RbLocal.IsChecked = true;
            TxtDstFolder.Text = d.Folder;
            TxtRemote.Text = "/";
        }
        TxtHost.Text = d.Host;
        TxtPort.Text = (d.Port <= 0 ? (d.Type == DestType.Sftp ? 22 : 21) : d.Port).ToString();
        TxtUser.Text = d.Username;
        TxtPass.Password = SecretProtector.Unprotect(d.Password);
        ChkTls.IsChecked = d.UseTls;
    }

    private Destination ReadDest()
    {
        if (RbFtp.IsChecked == true || RbSftp.IsChecked == true)
        {
            bool sftp = RbSftp.IsChecked == true;
            int.TryParse(TxtPort.Text, out var port); if (port <= 0) port = sftp ? 22 : 21;
            return new Destination
            {
                Type = sftp ? DestType.Sftp : DestType.Ftp,
                Host = TxtHost.Text.Trim(),
                Port = port,
                Folder = string.IsNullOrWhiteSpace(TxtRemote.Text) ? "/" : TxtRemote.Text.Trim(),
                Username = TxtUser.Text.Trim(),
                Password = SecretProtector.Protect(TxtPass.Password),
                UseTls = !sftp && ChkTls.IsChecked == true
            };
        }
        return new Destination { Type = DestType.Local, Folder = TxtDstFolder.Text.Trim() };
    }

    private void UpdatePanels()
    {
        bool ftp = RbFtp.IsChecked == true;
        bool sftp = RbSftp.IsChecked == true;
        bool server = ftp || sftp;
        bool local = RbLocal.IsChecked == true;
        PanelLocal.IsEnabled = local; PanelLocal.Opacity = local ? 1 : 0.5;
        PanelFtp.IsEnabled = server; PanelFtp.Opacity = server ? 1 : 0.5;
        ChkTls.Visibility = ftp ? Visibility.Visible : Visibility.Hidden;
    }

    private void DestType_Changed(object sender, RoutedEventArgs e)
    {
        if (RbSftp.IsChecked == true && (TxtPort.Text == "21" || string.IsNullOrWhiteSpace(TxtPort.Text))) TxtPort.Text = "22";
        else if (RbFtp.IsChecked == true && (TxtPort.Text == "22" || string.IsNullOrWhiteSpace(TxtPort.Text))) TxtPort.Text = "21";
        UpdatePanels();
    }

    private void BrowseDst_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog();
        if (!string.IsNullOrWhiteSpace(TxtDstFolder.Text)) { try { dlg.InitialDirectory = TxtDstFolder.Text; } catch { } }
        if (dlg.ShowDialog() == true) TxtDstFolder.Text = dlg.FolderName;
    }

    private void BrowseRemote_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtHost.Text))
        {
            SetTestResult("✗ " + L.T("ftpHost"), "ErrorBrush");
            return;
        }
        bool sftp = RbSftp.IsChecked == true;
        int.TryParse(TxtPort.Text, out var port); if (port <= 0) port = sftp ? 22 : 21;
        var d = new Destination
        {
            Type = sftp ? DestType.Sftp : DestType.Ftp,
            Host = TxtHost.Text.Trim(), Port = port, Folder = "/",
            Username = TxtUser.Text.Trim(),
            Password = SecretProtector.Protect(TxtPass.Password),
            UseTls = !sftp && ChkTls.IsChecked == true
        };
        var start = string.IsNullOrWhiteSpace(TxtRemote.Text) ? "/" : TxtRemote.Text.Trim();
        var br = new FtpBrowserWindow(d, start) { Owner = this };
        if (br.ShowDialog() == true && br.ChosenPath != null) TxtRemote.Text = br.ChosenPath;
    }

    private void SetTestResult(string text, string brushKey, string? tip = null)
    {
        TxtTestResult.Text = text;
        TxtTestResult.ToolTip = tip;
        TxtTestResult.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, brushKey);
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        var d = ReadDest();
        if ((d.Type != DestType.Ftp && d.Type != DestType.Sftp) || string.IsNullOrWhiteSpace(d.Host))
        {
            SetTestResult("✗ " + L.T("ftpHost"), "ErrorBrush");
            return;
        }
        BtnTest.IsEnabled = false; BtnTest.Content = L.T("testing");
        SetTestResult(L.T("testing"), "SubTextBrush");
        try
        {
            await Task.Run(() => TransferService.TestConnection(d));
            SetTestResult("✓ " + L.T("connOk"), "SuccessBrush");
        }
        catch (Exception ex)
        {
            SetTestResult("✗ " + L.T("connFailed"), "ErrorBrush", ex.Message);
        }
        finally { BtnTest.IsEnabled = true; BtnTest.Content = L.T("testConn"); }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Result = ReadDest();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
