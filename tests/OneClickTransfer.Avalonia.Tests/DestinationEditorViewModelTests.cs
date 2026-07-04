using OneClickTransfer.Avalonia.ViewModels;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.Tests;

public class DestinationEditorViewModelTests
{
    private static DestinationEditorViewModel New(Destination? d = null, AppSettings? s = null, FakeDialogService? dlg = null)
        => new(d, s ?? new AppSettings(), dlg ?? new FakeDialogService(), new FakeFilePicker());

    [Fact]
    public void New_defaults_to_local_port_21()
    {
        var vm = New();
        Assert.True(vm.IsLocal);
        Assert.False(vm.IsServer);
        Assert.Equal("21", vm.Port);
    }

    [Fact]
    public void Switch_to_sftp_sets_port_22_and_back_to_ftp_sets_21()
    {
        var vm = New();
        vm.IsSftp = true;
        Assert.Equal("22", vm.Port);
        Assert.True(vm.IsServer);
        vm.IsFtp = true;
        Assert.Equal("21", vm.Port);
    }

    [Fact]
    public void Custom_port_is_preserved_when_switching_type()
    {
        var vm = New();
        vm.IsFtp = true;
        vm.Port = "2121";
        vm.IsSftp = true;                 // 2121 não é 21/vazio -> preserva
        Assert.Equal("2121", vm.Port);
    }

    [Fact]
    public void ShowTls_only_for_ftp()
    {
        var vm = New();
        vm.IsFtp = true;
        Assert.True(vm.ShowTls);
        vm.IsSftp = true;
        Assert.False(vm.ShowTls);
        vm.IsLocal = true;
        Assert.False(vm.ShowTls);
    }

    [Fact]
    public void Loading_ftp_destination_fills_fields()
    {
        var d = new Destination
        {
            Type = DestType.Ftp, Host = "1.2.3.4", Port = 2121, Folder = "/pub",
            Username = "bob", Password = SecretProtector.Protect("s3cret"), UseTls = true
        };
        var vm = New(d);
        Assert.True(vm.IsFtp);
        Assert.Equal("1.2.3.4", vm.Host);
        Assert.Equal("2121", vm.Port);
        Assert.Equal("/pub", vm.RemoteFolder);
        Assert.Equal("bob", vm.Username);
        Assert.Equal("s3cret", vm.Password);   // descriptografada p/ edição
        Assert.True(vm.UseTls);
    }

    [Fact]
    public void Ok_on_local_returns_local_destination()
    {
        var vm = New();
        vm.LocalFolder = @"C:\dst";
        Destination? result = null;
        vm.CloseRequested += r => result = r;
        vm.OkCommand.Execute(null);
        Assert.NotNull(result);
        Assert.Equal(DestType.Local, result!.Type);
        Assert.Equal(@"C:\dst", result.Folder);
    }

    [Fact]
    public void Ok_on_ftp_returns_ftp_with_encrypted_password()
    {
        var vm = New();
        vm.IsFtp = true;
        vm.Host = "host";
        vm.Port = "21";
        vm.RemoteFolder = "/x";
        vm.Username = "u";
        vm.Password = "pw";
        vm.UseTls = true;
        Destination? result = null;
        vm.CloseRequested += r => result = r;
        vm.OkCommand.Execute(null);
        Assert.NotNull(result);
        Assert.Equal(DestType.Ftp, result!.Type);
        Assert.Equal("host", result.Host);
        Assert.Equal(21, result.Port);
        Assert.True(result.UseTls);
        Assert.NotEqual("pw", result.Password);                 // guardada criptografada
        Assert.Equal("pw", SecretProtector.Unprotect(result.Password));
    }

    // ---- Servidores FTP/SFTP salvos ----
    [Fact]
    public async System.Threading.Tasks.Task SavedServerSave_adds_to_settings_and_selects_it()
    {
        var s = new AppSettings();
        var vm = New(s: s, dlg: new FakeDialogService { PromptResult = "NAS" });
        vm.IsFtp = true;
        vm.Host = "ftp.example.com";
        vm.Port = "21";
        vm.Username = "u";
        vm.Password = "pw";

        await vm.SavedServerSaveCommand.ExecuteAsync(null);

        Assert.Single(s.SavedServers);
        Assert.Equal("NAS", s.SavedServers[0].Name);
        Assert.Equal("ftp.example.com", s.SavedServers[0].Host);
        Assert.Equal("pw", SecretProtector.Unprotect(s.SavedServers[0].Password));
        Assert.Contains("NAS", vm.SavedServerOptions);
    }

    [Fact]
    public void SelectingSavedServer_fills_fields()
    {
        var s = new AppSettings
        {
            SavedServers = { new SavedServer { Name = "NAS", Type = DestType.Sftp, Host = "h", Port = 22, Username = "bob", Password = SecretProtector.Protect("s3cret") } }
        };
        var vm = New(s: s);

        vm.SelectedSavedServerIndex = vm.SavedServerOptions.IndexOf("NAS");

        Assert.True(vm.IsSftp);
        Assert.Equal("h", vm.Host);
        Assert.Equal("22", vm.Port);
        Assert.Equal("bob", vm.Username);
        Assert.Equal("s3cret", vm.Password);
    }

    [Fact]
    public async System.Threading.Tasks.Task SavedServerDelete_removes_from_settings()
    {
        var s = new AppSettings { SavedServers = { new SavedServer { Name = "NAS", Type = DestType.Ftp, Host = "h" } } };
        var vm = New(s: s, dlg: new FakeDialogService { ConfirmResult = true });

        vm.SelectedSavedServerIndex = vm.SavedServerOptions.IndexOf("NAS");
        await vm.SavedServerDeleteCommand.ExecuteAsync(null);

        Assert.Empty(s.SavedServers);
    }

    [Fact]
    public void Cancel_returns_null()
    {
        var vm = New();
        bool raised = false;
        Destination? result = new();
        vm.CloseRequested += r => { raised = true; result = r; };
        vm.CancelCommand.Execute(null);
        Assert.True(raised);
        Assert.Null(result);
    }
}
