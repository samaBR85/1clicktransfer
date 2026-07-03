using OneClickTransfer.Avalonia.ViewModels;
using OneClickTransfer.Models;
using OneClickTransfer.Services;

namespace OneClickTransfer.Avalonia.Tests;

public class DestinationEditorViewModelTests
{
    private static DestinationEditorViewModel New(Destination? d = null)
        => new(d, new FakeDialogService(), new FakeFilePicker());

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
