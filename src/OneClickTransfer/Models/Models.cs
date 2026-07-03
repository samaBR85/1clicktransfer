using System.Collections.Generic;
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

/// <summary>O que sera transferido. Marco 1 usa Kind=File.</summary>
public class SourceSpec
{
    public string Path { get; set; } = "";
    public SourceKind Kind { get; set; } = SourceKind.File;
    public string Pattern { get; set; } = "*";
    public bool Recursive { get; set; } = true;

    public SourceSpec Clone() => new() { Path = Path, Kind = Kind, Pattern = Pattern, Recursive = Recursive };
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
    public string Language { get; set; } = "pt";      // pt | en
    public string Shortcut { get; set; } = "F4";   // atalho do TRANSFERIR (F5 e fixo p/ Atualizar)
    public double SplitRatio { get; set; } = 0.5;   // largura do card ORIGEM (0..1)
    public bool WatchEnabled { get; set; } = false; // envio automatico ao mudar o arquivo
    public OverwriteMode OverwriteMode { get; set; } = OverwriteMode.Always;

    // Configuracao "atual" (de trabalho) — espelha o v1
    public SourceSpec Source { get; set; } = new();
    public List<Destination> Destinations { get; set; } = new();

    // Perfis salvos (origem + destinos)
    public List<Profile> Profiles { get; set; } = new();
    public string ActiveProfile { get; set; } = "";

    // Grupos de destino salvos (conjuntos de destinos reutilizáveis)
    public List<DestGroup> DestGroups { get; set; } = new();
}
