using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace OneClickTransfer.Models;

public enum DestType { Local, Ftp, Sftp }
public enum SourceKind { File, Folder, Glob }
public enum OverwriteMode { Always, IfNewer, Never }

/// <summary>Um destino: pasta local, FTP/FTPS ou (futuro) SFTP.</summary>
public class Destination
{
    public DestType Type { get; set; } = DestType.Local;
    public string Folder { get; set; } = "";      // pasta local OU caminho remoto
    public string Host { get; set; } = "";
    public int Port { get; set; } = 21;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";     // criptografada (DPAPI)
    public bool UseTls { get; set; } = false;
    public bool Enabled { get; set; } = true;       // marcado = recebe a transferencia

    /// <summary>Resumo exibido nas listas (nao serializado).</summary>
    [JsonIgnore]
    public string Summary => Type switch
    {
        DestType.Local => "\U0001F4C1  " + Folder,
        DestType.Ftp => "\U0001F310  FTP  " + Host + ":" + Port + Folder,
        DestType.Sftp => "\U0001F310  SFTP  " + Host + ":" + Port + Folder,
        _ => ""
    };

    public Destination Clone() => new()
    {
        Type = Type, Folder = Folder, Host = Host, Port = Port,
        Username = Username, Password = Password, UseTls = UseTls, Enabled = Enabled
    };
}

/// <summary>O que sera transferido: um ou mais arquivos de origem.</summary>
public class SourceSpec
{
    public string Path { get; set; } = "";      // legado (1 arquivo) — migrado p/ Files
    public SourceKind Kind { get; set; } = SourceKind.File;
    public string Pattern { get; set; } = "*";
    public bool Recursive { get; set; } = true;
    public List<string> Files { get; set; } = new();  // vários arquivos de origem

    /// <summary>Lista efetiva de arquivos (Files, ou o Path legado se Files vazio).</summary>
    [JsonIgnore]
    public List<string> All => Files.Count > 0 ? Files
        : (string.IsNullOrEmpty(Path) ? new List<string>() : new List<string> { Path });

    [JsonIgnore] public int Count => All.Count;
    [JsonIgnore] public string First => All.Count > 0 ? All[0] : "";

    public SourceSpec Clone() => new()
    {
        Path = Path, Kind = Kind, Pattern = Pattern, Recursive = Recursive,
        Files = new List<string>(Files)
    };
}

/// <summary>Perfil salvo: origem + lista de destinos, com nome.</summary>
public class Profile
{
    public string Name { get; set; } = "";
    public SourceSpec Source { get; set; } = new();
    public List<Destination> Destinations { get; set; } = new();

    public Profile Clone() => new()
    {
        Name = Name,
        Source = Source.Clone(),
        Destinations = Destinations.ConvertAll(d => d.Clone())
    };
}

/// <summary>Uma tarefa: origem + destinos + modo de substituição próprios.
/// Várias tarefas coexistem; TRANSFERIR envia todas as marcadas (Enabled).</summary>
public class TransferJob
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool Watch { get; set; } = false;       // vigia a origem desta tarefa (envio automatico)
    public SourceSpec Source { get; set; } = new();
    public List<Destination> Destinations { get; set; } = new();
    public OverwriteMode Overwrite { get; set; } = OverwriteMode.Always;

    /// <summary>Selo 👁 exibido na lista quando a tarefa esta sendo vigiada.</summary>
    [JsonIgnore]
    public string WatchIcon => Watch ? "\U0001F441" : "";

    /// <summary>Rótulo da origem: nome do arquivo, ou "arquivo +N" se vários.</summary>
    [JsonIgnore]
    public string SourceFile
    {
        get
        {
            var all = Source?.All ?? new List<string>();
            if (all.Count == 0) return "";
            var p = all[0];
            var i = p.LastIndexOfAny(new[] { '/', '\\' });
            var name = i >= 0 ? p.Substring(i + 1) : p;
            return all.Count > 1 ? name + " +" + (all.Count - 1) : name;
        }
    }

    /// <summary>Resumo "arquivo → destino(s)" exibido na lista de tarefas.</summary>
    [JsonIgnore]
    public string Summary
    {
        get
        {
            var src = string.IsNullOrEmpty(SourceFile) ? "—" : SourceFile;
            var enabled = Destinations?.Where(d => d.Enabled).ToList() ?? new List<Destination>();
            string dst;
            if (enabled.Count == 0) dst = "—";
            else if (enabled.Count == 1) dst = enabled[0].Summary;
            else dst = enabled.Count + " destinos";
            return src + "  →  " + dst;
        }
    }

    public TransferJob Clone() => new()
    {
        Name = Name, Enabled = Enabled, Watch = Watch, Overwrite = Overwrite,
        Source = Source.Clone(),
        Destinations = Destinations.ConvertAll(d => d.Clone())
    };
}

/// <summary>Conjunto nomeado de destinos (reutilizável).</summary>
public class DestGroup
{
    public string Name { get; set; } = "";
    public List<Destination> Destinations { get; set; } = new();
    public DestGroup Clone() => new() { Name = Name, Destinations = Destinations.ConvertAll(d => d.Clone()) };
}

public class AppSettings
{
    // Preferencias globais
    public string Theme { get; set; } = "dark";       // dark | light
    public string Language { get; set; } = "en";      // pt | en (padrao: en)
    public string Shortcut { get; set; } = "F4";   // atalho do TRANSFERIR (F5 e fixo p/ Atualizar)
    public double SplitRatio { get; set; } = 0.5;   // largura do card ORIGEM (0..1)
    public double TasksHeight { get; set; } = 150;  // altura em px do painel TAREFAS

    // Geometria da janela principal (reabrir igual da ultima vez). 0 = usar padrao.
    public double WindowWidth { get; set; } = 0;
    public double WindowHeight { get; set; } = 0;
    public double WindowLeft { get; set; } = 0;
    public double WindowTop { get; set; } = 0;
    public bool WindowMaximized { get; set; } = false;
    public bool AutoUpdateCheck { get; set; } = true;   // procurar atualizacoes ao iniciar
    public OverwriteMode OverwriteMode { get; set; } = OverwriteMode.Always;  // legado (migrado p/ Jobs)

    // Tarefas: cada uma tem origem + destinos + modo próprios (v2.1)
    public List<TransferJob> Jobs { get; set; } = new();
    public int SelectedJob { get; set; } = 0;

    // Configuracao "atual" legada (v1/v2.0) — migrada para Jobs[0] no Normalize
    public SourceSpec Source { get; set; } = new();
    public List<Destination> Destinations { get; set; } = new();

    /// <summary>Tarefa atualmente selecionada na Home. Nunca nula (garante ao menos 1).</summary>
    [JsonIgnore]
    public TransferJob CurrentJob
    {
        get
        {
            if (Jobs.Count == 0) Jobs.Add(new TransferJob());
            if (SelectedJob < 0) SelectedJob = 0;
            if (SelectedJob >= Jobs.Count) SelectedJob = Jobs.Count - 1;
            return Jobs[SelectedJob];
        }
    }

    // Perfis salvos (origem + destinos)
    public List<Profile> Profiles { get; set; } = new();
    public string ActiveProfile { get; set; } = "";

    // Grupos de destino salvos (conjuntos de destinos reutilizáveis)
    public List<DestGroup> DestGroups { get; set; } = new();
}
