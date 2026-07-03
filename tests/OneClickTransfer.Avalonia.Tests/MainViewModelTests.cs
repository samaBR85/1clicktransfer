using System;
using System.IO;
using System.Threading.Tasks;
using OneClickTransfer.Avalonia.ViewModels;
using OneClickTransfer.Models;

namespace OneClickTransfer.Avalonia.Tests;

public class MainViewModelTests
{
    private static MainViewModel New(AppSettings s, FakeDialogService? dlg = null)
        => new(s, dlg ?? new FakeDialogService(), new FakeUiDispatcher());

    private static AppSettings WithJobs(params TransferJob[] jobs)
    {
        var s = new AppSettings { SelectedJob = 0, AutoUpdateCheck = false };
        foreach (var j in jobs) s.Jobs.Add(j);
        return s;
    }

    private static TransferJob Job(string name) => new() { Name = name, Enabled = true };

    // ---- Gerência de tarefas ----
    [Fact]
    public void Duplicate_inserts_copy_after_selected()
    {
        var s = WithJobs(Job("A"));
        var vm = New(s);
        vm.OnOpened();
        vm.DuplicateJobCommand.Execute(null);
        Assert.Equal(2, vm.Jobs.Count);
        Assert.Equal(2, s.Jobs.Count);
        Assert.Contains("(copy)", vm.Jobs[1].Name);
        vm.OnClosed();
    }

    [Fact]
    public async Task Rename_updates_selected_job_name()
    {
        var s = WithJobs(Job("A"));
        var vm = New(s, new FakeDialogService { PromptResult = "Renamed" });
        vm.OnOpened();
        await vm.RenameJobCommand.ExecuteAsync(null);
        Assert.Equal("Renamed", s.Jobs[0].Name);
        Assert.Equal("Renamed", vm.Jobs[0].Name);
        vm.OnClosed();
    }

    [Fact]
    public async Task Remove_confirmed_deletes_selected_job()
    {
        var s = WithJobs(Job("A"), Job("B"));
        var vm = New(s, new FakeDialogService { ConfirmResult = true });
        vm.OnOpened();
        vm.SelectedJobIndex = 0;
        await vm.RemoveJobCommand.ExecuteAsync(null);
        Assert.Single(vm.Jobs);
        Assert.Equal("B", vm.Jobs[0].Name);
        vm.OnClosed();
    }

    [Fact]
    public async Task Remove_last_job_keeps_one_default()
    {
        var s = WithJobs(Job("A"));
        var vm = New(s, new FakeDialogService { ConfirmResult = true });
        vm.OnOpened();
        await vm.RemoveJobCommand.ExecuteAsync(null);
        Assert.Single(s.Jobs);          // recria uma tarefa padrão
        vm.OnClosed();
    }

    [Fact]
    public async Task Remove_declined_keeps_job()
    {
        var s = WithJobs(Job("A"), Job("B"));
        var vm = New(s, new FakeDialogService { ConfirmResult = false });
        vm.OnOpened();
        await vm.RemoveJobCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Jobs.Count);
        vm.OnClosed();
    }

    // ---- CanTransfer ----
    [Fact]
    public void CanTransfer_false_without_ready_job()
    {
        var s = WithJobs(Job("A"));      // sem origem/destino
        var vm = New(s);
        vm.OnOpened();
        Assert.False(vm.CanTransfer);
        vm.OnClosed();
    }

    [Fact]
    public void CanTransfer_true_with_ready_job()
    {
        var (src, dstDir) = MakeSrcAndDest(out _);
        var s = WithJobs(ReadyJob("A", src, dstDir));
        var vm = New(s);
        vm.OnOpened();
        Assert.True(vm.CanTransfer);
        vm.OnClosed();
    }

    // ---- Contagem sent / skipped / failed (transferência local real) ----
    [Fact]
    public async Task Transfer_local_copies_file_and_reports_sent()
    {
        var (src, dstDir) = MakeSrcAndDest(out var fileName);
        var s = WithJobs(ReadyJob("A", src, dstDir, OverwriteMode.Always));
        var vm = New(s);
        vm.OnOpened();
        await vm.TransferCommand.ExecuteAsync(null);
        Assert.True(File.Exists(Path.Combine(dstDir, fileName)));
        Assert.Contains("1 sent", vm.StatusText);
        vm.OnClosed();
    }

    [Fact]
    public async Task Transfer_never_overwrite_reports_skipped()
    {
        var (src, dstDir) = MakeSrcAndDest(out var fileName);
        File.WriteAllText(Path.Combine(dstDir, fileName), "já existe");   // destino já tem o arquivo
        var s = WithJobs(ReadyJob("A", src, dstDir, OverwriteMode.Never));
        var vm = New(s);
        vm.OnOpened();
        await vm.TransferCommand.ExecuteAsync(null);
        Assert.Contains("1 skipped", vm.StatusText);
        vm.OnClosed();
    }

    [Fact]
    public async Task Transfer_invalid_dest_reports_failed()
    {
        var (src, _) = MakeSrcAndDest(out _);
        var destAsFile = Path.Combine(Path.GetTempPath(), "oct-" + Guid.NewGuid().ToString("N") + ".file");
        File.WriteAllText(destAsFile, "sou um arquivo, não pasta");  // CreateDirectory sobre arquivo -> falha
        var s = WithJobs(ReadyJob("A", src, destAsFile, OverwriteMode.Always));
        var vm = New(s);
        vm.OnOpened();
        await vm.TransferCommand.ExecuteAsync(null);
        Assert.Contains("1 failed", vm.StatusText);
        vm.OnClosed();
    }

    // ---- helpers ----
    private static (string src, string dstDir) MakeSrcAndDest(out string fileName)
    {
        var root = Path.Combine(Path.GetTempPath(), "oct-vm-" + Guid.NewGuid().ToString("N"));
        var srcDir = Path.Combine(root, "src");
        var dstDir = Path.Combine(root, "dst");
        Directory.CreateDirectory(srcDir);
        Directory.CreateDirectory(dstDir);
        fileName = "payload.txt";
        var src = Path.Combine(srcDir, fileName);
        File.WriteAllText(src, "conteúdo de teste");
        return (src, dstDir);
    }

    private static TransferJob ReadyJob(string name, string src, string destFolder, OverwriteMode mode = OverwriteMode.Always)
    {
        var j = Job(name);
        j.Source.Files.Add(src);
        j.Destinations.Add(new Destination { Type = DestType.Local, Folder = destFolder, Enabled = true });
        j.Overwrite = mode;
        return j;
    }
}
