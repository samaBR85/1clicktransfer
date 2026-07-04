using System.IO;
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
    public void Save_renames_the_task()
    {
        var s = SettingsWithJob(out var job);
        var vm = New(s);
        vm.TaskName = "Renomeada";
        vm.SaveCommand.Execute(null);
        Assert.Equal("Renomeada", job.Name);
    }

    [Fact]
    public void RemoveSrc_multi_removes_all_selected()
    {
        var s = SettingsWithJob(out _);
        var vm = New(s);
        vm.SrcFiles.Clear();
        vm.SrcFiles.Add("a"); vm.SrcFiles.Add("b"); vm.SrcFiles.Add("c");
        vm.RemoveSrcCommand.Execute(new System.Collections.Generic.List<object> { "a", "c" });
        Assert.Equal(new[] { "b" }, vm.SrcFiles.ToArray());
    }

    [Fact]
    public async System.Threading.Tasks.Task ChooseFolder_switches_to_folder_mode()
    {
        var s = SettingsWithJob(out _);
        var tmp = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(tmp, "x.bin"), "1");
            var vm = New(s, files: new FakeFilePicker { FolderToReturn = tmp });

            await vm.ChooseFolderCommand.ExecuteAsync(null);

            Assert.True(vm.IsFolderSource);
            Assert.Equal(tmp, vm.FolderSourcePath);
            Assert.Empty(vm.SrcFiles);
        }
        finally { Directory.Delete(tmp, true); }
    }

    [Fact]
    public async System.Threading.Tasks.Task Save_writes_folder_source_kind()
    {
        var s = SettingsWithJob(out var job);
        var vm = New(s, files: new FakeFilePicker { FolderToReturn = @"C:\some\folder" });
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        vm.SaveCommand.Execute(null);

        Assert.Equal(SourceKind.Folder, job.Source.Kind);
        Assert.Equal(@"C:\some\folder", job.Source.Path);
        Assert.Empty(job.Source.Files);
    }

    [Fact]
    public async System.Threading.Tasks.Task UseFilesInstead_reverts_to_empty_file_mode()
    {
        var s = SettingsWithJob(out _);
        var vm = New(s, files: new FakeFilePicker { FolderToReturn = @"C:\folder" });
        await vm.ChooseFolderCommand.ExecuteAsync(null);

        vm.UseFilesInsteadCommand.Execute(null);

        Assert.False(vm.IsFolderSource);
        Assert.Empty(vm.SrcFiles);
    }

    [Fact]
    public async System.Threading.Tasks.Task ChooseFolder_populates_exclude_items_from_root_level_all_included()
    {
        var tmp = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(tmp, "a.txt"), "x");
            File.WriteAllText(Path.Combine(tmp, "b.txt"), "y");
            Directory.CreateDirectory(Path.Combine(tmp, "sub"));

            var s = SettingsWithJob(out _);
            var vm = New(s, files: new FakeFilePicker { FolderToReturn = tmp });
            await vm.ChooseFolderCommand.ExecuteAsync(null);

            Assert.Equal(3, vm.FolderExcludeItems.Count);
            Assert.All(vm.FolderExcludeItems, i => Assert.True(i.IsIncluded));
        }
        finally { Directory.Delete(tmp, true); }
    }

    [Fact]
    public void Unchecking_a_folder_item_writes_the_right_exclude_pattern_on_save()
    {
        var tmp = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(tmp, "a.txt"), "x");
            Directory.CreateDirectory(Path.Combine(tmp, "sub"));

            var s = SettingsWithJob(out var job);
            job.Source = new SourceSpec { Kind = SourceKind.Folder, Path = tmp };
            var vm = New(s);

            var subItem = vm.FolderExcludeItems.Single(i => i.RealName == "sub");
            subItem.IsIncluded = false;

            vm.SaveCommand.Execute(null);

            Assert.Equal(new[] { "sub/" }, job.Source.ExcludePatterns);
        }
        finally { Directory.Delete(tmp, true); }
    }

    [Fact]
    public void LoadSourceFromSpec_restores_unchecked_state_for_saved_patterns()
    {
        var tmp = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(tmp, "a.txt"), "x");
            File.WriteAllText(Path.Combine(tmp, "b.tmp"), "y");

            var s = SettingsWithJob(out var job);
            job.Source = new SourceSpec { Kind = SourceKind.Folder, Path = tmp, ExcludePatterns = { "b.tmp" } };
            var vm = New(s);

            Assert.False(vm.FolderExcludeItems.Single(i => i.RealName == "b.tmp").IsIncluded);
            Assert.True(vm.FolderExcludeItems.Single(i => i.RealName == "a.txt").IsIncluded);
        }
        finally { Directory.Delete(tmp, true); }
    }

    [Fact]
    public void LoadSourceFromSpec_preserves_orphan_pattern_not_matching_any_visible_item()
    {
        var tmp = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(tmp, "a.txt"), "x");

            var s = SettingsWithJob(out var job);
            job.Source = new SourceSpec { Kind = SourceKind.Folder, Path = tmp, ExcludePatterns = { "*.tmp" } };
            var vm = New(s);

            // "*.tmp" não bate com nenhum item visível (nenhum arquivo .tmp na pasta agora),
            // mas deve sobreviver ao save em vez de ser silenciosamente descartado.
            vm.SaveCommand.Execute(null);
            Assert.Contains("*.tmp", job.Source.ExcludePatterns);
        }
        finally { Directory.Delete(tmp, true); }
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
