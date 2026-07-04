using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OneClickTransfer.Avalonia.ViewModels.Abstractions;
using OneClickTransfer.I18n;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.ViewModels;

/// <summary>Preferências globais do app: idioma, tema, atalho, auto-update.
/// ShowDialog&lt;bool&gt; via CloseRequested (true = salvou). Config da tarefa fica no TaskEditor.</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly AppSettings _s;
    private readonly IDialogService _dialogs;
    private readonly string _noneLabel;

    public event Action<bool>? CloseRequested;

    public SettingsViewModel(AppSettings s, IDialogService dialogs)
    {
        _s = s;
        _dialogs = dialogs;
        _noneLabel = L.Lang == "en" ? "None" : "Nenhum";

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
    }

    public ObservableCollection<string> KeyOptions { get; } = new();
    public ObservableCollection<string> ThemeOptions { get; } = new();
    public ObservableCollection<string> LangOptions { get; } = new();

    [ObservableProperty] private string _selectedKey = "";
    [ObservableProperty] private int _selectedThemeIndex;
    [ObservableProperty] private int _selectedLangIndex;
    [ObservableProperty] private bool _autoUpdateCheck;
    [ObservableProperty] private string _versionText = "";
    [ObservableProperty] private bool _checkingUpdate;

    // ---------------- About ----------------
    public const string GithubUrl = "https://github.com/samaBR85/1clicktransfer";
    public string AppNameText => L.T("appTitle");
    public string AboutLabel => L.T("about");
    public string GithubLabel => L.T("viewOnGithub");
    public string AuthorText => L.T("byAuthor");

    [RelayCommand]
    private void OpenGithub() => LinkOpener.Open(GithubUrl);

    // ---------------- Textos ----------------
    public string Title => L.T("settingsTitle");
    public string Sec3 => L.T("sec3Options");
    public string LangLabelText => L.T("langLabel");
    public string ThemeLabelText => L.T("themeLabel");
    public string ShortcutLabelText => L.T("shortcutLabel");
    public string F5Note => L.T("f5RefreshNote");
    public string CheckUpdateLabel => L.T("checkUpdates");
    public string AutoUpdateLabel => L.T("autoUpdateLabel");
    public string SaveLabel => L.T("save");
    public string CancelLabel => L.T("cancel");

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
                await _dialogs.ShowUpdateAsync(info);
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
