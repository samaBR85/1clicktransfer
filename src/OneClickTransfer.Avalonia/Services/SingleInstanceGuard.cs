using System;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OneClickTransfer.Avalonia.Services;

/// <summary>Garante 1 instância por caminho de executável (Mutex nomeado com hash do caminho) --
/// duas cópias em pastas diferentes rodam livremente. A instância nova avisa a existente via
/// named pipe pra se restaurar/focar, e sai sem abrir nada.</summary>
public sealed class SingleInstanceGuard
{
    private readonly Mutex _mutex;
    private readonly string _pipeName;
    public bool IsPrimary { get; }

    public SingleInstanceGuard(string exePath)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(exePath)))[..16];
        _pipeName = "1clickTransfer_Activate_" + hash;
        _mutex = new Mutex(initiallyOwned: true, name: "1clickTransfer_Lock_" + hash, out var createdNew);
        IsPrimary = createdNew;
    }

    public void NotifyExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(300);
        }
        catch { /* instância existente pode não estar com o listener pronto ainda -- ignora */ }
    }

    public void StartListening(Action onActivateRequested)
    {
        _ = Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    await server.WaitForConnectionAsync();
                    onActivateRequested();
                }
                catch { await Task.Delay(500); }
            }
        });
    }
}
