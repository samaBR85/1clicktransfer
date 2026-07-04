<p align="center">
  <img src="assets/app-icon-256.png" width="120" height="120" alt="1-Click Transfer">
</p>

<h1 align="center">1-Click Transfer</h1>

<p align="center">
  <a href="https://github.com/samaBR85/1clicktransfer/releases/latest"><img src="https://img.shields.io/github/v/release/samaBR85/1clicktransfer?label=download" alt="Release"></a>
  <img src="https://img.shields.io/github/downloads/samaBR85/1clicktransfer/total?label=downloads&color=success" alt="Downloads">
  <img src="https://img.shields.io/badge/license-MIT-blue" alt="MIT">
  <img src="https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-0078D4" alt="Windows | Linux | macOS">
  <img src="https://img.shields.io/badge/.NET-8-512BD4" alt=".NET 8">
  <img src="https://img.shields.io/badge/UI-Avalonia%2011-8B3FE8" alt="Avalonia 11">
  <img src="https://img.shields.io/badge/i18n-PT%20%2B%20EN-success" alt="PT + EN">
</p>

<p align="center">
  <a href="https://samabr85.github.io/1clicktransfer/"><b>🌐 Website</b></a> &nbsp;·&nbsp;
  <a href="README.md"><b>🇬🇧 Read in English</b></a>
</p>

<p align="center">
  <a href="https://ko-fi.com/E5N7227AO5"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="ko-fi"></a>
</p>

<hr>

Um app de desktop **multiplataforma** (**C# / .NET 8, Avalonia UI**) para **Windows, Linux e macOS**.
Um botão grande **TRANSFERIR** envia seu(s) **arquivo(s)** pré-escolhido(s) para o(s) **destino(s)**
pré-escolhido(s) — uma **pasta local/rede**, um servidor **FTP/FTPS** ou **SFTP**. Monte várias
**tarefas** independentes (cada uma com sua origem e seus destinos) e dispare todas com um clique
ou **uma de cada vez** — ou deixe o app **observar** um arquivo e enviá-lo sozinho quando ele mudar.

<p align="center">
  <img src="screenshots/v3.0/home.png" width="860" alt="Transferência 1-Clique — janela principal">
</p>

## Recursos
- **Várias tarefas** — cada tarefa é um par *origem → destinos*. Ligue/desligue quais quiser e
  transfira todas as marcadas com um clique, ou use **Enviar esta tarefa** para disparar só uma.
- **Vários arquivos de origem** por tarefa, para **vários destinos**: pastas local/rede,
  **FTP/FTPS** e **SFTP**. Os destinos ficam numa biblioteca salva, com checkbox por item,
  **grupos** nomeados, **presets** reutilizáveis e uma biblioteca de **servidores FTP/SFTP salvos**.
- **Fila de transferência** — um painel ao vivo mostra o que está na fila, em andamento, com falha
  e concluído, com progresso por item e um limite configurável de **destinos em paralelo**.
- **Origem em pasta com padrões de exclusão** — escolha uma pasta inteira (recursiva) como origem e
  exclua subpastas/arquivos com padrões simples no estilo `.gitignore`.
- **Botão direito num destino** — criar pasta, renomear, excluir ou copiar o caminho, tanto local
  quanto em destinos FTP/SFTP.
- **Observar (envio automático), por tarefa** — quando o arquivo de origem muda, a tarefa envia
  sozinha (ótimo para saídas de build).
- **Ícone na bandeja do sistema** — abrir a janela, enviar todas as tarefas habilitadas ou sair
  direto da bandeja; opção de **minimizar para a bandeja ao fechar**.
- **Notificações do sistema** ao concluir ou falhar uma transferência (Windows, Linux, macOS).
- **Linha de comando** — rode sem janela por script, cron ou Agendador de Tarefas:
  `1clickTransfer --task "Nome"`, `--all`, `--list`, `--silent`.
- **Auto-update** — verifica os Releases do GitHub; no Windows baixa e se substitui, no Linux/macOS
  mostra as novidades e abre a página do release.
- **Ações**: *Substituir*, *Substituir se for mais recente*, *Não Substituir*.
- **Navegadores de Origem/Destino** (com navegador de pastas FTP/SFTP), colunas e painéis
  redimensionáveis; a janela **lembra tamanho e posição**.
- Tema **escuro / claro**, interface **Português / Inglês** (troca em Configurações).
- Senhas guardadas **criptografadas** — **DPAPI** do Windows (por usuário); no Linux/macOS uma chave
  AES local ao lado das configurações (ofuscação, não segurança forte). Um executável **portátil** por SO.

<p align="center">
  <img src="screenshots/v3.0/edit-task.png" width="380" alt="Editar tarefa — origem em pasta com padrões de exclusão, destinos, presets">
  &nbsp;
  <img src="screenshots/v3.0/settings.png" width="380" alt="Configurações — destinos em paralelo, minimizar para bandeja, idioma, tema, atalho, updates">
</p>

## Baixar e usar
Baixe o **[Release](https://github.com/samaBR85/1clicktransfer/releases/latest)** mais recente do seu
SO — cada asset é um `.zip` com um executável **self-contained** dentro (no macOS, um `.app` com
ícone de verdade no Dock), sem runtime nem instalação:

| SO | Download | Como rodar |
|---|---|---|
| **Windows 10/11** | `1clickTransfer-win-x64.zip` | descompacte e duplo-clique em `1clickTransfer.exe`. O SmartScreen pode alertar (não é assinado): *Mais informações → Executar assim mesmo* |
| **Linux (x64)** | `1clickTransfer-linux-x64.zip` | descompacte e `chmod +x 1clickTransfer-linux-x64 && ./1clickTransfer-linux-x64` |
| **macOS (Intel)** | `1clickTransfer-osx-x64.zip` | descompacte pra obter `1clickTransfer.app`, clique com o botão direito → *Abrir* (Gatekeeper, build não assinado) |
| **macOS (Apple Silicon)** | `1clickTransfer-osx-arm64.zip` | igual ao Intel, build arm64 |

O `settings.json` é criado **ao lado do executável** (portátil — no macOS, ao lado de
`1clickTransfer.app/Contents/MacOS/1clickTransfer`). No Linux/macOS, se a pasta não for
gravável, ele usa `~/.config/1clicktransfer/settings.json`.

## Linha de comando
Dispare uma transferência sem abrir a janela — ótimo para scripts, cron e Agendador de Tarefas:

| Comando | O que faz |
|---|---|
| `1clickTransfer --task "Nome"` | envia essa tarefa (repita `--task` para várias) |
| `1clickTransfer --all` | envia todas as tarefas marcadas |
| `1clickTransfer --list` | lista as tarefas salvas |
| `1clickTransfer --silent` | sem saída no console (só o código de saída) |
| `1clickTransfer --help` | ajuda |

Sem argumentos → abre a janela normal. Códigos de saída: `0` = ok, `1` = alguma falha, `2` = erro de uso.
No macOS, o binário pra uso via CLI é `1clickTransfer.app/Contents/MacOS/1clickTransfer`.

## Rodar pelo código / compilar
Requer o **SDK do .NET 8**.
```bash
dotnet run --project src/OneClickTransfer.Avalonia        # roda pelo código-fonte
dotnet test 1clickTransfer.sln -c Release                 # roda os testes
# publica os binários single-file self-contained em dist-v3/ :
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-v3.ps1 -Rid all
```
O `build-v3.ps1` gera os binários `win-x64`, `linux-x64`, `osx-x64` e `osx-arm64` com os nomes
contratuais acima.

> **Organização.** `src/OneClickTransfer.Core` tem toda a lógica (modelos, serviços, i18n), sem UI;
> `src/OneClickTransfer.Avalonia` é a UI multiplataforma (v3). `src/OneClickTransfer` é o WPF v2
> (Windows, congelado). Os arquivos `TransferApp.ps1` e os dois `.vbs` na raiz são a versão **v1**
> original (PowerShell/VBScript) — ficam só por histórico e **não fazem parte da distribuição**.

## Licença
[MIT](LICENSE) © 2026 samaBR85.

## Créditos
UI/UX e ideias de recursos inspiradas no **[Cyberduck](https://cyberduck.io)**, o navegador
de transferência de arquivos open source. Este projeto **não compartilha código** com o
Cyberduck e é licenciado de forma independente sob MIT.
