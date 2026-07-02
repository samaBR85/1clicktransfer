using System.Collections.Generic;

namespace OneClickTransfer.I18n;

/// <summary>Textos PT/EN (portado do v1). Use L.T("chave") ou L.T("chave", args).</summary>
public static class L
{
    public static string Lang = "pt";

    private static readonly Dictionary<string, (string pt, string en)> M = new()
    {
        ["appTitle"] = ("Transferência 1-Clique", "1-Click Transfer"),
        ["refresh"] = ("Atualizar", "Refresh"),
        ["lightMode"] = ("Modo claro", "Light mode"),
        ["darkMode"] = ("Modo escuro", "Dark mode"),
        ["profile"] = ("Perfil:", "Profile:"),
        ["noneItem"] = ("(nenhum)", "(none)"),
        ["source"] = ("ORIGEM", "SOURCE"),
        ["destination"] = ("DESTINO", "DESTINATION"),
        ["folderPrefix"] = ("Pasta: ", "Folder: "),
        ["ftpPrefix"] = ("FTP: ", "FTP: "),
        ["noFile"] = ("(nenhum arquivo - clique em Configurar)", "(no file - click Settings)"),
        ["noDest"] = ("(nenhum destino - clique em Configurar)", "(no destination - click Settings)"),
        ["clickRefreshFtp"] = ("(clique em Atualizar para listar o FTP)", "(click Refresh to list the FTP)"),
        ["loadingFtp"] = ("Carregando lista do FTP...", "Loading FTP list..."),
        ["emptyFolder"] = ("(pasta vazia)", "(empty folder)"),
        ["cantListFtp"] = ("(não foi possível listar o FTP)", "(could not list the FTP)"),
        ["colName"] = ("Nome", "Name"),
        ["colSize"] = ("Tamanho", "Size"),
        ["colModified"] = ("Modificado", "Modified"),
        ["action"] = ("Ação:", "Action:"),
        ["replace"] = ("Substituir", "Replace"),
        ["replaceIfNewer"] = ("Substituir se for mais recente", "Replace if newer"),
        ["dontReplace"] = ("Não Substituir", "Don't replace"),
        ["transfer"] = ("TRANSFERIR", "TRANSFER"),
        ["settings"] = ("Configurar", "Settings"),
        ["shortcutHint"] = ("Atalho do teclado: {0}", "Keyboard shortcut: {0}"),
        ["clickSettingsStart"] = ("Clique em \"Configurar\" para começar.", "Click \"Settings\" to start."),
        ["refreshing"] = ("Atualizando...", "Refreshing..."),
        ["profileLoaded"] = ("Perfil carregado: {0}", "Profile loaded: {0}"),
        ["fieldsCleared"] = ("Campos limpos.", "Fields cleared."),
        ["settingsSaved"] = ("Configurações salvas.", "Settings saved."),
        ["checkingDest"] = ("Verificando destino...", "Checking destination..."),
        ["uploading"] = ("Enviando...", "Uploading..."),
        ["copying"] = ("Copiando...", "Copying..."),
        ["connectingFtp"] = ("Conectando ao FTP...", "Connecting to FTP..."),
        ["completed"] = ("Concluído com sucesso!", "Completed successfully!"),
        ["transferFailed"] = ("Falha na transferência.", "Transfer failed."),
        ["nothingNewer"] = ("Nada a enviar: o destino já está igual ou mais novo.", "Nothing to send: the destination is already the same or newer."),
        ["notSentExists"] = ("Não enviado: \"{0}\" já existe no destino.", "Not sent: \"{0}\" already exists at the destination."),
        ["srcNotFound"] = ("Arquivo de origem não encontrado!", "Source file not found!"),
        ["errorTitle"] = ("Erro", "Error"),
        ["transferErrorTitle"] = ("Erro na transferência", "Transfer error"),
        // Dialogo de configuracoes
        ["settingsTitle"] = ("Configurações", "Settings"),
        ["sec1File"] = ("1) Arquivo a ser transferido", "1) File to transfer"),
        ["browse"] = ("Procurar", "Browse"),
        ["sec2Where"] = ("2) Para onde enviar", "2) Where to send"),
        ["localFolder"] = ("Pasta local / rede", "Local / network folder"),
        ["ftpServer"] = ("Servidor FTP", "FTP server"),
        ["destFolderLabel"] = ("Pasta de destino:", "Destination folder:"),
        ["ftpHost"] = ("Servidor (host):", "Server (host):"),
        ["ftpPort"] = ("Porta:", "Port:"),
        ["ftpRemote"] = ("Pasta remota:", "Remote folder:"),
        ["ftpUser"] = ("Usuário:", "Username:"),
        ["ftpPass"] = ("Senha:", "Password:"),
        ["ftpTls"] = ("Usar TLS (FTPS)", "Use TLS (FTPS)"),
        ["testConn"] = ("Testar conexão", "Test connection"),
        ["testing"] = ("Testando...", "Testing..."),
        ["sec3Options"] = ("3) Opções", "3) Options"),
        ["shortcutLabel"] = ("Atalho do teclado para TRANSFERIR:", "Keyboard shortcut for TRANSFER:"),
        ["themeLabel"] = ("Tema:", "Theme:"),
        ["themeDark"] = ("Escuro", "Dark"),
        ["themeLight"] = ("Claro", "Light"),
        ["langLabel"] = ("Idioma / Language:", "Idioma / Language:"),
        ["profSaved"] = ("Perfis salvos:", "Saved profiles:"),
        ["selectItem"] = ("(selecione)", "(select)"),
        ["saveAs"] = ("Salvar atual como...", "Save current as..."),
        ["rename"] = ("Renomear", "Rename"),
        ["delete"] = ("Excluir", "Delete"),
        ["resetFields"] = ("Resetar campos", "Reset fields"),
        ["save"] = ("Salvar", "Save"),
        ["cancel"] = ("Cancelar", "Cancel"),
        ["connOkMsg"] = ("Conexão OK! O FTP respondeu corretamente.", "Connection OK! The FTP responded correctly."),
        ["successTitle"] = ("Sucesso", "Success"),
        ["langPtItem"] = ("Português", "Português"),
        ["langEnItem"] = ("English", "English"),
    };

    public static string T(string key)
        => M.TryGetValue(key, out var v) ? (Lang == "en" ? v.en : v.pt) : key;

    public static string T(string key, params object[] args)
        => string.Format(T(key), args);
}
