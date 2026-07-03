using System;
using System.Collections.Generic;
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
    private List<Destination> _dests = new();

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public SettingsWindow()
    {
        InitializeComponent();
        ApplyTexts();
        TxtSrc.Text = S.Source.Path;
        _dests = S.Destinations.ConvertAll(d => d.Clone());
        ReloadDests();
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
        BtnBrowseSrc.Content = L.T("browse");
        LblSec2.Text = L.T("sec2Where");
        BtnAddDest.Content = L.T("addDest");
        BtnEditDest.Content = L.T("editDest");
        BtnRemoveDest.Content = L.T("removeDest");
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
    public static string DestSummary(Destination d) => d.Type switch
    {
        DestType.Local => "\U0001F4C1  " + d.Folder,
        DestType.Ftp => "\U0001F310  FTP  " + d.Host + ":" + d.Port + d.Folder,
        DestType.Sftp => "\U0001F310  SFTP  " + d.Host + ":" + d.Port + d.Folder,
        _ => ""
    };

    private void ReloadDests()
    {
        var sel = LstDests.SelectedIndex;
        LstDests.Items.Clear();
        foreach (var d in _dests) LstDests.Items.Add(DestSummary(d));
        if (sel >= 0 && sel < LstDests.Items.Count) LstDests.SelectedIndex = sel;
    }

    private void AddDest_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new DestinationEditorWindow(null) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result != null) { _dests.Add(dlg.Result); ReloadDests(); LstDests.SelectedIndex = _dests.Count - 1; }
    }

    private void EditDest_Click(object sender, RoutedEventArgs e) => EditSelected();
    private void Dests_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e) => EditSelected();

    private void EditSelected()
    {
        var idx = LstDests.SelectedIndex;
        if (idx < 0) return;
        var dlg = new DestinationEditorWindow(_dests[idx].Clone()) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result != null) { _dests[idx] = dlg.Result; ReloadDests(); LstDests.SelectedIndex = idx; }
    }

    private void RemoveDest_Click(object sender, RoutedEventArgs e)
    {
        var idx = LstDests.SelectedIndex;
        if (idx < 0) return;
        _dests.RemoveAt(idx);
        ReloadDests();
    }

    private SourceSpec ReadSource() => new() { Path = TxtSrc.Text.Trim(), Kind = SourceKind.File };

    private void BrowseSrc_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog();
        if (dlg.ShowDialog() == true) TxtSrc.Text = dlg.FileName;
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
            TxtSrc.Text = prof.Source.Path;
            _dests = prof.Destinations.ConvertAll(d => d.Clone());
            ReloadDests();
        }
    }

    private void ProfSave_Click(object sender, RoutedEventArgs e)
    {
        var suggest = CmbProfiles.SelectedIndex > 0 ? CmbProfiles.SelectedItem?.ToString() ?? "" : "";
        var name = PromptDialog.Ask(this, L.T("saveAs"), L.T("profSaved"), suggest);
        if (name == null) return;
        var prof = new Profile { Name = name, Source = ReadSource(), Destinations = _dests.ConvertAll(d => d.Clone()) };
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
        TxtSrc.Text = "";
        _dests.Clear();
        ReloadDests();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        S.Source = ReadSource();
        S.Destinations = _dests.ConvertAll(d => d.Clone());
        S.Shortcut = CmbKey.SelectedIndex == 0 ? "None" : (CmbKey.SelectedItem?.ToString() ?? "F4");
        S.Theme = CmbTheme.SelectedIndex == 1 ? "light" : "dark";
        S.Language = CmbLang.SelectedIndex == 1 ? "en" : "pt";
        S.ActiveProfile = "";
        SettingsService.Save(S);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
