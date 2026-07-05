using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OneClickTransfer.Avalonia.ViewModels;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.Tests;

public class MainViewModelTests
{
    private static MainViewModel New(AppSettings s, FakeDialogService? dlg = null, FakeClipboard? clipboard = null)
        => new(s, dlg ?? new FakeDialogService(), new FakeUiDispatcher(), clipboard ?? new FakeClipboard(), new NullNotificationService());

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
    public void RefreshHome_folder_source_shows_folder_prefix_and_rows()
    {
        var tmp = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(tmp, "a.txt"), "x");
            var job = Job("A");
            job.Source = new SourceSpec { Kind = SourceKind.Folder, Path = tmp, Pattern = "*" };
            var s = WithJobs(job);
            var vm = New(s);
            vm.OnOpened();
            Assert.Contains(tmp, vm.Source.PathText);
            Assert.Single(vm.Source.Rows);
            vm.OnClosed();
        }
        finally { Directory.Delete(tmp, true); }
    }

    [Fact]
    public async Task Transfer_success_sets_last_transfer_text()
    {
        var (src, dstDir) = MakeSrcAndDest(out _);
        var s = WithJobs(ReadyJob("A", src, dstDir, OverwriteMode.Always));
        var vm = New(s);
        vm.OnOpened();
        Assert.False(vm.HasLastTransfer);
        await vm.TransferCommand.ExecuteAsync(null);
        Assert.True(vm.HasLastTransfer);
        Assert.Contains("Last transfer:", vm.LastTransferText);
        vm.OnClosed();
    }

    [Fact]
    public async Task Transfer_all_skipped_does_not_set_last_transfer()
    {
        var (src, dstDir) = MakeSrcAndDest(out var fileName);
        File.WriteAllText(Path.Combine(dstDir, fileName), "já existe");
        var s = WithJobs(ReadyJob("A", src, dstDir, OverwriteMode.Never));
        var vm = New(s);
        vm.OnOpened();
        await vm.TransferCommand.ExecuteAsync(null);
        Assert.False(vm.HasLastTransfer);
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

    [Fact]
    public async Task Transfer_ftp_offline_marks_failed_instead_of_crashing()
    {
        var (src, _) = MakeSrcAndDest(out _);
        var job = Job("A");
        job.Source.Files.Add(src);
        job.Destinations.Add(new Destination { Type = DestType.Ftp, Host = "notarealhost.invalid", Port = 21, Enabled = true });
        job.Overwrite = OverwriteMode.IfNewer;   // Always não passa pelo pré-check -- não reproduz o bug
        var s = WithJobs(job);
        var vm = New(s);
        vm.OnOpened();

        await vm.TransferCommand.ExecuteAsync(null);   // antes do fix: lança e derruba o teste

        Assert.Contains("1 failed", vm.StatusText);
        vm.OnClosed();
    }

    // ---- Auto-refresh de DESTINATION ao trocar de tarefa ----
    [Fact]
    public async Task SwitchJob_ftp_offline_shows_message_instead_of_hanging()
    {
        var (src, _) = MakeSrcAndDest(out _);
        var jobA = Job("A");
        var jobB = Job("B");
        jobB.Source.Files.Add(src);
        jobB.Destinations.Add(new Destination { Type = DestType.Ftp, Host = "notarealhost.invalid", Port = 21, Enabled = true });
        var s = WithJobs(jobA, jobB);
        var vm = New(s);
        vm.OnOpened();

        vm.SelectedJobIndex = 1;   // B: destino FTP inalcancavel

        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline && vm.Dest.Rows.Any(r => r.Name.Contains("Loading") || r.Name.Contains("Carregando")))
            await Task.Delay(100);

        Assert.Contains(vm.Dest.Rows, r => r.Name.Contains("offline") || r.Name.Contains("FTP address"));
        vm.OnClosed();
    }

    // ---- Menu de contexto do DESTINATION (local) ----
    [Fact]
    public async Task DeleteDestItem_local_removes_file_and_refreshes()
    {
        var (src, dstDir) = MakeSrcAndDest(out var fileName);
        File.Copy(src, Path.Combine(dstDir, fileName));
        var job = ReadyJob("A", src, dstDir);
        var s = WithJobs(job);
        var vm = New(s, new FakeDialogService { ConfirmResult = true });
        vm.OnOpened();

        var row = vm.Dest.Rows.First(r => r.RealName == fileName);
        await vm.DeleteDestItemCommand.ExecuteAsync(row);

        Assert.False(File.Exists(Path.Combine(dstDir, fileName)));
        vm.OnClosed();
    }

    [Fact]
    public async Task CopyDestPath_local_sets_clipboard_text()
    {
        var (src, dstDir) = MakeSrcAndDest(out var fileName);
        File.Copy(src, Path.Combine(dstDir, fileName));
        var job = ReadyJob("A", src, dstDir);
        var s = WithJobs(job);
        var clipboard = new FakeClipboard();
        var vm = New(s, clipboard: clipboard);
        vm.OnOpened();

        var row = vm.Dest.Rows.First(r => r.RealName == fileName);
        await vm.CopyDestPathCommand.ExecuteAsync(row);

        Assert.NotNull(clipboard.LastText);
        Assert.Contains(fileName, clipboard.LastText);
        vm.OnClosed();
    }

    [Fact]
    public async Task RenameDestItem_local_failure_sets_status_error_not_exception()
    {
        var (src, dstDir) = MakeSrcAndDest(out var fileName);
        File.Copy(src, Path.Combine(dstDir, fileName));
        var job = ReadyJob("A", src, dstDir);
        var s = WithJobs(job);
        // Nome novo == pasta existente vazia -> Directory.Move falha (destino ja existe).
        var conflictDir = Path.Combine(dstDir, "conflict");
        Directory.CreateDirectory(conflictDir);
        var vm = New(s, new FakeDialogService { PromptResult = "conflict" });
        vm.OnOpened();

        var row = vm.Dest.Rows.First(r => r.RealName == fileName);
        await vm.RenameDestItemCommand.ExecuteAsync(row);

        Assert.True(vm.StatusError);
        vm.OnClosed();
    }

    // ---- Fila de transferência + paralelismo ----
    [Fact]
    public async Task Transfer_multiple_destinations_respects_max_parallel_and_counts_correctly()
    {
        var (src, _) = MakeSrcAndDest(out var fileName);
        var dstA = Path.Combine(Path.GetTempPath(), "oct-dst-" + Guid.NewGuid().ToString("N"));
        var dstB = Path.Combine(Path.GetTempPath(), "oct-dst-" + Guid.NewGuid().ToString("N"));
        var job = Job("A");
        job.Source.Files.Add(src);
        job.Destinations.Add(new Destination { Type = DestType.Local, Folder = dstA, Enabled = true });
        job.Destinations.Add(new Destination { Type = DestType.Local, Folder = dstB, Enabled = true });
        var s = WithJobs(job);
        s.MaxParallelDestinations = 1;   // forca sequencial
        var vm = New(s);
        vm.OnOpened();

        await vm.TransferCommand.ExecuteAsync(null);

        Assert.True(File.Exists(Path.Combine(dstA, fileName)));
        Assert.True(File.Exists(Path.Combine(dstB, fileName)));
        Assert.Contains("2 sent", vm.StatusText);
        vm.OnClosed();
        Directory.Delete(dstA, true); Directory.Delete(dstB, true);
    }

    [Fact]
    public async Task Transfer_populates_queue_items_success_and_failure()
    {
        var (src, _) = MakeSrcAndDest(out var fileName);
        var goodDst = Path.Combine(Path.GetTempPath(), "oct-dst-" + Guid.NewGuid().ToString("N"));
        var badDst = Path.Combine(Path.GetTempPath(), "oct-" + Guid.NewGuid().ToString("N") + ".file");
        File.WriteAllText(badDst, "sou um arquivo, não pasta");   // CreateDirectory sobre arquivo -> falha
        var job = Job("A");
        job.Source.Files.Add(src);
        job.Destinations.Add(new Destination { Type = DestType.Local, Folder = goodDst, Enabled = true });
        job.Destinations.Add(new Destination { Type = DestType.Local, Folder = badDst, Enabled = true });
        var s = WithJobs(job);
        s.MaxParallelDestinations = 3;
        var vm = New(s);
        vm.OnOpened();

        await vm.TransferCommand.ExecuteAsync(null);

        Assert.Empty(vm.QueuedItems);
        Assert.Single(vm.SucceededItems);
        Assert.Single(vm.FailedItems);
        Assert.True(vm.HasQueueActivity);
        vm.OnClosed();
        Directory.Delete(goodDst, true); File.Delete(badDst);
    }

    // ---- Envio individual (topo, painel direito) ----
    [Fact]
    public async Task TransferSelected_sends_only_the_selected_job()
    {
        var (srcA, dstA) = MakeSrcAndDest(out var fileA);
        var (srcB, dstB) = MakeSrcAndDest(out var fileB);
        var s = WithJobs(ReadyJob("A", srcA, dstA), ReadyJob("B", srcB, dstB));
        var vm = New(s);
        vm.OnOpened();
        vm.SelectedJobIndex = 1;   // B
        await vm.TransferSelectedCommand.ExecuteAsync(null);
        Assert.True(File.Exists(Path.Combine(dstB, fileB)));    // só B foi enviado
        Assert.False(File.Exists(Path.Combine(dstA, fileA)));
        vm.OnClosed();
    }

    [Fact]
    public void CanTransferSelected_reflects_selected_job_readiness()
    {
        var (src, dst) = MakeSrcAndDest(out _);
        var s = WithJobs(Job("vazia"), ReadyJob("pronta", src, dst));
        var vm = New(s);
        vm.OnOpened();
        vm.SelectedJobIndex = 0;   // vazia
        Assert.False(vm.CanTransferSelected);
        vm.SelectedJobIndex = 1;   // pronta
        Assert.True(vm.CanTransferSelected);
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
