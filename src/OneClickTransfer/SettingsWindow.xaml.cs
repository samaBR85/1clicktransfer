using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    public SettingsWindow()
    {
        InitializeComponent();
        ApplyTexts();
        LoadFields(S.Source, S.Destinations.FirstOrDefault());
        ReloadProfiles();
        UpdatePanels();
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
        RbLocal.Content = L.T("localFolder");
        RbFtp.Content = L.T("ftpServer");
        LblDstFolder.Text = L.T("destFolderLabel");
        BtnBrowseDst.Content = L.T("browse");
        LblHost.Text = L.T("ftpHost");
        LblPort.Text = L.T("ftpPort");
        LblRemote.Text = L.T("ftpRemote");
        LblUser.Text = L.T("ftpUser");
        LblPass.Text = L.T("ftpPass");
        ChkTls.Content = L.T("ftpTls");
        BtnTest.Content = L.T("testConn");
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

        // Combos
        var noneLabel = L.Lang == "en" ? "None" : "Nenhum";
        CmbKey.Items.Clear();
        CmbKey.Items.Add(noneLabel);
        for (int i = 2; i <= 12; i++) CmbKey.Items.Add("F" + i);
        CmbKey.SelectedItem = (S.Shortcut is "None" or "Nenhum" or "") ? noneLabel
            : (CmbKey.Items.Contains(S.Shortcut) ? S.Shortcut : "F5");

        CmbTheme.Items.Clear();
        CmbTheme.Items.Add(L.T("themeDark"));
        CmbTheme.Items.Add(L.T("themeLight"));
        CmbTheme.SelectedIndex = S.Theme == "light" ? 1 : 0;

        CmbLang.Items.Clear();
        CmbLang.Items.Add(L.T("langPtItem"));
        CmbLang.Items.Add(L.T("langEnItem"));
        CmbLang.SelectedIndex = S.Language == "en" ? 1 : 0;
    }

    private void LoadFields(SourceSpec src, Destination? d)
    {
        TxtSrc.Text = src.Path;
        d ??= new Destination();
        if (d.Type == DestType.Ftp)
        {
            RbFtp.IsChecked = true;
            TxtRemote.Text = string.IsNullOrEmpty(d.Folder) ? "/" : d.Folder;
            TxtDstFolder.Text = "";
        }
        else
        {
            RbLocal.IsChecked = true;
            TxtDstFolder.Text = d.Folder;
            TxtRemote.Text = "/";
        }
        TxtHost.Text = d.Host;
        TxtPort.Text = (d.Port <= 0 ? 21 : d.Port).ToString();
        TxtUser.Text = d.Username;
        TxtPass.Password = SecretProtector.Unprotect(d.Password);
        ChkTls.IsChecked = d.UseTls;
    }

    private SourceSpec ReadSource() => new() { Path = TxtSrc.Text.Trim(), Kind = SourceKind.File };

    private Destination ReadDest()
    {
        if (RbFtp.IsChecked == true)
        {
            int.TryParse(TxtPort.Text, out var port); if (port <= 0) port = 21;
            return new Destination
            {
                Type = DestType.Ftp,
                Host = TxtHost.Text.Trim(),
                Port = port,
                Folder = string.IsNullOrWhiteSpace(TxtRemote.Text) ? "/" : TxtRemote.Text.Trim(),
                Username = TxtUser.Text.Trim(),
                Password = SecretProtector.Protect(TxtPass.Password),
                UseTls = ChkTls.IsChecked == true
            };
        }
        return new Destination { Type = DestType.Local, Folder = TxtDstFolder.Text.Trim() };
    }

    private void UpdatePanels()
    {
        bool ftp = RbFtp.IsChecked == true;
        PanelLocal.IsEnabled = !ftp; PanelLocal.Opacity = ftp ? 0.5 : 1;
        PanelFtp.IsEnabled = ftp; PanelFtp.Opacity = ftp ? 1 : 0.5;
    }
    private void DestType_Changed(object sender, RoutedEventArgs e) => UpdatePanels();

    private void BrowseSrc_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog();
        if (dlg.ShowDialog() == true) TxtSrc.Text = dlg.FileName;
    }

    private void BrowseDst_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog();
        if (!string.IsNullOrWhiteSpace(TxtDstFolder.Text)) { try { dlg.InitialDirectory = TxtDstFolder.Text; } catch { } }
        if (dlg.ShowDialog() == true) TxtDstFolder.Text = dlg.FolderName;
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        var d = ReadDest();
        if (d.Type != DestType.Ftp || string.IsNullOrWhiteSpace(d.Host))
        {
            MessageBox.Show(this, L.T("ftpHost"), L.T("errorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        BtnTest.IsEnabled = false; BtnTest.Content = L.T("testing");
        try
        {
            await Task.Run(() => TransferService.FtpTestConnection(d));
            MessageBox.Show(this, L.T("connOkMsg"), L.T("successTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, L.T("errorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally { BtnTest.IsEnabled = true; BtnTest.Content = L.T("testConn"); }
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
        if (prof != null) { LoadFields(prof.Source, prof.Destinations.FirstOrDefault()); UpdatePanels(); }
    }

    private void ProfSave_Click(object sender, RoutedEventArgs e)
    {
        var suggest = CmbProfiles.SelectedIndex > 0 ? CmbProfiles.SelectedItem?.ToString() ?? "" : "";
        var name = PromptDialog.Ask(this, L.T("saveAs"), L.T("profSaved"), suggest);
        if (name == null) return;
        var prof = new Profile { Name = name, Source = ReadSource(), Destinations = { ReadDest() } };
        var existing = S.Profiles.FindIndex(p => p.Name == name);
        if (existing >= 0) S.Profiles[existing] = prof; else S.Profiles.Add(prof);
        SettingsService.Save(S);
        ReloadProfiles();
        CmbProfiles.SelectedItem = name;
    }

    private void ProfRename_Click(object sender, RoutedEventArgs e)
    {
        if (CmbProfiles.SelectedIndex <= 0) return;
        var name = CmbProfiles.SelectedItem!.ToString()!;
        var nn = PromptDialog.Ask(this, L.T("rename"), L.T("rename"), name);
        if (nn == null) return;
        var prof = S.Profiles.FirstOrDefault(p => p.Name == name);
        if (prof != null) { prof.Name = nn; if (S.ActiveProfile == name) S.ActiveProfile = nn; SettingsService.Save(S); ReloadProfiles(); CmbProfiles.SelectedItem = nn; }
    }

    private void ProfDelete_Click(object sender, RoutedEventArgs e)
    {
        if (CmbProfiles.SelectedIndex <= 0) return;
        var name = CmbProfiles.SelectedItem!.ToString()!;
        if (MessageBox.Show(this, name, L.T("delete"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        S.Profiles.RemoveAll(p => p.Name == name);
        if (S.ActiveProfile == name) S.ActiveProfile = "";
        SettingsService.Save(S);
        ReloadProfiles();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        TxtSrc.Text = ""; RbLocal.IsChecked = true; TxtDstFolder.Text = "";
        TxtHost.Text = ""; TxtPort.Text = "21"; TxtRemote.Text = "/"; TxtUser.Text = ""; TxtPass.Password = ""; ChkTls.IsChecked = false;
        UpdatePanels();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        S.Source = ReadSource();
        S.Destinations = new() { ReadDest() };
        S.Shortcut = CmbKey.SelectedIndex == 0 ? "None" : (CmbKey.SelectedItem?.ToString() ?? "F5");
        S.Theme = CmbTheme.SelectedIndex == 1 ? "light" : "dark";
        S.Language = CmbLang.SelectedIndex == 1 ? "en" : "pt";
        S.ActiveProfile = "";
        SettingsService.Save(S);
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
}
