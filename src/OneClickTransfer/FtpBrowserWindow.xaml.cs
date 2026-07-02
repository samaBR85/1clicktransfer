using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using OneClickTransfer.I18n;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer;

public partial class FtpBrowserWindow : Window
{
    private sealed class Item
    {
        public string Display { get; set; } = "";
        public string? Name { get; set; }
        public bool IsUp { get; set; }
        public override string ToString() => Display;
    }

    private readonly Destination _dest;
    private string _cur;
    private readonly ObservableCollection<Item> _items = new();

    public string? ChosenPath { get; private set; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public FtpBrowserWindow(Destination dest, string startPath)
    {
        InitializeComponent();
        _dest = dest;
        _cur = string.IsNullOrWhiteSpace(startPath) ? "/" : startPath;
        Lb.ItemsSource = _items;
        Title = L.T("ftpBrowserTitle");
        LblHint.Text = L.T("dblClickEnter");
        BtnSelect.Content = L.T("selectThisFolder");
        BtnCancel.Content = L.T("cancel");
        Loaded += async (_, _) =>
        {
            try { var h = new WindowInteropHelper(this).Handle; int v = App.Settings.Theme != "light" ? 1 : 0; DwmSetWindowAttribute(h, 20, ref v, 4); } catch { }
            await LoadDir(_cur);
        };
    }

    private async Task LoadDir(string path)
    {
        _cur = path;
        LblCur.Text = L.T("currentFolder", _cur);
        _items.Clear();
        if (_cur.TrimEnd('/').Length > 0)
            _items.Add(new Item { Display = L.T("upFolder"), IsUp = true });
        _items.Add(new Item { Display = L.T("ftpConnecting"), Name = null });
        try
        {
            var list = await Task.Run(() => TransferService.ListPath(_dest, _cur));
            _items.Clear();
            if (_cur.TrimEnd('/').Length > 0)
                _items.Add(new Item { Display = L.T("upFolder"), IsUp = true });
            foreach (var e in list.Where(x => x.IsDir).OrderBy(x => x.Name))
                _items.Add(new Item { Display = "\U0001F4C1  " + e.Name, Name = e.Name });
        }
        catch (Exception ex)
        {
            _items.Clear();
            if (_cur.TrimEnd('/').Length > 0)
                _items.Add(new Item { Display = L.T("upFolder"), IsUp = true });
            _items.Add(new Item { Display = L.T("listErrorPrefix", ex.Message), Name = null });
        }
    }

    private async void Lb_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (Lb.SelectedItem is not Item it) return;
        if (it.IsUp)
        {
            var c = _cur.TrimEnd('/');
            var idx = c.LastIndexOf('/');
            await LoadDir(idx <= 0 ? "/" : c.Substring(0, idx));
        }
        else if (!string.IsNullOrEmpty(it.Name))
        {
            await LoadDir(_cur.TrimEnd('/') + "/" + it.Name);
        }
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        ChosenPath = _cur;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
