using System;
using System.Collections.Generic;
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
    private readonly ObservableCollection<TransferJob> _jobs = new();
    private bool _jobSync;
    private bool _rbSync;
    private string _srcDir = "";   // pasta atual navegada na ORIGEM
    private string _dstDir = "";   // pasta atual navegada no DESTINO
    private readonly List<System.IO.FileSystemWatcher> _watchers = new();
    private readonly Dictionary<TransferJob, System.Windows.Threading.DispatcherTimer> _watchTimers = new();
    private bool _transferring;

    private AppSettings S => App.Settings;
    // Tarefa selecionada na lista (o que os cards ORIGEM/DESTINO mostram).
    private TransferJob Job => S.CurrentJob;
    // Destino usado para navegacao na home (so quando ha exatamente 1 ativo na tarefa selecionada)
    private Destination? SingleNavDest
    {
        get { var en = Job.Destinations.Where(x => x.Enabled).ToList(); return en.Count == 1 ? en[0] : null; }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public MainWindow()
    {
        InitializeComponent();
        ApplyWindowBounds();   // reabre no tamanho/posicao da ultima vez
        GridSrc.ItemsSource = _src;
        GridDst.ItemsSource = _dst;
        LstJobs.ItemsSource = _jobs;
        Loaded += (_, _) =>
        {
            ApplyDwm();
            ApplyTexts();
            ApplySplit();
            ApplyTasksHeight();
            RefreshJobs();
            RefreshHome();
            UpdateReadyState();
            StartOrStopWatch();
            if (S.WatchEnabled) SetStatus(L.T("watchStatus"), StatusKind.Sub);
            else if (Job.Source.Count == 0)
                SetStatus(L.T("clickSettingsStart"), StatusKind.Sub);
            if (S.AutoUpdateCheck) _ = CheckUpdatesAtStartup();
        };
        KeyDown += MainWindow_KeyDown;
        Closing += (_, _) => SaveWindowBounds();
        Closed += (_, _) => StopAllWatchers();
    }

    // ---------------- Geometria da janela (reabrir igual) ----------------
    private void ApplyWindowBounds()
    {
        if (S.WindowWidth >= 400 && S.WindowHeight >= 400 &&
            IsOnScreen(S.WindowLeft, S.WindowTop, S.WindowWidth, S.WindowHeight))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = S.WindowLeft; Top = S.WindowTop;
            Width = S.WindowWidth; Height = S.WindowHeight;
        }
        if (S.WindowMaximized) WindowState = WindowState.Maximized;
    }

    private void SaveWindowBounds()
    {
        try
        {
            if (WindowState == WindowState.Normal)
            {
                S.WindowLeft = Left; S.WindowTop = Top;
                S.WindowWidth = Width; S.WindowHeight = Height;
                S.WindowMaximized = false;
            }
            else
            {
                // Maximizada/minimizada: RestoreBounds guarda o retangulo "normal".
                var rb = RestoreBounds;
                if (rb.Width > 0 && rb.Height > 0)
                {
                    S.WindowLeft = rb.Left; S.WindowTop = rb.Top;
                    S.WindowWidth = rb.Width; S.WindowHeight = rb.Height;
                }
                S.WindowMaximized = WindowState == WindowState.Maximized;
            }
            SettingsService.Save(S);
        }
        catch { }
    }

    // Garante que a janela restaurada fique visivel (config de monitores pode ter mudado).
    private static bool IsOnScreen(double l, double t, double w, double h)
    {
        var vl = SystemParameters.VirtualScreenLeft;
        var vt = SystemParameters.VirtualScreenTop;
        var vr = vl + SystemParameters.VirtualScreenWidth;
        var vb = vt + SystemParameters.VirtualScreenHeight;
        return l + w > vl + 60 && l < vr - 60 && t + h > vt + 40 && t < vb - 40;
    }

    // ---------------- i18n / tema ----------------
    private void ApplyTexts()
    {
        Title = L.T("appTitle");
        TxtTitle.Text = L.T("appTitle");
        BtnWatch.Content = L.T("watch");
        BtnRefresh.Content = L.T("refresh");
        TxtTasksHdr.Text = L.T("tasks");
        BtnJobNew.Content = L.T("taskNew");
        BtnJobDup.Content = L.T("taskDuplicate");
        BtnJobRen.Content = L.T("taskRename");
        BtnJobDel.Content = L.T("taskRemove");
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

    private void ApplySplit()
    {
        var r = S.SplitRatio;
        if (r < 0.15) r = 0.15; else if (r > 0.85) r = 0.85;
        ColSrc.Width = new GridLength(r, GridUnitType.Star);
        ColDst.Width = new GridLength(1 - r, GridUnitType.Star);
    }

    private void Splitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        var total = ColSrc.ActualWidth + ColDst.ActualWidth;
        if (total > 0)
        {
            S.SplitRatio = ColSrc.ActualWidth / total;
            SettingsService.Save(S);
        }
    }

    private void ApplyTasksHeight()
    {
        var h = S.TasksHeight;
        if (double.IsNaN(h) || h < 140) h = 150;
        if (h > 600) h = 600;
        RowTasks.Height = new GridLength(h, GridUnitType.Pixel);
    }

    private void TasksSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (RowTasks.ActualHeight >= 52)
        {
            S.TasksHeight = RowTasks.ActualHeight;
            SettingsService.Save(S);
        }
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

    // ---------------- Tarefas ----------------
    private void RefreshJobs()
    {
        _jobSync = true;
        _jobs.Clear();
        foreach (var j in S.Jobs) _jobs.Add(j);
        var idx = S.SelectedJob;
        if (idx < 0 || idx >= _jobs.Count) idx = _jobs.Count > 0 ? 0 : -1;
        LstJobs.SelectedIndex = idx;
        _jobSync = false;
    }

    private void Jobs_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_jobSync) return;
        var idx = LstJobs.SelectedIndex;
        if (idx < 0) return;
        S.SelectedJob = idx;
        SettingsService.Save(S);
        RefreshHome();
        UpdateReadyState();
    }

    private void Jobs_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => OpenSettings();

    private void JobEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_jobSync) return;
        SettingsService.Save(S);
        LstJobs.Items.Refresh();
        UpdateReadyState();
        StartOrStopWatch();
    }

    private void JobNew_Click(object sender, RoutedEventArgs e)
    {
        var job = new TransferJob { Name = SettingsService.DefaultJobName(S.Jobs.Count), Enabled = true };
        S.Jobs.Add(job);
        S.SelectedJob = S.Jobs.Count - 1;
        SettingsService.Save(S);
        RefreshJobs();
        RefreshHome();
        UpdateReadyState();
        OpenSettings();   // guia o usuario a escolher arquivo + destino da nova tarefa
    }

    private void JobDuplicate_Click(object sender, RoutedEventArgs e)
    {
        var idx = LstJobs.SelectedIndex;
        if (idx < 0) return;
        var copy = S.Jobs[idx].Clone();
        copy.Name = S.Jobs[idx].Name + " " + L.T("copySuffix");
        S.Jobs.Insert(idx + 1, copy);
        S.SelectedJob = idx + 1;
        SettingsService.Save(S);
        RefreshJobs();
        RefreshHome();
        UpdateReadyState();
        StartOrStopWatch();
    }

    private void JobRename_Click(object sender, RoutedEventArgs e)
    {
        var idx = LstJobs.SelectedIndex;
        if (idx < 0) return;
        var job = S.Jobs[idx];
        var nn = PromptDialog.Ask(this, L.T("taskRename"), L.T("taskNamePrompt"), job.Name);
        if (nn == null) return;
        if (!string.IsNullOrWhiteSpace(nn)) job.Name = nn.Trim();
        SettingsService.Save(S);
        RefreshJobs();
    }

    private void JobRemove_Click(object sender, RoutedEventArgs e)
    {
        var idx = LstJobs.SelectedIndex;
        if (idx < 0) return;
        var job = S.Jobs[idx];
        if (MessageBox.Show(this, L.T("taskRemoveConfirm", job.Name), L.T("taskRemove"),
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        S.Jobs.RemoveAt(idx);
        if (S.Jobs.Count == 0) S.Jobs.Add(new TransferJob { Name = SettingsService.DefaultJobName(0) });
        if (S.SelectedJob >= S.Jobs.Count) S.SelectedJob = S.Jobs.Count - 1;
        SettingsService.Save(S);
        RefreshJobs();
        RefreshHome();
        UpdateReadyState();
        StartOrStopWatch();
    }

    // ---------------- Home ----------------
    private static string FormatSize(long b) => b.ToString("#,0", PtBr);

    // Velocidade adaptativa (B/s..GB/s) e tamanho adaptativo (B..TB), com virgula decimal pt-BR.
    private static string FormatSpeed(double bytesPerSec)
    {
        if (bytesPerSec <= 0) return "";
        string[] u = { "B/s", "KB/s", "MB/s", "GB/s" };
        double v = bytesPerSec; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return v.ToString(i == 0 ? "0" : "0.0", PtBr) + " " + u[i];
    }

    private static string FormatBytes(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes; int i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return v.ToString(i == 0 ? "0" : "0.0", PtBr) + " " + u[i];
    }

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
        // Origem da tarefa selecionada
        var files = Job.Source.All;
        if (files.Count >= 2)
        {
            // Vários arquivos: mostra resumo (não-navegável). Editar em Configurar.
            _srcDir = "";
            _src.Clear();
            TxtSrcPath.Text = L.T("srcCount", files.Count);
            foreach (var f in files)
                _src.Add(new FileRow { Name = "\U0001F4C4  " + Path.GetFileName(f) });
        }
        else
        {
            // 0 ou 1 arquivo: navegação por pastas como antes
            _srcDir = files.Count == 1 ? (Path.GetDirectoryName(files[0]) ?? "") : "";
            RefillSource();
        }

        // Destino(s) da tarefa selecionada
        var dests = Job.Destinations;
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
        var first = Job.Source.First;
        var leaf = (!string.IsNullOrEmpty(first) &&
                    string.Equals(Path.GetDirectoryName(first), _srcDir, StringComparison.OrdinalIgnoreCase))
                    ? Path.GetFileName(first) : null;
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
        // Várias origens: a lista é editada no Configurar.
        if (Job.Source.Count >= 2) { OpenSettings(); return; }
        if (GridSrc.SelectedItem is not FileRow it || string.IsNullOrEmpty(_srcDir)) return;
        if (it.IsUp) { _srcDir = Directory.GetParent(_srcDir)?.FullName ?? _srcDir; RefillSource(); }
        else if (it.IsDir) { _srcDir = Path.Combine(_srcDir, it.RealName); RefillSource(); }
        else if (!string.IsNullOrEmpty(it.RealName))
        {
            // duplo-clique em arquivo = define como (única) origem da tarefa selecionada
            var full = Path.Combine(_srcDir, it.RealName);
            Job.Source.Files = new List<string> { full };
            Job.Source.Path = full;
            SettingsService.Save(S);
            RefillSource();
            LstJobs.Items.Refresh();
            UpdateReadyState();
            StartOrStopWatch();
            SetStatus(it.RealName, StatusKind.Sub);
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
            RefillDestLocal(); LstJobs.Items.Refresh(); UpdateReadyState();
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
            LstJobs.Items.Refresh();
            var prefix = d.Type == DestType.Sftp ? L.T("sftpPrefix") : L.T("ftpPrefix");
            TxtDstPath.Text = prefix + d.Host + np;
            FetchRemoteAt(d, np);
        }
    }

    private bool DestReady(Destination? d)
        => d != null && (d.Type == DestType.Local ? !string.IsNullOrEmpty(d.Folder) : !string.IsNullOrEmpty(d.Host));

    private bool JobHasReadyDest(TransferJob j) => j.Destinations.Any(d => d.Enabled && DestReady(d));
    private bool JobReady(TransferJob j) => j.Enabled && j.Source.Count > 0 && JobHasReadyDest(j);

    private void UpdateReadyState()
    {
        BtnGo.IsEnabled = S.Jobs.Any(JobReady);
        BtnWatch.IsChecked = S.WatchEnabled;
        BtnTheme.Content = S.Theme == "light" ? L.T("darkMode") : L.T("lightMode");
        var hasSc = !string.IsNullOrEmpty(S.Shortcut) && S.Shortcut != "None" && S.Shortcut != "Nenhum";
        TxtHint.Text = hasSc
            ? L.T("shortcutHint", S.Shortcut) + "   ·   " + L.T("refreshHint")
            : L.T("refreshHint");
        _rbSync = true;
        RbAlways.IsChecked = Job.Overwrite == OverwriteMode.Always;
        RbIfNewer.IsChecked = Job.Overwrite == OverwriteMode.IfNewer;
        RbNever.IsChecked = Job.Overwrite == OverwriteMode.Never;
        _rbSync = false;
    }

    private enum StatusKind { Sub, Success, Error }
    private void SetStatus(string text, StatusKind kind)
    {
        TxtStatus.Text = text;
        TxtStatus.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,
            kind switch { StatusKind.Success => "SuccessBrush", StatusKind.Error => "ErrorBrush", _ => "SubTextBrush" });
    }

    private void Overwrite_Changed(object sender, RoutedEventArgs e)
    {
        if (_rbSync) return;
        Job.Overwrite = RbIfNewer.IsChecked == true ? OverwriteMode.IfNewer
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
        if (e.Key.ToString() == S.Shortcut && BtnGo.IsEnabled) { e.Handled = true; _ = DoTransferJobs(S.Jobs.ToList()); }
    }

    private void BtnGo_Click(object sender, RoutedEventArgs e) => _ = DoTransferJobs(S.Jobs.ToList());

    // ---------------- Watch (envio automatico) ----------------
    private void Watch_Toggled(object sender, RoutedEventArgs e)
    {
        S.WatchEnabled = BtnWatch.IsChecked == true;
        SettingsService.Save(S);
        StartOrStopWatch();
        SetStatus(S.WatchEnabled ? L.T("watchStatus") : "", StatusKind.Sub);
    }

    private void StopAllWatchers()
    {
        foreach (var w in _watchers) { try { w.EnableRaisingEvents = false; w.Dispose(); } catch { } }
        _watchers.Clear();
        foreach (var t in _watchTimers.Values) { try { t.Stop(); } catch { } }
        _watchTimers.Clear();
    }

    private void StartOrStopWatch()
    {
        StopAllWatchers();
        if (!S.WatchEnabled) return;
        foreach (var job in S.Jobs.Where(JobReady))
        {
            foreach (var path in job.Source.All)
            {
                var dir = Path.GetDirectoryName(path);
                var file = Path.GetFileName(path);
                if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(file) || !Directory.Exists(dir)) continue;
                try
                {
                    var w = new System.IO.FileSystemWatcher(dir, file)
                    {
                        NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size
                                     | System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.CreationTime
                    };
                    var captured = job;
                    System.IO.FileSystemEventHandler h = (_, _) => OnWatchEvent(captured);
                    w.Changed += h;
                    w.Created += h;
                    w.Renamed += (_, _) => OnWatchEvent(captured);
                    w.EnableRaisingEvents = true;
                    _watchers.Add(w);
                }
                catch { }
            }
        }
    }

    private void OnWatchEvent(TransferJob job)
    {
        // Debounce por tarefa: builds costumam escrever o arquivo varias vezes seguidas
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_watchTimers.TryGetValue(job, out var timer))
            {
                timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                var jb = job;
                timer.Tick += (_, _) => { timer!.Stop(); TriggerWatch(jb); };
                _watchTimers[job] = timer;
            }
            timer.Stop();
            timer.Start();
        }));
    }

    private void TriggerWatch(TransferJob job)
    {
        if (!S.WatchEnabled || _transferring) return;
        if (!JobReady(job) || !job.Source.All.Any(File.Exists)) return;
        _ = DoTransferJobs(new List<TransferJob> { job });
    }

    // ---------------- Transferencia ----------------
    private async Task DoTransferJobs(List<TransferJob> jobs)
    {
        if (_transferring) return;
        jobs = jobs.Where(JobReady).ToList();
        // Ignora tarefas cujos arquivos de origem sumiram; avisa se nenhuma sobrou.
        var withFile = jobs.Where(j => j.Source.All.Any(File.Exists)).ToList();
        if (withFile.Count == 0)
        {
            if (jobs.Count > 0)
            {
                SetStatus(L.T("srcNotFound"), StatusKind.Error);
                MessageBox.Show(this, jobs[0].Source.First, L.T("errorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return;
        }
        jobs = withFile;

        _transferring = true;
        BtnGo.IsEnabled = BtnCfg.IsEnabled = BtnRefresh.IsEnabled = false;
        Prog.Value = 0;
        int sent = 0, skipped = 0, failed = 0;
        string? lastError = null;
        var steps = jobs.Sum(j => j.Source.All.Count(File.Exists) * j.Destinations.Count(d => d.Enabled && DestReady(d)));
        int step = 0;
        try
        {
            foreach (var j in jobs)
            {
                var mode = j.Overwrite;
                var dests = j.Destinations.Where(d => d.Enabled && DestReady(d)).ToList();
                var files = j.Source.All.Where(File.Exists).ToList();
                foreach (var src in files)
                {
                    var fileName = Path.GetFileName(src);
                    foreach (var d in dests)
                    {
                        step++;
                        var label = steps > 1
                            ? j.Name + " · " + fileName + " → " + d.Summary
                            : (d.Type == DestType.Local ? L.T("copying") : L.T("uploading"));
                        SetStatus(L.T("sendingTo", label, step, steps), StatusKind.Sub);
                        Prog.Value = 0;
                        TxtRate.Text = "";
                        var rateSw = System.Diagnostics.Stopwatch.StartNew();
                        double lastUiMs = -1000;   // forca a 1a atualizacao
                        double lastPct = -1;
                        double lastBps = 0;        // preserva ultima taxa nao-zero (janelas do SFTP)
                        try
                        {
                            if (mode != OverwriteMode.Always)
                            {
                                bool exists = await Task.Run(() => TransferService.DestExists(d, fileName));
                                if (exists)
                                {
                                    if (mode == OverwriteMode.Never) { skipped++; continue; }
                                    if (mode == OverwriteMode.IfNewer && await Task.Run(() => !IsSourceNewer(d, src, fileName))) { skipped++; continue; }
                                }
                            }
                            await Task.Run(() => TransferService.Send(d, src, tp =>
                            {
                                if (tp.BytesPerSec > 0) lastBps = tp.BytesPerSec;
                                double nowMs = rateSw.Elapsed.TotalMilliseconds;
                                // Throttle: UI a cada ~200ms OU quando a % mudar >= 1 pt.
                                if (nowMs - lastUiMs < 200 && Math.Abs(tp.Percent - lastPct) < 1) return;
                                lastUiMs = nowMs; lastPct = tp.Percent;
                                var bps = lastBps; var done = tp.Transferred; var total = tp.Total;
                                Dispatcher.Invoke(() =>
                                {
                                    Prog.Value = tp.Percent;
                                    TxtRate.Text = bps > 0
                                        ? FormatSpeed(bps) + " — " + FormatBytes(done) + " / " + FormatBytes(total)
                                        : (total > 0 ? FormatBytes(done) + " / " + FormatBytes(total) : "");
                                });
                            }));
                            Prog.Value = 100;
                            sent++;
                        }
                        catch (Exception ex) { failed++; lastError = ex.Message; }
                    }
                }
            }

            var kind = failed > 0 ? StatusKind.Error : (sent > 0 ? StatusKind.Success : StatusKind.Sub);
            SetStatus(L.T("transferDone", sent, skipped, failed), kind);
            if (failed == 0) Prog.Value = sent > 0 ? 100 : 0;
            TxtStatus.ToolTip = lastError;
            RefreshHome(true);
            LstJobs.Items.Refresh();
        }
        finally
        {
            _transferring = false;
            BtnCfg.IsEnabled = BtnRefresh.IsEnabled = true;
            TxtRate.Text = "";
            UpdateReadyState();
        }
    }

    private bool IsSourceNewer(Destination d, string sourcePath, string fileName)
    {
        var srcT = File.GetLastWriteTime(sourcePath);
        if (d.Type == DestType.Local)
        {
            var dst = Path.Combine(d.Folder, fileName);
            if (!File.Exists(dst)) return true;
            return srcT > File.GetLastWriteTime(dst);
        }
        var rt = TransferService.DestModified(d, fileName);
        return rt == null || srcT > rt.Value;
    }

    // ---------------- Auto-update ----------------
    private async Task CheckUpdatesAtStartup()
    {
        try
        {
            var info = await UpdateService.CheckAsync();
            if (info != null && !_transferring)
                new UpdateWindow(info) { Owner = this }.ShowDialog();
        }
        catch { /* silencioso no arranque (sem internet etc.) */ }
    }

    private void BtnCfg_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void OpenSettings()
    {
        var dlg = new SettingsWindow { Owner = this };
        dlg.ShowDialog();
        // Recarrega tudo (idioma/tema/tarefas podem ter mudado)
        App.Settings = SettingsService.Load();
        L.Lang = S.Language == "en" ? "en" : "pt";
        ThemeManager.Apply(S.Theme);
        ApplyDwm();
        ApplyTexts();
        RefreshJobs();
        RefreshHome();
        UpdateReadyState();
        StartOrStopWatch();
        SetStatus(L.T("settingsSaved"), StatusKind.Sub);
    }
}
