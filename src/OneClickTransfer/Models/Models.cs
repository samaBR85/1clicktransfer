using System.Collections.Generic;

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

    public Destination Clone() => new()
    {
        Type = Type, Folder = Folder, Host = Host, Port = Port,
        Username = Username, Password = Password, UseTls = UseTls
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

public class AppSettings
{
    // Preferencias globais
    public string Theme { get; set; } = "dark";       // dark | light
    public string Language { get; set; } = "pt";      // pt | en
    public string Shortcut { get; set; } = "F5";
    public OverwriteMode OverwriteMode { get; set; } = OverwriteMode.Always;

    // Configuracao "atual" (de trabalho) — espelha o v1
    public SourceSpec Source { get; set; } = new();
    public List<Destination> Destinations { get; set; } = new();

    // Perfis salvos
    public List<Profile> Profiles { get; set; } = new();
    public string ActiveProfile { get; set; } = "";
}
