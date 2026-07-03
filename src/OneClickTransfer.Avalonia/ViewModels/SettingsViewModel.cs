using System;
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

/// <summary>Configurações da tarefa selecionada + opções globais + perfis/grupos.
/// ShowDialog&lt;bool&gt; via CloseRequested (true = salvou).</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings _s;
    private readonly IDialogService _dialogs;
    private readonly IFilePickerService _files;
    private readonly string _noneLabel;
    private bool _grpSync;
    private bool _profSync;

    public event Action<bool>? CloseRequested;

    public SettingsViewModel(AppSettings s, IDialogService dialogs, IFilePickerService files)
    {
        _s = s;
        _dialogs = dialogs;
        _files = files;
        _noneLabel = L.Lang == "en" ? "None" : "Nenhum";

        Title = L.T("settingsTitle") + " — " + L.T("editingTask", _s.CurrentJob.Name);
        LoadSrc(_s.CurrentJob.Source.All);
        LoadDests(_s.CurrentJob.Destinations);

        // Combos de opções
        KeyOptions.Add(_noneLabel);
        for (int i = 2; i <= 12; i++) { if (i == 5) continue; KeyOptions.Add("F" + i); }
        SelectedKey = (_s.Shortcut is "None" or "Nenhum" or "") ? _noneLabel
            : (KeyOptions.Contains(_s.Shortcut) ? _s.Shortcut : "F4");

        ThemeOptions.Add(L.T("themeDark"));
        ThemeOptions.Add(L.T("themeLight"));
        SelectedThemeIndex = _s.Theme == "light" ? 1 : 0;

        LangOptions.Add(L.T("langPtItem"));
        LangOptions.Add(L.T("langEnItem"));
        SelectedLangIndex = _s.Language == "en" ? 1 : 0;

        AutoUpdateCheck = _s.AutoUpdateCheck;
        VersionText = "v" + UpdateService.Current;

        ReloadGroups();
        ReloadProfiles();
    }

    // ---------------- Coleções ----------------
    public ObservableCollection<string> SrcFiles { get; } = new();
    public ObservableCollection<Destination> Dests { get; } = new();
    public ObservableCollection<string> KeyOptions { get; } = new();
    public ObservableCollection<string> ThemeOptions { get; } = new();
    public ObservableCollection<string> LangOptions { get; } = new();
    public ObservableCollection<string> GroupOptions { get; } = new();
    public ObservableCollection<string> ProfileOptions { get; } = new();

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private int _selectedSrcIndex = -1;
    [ObservableProperty] private int _selectedDestIndex = -1;
    [ObservableProperty] private string _selectedKey = "";
    [ObservableProperty] private int _selectedThemeIndex;
    [ObservableProperty] private int _selectedLangIndex;
    [ObservableProperty] private int _selectedGroupIndex;
    [ObservableProperty] private int _selectedProfileIndex;
    [ObservableProperty] private bool _autoUpdateCheck;
    [ObservableProperty] private string _versionText = "";
    [ObservableProperty] private bool _checkingUpdate;

    // ---------------- Textos ----------------
    public string Sec1 => L.T("sec1File");
    public string AddFileLabel => L.T("addFile");
    public string RemoveFileLabel => L.T("removeFile");
    public string Sec2 => L.T("sec2Where");
    public string AddDestLabel => L.T("addDest");
    public string EditDestLabel => L.T("editDest");
    public string DuplicateDestLabel => L.T("duplicateDest");
    public string RemoveDestLabel => L.T("removeDest");
    public string GroupLabel => L.T("groupLabel");
    public string SaveGroupLabel => L.T("saveGroup");
    public string DeleteGroupLabel => L.T("deleteGroup");
    public string Sec3 => L.T("sec3Options");
    public string LangLabelText => L.T("langLabel");
    public string ThemeLabelText => L.T("themeLabel");
    public string ShortcutLabelText => L.T("shortcutLabel");
    public string F5Note => L.T("f5RefreshNote");
    public string CheckUpdateLabel => L.T("checkUpdates");
    public string AutoUpdateLabel => L.T("autoUpdateLabel");
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

    private SourceSpec ReadSource() => new()
    {
        Files = SrcFiles.ToList(),
        Path = SrcFiles.Count > 0 ? SrcFiles[0] : "",
        Kind = SourceKind.File
    };

    [RelayCommand]
    private async Task AddSrcAsync()
    {
        var files = await _files.PickFilesAsync(true);
        foreach (var f in files)
            if (!SrcFiles.Any(x => string.Equals(x, f, StringComparison.OrdinalIgnoreCase)))
                SrcFiles.Add(f);
    }

    [RelayCommand]
    private void RemoveSrc()
    {
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
    private void RemoveDest()
    {
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

    // ---------------- Perfis ----------------
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
        if (prof != null) { LoadSrc(prof.Source.All); LoadDests(prof.Destinations); }
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
    }

    // ---------------- Atualizações ----------------
    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        CheckingUpdate = true;
        try
        {
            var info = await UpdateService.CheckAsync();
            if (info == null)
                await _dialogs.ShowMessageAsync(L.T("updateTitle"), L.T("upToDate", UpdateService.Current.ToString()));
            else
                try { await _dialogs.ShowUpdateAsync(info); }
                catch (NotImplementedException) { await _dialogs.ShowMessageAsync(L.T("updateTitle"), L.T("updateAvailable", info.Version.ToString())); }
        }
        catch
        {
            await _dialogs.ShowMessageAsync(L.T("updateTitle"), L.T("updateCheckFailed"));
        }
        finally { CheckingUpdate = false; }
    }

    // ---------------- Salvar / Cancelar ----------------
    [RelayCommand]
    private void Save()
    {
        _s.CurrentJob.Source = ReadSource();
        _s.CurrentJob.Destinations = Dests.Select(d => d.Clone()).ToList();
        _s.Shortcut = SelectedKey == _noneLabel ? "None" : SelectedKey;
        _s.Theme = SelectedThemeIndex == 1 ? "light" : "dark";
        _s.Language = SelectedLangIndex == 1 ? "en" : "pt";
        _s.AutoUpdateCheck = AutoUpdateCheck;
        SettingsService.Save(_s);
        CloseRequested?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(false);
}
