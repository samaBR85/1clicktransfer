using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using OneClickTransfer.I18n;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer;

public class FileRow
{
    public string Name { get; set; } = "";
    public string Size { get; set; } = "";
    public string Modified { get; set; } = "";
    public bool Highlight { get; set; }
}

public partial class MainWindow : Window
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly ObservableCollection<FileRow> _src = new();
    private readonly ObservableCollection<FileRow> _dst = new();
    private bool _profSync;
    private bool _rbSync;

    private AppSettings S => App.Settings;
    private Destination? Dest0 => S.Destinations.FirstOrDefault();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public MainWindow()
    {
        InitializeComponent();
        GridSrc.ItemsSource = _src;
        GridDst.ItemsSource = _dst;
        Loaded += (_, _) =>
        {
            ApplyDwm();
            ApplyTexts();
            SyncProfileCombo();
            RefreshHome();
            UpdateReadyState();
            if (string.IsNullOrEmpty(S.Source.Path))
                SetStatus(L.T("clickSettingsStart"), StatusKind.Sub);
        };
        KeyDown += MainWindow_KeyDown;
    }

    // ---------------- i18n / tema ----------------
    private void ApplyTexts()
    {
        Title = L.T("appTitle");
        TxtTitle.Text = L.T("appTitle");
        BtnRefresh.Content = L.T("refresh");
        TxtProfile.Text = L.T("profile");
        TxtSrcHdr.Text = L.T("source");
        TxtDstHdr.Text = L.T("destination");
        RbAlways.Content = L.T("replace");
        RbIfNewer.Content = L.T("replaceIfNewer");
        RbNever.Content = L.T("dontReplace");
        BtnGo.Content = L.T("transfer");
        BtnCfg.Content = L.T("settings");
        TxtAction.Text = L.T("action");
        SrcColName.Header = DstColName.Header = L.T("colName");
        SrcColSize.Header = DstColSize.Header = L.T("colSize");
        SrcColMod.Header = DstColMod.Header = L.T("colModified");
    }

    private void ApplyDwm()
    {
        try
        {
            var h = new WindowInteropHelper(this).Handle;
            int v = S.Theme != "light" ? 1 : 0;
            DwmSetWindowAttribute(h, 20, ref v, 4);
        }
        catch { }
    }

    private void BtnTheme_Click(object sender, RoutedEventArgs e)
    {
        S.Theme = S.Theme == "light" ? "dark" : "light";
        SettingsService.Save(S);
        ThemeManager.Apply(S.Theme);
        ApplyDwm();
        UpdateReadyState();
    }

    // ---------------- Home ----------------
    private static string FormatSize(long b) => b.ToString("#,0", PtBr);

    private FileRow ToRow(RemoteEntry e, bool highlight) => new()
    {
        Name = (e.IsDir ? "\U0001F4C1  " : "  ") + e.Name,
        Size = e.IsDir ? "" : FormatSize(e.Size),
        Modified = e.Modified == DateTime.MinValue ? "" : e.Modified.ToString("dd/MM/yyyy HH:mm"),
        Highlight = highlight
    };

    private void RefreshHome(bool fetchFtp = false)
    {
        // Origem
        _src.Clear();
        var srcPath = S.Source.Path;
        if (!string.IsNullOrEmpty(srcPath))
        {
            var folder = Path.GetDirectoryName(srcPath) ?? "";
            var leaf = Path.GetFileName(srcPath);
            TxtSrcPath.Text = L.T("folderPrefix") + folder;
            foreach (var it in TransferService.LocalList(folder))
                _src.Add(ToRow(it, it.Name.Equals(leaf, StringComparison.OrdinalIgnoreCase)));
        }
        else TxtSrcPath.Text = L.T("noFile");

        // Destino
        _dst.Clear();
        var d = Dest0;
        if (d == null) { TxtDstPath.Text = L.T("noDest"); return; }

        if (d.Type == DestType.Local)
        {
            TxtDstPath.Text = string.IsNullOrEmpty(d.Folder) ? L.T("noDest") : L.T("folderPrefix") + d.Folder;
            if (!string.IsNullOrEmpty(d.Folder))
                foreach (var it in TransferService.LocalList(d.Folder))
                    _dst.Add(ToRow(it, false));
        }
        else // FTP
        {
            TxtDstPath.Text = L.T("ftpPrefix") + d.Host + d.Folder;
            if (fetchFtp) { _dst.Add(new FileRow { Name = "  " + L.T("loadingFtp") }); FetchFtpAsync(d); }
            else _dst.Add(new FileRow { Name = "  " + L.T("clickRefreshFtp") });
        }
    }

    private async void FetchFtpAsync(Destination d)
    {
        try
        {
            var list = await Task.Run(() => TransferService.FtpList(d));
            _dst.Clear();
            foreach (var it in list.OrderByDescending(x => x.IsDir).ThenBy(x => x.Name))
                _dst.Add(ToRow(it, false));
            if (_dst.Count == 0) _dst.Add(new FileRow { Name = "  " + L.T("emptyFolder") });
        }
        catch
        {
            _dst.Clear();
            _dst.Add(new FileRow { Name = "  " + L.T("cantListFtp") });
        }
    }

    private bool DestReady(Destination? d)
        => d != null && (d.Type == DestType.Local ? !string.IsNullOrEmpty(d.Folder) : !string.IsNullOrEmpty(d.Host));

    private void UpdateReadyState()
    {
        var ok = !string.IsNullOrEmpty(S.Source.Path) && DestReady(Dest0);
        BtnGo.IsEnabled = ok;
        BtnTheme.Content = S.Theme == "light" ? L.T("darkMode") : L.T("lightMode");
        var hasSc = !string.IsNullOrEmpty(S.Shortcut) && S.Shortcut != "None" && S.Shortcut != "Nenhum";
        TxtHint.Text = hasSc
            ? L.T("shortcutHint", S.Shortcut) + "   ·   " + L.T("refreshHint")
            : L.T("refreshHint");
        _rbSync = true;
        RbAlways.IsChecked = S.OverwriteMode == OverwriteMode.Always;
        RbIfNewer.IsChecked = S.OverwriteMode == OverwriteMode.IfNewer;
        RbNever.IsChecked = S.OverwriteMode == OverwriteMode.Never;
        _rbSync = false;
    }

    private enum StatusKind { Sub, Success, Error }
    private void SetStatus(string text, StatusKind kind)
    {
        TxtStatus.Text = text;
        TxtStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,
            kind switch { StatusKind.Success => "SuccessBrush", StatusKind.Error => "ErrorBrush", _ => "SubTextBrush" });
    }

    // ---------------- Perfis ----------------
    private void SyncProfileCombo()
    {
        _profSync = true;
        CmbProfile.Items.Clear();
        CmbProfile.Items.Add(L.T("noneItem"));
        foreach (var p in S.Profiles) CmbProfile.Items.Add(p.Name);
        var idx = 0;
        if (!string.IsNullOrEmpty(S.ActiveProfile))
        {
            var found = S.Profiles.FindIndex(p => p.Name == S.ActiveProfile);
            if (found >= 0) idx = found + 1;
        }
        CmbProfile.SelectedIndex = idx;
        _profSync = false;
    }

    private void CmbProfile_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_profSync) return;
        if (CmbProfile.SelectedIndex <= 0)
        {
            S.Source = new SourceSpec();
            S.Destinations = new();
            S.ActiveProfile = "";
            SettingsService.Save(S);
            RefreshHome(); UpdateReadyState();
            SetStatus(L.T("fieldsCleared"), StatusKind.Sub);
            return;
        }
        var name = CmbProfile.SelectedItem?.ToString() ?? "";
        var prof = S.Profiles.FirstOrDefault(p => p.Name == name);
        if (prof == null) return;
        S.Source = prof.Source.Clone();
        S.Destinations = prof.Destinations.ConvertAll(x => x.Clone());
        S.ActiveProfile = name;
        SettingsService.Save(S);
        RefreshHome(); UpdateReadyState();
        SetStatus(L.T("profileLoaded", name), StatusKind.Sub);
    }

    private void Overwrite_Changed(object sender, RoutedEventArgs e)
    {
        if (_rbSync) return;
        S.OverwriteMode = RbIfNewer.IsChecked == true ? OverwriteMode.IfNewer
                        : RbNever.IsChecked == true ? OverwriteMode.Never
                        : OverwriteMode.Always;
        SettingsService.Save(S);
    }

    // ---------------- Botoes ----------------
    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        SetStatus(L.T("refreshing"), StatusKind.Sub);
        RefreshHome(true);
        SetStatus("", StatusKind.Sub);
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        // F5 = Atualizar (fixo, nao trocavel)
        if (e.Key == Key.F5)
        {
            e.Handled = true;
            if (BtnRefresh.IsEnabled) { SetStatus(L.T("refreshing"), StatusKind.Sub); RefreshHome(true); SetStatus("", StatusKind.Sub); }
            return;
        }
        // Atalho configuravel = Transferir
        if (string.IsNullOrEmpty(S.Shortcut) || S.Shortcut is "None" or "Nenhum") return;
        if (e.Key.ToString() == S.Shortcut && BtnGo.IsEnabled) { e.Handled = true; _ = DoTransfer(); }
    }

    private void BtnGo_Click(object sender, RoutedEventArgs e) => _ = DoTransfer();

    private async Task DoTransfer()
    {
        if (!BtnGo.IsEnabled) return;
        if (!File.Exists(S.Source.Path))
        {
            SetStatus(L.T("srcNotFound"), StatusKind.Error);
            MessageBox.Show(this, S.Source.Path, L.T("errorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        var d = Dest0!;
        var fileName = Path.GetFileName(S.Source.Path);
        BtnGo.IsEnabled = BtnCfg.IsEnabled = BtnRefresh.IsEnabled = false;
        Prog.Value = 0;
        try
        {
            var mode = S.OverwriteMode;
            if (mode != OverwriteMode.Always)
            {
                SetStatus(L.T("checkingDest"), StatusKind.Sub);
                bool exists = await Task.Run(() => d.Type == DestType.Local
                    ? TransferService.LocalExists(d.Folder, fileName)
                    : TransferService.FtpExists(d, fileName));
                if (exists)
                {
                    if (mode == OverwriteMode.Never) { SetStatus(L.T("notSentExists", fileName), StatusKind.Sub); return; }
                    if (mode == OverwriteMode.IfNewer)
                    {
                        bool notNewer = await Task.Run(() => !IsSourceNewer(d, fileName));
                        if (notNewer) { SetStatus(L.T("nothingNewer"), StatusKind.Sub); return; }
                    }
                }
            }

            if (d.Type == DestType.Local)
            {
                SetStatus(L.T("copying"), StatusKind.Sub);
                await Task.Run(() => TransferService.LocalCopy(S.Source.Path, d.Folder));
            }
            else
            {
                SetStatus(L.T("uploading"), StatusKind.Sub);
                await Task.Run(() => TransferService.FtpUpload(d, S.Source.Path,
                    pct => Dispatcher.Invoke(() => Prog.Value = pct)));
            }
            Prog.Value = 100;
            SetStatus(L.T("completed"), StatusKind.Success);
            RefreshHome(true);
        }
        catch (Exception ex)
        {
            Prog.Value = 0;
            SetStatus(L.T("transferFailed"), StatusKind.Error);
            MessageBox.Show(this, ex.Message, L.T("transferErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnCfg.IsEnabled = BtnRefresh.IsEnabled = true;
            UpdateReadyState();
        }
    }

    private bool IsSourceNewer(Destination d, string fileName)
    {
        var srcT = File.GetLastWriteTime(S.Source.Path);
        if (d.Type == DestType.Local)
        {
            var dst = Path.Combine(d.Folder, fileName);
            if (!File.Exists(dst)) return true;
            return srcT > File.GetLastWriteTime(dst);
        }
        var rt = TransferService.FtpModified(d, fileName);
        return rt == null || srcT > rt.Value;
    }

    private void BtnCfg_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow { Owner = this };
        dlg.ShowDialog();
        // Recarrega tudo (idioma/tema/perfis podem ter mudado)
        App.Settings = SettingsService.Load();
        L.Lang = S.Language == "en" ? "en" : "pt";
        ThemeManager.Apply(S.Theme);
        ApplyDwm();
        ApplyTexts();
        SyncProfileCombo();
        RefreshHome();
        UpdateReadyState();
        SetStatus(L.T("settingsSaved"), StatusKind.Sub);
    }
}
