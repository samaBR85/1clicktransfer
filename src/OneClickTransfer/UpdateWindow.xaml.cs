using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using OneClickTransfer.I18n;
using OneClickTransfer.Services;

namespace OneClickTransfer;

public partial class UpdateWindow : Window
{
    private readonly UpdateInfo _info;
    private bool _busy;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public UpdateWindow(UpdateInfo info)
    {
        InitializeComponent();
        _info = info;
        Title = L.T("updateTitle");
        TxtHead.Text = L.T("updateAvailable", info.Tag);
        TxtCurrent.Text = L.T("updateCurrentVersion", UpdateService.Current.ToString());
        TxtWhatsNew.Text = L.T("whatsNew");
        TxtNotes.Text = string.IsNullOrWhiteSpace(info.Notes) ? "—" : info.Notes.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
        BtnUpdate.Content = L.T("updateNow");
        BtnLater.Content = L.T("updateLater");
        Loaded += (_, _) =>
        {
            try { var h = new WindowInteropHelper(this).Handle; int v = App.Settings.Theme != "light" ? 1 : 0; DwmSetWindowAttribute(h, 20, ref v, 4); } catch { }
        };
    }

    private void Later_Click(object sender, RoutedEventArgs e) { if (!_busy) { DialogResult = false; Close(); } }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        BtnUpdate.IsEnabled = BtnLater.IsEnabled = false;
        Prog.Visibility = Visibility.Visible;
        TxtStatus.Text = L.T("updateDownloading");
        try
        {
            var progress = new Progress<double>(p => Prog.Value = p);
            await UpdateService.DownloadAndSwapAsync(_info, progress);
            TxtStatus.Text = L.T("updateRestarting");
            UpdateService.Restart();
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            _busy = false;
            BtnUpdate.IsEnabled = BtnLater.IsEnabled = true;
            Prog.Visibility = Visibility.Collapsed;
            TxtStatus.Text = "";
            MessageBox.Show(this, L.T("updateFailed", ex.Message), L.T("updateTitle"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
