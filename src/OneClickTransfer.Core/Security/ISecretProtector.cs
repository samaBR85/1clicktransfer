namespace OneClickTransfer.Security;

/// <summary>Protecao de segredos (senha de FTP/SFTP) com implementacao por plataforma.</summary>
public interface ISecretProtector
{
    /// <summary>Criptografa o texto. Retorna "" se vazio ou em falha.</summary>
    string Protect(string plain);

    /// <summary>Descriptografa o valor armazenado. Retorna "" se vazio ou em falha.</summary>
    string Unprotect(string stored);
}
