using System.Linq;
using OneClickTransfer.Avalonia.ViewModels;
using OneClickTransfer.Models;

namespace OneClickTransfer.Avalonia.Tests;

public class TaskEditorViewModelTests
{
    private static AppSettings SettingsWithJob(out TransferJob job)
    {
        job = new TransferJob { Name = "T1", Enabled = true };
        job.Source.Files.Add(@"C:\a.txt");
        job.Destinations.Add(new Destination { Type = DestType.Local, Folder = @"C:\dst" });
        return new AppSettings { Jobs = { job }, SelectedJob = 0 };
    }

    private static TaskEditorViewModel New(AppSettings s, FakeDialogService? dlg = null, FakeFilePicker? files = null)
        => new(s, dlg ?? new FakeDialogService(), files ?? new FakeFilePicker());

    [Fact]
    public void Ctor_loads_source_and_dests_from_current_job()
    {
        var s = SettingsWithJob(out _);
        var vm = New(s);
        Assert.Single(vm.SrcFiles);
        Assert.Equal(@"C:\a.txt", vm.SrcFiles[0]);
        Assert.Single(vm.Dests);
    }

    [Fact]
    public async System.Threading.Tasks.Task AddSrc_adds_picked_files_deduped()
    {
        var s = SettingsWithJob(out _);
        var files = new FakeFilePicker { FilesToReturn = new[] { @"C:\a.txt", @"C:\b.txt" } };
        var vm = New(s, files: files);
        await vm.AddSrcCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.SrcFiles.Count);           // a.txt já existia -> não duplica
        Assert.Contains(@"C:\b.txt", vm.SrcFiles);
    }

    [Fact]
    public void RemoveSrc_removes_selected()
    {
        var s = SettingsWithJob(out _);
        var vm = New(s);
        vm.SelectedSrcIndex = 0;
        vm.RemoveSrcCommand.Execute(null);
        Assert.Empty(vm.SrcFiles);
    }

    [Fact]
    public async System.Threading.Tasks.Task AddDest_appends_edited_destination()
    {
        var s = SettingsWithJob(out _);
        var dlg = new FakeDialogService { EditResult = new Destination { Type = DestType.Local, Folder = @"C:\new" } };
        var vm = New(s, dlg);
        await vm.AddDestCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Dests.Count);
        Assert.Equal(@"C:\new", vm.Dests[1].Folder);
    }

    [Fact]
    public void Reset_clears_source_and_dests()
    {
        var s = SettingsWithJob(out _);
        var vm = New(s);
        vm.ResetCommand.Execute(null);
        Assert.Empty(vm.SrcFiles);
        Assert.Empty(vm.Dests);
    }

    [Fact]
    public void Save_writes_source_and_dests_to_current_job_and_closes_true()
    {
        var s = SettingsWithJob(out var job);
        var files = new FakeFilePicker();
        var vm = New(s, files: files);
        vm.SrcFiles.Clear();
        vm.SrcFiles.Add(@"C:\novo.bin");
        bool? closed = null;
        vm.CloseRequested += ok => closed = ok;

        vm.SaveCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(new[] { @"C:\novo.bin" }, job.Source.Files.ToArray());
        Assert.Single(job.Destinations);
    }
}
