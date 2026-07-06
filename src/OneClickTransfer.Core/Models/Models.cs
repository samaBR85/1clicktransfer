using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
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
    public bool ForceLegacyPasv { get; set; } = false;  // pula EPSV, vai direto pra PASV (servidores como o ftpd do 3DS)
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
        Username = Username, Password = Password, UseTls = UseTls, ForceLegacyPasv = ForceLegacyPasv, Enabled = Enabled
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
    public List<string> ExcludePatterns { get; set; } = new();  // ex: "node_modules/", ".git/", "*.tmp"

    /// <summary>Kind==Folder: se true, o destino recebe a pasta escolhida como uma subpasta
    /// nomeada (ex.: dest/AppGuide/de/...) em vez de só o conteúdo dela (dest/de/...).</summary>
    public bool KeepRootFolderName { get; set; }

    /// <summary>Lista efetiva de arquivos: se Kind==Folder, expande a pasta (recursivo, dinâmico —
    /// reavaliado a cada chamada), aplicando ExcludePatterns; senão usa Files, ou o Path legado se
    /// Files vazio.</summary>
    [JsonIgnore]
    public List<string> All
    {
        get
        {
            if (Kind == SourceKind.Folder)
            {
                if (string.IsNullOrEmpty(Path) || !Directory.Exists(Path)) return new List<string>();
                try
                {
                    // Recursive sempre true aqui (decisao do usuario); campo fica so p/ retro-compat do JSON.
                    return Directory.EnumerateFiles(Path, string.IsNullOrEmpty(Pattern) ? "*" : Pattern, SearchOption.AllDirectories)
                        .Where(f => !IsExcluded(System.IO.Path.GetRelativePath(Path, f), ExcludePatterns))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                }
                catch { return new List<string>(); }
            }
            return Files.Count > 0 ? Files
                : (string.IsNullOrEmpty(Path) ? new List<string>() : new List<string> { Path });
        }
    }

    /// <summary>Casamento simples estilo .gitignore: "pasta/" bate qualquer segmento de diretorio,
    /// "*.ext"/nome-com-curinga bate o nome do arquivo, nome sem barra bate qualquer segmento.</summary>
    private static bool IsExcluded(string relativePath, List<string> patterns)
    {
        if (patterns == null || patterns.Count == 0) return false;
        var segments = relativePath.Split(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        var fileName = segments[^1];
        foreach (var raw in patterns)
        {
            var p = raw?.Trim() ?? "";
            if (p.Length == 0) continue;
            if (p.EndsWith('/') || p.EndsWith('\\'))
            {
                var folderName = p.TrimEnd('/', '\\');
                if (segments.Take(segments.Length - 1).Any(s => string.Equals(s, folderName, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            else if (FileSystemName.MatchesSimpleExpression(p, fileName))
            {
                return true;
            }
            else if (segments.Any(s => string.Equals(s, p, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }

    [JsonIgnore] public int Count => All.Count;
    [JsonIgnore] public string First => All.Count > 0 ? All[0] : "";

    /// <summary>Caminho relativo usado pra recriar a estrutura no destino: em modo arquivo-a-avulso
    /// é só o nome do arquivo (não há árvore); em modo pasta é relativo à raiz escolhida, com a
    /// própria pasta como prefixo se KeepRootFolderName estiver marcado.</summary>
    public string RelPathFor(string absoluteFilePath)
    {
        if (Kind != SourceKind.Folder) return System.IO.Path.GetFileName(absoluteFilePath);
        var rel = System.IO.Path.GetRelativePath(Path, absoluteFilePath);
        if (!KeepRootFolderName) return rel;
        var rootName = System.IO.Path.GetFileName(Path.TrimEnd('\\', '/'));
        return string.IsNullOrEmpty(rootName) ? rel : System.IO.Path.Combine(rootName, rel);
    }

    public SourceSpec Clone() => new()
    {
        Path = Path, Kind = Kind, Pattern = Pattern, Recursive = Recursive,
        Files = new List<string>(Files), ExcludePatterns = new List<string>(ExcludePatterns),
        KeepRootFolderName = KeepRootFolderName
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

/// <summary>Servidor FTP/SFTP salvo (reutilizável no editor de destino).</summary>
public class SavedServer
{
    public string Name { get; set; } = "";
    public DestType Type { get; set; } = DestType.Ftp;
    public string Host { get; set; } = "";
    public int Port { get; set; } = 21;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";     // criptografada (DPAPI)
    public bool UseTls { get; set; } = false;
    public bool ForceLegacyPasv { get; set; } = false;

    public SavedServer Clone() => new()
    {
        Name = Name, Type = Type, Host = Host, Port = Port,
        Username = Username, Password = Password, UseTls = UseTls, ForceLegacyPasv = ForceLegacyPasv
    };
}

public class AppSettings
{
    // Preferencias globais
    public string Theme { get; set; } = "dark";       // dark | light
    public string Language { get; set; } = "en";      // pt | en (padrao: en)
    public string Shortcut { get; set; } = "F4";   // atalho do TRANSFERIR (F5 e fixo p/ Atualizar)
    public double SplitRatio { get; set; } = 0.5;   // largura do card ORIGEM (0..1)
    public double TasksHeight { get; set; } = 150;  // altura em px do painel TAREFAS
    public double QueueHeight { get; set; } = 160;  // altura em px do painel TRANSFER QUEUE

    // Geometria da janela principal (reabrir igual da ultima vez). 0 = usar padrao.
    public double WindowWidth { get; set; } = 0;
    public double WindowHeight { get; set; } = 0;
    public double WindowLeft { get; set; } = 0;
    public double WindowTop { get; set; } = 0;
    public bool WindowMaximized { get; set; } = false;
    public bool AutoUpdateCheck { get; set; } = true;   // procurar atualizacoes ao iniciar
    public OverwriteMode OverwriteMode { get; set; } = OverwriteMode.Always;  // legado (migrado p/ Jobs)
    public DateTime? LastTransferAt { get; set; } = null;   // ultimo envio bem-sucedido (rodape)
    public int MaxParallelDestinations { get; set; } = 3;   // limite de destinos enviados ao mesmo tempo (1-8)
    public bool MinimizeToTrayOnClose { get; set; } = false;   // opt-in: fechar a janela so minimiza p/ bandeja
    public bool KeepWatchWhileMinimized { get; set; } = false;   // opt-in: Watch continua ativo com a janela escondida na bandeja

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

    // Servidores FTP/SFTP salvos (reutilizáveis no editor de destino)
    public List<SavedServer> SavedServers { get; set; } = new();
}
