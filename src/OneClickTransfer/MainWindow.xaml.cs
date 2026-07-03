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
    public bool IsDir { get; set; }
    public bool IsUp { get; set; }
    public string RealName { get; set; } = "";
}

public partial class MainWindow : Window
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private readonly ObservableCollection<FileRow> _src = new();
    private readonly ObservableCollection<FileRow> _dst = new();
    private bool _profSync;
    private bool _rbSync;
    private string _srcDir = "";   // pasta atual navegada na ORIGEM
    private string _dstDir = "";   // pasta atual navegada no DESTINO

    private AppSettings S => App.Settings;
    private Destination? Dest0 => S.Destinations.FirstOrDefault();
    // Destino usado para navegacao na home (so quando ha exatamente 1 ativo)
    private Destination? SingleNavDest
    {
        get { var en = S.Destinations.Where(x => x.Enabled).ToList(); return en.Count == 1 ? en[0] : null; }
    }

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
        Highlight = highlight, IsDir = e.IsDir, RealName = e.Name
    };

    private static FileRow UpRow() => new() { Name = "  " + L.T("upFolder"), IsUp = true, IsDir = true };
    private static FileRow InfoRow(string key) => new() { Name = "  " + L.T(key) };

    private void RefreshHome(bool fetchFtp = false)
    {
        // Origem: comeca na pasta do arquivo escolhido
        _srcDir = !string.IsNullOrEmpty(S.Source.Path) ? (Path.GetDirectoryName(S.Source.Path) ?? "") : "";
        RefillSource();

        // Destino(s)
        var dests = S.Destinations;
        _dst.Clear();
        if (dests.Count == 0) { TxtDstPath.Text = L.T("noDest"); return; }

        var enabled = dests.Where(x => x.Enabled).ToList();
        if (enabled.Count == 1)
        {
            // Exatamente 1 destino ativo: listagem navegavel
            var d = enabled[0];
            _dstDir = d.Folder;
            if (d.Type == DestType.Local)
            {
                RefillDestLocal();
            }
            else
            {
                var prefix = d.Type == DestType.Sftp ? L.T("sftpPrefix") : L.T("ftpPrefix");
                TxtDstPath.Text = prefix + d.Host + (_dstDir ?? "/");
                if (fetchFtp) { _dst.Add(InfoRow("loadingFtp")); FetchRemoteAt(d, string.IsNullOrEmpty(_dstDir) ? "/" : _dstDir); }
                else _dst.Add(InfoRow("clickRefreshFtp"));
            }
            return;
        }

        // 0 ou 2+ destinos ativos: lista de resumos com marcador de ativo
        TxtDstPath.Text = L.T("destCount", enabled.Count);
        foreach (var dd in dests)
            _dst.Add(new FileRow { Name = (dd.Enabled ? "☑  " : "☐  ") + dd.Summary });
    }

    private void RefillSource()
    {
        _src.Clear();
        if (string.IsNullOrEmpty(_srcDir) || !Directory.Exists(_srcDir)) { TxtSrcPath.Text = L.T("noFile"); return; }
        TxtSrcPath.Text = L.T("folderPrefix") + _srcDir;
        var leaf = (!string.IsNullOrEmpty(S.Source.Path) &&
                    string.Equals(Path.GetDirectoryName(S.Source.Path), _srcDir, StringComparison.OrdinalIgnoreCase))
                    ? Path.GetFileName(S.Source.Path) : null;
        if (Directory.GetParent(_srcDir) != null) _src.Add(UpRow());
        foreach (var it in TransferService.LocalList(_srcDir))
            _src.Add(ToRow(it, leaf != null && it.Name.Equals(leaf, StringComparison.OrdinalIgnoreCase)));
    }

    private void RefillDestLocal()
    {
        _dst.Clear();
        if (string.IsNullOrEmpty(_dstDir)) { TxtDstPath.Text = L.T("noDest"); return; }
        TxtDstPath.Text = L.T("folderPrefix") + _dstDir;
        if (!Directory.Exists(_dstDir)) return;
        if (Directory.GetParent(_dstDir) != null) _dst.Add(UpRow());
        foreach (var it in TransferService.LocalList(_dstDir))
            _dst.Add(ToRow(it, false));
    }

    private async void FetchRemoteAt(Destination d, string path)
    {
        _dst.Clear(); _dst.Add(InfoRow("loadingFtp"));
        try
        {
            var list = await Task.Run(() => TransferService.ListPath(d, path));
            _dst.Clear();
            if (path.TrimEnd('/').Length > 0) _dst.Add(UpRow());
            foreach (var it in list.OrderByDescending(x => x.IsDir).ThenBy(x => x.Name))
                _dst.Add(ToRow(it, false));
            if (_dst.Count == 0 || (_dst.Count == 1 && _dst[0].IsUp)) _dst.Add(InfoRow("emptyFolder"));
        }
        catch
        {
            _dst.Clear();
            if (path.TrimEnd('/').Length > 0) _dst.Add(UpRow());
            _dst.Add(InfoRow("cantListFtp"));
        }
    }

    // ---------------- Navegacao nas listas ----------------
    private void GridSrc_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (GridSrc.SelectedItem is not FileRow it || string.IsNullOrEmpty(_srcDir)) return;
        if (it.IsUp) { _srcDir = Directory.GetParent(_srcDir)?.FullName ?? _srcDir; RefillSource(); }
        else if (it.IsDir) { _srcDir = Path.Combine(_srcDir, it.RealName); RefillSource(); }
        else if (!string.IsNullOrEmpty(it.RealName))
        {
            // duplo-clique em arquivo = escolhe como origem
            S.Source.Path = Path.Combine(_srcDir, it.RealName);
            S.ActiveProfile = "";
            SettingsService.Save(S);
            SyncProfileCombo();
            RefillSource();
            UpdateReadyState();
            SetStatus(Path.GetFileName(S.Source.Path), StatusKind.Sub);
        }
    }

    private void GridDst_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var d = SingleNavDest;
        if (d == null || GridDst.SelectedItem is not FileRow it) return;
        if (!it.IsDir && !it.IsUp) return;   // arquivos no destino nao navegam

        if (d.Type == DestType.Local)
        {
            if (string.IsNullOrEmpty(_dstDir)) return;
            if (it.IsUp) _dstDir = Directory.GetParent(_dstDir)?.FullName ?? _dstDir;
            else _dstDir = Path.Combine(_dstDir, it.RealName);
            d.Folder = _dstDir; SettingsService.Save(S);
            RefillDestLocal(); UpdateReadyState();
        }
        else // remoto
        {
            string np;
            if (it.IsUp)
            {
                var c = _dstDir.TrimEnd('/');
                var idx = c.LastIndexOf('/');
                np = idx <= 0 ? "/" : c.Substring(0, idx);
            }
            else np = _dstDir.TrimEnd('/') + "/" + it.RealName;
            _dstDir = np; d.Folder = np; SettingsService.Save(S);
            var prefix = d.Type == DestType.Sftp ? L.T("sftpPrefix") : L.T("ftpPrefix");
            TxtDstPath.Text = prefix + d.Host + np;
            FetchRemoteAt(d, np);
        }
    }

    private bool DestReady(Destination? d)
        => d != null && (d.Type == DestType.Local ? !string.IsNullOrEmpty(d.Folder) : !string.IsNullOrEmpty(d.Host));

    private void UpdateReadyState()
    {
        var ok = !string.IsNullOrEmpty(S.Source.Path) && S.Destinations.Any(d => d.Enabled && DestReady(d));
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
        var dests = S.Destinations.Where(d => d.Enabled && DestReady(d)).ToList();
        if (dests.Count == 0) return;
        var fileName = Path.GetFileName(S.Source.Path);
        var mode = S.OverwriteMode;
        BtnGo.IsEnabled = BtnCfg.IsEnabled = BtnRefresh.IsEnabled = false;
        Prog.Value = 0;
        int sent = 0, skipped = 0, failed = 0;
        string? lastError = null;
        try
        {
            for (int i = 0; i < dests.Count; i++)
            {
                var d = dests[i];
                var label = dests.Count > 1 ? d.Summary : (d.Type == DestType.Local ? L.T("copying") : L.T("uploading"));
                SetStatus(L.T("sendingTo", label, i + 1, dests.Count), StatusKind.Sub);
                Prog.Value = 0;
                try
                {
                    if (mode != OverwriteMode.Always)
                    {
                        bool exists = await Task.Run(() => TransferService.DestExists(d, fileName));
                        if (exists)
                        {
                            if (mode == OverwriteMode.Never) { skipped++; continue; }
                            if (mode == OverwriteMode.IfNewer && await Task.Run(() => !IsSourceNewer(d, fileName))) { skipped++; continue; }
                        }
                    }
                    await Task.Run(() => TransferService.Send(d, S.Source.Path,
                        pct => Dispatcher.Invoke(() => Prog.Value = pct)));
                    Prog.Value = 100;
                    sent++;
                }
                catch (Exception ex) { failed++; lastError = ex.Message; }
            }

            var kind = failed > 0 ? StatusKind.Error : (sent > 0 ? StatusKind.Success : StatusKind.Sub);
            SetStatus(L.T("transferDone", sent, skipped, failed), kind);
            if (failed == 0) Prog.Value = sent > 0 ? 100 : 0;
            if (lastError != null) TxtStatus.ToolTip = lastError;
            RefreshHome(true);
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
        var rt = TransferService.DestModified(d, fileName);
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
