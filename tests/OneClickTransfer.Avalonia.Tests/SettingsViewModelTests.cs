using OneClickTransfer.Avalonia.ViewModels;
using OneClickTransfer.I18n;
using OneClickTransfer.Models;

namespace OneClickTransfer.Avalonia.Tests;

public class SettingsViewModelTests
{
    private static AppSettings Globals()
        => new() { Shortcut = "F4", Theme = "dark", Language = "en", AutoUpdateCheck = true, MaxParallelDestinations = 3 };

    private static SettingsViewModel New(AppSettings s)
        => new(s, new FakeDialogService());

    [Fact]
    public void Ctor_reflects_current_globals()
    {
        var s = Globals();
        var vm = New(s);
        Assert.Equal(0, vm.SelectedThemeIndex);   // dark
        Assert.Equal(1, vm.SelectedLangIndex);    // en
        Assert.Equal("F4", vm.SelectedKey);
        Assert.True(vm.AutoUpdateCheck);
    }

    [Fact]
    public void Save_writes_fields_to_settings_and_closes_true()
    {
        var s = Globals();
        var vm = New(s);
        vm.SelectedThemeIndex = 1;   // light
        vm.SelectedLangIndex = 1;    // en
        vm.SelectedKey = "F6";
        vm.AutoUpdateCheck = false;
        bool? closed = null;
        vm.CloseRequested += ok => closed = ok;

        vm.SaveCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal("light", s.Theme);
        Assert.Equal("en", s.Language);
        Assert.Equal("F6", s.Shortcut);
        Assert.False(s.AutoUpdateCheck);
    }

    [Fact]
    public void Save_writes_max_parallel_destinations()
    {
        var s = Globals();
        var vm = New(s);
        vm.MaxParallelDestinations = 6;
        vm.SaveCommand.Execute(null);
        Assert.Equal(6, s.MaxParallelDestinations);
    }

    [Fact]
    public void Ctor_reflects_KeepWatchWhileMinimized()
    {
        var s = Globals();
        s.MinimizeToTrayOnClose = true;
        s.KeepWatchWhileMinimized = true;
        var vm = New(s);
        Assert.True(vm.MinimizeToTrayOnClose);
        Assert.True(vm.KeepWatchWhileMinimized);
    }

    [Fact]
    public void Save_writes_KeepWatchWhileMinimized()
    {
        var s = Globals();
        var vm = New(s);
        vm.KeepWatchWhileMinimized = true;
        vm.SaveCommand.Execute(null);
        Assert.True(s.KeepWatchWhileMinimized);
    }

    [Fact]
    public void Save_none_shortcut_stores_None()
    {
        var s = Globals();
        var vm = New(s);
        vm.SelectedKey = L.Lang == "en" ? "None" : "Nenhum";
        vm.SaveCommand.Execute(null);
        Assert.Equal("None", s.Shortcut);
    }
}
