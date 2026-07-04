using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneClickTransfer.Avalonia.ViewModels.Abstractions;
using OneClickTransfer.I18n;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.ViewModels;

/// <summary>Editor da tarefa selecionada: arquivos de origem + destinos + grupos + presets.
/// ShowDialog&lt;bool&gt; via CloseRequested (true = salvou).</summary>
public sealed partial class TaskEditorViewModel : ViewModelBase
{
    private readonly AppSettings _s;
    private readonly IDialogService _dialogs;
    private readonly IFilePickerService _files;
    private bool _grpSync;
    private bool _profSync;

    public event Action<bool>? CloseRequested;

    public TaskEditorViewModel(AppSettings s, IDialogService dialogs, IFilePickerService files)
    {
        _s = s;
        _dialogs = dialogs;
        _files = files;

        Title = L.T("taskEditorTitle", _s.CurrentJob.Name);
        TaskName = _s.CurrentJob.Name;
        LoadSourceFromSpec(_s.CurrentJob.Source);
        LoadDests(_s.CurrentJob.Destinations);
        ReloadGroups();
        ReloadProfiles();
    }

    // ---------------- Coleções ----------------
    public ObservableCollection<string> SrcFiles { get; } = new();
    public ObservableCollection<Destination> Dests { get; } = new();
    public ObservableCollection<string> GroupOptions { get; } = new();
    public ObservableCollection<string> ProfileOptions { get; } = new();

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _taskName = "";
    [ObservableProperty] private int _selectedSrcIndex = -1;
    [ObservableProperty] private int _selectedDestIndex = -1;
    [ObservableProperty] private int _selectedGroupIndex;
    [ObservableProperty] private int _selectedProfileIndex;
    [ObservableProperty] private bool _isFolderSource;
    [ObservableProperty] private string _folderSourcePath = "";
    [ObservableProperty] private int _folderSourceFileCount;
    [ObservableProperty] private string _excludePatternsText = "";

    // ---------------- Textos ----------------
    public string TaskNameLabel => L.T("taskNamePrompt");
    public string Sec1 => L.T("sec1File");
    public string AddFileLabel => L.T("addFile");
    public string RemoveFileLabel => L.T("removeFile");
    public string ChooseFolderLabel => L.T("chooseFolder");
    public string UseFilesInsteadLabel => L.T("useFilesInstead");
    public string FolderSourceSummary => L.T("folderSourceSummary", FolderSourcePath, FolderSourceFileCount);
    public string ExcludePatternsLabel => L.T("excludePatternsLabel");
    public string ExcludePatternsHint => L.T("excludePatternsHint");
    public string Sec2 => L.T("sec2Where");
    public string AddDestLabel => L.T("addDest");
    public string EditDestLabel => L.T("editDest");
    public string DuplicateDestLabel => L.T("duplicateDest");
    public string RemoveDestLabel => L.T("removeDest");
    public string GroupLabel => L.T("groupLabel");
    public string SaveGroupLabel => L.T("saveGroup");
    public string DeleteGroupLabel => L.T("deleteGroup");
    public string ProfilesLabel => L.T("profSaved");
    public string SaveAsLabel => L.T("saveAs");
    public string RenameLabel => L.T("rename");
    public string DeleteLabel => L.T("delete");
    public string ResetLabel => L.T("resetFields");
    public string SaveLabel => L.T("save");
    public string CancelLabel => L.T("cancel");

    // ---------------- Origem ----------------
    private void LoadSrc(IEnumerable<string> files)
    {
        SrcFiles.Clear();
        foreach (var f in files) if (!string.IsNullOrWhiteSpace(f)) SrcFiles.Add(f);
    }

    private void LoadSourceFromSpec(SourceSpec src)
    {
        if (src.Kind == SourceKind.Folder)
        {
            IsFolderSource = true;
            FolderSourcePath = src.Path;
            ExcludePatternsText = string.Join(", ", src.ExcludePatterns);
            FolderSourceFileCount = src.All.Count;   // so pra exibir, avaliado agora
            SrcFiles.Clear();
        }
        else
        {
            IsFolderSource = false;
            FolderSourcePath = "";
            ExcludePatternsText = "";
            FolderSourceFileCount = 0;
            LoadSrc(src.All);
        }
    }

    private static List<string> ParseExcludePatterns(string text)
        => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private void RecomputeFolderCount()
    {
        if (!IsFolderSource) { FolderSourceFileCount = 0; return; }
        FolderSourceFileCount = new SourceSpec
        {
            Kind = SourceKind.Folder,
            Path = FolderSourcePath,
            Pattern = "*",
            ExcludePatterns = ParseExcludePatterns(ExcludePatternsText)
        }.All.Count;
    }

    partial void OnExcludePatternsTextChanged(string value) => RecomputeFolderCount();

    private SourceSpec ReadSource() => IsFolderSource
        ? new SourceSpec
        {
            Kind = SourceKind.Folder, Path = FolderSourcePath, Pattern = "*", Recursive = true,
            Files = new List<string>(), ExcludePatterns = ParseExcludePatterns(ExcludePatternsText)
        }
        : new SourceSpec { Files = SrcFiles.ToList(), Path = SrcFiles.Count > 0 ? SrcFiles[0] : "", Kind = SourceKind.File };

    [RelayCommand]
    private async Task AddSrcAsync()
    {
        var files = await _files.PickFilesAsync(true);
        foreach (var f in files)
            if (!SrcFiles.Any(x => string.Equals(x, f, StringComparison.OrdinalIgnoreCase)))
                SrcFiles.Add(f);
    }

    [RelayCommand]
    private async Task ChooseFolderAsync()
    {
        var folder = await _files.PickFolderAsync(string.IsNullOrWhiteSpace(FolderSourcePath) ? null : FolderSourcePath);
        if (string.IsNullOrWhiteSpace(folder)) return;
        SrcFiles.Clear();
        IsFolderSource = true;
        FolderSourcePath = folder;
        ExcludePatternsText = "";
        RecomputeFolderCount();
    }

    [RelayCommand]
    private void UseFilesInstead()
    {
        IsFolderSource = false;
        FolderSourcePath = "";
        ExcludePatternsText = "";
        FolderSourceFileCount = 0;
        SrcFiles.Clear();
    }

    [RelayCommand]
    private void RemoveSrc(IList? items)
    {
        // Multi-seleção: remove todos os selecionados (copia antes de mexer na coleção).
        if (items != null && items.Count > 0)
        {
            foreach (var s in items.Cast<object>().OfType<string>().ToList()) SrcFiles.Remove(s);
            return;
        }
        var idx = SelectedSrcIndex;
        if (idx >= 0 && idx < SrcFiles.Count) SrcFiles.RemoveAt(idx);
    }

    // ---------------- Destinos ----------------
    private void LoadDests(IEnumerable<Destination> src)
    {
        Dests.Clear();
        foreach (var d in src) Dests.Add(d.Clone());
    }

    [RelayCommand]
    private async Task AddDestAsync()
    {
        var r = await _dialogs.EditDestinationAsync(null);
        if (r != null) { Dests.Add(r); SelectedDestIndex = Dests.Count - 1; }
    }

    [RelayCommand]
    private async Task EditDestAsync()
    {
        var idx = SelectedDestIndex;
        if (idx < 0) return;
        var old = Dests[idx];
        var r = await _dialogs.EditDestinationAsync(old.Clone());
        if (r != null) { r.Enabled = old.Enabled; Dests[idx] = r; SelectedDestIndex = idx; }
    }

    [RelayCommand]
    private async Task DuplicateDestAsync()
    {
        var idx = SelectedDestIndex;
        if (idx < 0) return;
        var src = Dests[idx];
        var r = await _dialogs.EditDestinationAsync(src.Clone());
        if (r != null) { r.Enabled = src.Enabled; Dests.Insert(idx + 1, r); SelectedDestIndex = idx + 1; }
    }

    [RelayCommand]
    private void RemoveDest(IList? items)
    {
        if (items != null && items.Count > 0)
        {
            foreach (var d in items.Cast<object>().OfType<Destination>().ToList()) Dests.Remove(d);
            return;
        }
        var idx = SelectedDestIndex;
        if (idx >= 0 && idx < Dests.Count) Dests.RemoveAt(idx);
    }

    // ---------------- Grupos de destino ----------------
    private void ReloadGroups()
    {
        _grpSync = true;
        GroupOptions.Clear();
        GroupOptions.Add(L.T("selectItem"));
        foreach (var g in _s.DestGroups) GroupOptions.Add(g.Name);
        SelectedGroupIndex = 0;
        _grpSync = false;
    }

    partial void OnSelectedGroupIndexChanged(int value)
    {
        if (_grpSync || value <= 0 || value >= GroupOptions.Count) return;
        var name = GroupOptions[value];
        var g = _s.DestGroups.FirstOrDefault(x => x.Name == name);
        if (g != null) LoadDests(g.Destinations);
    }

    [RelayCommand]
    private async Task GroupSaveAsync()
    {
        if (Dests.Count == 0) return;
        var suggest = SelectedGroupIndex > 0 ? GroupOptions[SelectedGroupIndex] : "";
        var name = await _dialogs.PromptAsync(L.T("saveGroup"), L.T("groupNamePrompt"), suggest);
        if (string.IsNullOrWhiteSpace(name)) return;
        var grp = new DestGroup { Name = name.Trim(), Destinations = Dests.Select(d => d.Clone()).ToList() };
        var idx = _s.DestGroups.FindIndex(x => x.Name == grp.Name);
        if (idx >= 0) _s.DestGroups[idx] = grp; else _s.DestGroups.Add(grp);
        SettingsService.Save(_s);
        ReloadGroups();
        SelectGroupByName(grp.Name);
    }

    [RelayCommand]
    private async Task GroupDeleteAsync()
    {
        if (SelectedGroupIndex <= 0) { await _dialogs.ShowMessageAsync(L.T("groupLabel"), L.T("selectItem")); return; }
        var name = GroupOptions[SelectedGroupIndex];
        if (!await _dialogs.ConfirmAsync(L.T("deleteGroup"), name)) return;
        _s.DestGroups.RemoveAll(x => x.Name == name);
        SettingsService.Save(_s);
        ReloadGroups();
    }

    private void SelectGroupByName(string name)
    {
        var i = GroupOptions.IndexOf(name);
        if (i >= 0) { _grpSync = true; SelectedGroupIndex = i; _grpSync = false; }
    }

    // ---------------- Presets (origem+destino) ----------------
    private void ReloadProfiles()
    {
        _profSync = true;
        ProfileOptions.Clear();
        ProfileOptions.Add(L.T("selectItem"));
        foreach (var p in _s.Profiles) ProfileOptions.Add(p.Name);
        SelectedProfileIndex = 0;
        _profSync = false;
    }

    partial void OnSelectedProfileIndexChanged(int value)
    {
        if (_profSync || value <= 0 || value >= ProfileOptions.Count) return;
        var name = ProfileOptions[value];
        var prof = _s.Profiles.FirstOrDefault(p => p.Name == name);
        if (prof != null) { LoadSourceFromSpec(prof.Source); LoadDests(prof.Destinations); }
    }

    [RelayCommand]
    private async Task ProfSaveAsync()
    {
        var suggest = SelectedProfileIndex > 0 ? ProfileOptions[SelectedProfileIndex] : "";
        var name = await _dialogs.PromptAsync(L.T("saveAs"), L.T("profSaved"), suggest);
        if (string.IsNullOrWhiteSpace(name)) return;
        var prof = new Profile { Name = name.Trim(), Source = ReadSource(), Destinations = Dests.Select(d => d.Clone()).ToList() };
        var existing = _s.Profiles.FindIndex(p => p.Name == prof.Name);
        if (existing >= 0) _s.Profiles[existing] = prof; else _s.Profiles.Add(prof);
        SettingsService.Save(_s);
        ReloadProfiles();
        SelectProfileByName(prof.Name);
    }

    [RelayCommand]
    private async Task ProfRenameAsync()
    {
        if (SelectedProfileIndex <= 0) { await _dialogs.ShowMessageAsync(L.T("profilesTitle"), L.T("selectProfileWarn")); return; }
        var name = ProfileOptions[SelectedProfileIndex];
        var nn = await _dialogs.PromptAsync(L.T("rename"), L.T("rename"), name);
        if (string.IsNullOrWhiteSpace(nn)) return;
        var prof = _s.Profiles.FirstOrDefault(p => p.Name == name);
        if (prof != null)
        {
            prof.Name = nn.Trim();
            if (_s.ActiveProfile == name) _s.ActiveProfile = nn.Trim();
            SettingsService.Save(_s);
            ReloadProfiles();
            SelectProfileByName(nn.Trim());
        }
    }

    [RelayCommand]
    private async Task ProfDeleteAsync()
    {
        if (SelectedProfileIndex <= 0) { await _dialogs.ShowMessageAsync(L.T("profilesTitle"), L.T("selectProfileWarn")); return; }
        var name = ProfileOptions[SelectedProfileIndex];
        if (!await _dialogs.ConfirmAsync(L.T("delete"), name)) return;
        _s.Profiles.RemoveAll(p => p.Name == name);
        if (_s.ActiveProfile == name) _s.ActiveProfile = "";
        SettingsService.Save(_s);
        ReloadProfiles();
    }

    private void SelectProfileByName(string name)
    {
        var i = ProfileOptions.IndexOf(name);
        if (i >= 0) { _profSync = true; SelectedProfileIndex = i; _profSync = false; }
    }

    [RelayCommand]
    private void Reset()
    {
        SrcFiles.Clear();
        Dests.Clear();
        IsFolderSource = false;
        FolderSourcePath = "";
        ExcludePatternsText = "";
        FolderSourceFileCount = 0;
    }

    // ---------------- Salvar / Cancelar ----------------
    [RelayCommand]
    private void Save()
    {
        if (!string.IsNullOrWhiteSpace(TaskName)) _s.CurrentJob.Name = TaskName.Trim();
        _s.CurrentJob.Source = ReadSource();
        _s.CurrentJob.Destinations = Dests.Select(d => d.Clone()).ToList();
        SettingsService.Save(_s);
        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}
