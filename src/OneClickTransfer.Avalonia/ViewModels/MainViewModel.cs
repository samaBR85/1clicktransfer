using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneClickTransfer.Avalonia.ViewModels.Abstractions;
using OneClickTransfer.I18n;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.ViewModels;

/// <summary>ViewModel da Home. Porta a lógica de MainWindow.xaml.cs (WPF) para MVVM estrito.</summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly IDialogService _dialogs;
    private readonly IUiDispatcher _ui;
    private readonly IClipboardService _clipboard;
    private readonly INotificationService _notifications;
    private readonly WatchCoordinator _watch;

    private AppSettings _s;
    private bool _jobSync;      // suprime reação ao trocar SelectedJobIndex durante refresh
    private bool _rbSync;       // suprime reação aos rádios durante sync
    private bool _watchSync;    // suprime reação ao toggle de watch durante sync
    private bool _transferring;
    private string _srcDir = "";
    private string _dstDir = "";

    /// <summary>Disparado após reabrir Configurar (E9): permite ao root recarregar App.Settings.</summary>
    public event Action<AppSettings>? SettingsReloaded;

    public MainViewModel(AppSettings settings, IDialogService dialogs, IUiDispatcher ui, IClipboardService clipboard, INotificationService notifications)
    {
        _s = settings;
        _dialogs = dialogs;
        _ui = ui;
        _clipboard = clipboard;
        _notifications = notifications;
        _watch = new WatchCoordinator(ui, TriggerWatch);
        Source = new FilePanelViewModel(RefreshSource, NavigateSource);
        Dest = new FilePanelViewModel(RefreshDest, NavigateDest);
    }

    // ---------------- Painéis / coleções ----------------
    public ObservableCollection<JobItemViewModel> Jobs { get; } = new();
    public FilePanelViewModel Source { get; }
    public FilePanelViewModel Dest { get; }

    // ---------------- Fila de transferência ----------------
    public ObservableCollection<TransferQueueItem> QueuedItems { get; } = new();
    public ObservableCollection<TransferQueueItem> FailedItems { get; } = new();
    public ObservableCollection<TransferQueueItem> SucceededItems { get; } = new();

    [ObservableProperty] private int _selectedQueueTabIndex;
    partial void OnSelectedQueueTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsQueueTabActive));
        OnPropertyChanged(nameof(IsFailedTabActive));
        OnPropertyChanged(nameof(IsSucceededTabActive));
        OnPropertyChanged(nameof(ActiveQueueItems));
    }
    public bool IsQueueTabActive => SelectedQueueTabIndex == 0;
    public bool IsFailedTabActive => SelectedQueueTabIndex == 1;
    public bool IsSucceededTabActive => SelectedQueueTabIndex == 2;

    /// <summary>Coleção da aba ativa — mesmo DataGrid da View pra todas as abas, então as
    /// larguras de coluna redimensionadas pelo usuário ficam automaticamente compartilhadas.</summary>
    public ObservableCollection<TransferQueueItem> ActiveQueueItems => SelectedQueueTabIndex switch
    {
        1 => FailedItems,
        2 => SucceededItems,
        _ => QueuedItems,
    };

    [RelayCommand] private void SelectQueueQueuedTab() => SelectedQueueTabIndex = 0;
    [RelayCommand] private void SelectQueueFailedTab() => SelectedQueueTabIndex = 1;
    [RelayCommand] private void SelectQueueSucceededTab() => SelectedQueueTabIndex = 2;

    public bool HasQueueActivity => QueuedItems.Count > 0 || FailedItems.Count > 0 || SucceededItems.Count > 0;
    public string QueuePanelTitle => L.T("queuePanelTitle");
    public string QueueTabQueuedLabel => L.T("queueTabQueued", QueuedItems.Count);
    public string QueueTabFailedLabel => L.T("queueTabFailed", FailedItems.Count);
    public string QueueTabSucceededLabel => L.T("queueTabSucceeded", SucceededItems.Count);
    public string QueueClearCompletedLabel => L.T("queueClearCompleted");
    public string QueueColName => L.T("queueColName");
    public string QueueColProgress => L.T("queueColProgress");
    public string QueueColSize => L.T("queueColSize");
    public string QueueColTime => L.T("queueColTime");
    public bool CanClearCompletedQueue => FailedItems.Count > 0 || SucceededItems.Count > 0;

    public string QueueTotalText
    {
        get
        {
            if (QueuedItems.Count == 0) return L.T("queueEmpty");
            var totalBytes = QueuedItems.Sum(i => i.SizeBytes);
            return L.T("queueTotal", FormatBytes(totalBytes));
        }
    }

    [RelayCommand]
    private void ClearCompletedQueue()
    {
        FailedItems.Clear();
        SucceededItems.Clear();
        RaiseQueueInfo();
    }

    private void RaiseQueueInfo()
    {
        OnPropertyChanged(nameof(HasQueueActivity));
        OnPropertyChanged(nameof(QueueTabQueuedLabel));
        OnPropertyChanged(nameof(QueueTabFailedLabel));
        OnPropertyChanged(nameof(QueueTabSucceededLabel));
        OnPropertyChanged(nameof(QueueTotalText));
        OnPropertyChanged(nameof(CanClearCompletedQueue));
    }

    private AppSettings S => _s;
    private TransferJob Job => _s.CurrentJob;
    private Destination? SingleNavDest
    {
        get { var en = Job.Destinations.Where(x => x.Enabled).ToList(); return en.Count == 1 ? en[0] : null; }
    }

    // ---------------- Estado observável ----------------
    [ObservableProperty] private int _selectedJobIndex = -1;
    [ObservableProperty] private bool _overwriteAlways;
    [ObservableProperty] private bool _overwriteIfNewer;
    [ObservableProperty] private bool _overwriteNever;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _rateText = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _statusSuccess;
    [ObservableProperty] private bool _statusError;
    [ObservableProperty] private bool _isWatchChecked;
    [ObservableProperty] private bool _canTransfer;
    [ObservableProperty] private bool _isTransferring;

    public bool CanGo => CanTransfer && !IsTransferring;
    public bool NotTransferring => !IsTransferring;

    // Painel da tarefa selecionada (topo, coluna direita)
    public string SelectedJobName => Job.Name;
    public string SelectedJobFilesText => L.T("srcCount", Job.Source.Count);
    public string SelectedJobDestsText => L.T("destCount", Job.Destinations.Count(d => d.Enabled));
    public bool CanTransferSelected => JobReady(Job) && !IsTransferring;
    public string SendThisTaskLabel => L.T("sendThisTask");
    public string EditTaskLabel => L.T("editTaskBtn");

    private void RaiseSelectedJobInfo()
    {
        OnPropertyChanged(nameof(SelectedJobName));
        OnPropertyChanged(nameof(SelectedJobFilesText));
        OnPropertyChanged(nameof(SelectedJobDestsText));
        OnPropertyChanged(nameof(CanTransferSelected));
    }

    partial void OnSelectedJobIndexChanged(int value)
    {
        if (_jobSync) return;
        if (value < 0) return;
        S.SelectedJob = value;
        SettingsService.Save(S);
        // Ao trocar de tarefa, busca o FTP/SFTP de verdade (nao so o placeholder "clique p/ atualizar") --
        // FetchRemoteAt ja roda via Task.Run e trata falha com "(FTP address offline)" em vez de travar.
        RefreshSourcePanel();
        RefreshDestPanel(fetchFtp: true);
        UpdateReadyState();
    }

    partial void OnOverwriteAlwaysChanged(bool value) { if (value) SetOverwrite(OverwriteMode.Always); }
    partial void OnOverwriteIfNewerChanged(bool value) { if (value) SetOverwrite(OverwriteMode.IfNewer); }
    partial void OnOverwriteNeverChanged(bool value) { if (value) SetOverwrite(OverwriteMode.Never); }

    private void SetOverwrite(OverwriteMode mode)
    {
        if (_rbSync) return;
        Job.Overwrite = mode;
        SettingsService.Save(S);
    }

    partial void OnCanTransferChanged(bool value) => OnPropertyChanged(nameof(CanGo));

    partial void OnIsTransferringChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGo));
        OnPropertyChanged(nameof(NotTransferring));
        OnPropertyChanged(nameof(CanTransferSelected));
    }

    partial void OnIsWatchCheckedChanged(bool value)
    {
        if (_watchSync) return;
        Job.Watch = value;
        SettingsService.Save(S);
        JobItemFor(Job)?.Refresh();     // atualiza o selo 👁 na lista
        _watch.Restart(WatchedJobs());
        SetStatus(value ? L.T("watchStatus") : "", StatusKind.Sub);
    }

    // ---------------- Textos (i18n) ----------------
    public string Title => L.T("appTitle");
    public string VersionText => "v" + UpdateService.Current;
    public string TasksHeader => L.T("tasks");
    public string NewLabel => L.T("taskNew");
    public string DuplicateLabel => L.T("taskDuplicate");
    public string RenameLabel => L.T("taskRename");
    public string RemoveLabel => L.T("taskRemove");
    public string SourceHeader => L.T("source");
    public string DestHeader => L.T("destination");
    public string WatchTip => L.T("watchTip");
    public string RefreshTip => L.T("refreshTip");
    public string ColName => L.T("colName");
    public string ColSize => L.T("colSize");
    public string ColModified => L.T("colModified");
    public string ActionLabel => L.T("action");
    public string ReplaceLabel => L.T("replace");
    public string ReplaceIfNewerLabel => L.T("replaceIfNewer");
    public string DontReplaceLabel => L.T("dontReplace");
    public string TransferLabel => L.T("transfer");
    public string SettingsLabel => L.T("settings");

    public string HintText
    {
        get
        {
            var hasSc = !string.IsNullOrEmpty(S.Shortcut) && S.Shortcut != "None" && S.Shortcut != "Nenhum";
            return hasSc
                ? L.T("shortcutHint", S.Shortcut) + "   ·   " + L.T("refreshHint")
                : L.T("refreshHint");
        }
    }

    public bool HasLastTransfer => S.LastTransferAt.HasValue;

    public string LastTransferText
    {
        get
        {
            if (S.LastTransferAt is not DateTime dt) return "";
            var now = DateTime.Now;
            return dt.Date == now.Date
                ? L.T("lastTransferToday", dt.ToString("HH:mm"))
                : L.T("lastTransferOther", dt.ToString("dd/MM"), dt.ToString("HH:mm"));
        }
    }

    private void Retranslate()
    {
        foreach (var p in new[] { nameof(Title), nameof(TasksHeader), nameof(NewLabel), nameof(DuplicateLabel),
            nameof(RenameLabel), nameof(RemoveLabel), nameof(SourceHeader), nameof(DestHeader), nameof(WatchTip),
            nameof(RefreshTip), nameof(ColName), nameof(ColSize), nameof(ColModified), nameof(ActionLabel),
            nameof(ReplaceLabel), nameof(ReplaceIfNewerLabel), nameof(DontReplaceLabel), nameof(TransferLabel),
            nameof(SettingsLabel), nameof(HintText), nameof(LastTransferText), nameof(QueuePanelTitle),
            nameof(QueueClearCompletedLabel), nameof(QueueColName), nameof(QueueColProgress),
            nameof(QueueColSize), nameof(QueueColTime) })
            OnPropertyChanged(p);
        RaiseQueueInfo();
    }

    // ---------------- Ciclo de vida ----------------
    /// <summary>Chamado pela View no Opened (equivale ao Loaded do WPF).</summary>
    public void OnOpened()
    {
        RefreshJobs();
        RefreshHome();
        UpdateReadyState();
        _watch.Restart(WatchedJobs());
        if (Job.Watch) SetStatus(L.T("watchStatus"), StatusKind.Sub);
        else if (Job.Source.Count == 0) SetStatus(L.T("clickSettingsStart"), StatusKind.Sub);
        if (S.AutoUpdateCheck) _ = CheckUpdatesAtStartupAsync();
    }

    public void OnClosed() => _watch.Stop();

    // ---------------- Tarefas ----------------
    private JobItemViewModel? JobItemFor(TransferJob job) => Jobs.FirstOrDefault(j => ReferenceEquals(j.Model, job));
    private IEnumerable<TransferJob> WatchedJobs() => S.Jobs.Where(j => j.Watch && JobReady(j));

    private void RefreshJobs()
    {
        _jobSync = true;
        Jobs.Clear();
        foreach (var j in S.Jobs) Jobs.Add(new JobItemViewModel(j, OnJobEnabledChanged));
        var idx = S.SelectedJob;
        if (idx < 0 || idx >= Jobs.Count) idx = Jobs.Count > 0 ? 0 : -1;
        SelectedJobIndex = idx;
        S.SelectedJob = idx < 0 ? 0 : idx;
        _jobSync = false;
    }

    private void OnJobEnabledChanged()
    {
        SettingsService.Save(S);
        UpdateReadyState();
        _watch.Restart(WatchedJobs());
    }

    /// <summary>Duplo-clique numa tarefa: abre o editor da tarefa.</summary>
    [RelayCommand]
    private Task JobActivateAsync() => EditTaskAsync();

    [RelayCommand]
    private async Task EditTaskAsync()
    {
        await _dialogs.ShowTaskEditorAsync();
        // O editor mutou o mesmo App.Settings; basta refrescar (idioma/tema não mudam aqui).
        RefreshJobs();
        RefreshHome();
        UpdateReadyState();
        _watch.Restart(WatchedJobs());
        SetStatus(L.T("settingsSaved"), StatusKind.Sub);
    }

    [RelayCommand]
    private async Task NewJobAsync()
    {
        var job = new TransferJob { Name = SettingsService.DefaultJobName(S.Jobs.Count), Enabled = true };
        S.Jobs.Add(job);
        S.SelectedJob = S.Jobs.Count - 1;
        SettingsService.Save(S);
        RefreshJobs();
        RefreshHome();
        UpdateReadyState();

        var saved = await _dialogs.ShowTaskEditorAsync();   // configura arquivo + destino da nova tarefa
        // Cancelou e a tarefa nova ficou vazia -> remove (não polui a lista).
        if (!saved && job.Source.Count == 0 && job.Destinations.Count == 0)
        {
            S.Jobs.Remove(job);
            if (S.Jobs.Count == 0) S.Jobs.Add(new TransferJob { Name = SettingsService.DefaultJobName(0) });
            if (S.SelectedJob >= S.Jobs.Count) S.SelectedJob = S.Jobs.Count - 1;
            SettingsService.Save(S);
        }
        RefreshJobs();
        RefreshHome();
        UpdateReadyState();
        _watch.Restart(WatchedJobs());
    }

    [RelayCommand]
    private void DuplicateJob()
    {
        var idx = SelectedJobIndex;
        if (idx < 0) return;
        var copy = S.Jobs[idx].Clone();
        copy.Name = S.Jobs[idx].Name + " " + L.T("copySuffix");
        S.Jobs.Insert(idx + 1, copy);
        S.SelectedJob = idx + 1;
        SettingsService.Save(S);
        RefreshJobs();
        RefreshHome();
        UpdateReadyState();
        _watch.Restart(WatchedJobs());
    }

    [RelayCommand]
    private async Task RenameJobAsync()
    {
        var idx = SelectedJobIndex;
        if (idx < 0) return;
        var job = S.Jobs[idx];
        var nn = await _dialogs.PromptAsync(L.T("taskRename"), L.T("taskNamePrompt"), job.Name);
        if (nn == null) return;
        if (!string.IsNullOrWhiteSpace(nn)) job.Name = nn.Trim();
        SettingsService.Save(S);
        JobItemFor(job)?.Refresh();
    }

    [RelayCommand]
    private async Task RemoveJobAsync()
    {
        var idx = SelectedJobIndex;
        if (idx < 0) return;
        var job = S.Jobs[idx];
        if (!await _dialogs.ConfirmAsync(L.T("taskRemove"), L.T("taskRemoveConfirm", job.Name))) return;
        S.Jobs.RemoveAt(idx);
        if (S.Jobs.Count == 0) S.Jobs.Add(new TransferJob { Name = SettingsService.DefaultJobName(0) });
        if (S.SelectedJob >= S.Jobs.Count) S.SelectedJob = S.Jobs.Count - 1;
        SettingsService.Save(S);
        RefreshJobs();
        RefreshHome();
        UpdateReadyState();
        _watch.Restart(WatchedJobs());
    }

    // ---------------- Home (listagens) ----------------
    private static string FormatSize(long b) => b.ToString("#,0", PtBr);

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

    private static FileRow ToRow(RemoteEntry e, bool highlight) => new()
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
        RefreshSourcePanel();
        RefreshDestPanel(fetchFtp);
    }

    private void RefreshSourcePanel()
    {
        var files = Job.Source.All;
        bool isFolder = Job.Source.Kind == SourceKind.Folder;
        if (files.Count >= 2 || isFolder)
        {
            _srcDir = "";
            Source.Rows.Clear();
            Source.PathText = isFolder ? L.T("folderPrefix") + Job.Source.Path : L.T("srcCount", files.Count);
            if (isFolder && files.Count == 0) Source.Rows.Add(InfoRow("emptyFolder"));
            else foreach (var f in files) Source.Rows.Add(BuildFileRow(f, isFolder ? Job.Source.Path : null));
        }
        else
        {
            _srcDir = files.Count == 1 ? (Path.GetDirectoryName(files[0]) ?? "") : "";
            RefillSource();
        }
    }

    private static FileRow BuildFileRow(string f, string? relativeToRoot)
    {
        var fi = new FileInfo(f);
        var displayName = relativeToRoot != null ? Path.GetRelativePath(relativeToRoot, f) : Path.GetFileName(f);
        return new FileRow
        {
            Name = "\U0001F4C4  " + displayName,
            Size = fi.Exists ? FormatSize(fi.Length) : "",
            Modified = fi.Exists ? fi.LastWriteTime.ToString("dd/MM/yyyy HH:mm") : "",
            RealName = Path.GetFileName(f)
        };
    }

    private void RefreshDestPanel(bool fetchFtp = false)
    {
        var dests = Job.Destinations;
        Dest.Rows.Clear();
        if (dests.Count == 0) { Dest.PathText = L.T("noDest"); return; }

        var enabled = dests.Where(x => x.Enabled).ToList();
        if (enabled.Count == 1)
        {
            var d = enabled[0];
            _dstDir = d.Folder;
            if (d.Type == DestType.Local)
            {
                RefillDestLocal();
            }
            else
            {
                var prefix = d.Type == DestType.Sftp ? L.T("sftpPrefix") : L.T("ftpPrefix");
                Dest.PathText = prefix + d.Host + (_dstDir ?? "/");
                if (fetchFtp) FetchRemoteAt(d, string.IsNullOrEmpty(_dstDir) ? "/" : _dstDir);
                else Dest.Rows.Add(InfoRow("clickRefreshFtp"));
            }
            return;
        }

        Dest.PathText = L.T("destCount", enabled.Count);
        foreach (var dd in dests)
            Dest.Rows.Add(new FileRow { Name = (dd.Enabled ? "☑  " : "☐  ") + dd.Summary });
    }

    private void RefillSource()
    {
        Source.Rows.Clear();
        if (string.IsNullOrEmpty(_srcDir) || !Directory.Exists(_srcDir)) { Source.PathText = L.T("noFile"); return; }
        Source.PathText = L.T("folderPrefix") + _srcDir;
        var first = Job.Source.First;
        var leaf = (!string.IsNullOrEmpty(first) &&
                    string.Equals(Path.GetDirectoryName(first), _srcDir, StringComparison.OrdinalIgnoreCase))
                    ? Path.GetFileName(first) : null;
        if (Directory.GetParent(_srcDir) != null) Source.Rows.Add(UpRow());
        foreach (var it in TransferService.LocalList(_srcDir))
            Source.Rows.Add(ToRow(it, leaf != null && it.Name.Equals(leaf, StringComparison.OrdinalIgnoreCase)));
    }

    private void RefillDestLocal()
    {
        Dest.Rows.Clear();
        if (string.IsNullOrEmpty(_dstDir)) { Dest.PathText = L.T("noDest"); return; }
        Dest.PathText = L.T("folderPrefix") + _dstDir;
        if (!Directory.Exists(_dstDir)) return;
        if (Directory.GetParent(_dstDir) != null) Dest.Rows.Add(UpRow());
        foreach (var it in TransferService.LocalList(_dstDir))
            Dest.Rows.Add(ToRow(it, false));
    }

    private async void FetchRemoteAt(Destination d, string path)
    {
        Dest.Rows.Clear();
        Dest.Rows.Add(InfoRow("loadingFtp"));
        try
        {
            var list = await Task.Run(() => TransferService.ListPath(d, path));
            Dest.Rows.Clear();
            if (path.TrimEnd('/').Length > 0) Dest.Rows.Add(UpRow());
            foreach (var it in list.OrderByDescending(x => x.IsDir).ThenBy(x => x.Name))
                Dest.Rows.Add(ToRow(it, false));
            if (Dest.Rows.Count == 0 || (Dest.Rows.Count == 1 && Dest.Rows[0].IsUp)) Dest.Rows.Add(InfoRow("emptyFolder"));
        }
        catch
        {
            Dest.Rows.Clear();
            if (path.TrimEnd('/').Length > 0) Dest.Rows.Add(UpRow());
            Dest.Rows.Add(InfoRow("ftpOffline"));
        }
    }

    // ---------------- Navegação (duplo-clique) ----------------
    private void NavigateSource(FileRow? it)
    {
        if (Job.Source.Count >= 2 || Job.Source.Kind == SourceKind.Folder) { _ = EditTaskAsync(); return; }   // múltiplas origens/pasta: edita no editor da tarefa
        if (it is null || string.IsNullOrEmpty(_srcDir)) return;
        if (it.IsUp) { _srcDir = Directory.GetParent(_srcDir)?.FullName ?? _srcDir; RefillSource(); }
        else if (it.IsDir) { _srcDir = Path.Combine(_srcDir, it.RealName); RefillSource(); }
        else if (!string.IsNullOrEmpty(it.RealName))
        {
            var full = Path.Combine(_srcDir, it.RealName);
            Job.Source.Files = new List<string> { full };
            Job.Source.Path = full;
            SettingsService.Save(S);
            RefillSource();
            JobItemFor(Job)?.Refresh();
            UpdateReadyState();
            _watch.Restart(WatchedJobs());
            SetStatus(it.RealName, StatusKind.Sub);
        }
    }

    private void NavigateDest(FileRow? it)
    {
        var d = SingleNavDest;
        if (d == null || it is null) return;
        if (!it.IsDir && !it.IsUp) return;

        if (d.Type == DestType.Local)
        {
            if (string.IsNullOrEmpty(_dstDir)) return;
            if (it.IsUp) _dstDir = Directory.GetParent(_dstDir)?.FullName ?? _dstDir;
            else _dstDir = Path.Combine(_dstDir, it.RealName);
            d.Folder = _dstDir; SettingsService.Save(S);
            RefillDestLocal(); JobItemFor(Job)?.Refresh(); UpdateReadyState();
        }
        else
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
            JobItemFor(Job)?.Refresh();
            var prefix = d.Type == DestType.Sftp ? L.T("sftpPrefix") : L.T("ftpPrefix");
            Dest.PathText = prefix + d.Host + np;
            FetchRemoteAt(d, np);
        }
    }

    // ---------------- Menu de contexto (DESTINATION: local + FTP/SFTP) ----------------
    public string CtxCreateFolderLabel => L.T("ctxCreateFolder");
    public string CtxRenameLabel => L.T("rename");
    public string CtxDeleteLabel => L.T("delete");
    public string CtxCopyPathLabel => L.T("ctxCopyPath");

    private string JoinDestPath(string name)
        => SingleNavDest?.Type == DestType.Local ? Path.Combine(_dstDir, name) : _dstDir.TrimEnd('/') + "/" + name;

    [RelayCommand]
    private async Task CreateFolderInDestAsync()
    {
        var d = SingleNavDest;
        if (d == null) return;
        var name = await _dialogs.PromptAsync(L.T("ctxCreateFolder"), L.T("ctxNewFolderPrompt"), "");
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            var parent = _dstDir;
            await Task.Run(() => TransferService.CreateFolder(d, parent, name.Trim()));
            RefreshDestPanel(fetchFtp: true);
        }
        catch (Exception ex) { SetStatus(L.T("ctxOpFailed", ex.Message), StatusKind.Error); }
    }

    [RelayCommand]
    private async Task RenameDestItemAsync(FileRow? it)
    {
        var d = SingleNavDest;
        if (d == null || it is null || it.IsUp) return;
        var newName = await _dialogs.PromptAsync(L.T("rename"), L.T("ctxRenamePrompt"), it.RealName);
        if (string.IsNullOrWhiteSpace(newName) || newName == it.RealName) return;
        try
        {
            var oldPath = JoinDestPath(it.RealName);
            var newPath = JoinDestPath(newName.Trim());
            await Task.Run(() => TransferService.Rename(d, oldPath, newPath));
            RefreshDestPanel(fetchFtp: true);
        }
        catch (Exception ex) { SetStatus(L.T("ctxOpFailed", ex.Message), StatusKind.Error); }
    }

    [RelayCommand]
    private async Task DeleteDestItemAsync(FileRow? it)
    {
        var d = SingleNavDest;
        if (d == null || it is null || it.IsUp) return;
        if (!await _dialogs.ConfirmAsync(L.T("delete"), L.T("ctxDeleteConfirm", it.RealName))) return;
        try
        {
            var path = JoinDestPath(it.RealName);
            await Task.Run(() => TransferService.Delete(d, path, it.IsDir));
            RefreshDestPanel(fetchFtp: true);
        }
        catch (Exception ex) { SetStatus(L.T("ctxOpFailed", ex.Message), StatusKind.Error); }
    }

    [RelayCommand]
    private async Task CopyDestPathAsync(FileRow? it)
    {
        if (SingleNavDest == null || it is null || it.IsUp) return;
        var path = JoinDestPath(it.RealName);
        await _clipboard.SetTextAsync(path);
        FlashStatus(L.T("ctxPathCopied"));
    }

    // ---------------- Estado / rádios / status ----------------
    private bool DestReady(Destination? d)
        => d != null && (d.Type == DestType.Local ? !string.IsNullOrEmpty(d.Folder) : !string.IsNullOrEmpty(d.Host));
    private bool JobHasReadyDest(TransferJob j) => j.Destinations.Any(d => d.Enabled && DestReady(d));
    private bool JobReady(TransferJob j) => j.Enabled && j.Source.Count > 0 && JobHasReadyDest(j);

    private void UpdateReadyState()
    {
        CanTransfer = S.Jobs.Any(JobReady);

        _watchSync = true;
        IsWatchChecked = Job.Watch;
        _watchSync = false;

        _rbSync = true;
        OverwriteAlways = Job.Overwrite == OverwriteMode.Always;
        OverwriteIfNewer = Job.Overwrite == OverwriteMode.IfNewer;
        OverwriteNever = Job.Overwrite == OverwriteMode.Never;
        _rbSync = false;

        RaiseSelectedJobInfo();
        OnPropertyChanged(nameof(HintText));
    }

    private enum StatusKind { Sub, Success, Error }
    private void SetStatus(string text, StatusKind kind)
    {
        StatusText = text;
        StatusSuccess = kind == StatusKind.Success;
        StatusError = kind == StatusKind.Error;
    }

    // ---------------- Comandos de refresh ----------------
    // Feedback: uma confirmação verde que some sozinha (o refresh local é instantâneo e "invisível").
    private System.Threading.Timer? _flashTimer;
    private void FlashStatus(string text)
    {
        SetStatus(text, StatusKind.Success);
        _flashTimer?.Dispose();
        _flashTimer = new System.Threading.Timer(
            _ => _ui.Post(() => { if (StatusText == text) SetStatus("", StatusKind.Sub); }),
            null, 1600, System.Threading.Timeout.Infinite);
    }

    private void RefreshSource()
    {
        if (_transferring) return;
        RefreshSourcePanel();
        FlashStatus(L.T("refreshedOk"));
    }

    private void RefreshDest()
    {
        if (_transferring) return;
        RefreshDestPanel(true);
        FlashStatus(L.T("refreshedOk"));
    }

    /// <summary>F5: refresh completo dos dois painéis (busca o FTP).</summary>
    public void RefreshAll()
    {
        if (_transferring) return;
        RefreshHome(true);
        FlashStatus(L.T("refreshedOk"));
    }

    /// <summary>Trata F5 (refresh) e o atalho configurável (transferir). Retorna true se consumiu a tecla.</summary>
    public bool HandleKey(string key)
    {
        if (key == "F5") { RefreshAll(); return true; }
        if (string.IsNullOrEmpty(S.Shortcut) || S.Shortcut is "None" or "Nenhum") return false;
        if (key == S.Shortcut && CanGo) { _ = TransferAsync(); return true; }
        return false;
    }

    // ---------------- Watch ----------------
    private void TriggerWatch(TransferJob job)
    {
        if (_transferring || !job.Watch) return;
        if (!JobReady(job) || !job.Source.All.Any(File.Exists)) return;
        _ = DoTransferJobs(new List<TransferJob> { job });
    }

    // ---------------- Transferência ----------------
    [RelayCommand]
    private Task TransferAsync() => DoTransferJobs(S.Jobs.ToList());

    /// <summary>Envia só a tarefa selecionada (botão do painel direito do topo).</summary>
    [RelayCommand]
    private Task TransferSelectedAsync() => DoTransferJobs(new List<TransferJob> { Job });

    private sealed class WorkItem
    {
        public TransferJob Job = null!;
        public string Src = "";
        public string RelPath = "";
        public Destination Dest = null!;
        public TransferQueueItem QueueItem = null!;
    }


    private static long SafeFileLength(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }

    private async Task DoTransferJobs(List<TransferJob> jobs)
    {
        if (_transferring) return;
        jobs = jobs.Where(JobReady).ToList();
        var withFile = jobs.Where(j => j.Source.All.Any(File.Exists)).ToList();
        if (withFile.Count == 0)
        {
            if (jobs.Count > 0)
            {
                SetStatus(L.T("srcNotFound"), StatusKind.Error);
                await _dialogs.ShowMessageAsync(L.T("errorTitle"), jobs[0].Source.First, error: true);
            }
            return;
        }
        jobs = withFile;

        _transferring = true;
        IsTransferring = true;
        ProgressValue = 0;
        int sent = 0, skipped = 0, failed = 0;
        string? lastError = null;

        // Monta a lista de work-items (arquivo x destino) ANTES de transferir; modo de sobrescrita
        // ja decide aqui quem entra na fila ou e pulado direto (skip nao aparece na fila visualmente).
        var workItems = new List<WorkItem>();
        foreach (var j in jobs)
        {
            var dests = j.Destinations.Where(d => d.Enabled && DestReady(d)).ToList();
            var files = j.Source.All.Where(File.Exists).ToList();
            var relPaths = files.Select(f => j.Source.RelPathFor(f)).ToList();

            // 1 listagem por destino (não por arquivo) antes do loop de sempre -- evita reconectar
            // via FTP/SFTP pra cada arquivo só pra checar exists/modified.
            var caches = new Dictionary<Destination, RemoteListingCache?>();
            foreach (var d in dests)
                caches[d] = j.Overwrite != OverwriteMode.Always
                    ? await Task.Run(() => TransferService.BuildListingCache(d, relPaths))
                    : null;

            foreach (var src in files)
            {
                var fileName = Path.GetFileName(src);
                var relPath = j.Source.RelPathFor(src);
                foreach (var d in dests)
                {
                    if (j.Overwrite != OverwriteMode.Always)
                    {
                        var cache = caches[d];
                        bool exists = await Task.Run(() => TransferService.DestExists(d, relPath, cache));
                        if (exists)
                        {
                            if (j.Overwrite == OverwriteMode.Never) { skipped++; continue; }
                            if (j.Overwrite == OverwriteMode.IfNewer && await Task.Run(() => !TransferService.IsSourceNewer(d, src, relPath, cache))) { skipped++; continue; }
                        }
                    }
                    var qi = new TransferQueueItem
                    {
                        FileName = fileName,
                        DestSummary = d.Summary,
                        SizeBytes = SafeFileLength(src),
                        State = QueueItemState.Queued,
                        StatusText = L.T("queueWaiting")
                    };
                    workItems.Add(new WorkItem { Job = j, Src = src, RelPath = relPath, Dest = d, QueueItem = qi });
                }
            }
        }

        _ui.Post(() =>
        {
            foreach (var w in workItems) QueuedItems.Add(w.QueueItem);
            RaiseQueueInfo();
        });

        var steps = workItems.Count;
        int started = 0;
        int completed = 0;

        try
        {
            var maxParallel = Math.Max(1, S.MaxParallelDestinations);
            using var gate = new SemaphoreSlim(maxParallel);

            var tasks = workItems.Select(async w =>
            {
                await gate.WaitAsync();
                try
                {
                    var d = w.Dest; var src = w.Src; var qi = w.QueueItem;
                    var startedNow = Interlocked.Increment(ref started);
                    _ui.Post(() =>
                    {
                        qi.State = QueueItemState.Running;
                        qi.StatusText = d.Type == DestType.Local ? L.T("copying") : L.T("uploading");
                        SetStatus(L.T("sendingTo", w.Job.Name + " · " + qi.FileName + " → " + qi.DestSummary, startedNow, steps), StatusKind.Sub);
                    });

                    var rateSw = Stopwatch.StartNew();
                    double lastUiMs = -1000, lastPct = -1, lastBps = 0;
                    try
                    {
                        await Task.Run(() => TransferService.Send(d, src, tp =>
                        {
                            if (tp.BytesPerSec > 0) lastBps = tp.BytesPerSec;
                            double nowMs = rateSw.Elapsed.TotalMilliseconds;
                            if (nowMs - lastUiMs < 200 && Math.Abs(tp.Percent - lastPct) < 1) return;
                            lastUiMs = nowMs; lastPct = tp.Percent;
                            var bps = lastBps; var done = tp.Transferred; var total = tp.Total;
                            _ui.Post(() =>
                            {
                                qi.Progress = tp.Percent;
                                qi.StatusText = bps > 0
                                    ? FormatSpeed(bps) + " — " + FormatBytes(done) + " / " + FormatBytes(total)
                                    : (total > 0 ? FormatBytes(done) + " / " + FormatBytes(total) : "");
                            });
                        }, w.RelPath));
                        Interlocked.Increment(ref sent);
                        var doneNow = Interlocked.Increment(ref completed);
                        _ui.Post(() =>
                        {
                            qi.Progress = 100;
                            qi.State = QueueItemState.Success;
                            qi.FinishedAt = DateTime.Now;
                            QueuedItems.Remove(qi);
                            SucceededItems.Insert(0, qi);
                            RaiseQueueInfo();
                            ProgressValue = steps > 0 ? doneNow * 100.0 / steps : 0;
                        });
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref failed);
                        lastError = ex.Message;
                        var doneNow = Interlocked.Increment(ref completed);
                        _ui.Post(() =>
                        {
                            qi.State = QueueItemState.Failed;
                            qi.FinishedAt = DateTime.Now;
                            qi.ErrorMessage = ex.Message;
                            QueuedItems.Remove(qi);
                            FailedItems.Insert(0, qi);
                            RaiseQueueInfo();
                            ProgressValue = steps > 0 ? doneNow * 100.0 / steps : 0;
                        });
                    }
                }
                finally { gate.Release(); }
            });

            await Task.WhenAll(tasks);

            var kind = failed > 0 ? StatusKind.Error : (sent > 0 ? StatusKind.Success : StatusKind.Sub);
            SetStatus(L.T("transferDone", sent, skipped, failed), kind);
            _notifications.Notify(L.T("appTitle"), L.T("transferDone", sent, skipped, failed), failed > 0);
            if (sent > 0)
            {
                S.LastTransferAt = DateTime.Now;
                SettingsService.Save(S);
                OnPropertyChanged(nameof(LastTransferText));
                OnPropertyChanged(nameof(HasLastTransfer));
            }
            if (failed == 0) ProgressValue = sent > 0 ? 100 : 0;
            _ = lastError;   // detalhe do erro fica disponível p/ tooltip (E9 refina)
            RefreshHome(true);
            foreach (var j in Jobs) j.Refresh();
        }
        finally
        {
            _transferring = false;
            IsTransferring = false;
            RateText = "";
            UpdateReadyState();
        }
    }

    // ---------------- Auto-update / Configurar ----------------
    private async Task CheckUpdatesAtStartupAsync()
    {
        try
        {
            var info = await UpdateService.CheckAsync();
            if (info != null && !_transferring) await _dialogs.ShowUpdateAsync(info);
        }
        catch { /* silencioso no arranque (sem internet, E10 pendente etc.) */ }
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        try { await _dialogs.ShowSettingsAsync(); }
        catch (NotImplementedException) { return; }   // Settings chega na E9

        _s = SettingsService.Load();
        L.Lang = _s.Language == "en" ? "en" : "pt";
        SettingsReloaded?.Invoke(_s);
        Retranslate();
        RefreshJobs();
        RefreshHome();
        UpdateReadyState();
        _watch.Restart(WatchedJobs());
        SetStatus(L.T("settingsSaved"), StatusKind.Sub);
    }
}
