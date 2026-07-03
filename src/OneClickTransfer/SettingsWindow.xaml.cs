using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using OneClickTransfer.I18n;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer;

public partial class SettingsWindow : Window
{
    private AppSettings S => App.Settings;
    private bool _profSync;
    private bool _grpSync;
    private readonly ObservableCollection<Destination> _dests = new();
    private readonly ObservableCollection<string> _srcFiles = new();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public SettingsWindow()
    {
        InitializeComponent();
        ApplyTexts();
        Title = L.T("settingsTitle") + " — " + L.T("editingTask", S.CurrentJob.Name);
        LstSrc.ItemsSource = _srcFiles;
        LoadSrc(S.CurrentJob.Source.All);
        LstDests.ItemsSource = _dests;
        LoadDests(S.CurrentJob.Destinations);
        ReloadGroups();
        ReloadProfiles();
        Loaded += (_, _) =>
        {
            try { var h = new WindowInteropHelper(this).Handle; int v = S.Theme != "light" ? 1 : 0; DwmSetWindowAttribute(h, 20, ref v, 4); } catch { }
        };
    }

    private void ApplyTexts()
    {
        Title = L.T("settingsTitle");
        LblSec1.Text = L.T("sec1File");
        BtnAddSrc.Content = L.T("addFile");
        BtnRemoveSrc.Content = L.T("removeFile");
        LblSec2.Text = L.T("sec2Where");
        BtnAddDest.Content = L.T("addDest");
        BtnEditDest.Content = L.T("editDest");
        BtnDuplicateDest.Content = L.T("duplicateDest");
        BtnRemoveDest.Content = L.T("removeDest");
        LblGroup.Text = L.T("groupLabel");
        BtnGroupSave.Content = L.T("saveGroup");
        BtnGroupDelete.Content = L.T("deleteGroup");
        LblF5Note.Text = L.T("f5RefreshNote");
        LblSec3.Text = L.T("sec3Options");
        LblShortcut.Text = L.T("shortcutLabel");
        LblTheme.Text = L.T("themeLabel");
        LblLang.Text = L.T("langLabel");
        LblProfiles.Text = L.T("profSaved");
        BtnProfSave.Content = L.T("saveAs");
        BtnProfRename.Content = L.T("rename");
        BtnProfDelete.Content = L.T("delete");
        BtnReset.Content = L.T("resetFields");
        BtnSave.Content = L.T("save");
        BtnCancel.Content = L.T("cancel");

        var noneLabel = L.Lang == "en" ? "None" : "Nenhum";
        CmbKey.Items.Clear();
        CmbKey.Items.Add(noneLabel);
        for (int i = 2; i <= 12; i++) { if (i == 5) continue; CmbKey.Items.Add("F" + i); }
        CmbKey.SelectedItem = (S.Shortcut is "None" or "Nenhum" or "") ? noneLabel
            : (CmbKey.Items.Contains(S.Shortcut) ? S.Shortcut : "F4");

        CmbTheme.Items.Clear();
        CmbTheme.Items.Add(L.T("themeDark"));
        CmbTheme.Items.Add(L.T("themeLight"));
        CmbTheme.SelectedIndex = S.Theme == "light" ? 1 : 0;

        CmbLang.Items.Clear();
        CmbLang.Items.Add(L.T("langPtItem"));
        CmbLang.Items.Add(L.T("langEnItem"));
        CmbLang.SelectedIndex = S.Language == "en" ? 1 : 0;
    }

    // ---------------- Destinos ----------------
    private void LoadDests(System.Collections.Generic.IEnumerable<Destination> src)
    {
        _dests.Clear();
        foreach (var d in src) _dests.Add(d.Clone());
    }

    private void AddDest_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new DestinationEditorWindow(null) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result != null) { _dests.Add(dlg.Result); LstDests.SelectedIndex = _dests.Count - 1; }
    }

    private void EditDest_Click(object sender, RoutedEventArgs e) => EditSelected();
    private void Dests_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => EditSelected();

    private void EditSelected()
    {
        var idx = LstDests.SelectedIndex;
        if (idx < 0) return;
        var old = _dests[idx];
        var dlg = new DestinationEditorWindow(old.Clone()) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result != null)
        {
            dlg.Result.Enabled = old.Enabled;   // preserva o estado marcado/desmarcado
            _dests[idx] = dlg.Result;
            LstDests.SelectedIndex = idx;
        }
    }

    private void DuplicateDest_Click(object sender, RoutedEventArgs e)
    {
        var idx = LstDests.SelectedIndex;
        if (idx < 0) return;
        var src = _dests[idx];
        // Abre o editor ja preenchido com o destino selecionado; ao confirmar, cria um NOVO
        var dlg = new DestinationEditorWindow(src.Clone()) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result != null)
        {
            dlg.Result.Enabled = src.Enabled;
            _dests.Insert(idx + 1, dlg.Result);
            LstDests.SelectedIndex = idx + 1;
        }
    }

    private void RemoveDest_Click(object sender, RoutedEventArgs e)
    {
        var idx = LstDests.SelectedIndex;
        if (idx < 0) return;
        _dests.RemoveAt(idx);
    }

    // ---------------- Grupos de destino ----------------
    private void ReloadGroups()
    {
        _grpSync = true;
        CmbGroups.Items.Clear();
        CmbGroups.Items.Add(L.T("selectItem"));
        foreach (var g in S.DestGroups) CmbGroups.Items.Add(g.Name);
        CmbGroups.SelectedIndex = 0;
        _grpSync = false;
    }

    private void Groups_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_grpSync || CmbGroups.SelectedIndex <= 0) return;
        var name = CmbGroups.SelectedItem?.ToString() ?? "";
        var g = S.DestGroups.FirstOrDefault(x => x.Name == name);
        if (g != null) LoadDests(g.Destinations);
    }

    private void GroupSave_Click(object sender, RoutedEventArgs e)
    {
        if (_dests.Count == 0) return;
        var suggest = CmbGroups.SelectedIndex > 0 ? CmbGroups.SelectedItem?.ToString() ?? "" : "";
        var name = PromptDialog.Ask(this, L.T("saveGroup"), L.T("groupNamePrompt"), suggest);
        if (name == null) return;
        var grp = new DestGroup { Name = name, Destinations = _dests.Select(d => d.Clone()).ToList() };
        var idx = S.DestGroups.FindIndex(x => x.Name == name);
        if (idx >= 0) S.DestGroups[idx] = grp; else S.DestGroups.Add(grp);
        SettingsService.Save(S);
        ReloadGroups();
        CmbGroups.SelectedItem = name;
    }

    private void GroupDelete_Click(object sender, RoutedEventArgs e)
    {
        if (CmbGroups.SelectedIndex <= 0) { MessageBox.Show(this, L.T("selectItem"), L.T("groupLabel"), MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var name = CmbGroups.SelectedItem!.ToString()!;
        if (MessageBox.Show(this, name, L.T("deleteGroup"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        S.DestGroups.RemoveAll(x => x.Name == name);
        SettingsService.Save(S);
        ReloadGroups();
    }

    // ---------------- Origem (1+ arquivos) ----------------
    private void LoadSrc(System.Collections.Generic.IEnumerable<string> files)
    {
        _srcFiles.Clear();
        foreach (var f in files) if (!string.IsNullOrWhiteSpace(f)) _srcFiles.Add(f);
    }

    private SourceSpec ReadSource() => new()
    {
        Files = _srcFiles.ToList(),
        Path = _srcFiles.Count > 0 ? _srcFiles[0] : "",
        Kind = SourceKind.File
    };

    private void AddSrc_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Multiselect = true };
        if (dlg.ShowDialog() != true) return;
        foreach (var f in dlg.FileNames)
            if (!_srcFiles.Any(x => string.Equals(x, f, StringComparison.OrdinalIgnoreCase)))
                _srcFiles.Add(f);
    }

    private void RemoveSrc_Click(object sender, RoutedEventArgs e)
    {
        var sel = LstSrc.SelectedItems.Cast<string>().ToList();
        foreach (var f in sel) _srcFiles.Remove(f);
    }

    // ---------------- Perfis ----------------
    private void ReloadProfiles()
    {
        _profSync = true;
        CmbProfiles.Items.Clear();
        CmbProfiles.Items.Add(L.T("selectItem"));
        foreach (var p in S.Profiles) CmbProfiles.Items.Add(p.Name);
        CmbProfiles.SelectedIndex = 0;
        _profSync = false;
    }

    private void Profiles_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_profSync || CmbProfiles.SelectedIndex <= 0) return;
        var name = CmbProfiles.SelectedItem?.ToString() ?? "";
        var prof = S.Profiles.FirstOrDefault(p => p.Name == name);
        if (prof != null)
        {
            LoadSrc(prof.Source.All);
            LoadDests(prof.Destinations);
        }
    }

    private void ProfSave_Click(object sender, RoutedEventArgs e)
    {
        var suggest = CmbProfiles.SelectedIndex > 0 ? CmbProfiles.SelectedItem?.ToString() ?? "" : "";
        var name = PromptDialog.Ask(this, L.T("saveAs"), L.T("profSaved"), suggest);
        if (name == null) return;
        var prof = new Profile { Name = name, Source = ReadSource(), Destinations = _dests.Select(d => d.Clone()).ToList() };
        var existing = S.Profiles.FindIndex(p => p.Name == name);
        if (existing >= 0) S.Profiles[existing] = prof; else S.Profiles.Add(prof);
        SettingsService.Save(S);
        ReloadProfiles();
        CmbProfiles.SelectedItem = name;
    }

    private void ProfRename_Click(object sender, RoutedEventArgs e)
    {
        if (CmbProfiles.SelectedIndex <= 0) { MessageBox.Show(this, L.T("selectProfileWarn"), L.T("profilesTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var name = CmbProfiles.SelectedItem!.ToString()!;
        var nn = PromptDialog.Ask(this, L.T("rename"), L.T("rename"), name);
        if (nn == null) return;
        var prof = S.Profiles.FirstOrDefault(p => p.Name == name);
        if (prof != null) { prof.Name = nn; if (S.ActiveProfile == name) S.ActiveProfile = nn; SettingsService.Save(S); ReloadProfiles(); CmbProfiles.SelectedItem = nn; }
    }

    private void ProfDelete_Click(object sender, RoutedEventArgs e)
    {
        if (CmbProfiles.SelectedIndex <= 0) { MessageBox.Show(this, L.T("selectProfileWarn"), L.T("profilesTitle"), MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        var name = CmbProfiles.SelectedItem!.ToString()!;
        if (MessageBox.Show(this, name, L.T("delete"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        S.Profiles.RemoveAll(p => p.Name == name);
        if (S.ActiveProfile == name) S.ActiveProfile = "";
        SettingsService.Save(S);
        ReloadProfiles();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _srcFiles.Clear();
        _dests.Clear();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // Grava na tarefa selecionada (origem + destinos)
        S.CurrentJob.Source = ReadSource();
        S.CurrentJob.Destinations = _dests.Select(d => d.Clone()).ToList();
        S.Shortcut = CmbKey.SelectedIndex == 0 ? "None" : (CmbKey.SelectedItem?.ToString() ?? "F4");
        S.Theme = CmbTheme.SelectedIndex == 1 ? "light" : "dark";
        S.Language = CmbLang.SelectedIndex == 1 ? "en" : "pt";
        SettingsService.Save(S);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
