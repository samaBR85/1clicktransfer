<p align="center">
  <img src="assets/app-icon-256.png" width="128" height="128" alt="1-Click Transfer">
</p>

<h1 align="center">1-Click Transfer &nbsp;·&nbsp; Transferência 1-Clique</h1>

<p align="center">
  Send a pre-chosen file to a pre-chosen destination (local folder or FTP) with a single click.<br>
  <em>Envie um arquivo pré-escolhido para um destino pré-escolhido (pasta local ou FTP) com um clique.</em>
</p>

<p align="center">
  <a href="https://github.com/samaBR85/1clicktransfer/releases/latest"><img src="https://img.shields.io/github/v/release/samaBR85/1clicktransfer?label=download" alt="Release"></a>
  <img src="https://img.shields.io/badge/license-MIT-blue" alt="MIT">
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4" alt="Windows 10/11">
  <img src="https://img.shields.io/badge/i18n-PT%20%2B%20EN-success" alt="PT + EN">
</p>

<p align="center">
  <a href="https://samabr85.github.io/1clicktransfer/"><b>🌐 Website</b></a> &nbsp;·&nbsp;
  <a href="#english"><b>English</b></a> &nbsp;·&nbsp;
  <a href="#português"><b>Português</b></a>
</p>

---

## English

A tiny native Windows app (PowerShell + WinForms). One big **TRANSFER** button copies a
pre-chosen file to a pre-chosen destination — a **local/network folder** or an **FTP/FTPS server**.
Modern dark/light UI, resizable window, and a **Portuguese/English** language switch.

### Features
- **1-click transfer** to a local/network folder or FTP/FTPS.
- **Profiles**: save multiple source+destination presets (incl. FTP with password) and switch from the home dropdown.
- **Action modes**: *Replace*, *Replace if newer*, *Don't replace*.
- **Source/Destination panels** listing files, with a *Refresh* button and an FTP folder browser.
- **Dark mode** (default) + light mode, resizable window, configurable keyboard shortcut.
- **Bilingual** UI (PT/EN) selectable in Settings.
- FTP password stored **encrypted** (Windows DPAPI, per user).

### Download & run
1. Grab the latest **[Release](https://github.com/samaBR85/1clicktransfer/releases/latest)** (`1clickTransfer-x.y.z.zip`).
2. Unzip and run **`1clickTransfer.exe`**. No installation needed.
3. First launch may trigger Windows **SmartScreen** (the app isn't code-signed): click
   *More info → Run anyway*. It's safe — the full source is here.

`settings.json` is created **next to the .exe** (portable).

### Run from source
Requires Windows PowerShell 5.1 (built into Windows). Double-click **`Iniciar.vbs`** (runs the
script without a console window), or run `powershell -ExecutionPolicy Bypass -File TransferApp.ps1`.

### Build the .exe
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\make-icon.ps1   # generates assets\app.ico
powershell -NoProfile -ExecutionPolicy Bypass -File tools\build-exe.ps1    # -> dist\1clickTransfer.exe (+ zip)
```
Uses [PS2EXE](https://github.com/MScholtes/PS2EXE) (installed automatically).

### License
[MIT](LICENSE) © 2026 samaBR85.

---

## Português

Um app nativo e pequeno para Windows (PowerShell + WinForms). Um botão grande **TRANSFERIR**
copia um arquivo pré-escolhido para um destino pré-escolhido — uma **pasta local/rede** ou um
**servidor FTP/FTPS**. Visual moderno escuro/claro, janela redimensionável e troca de idioma
**Português/Inglês**.

### Recursos
- **Transferência em 1 clique** para pasta local/rede ou FTP/FTPS.
- **Perfis**: salve vários conjuntos origem+destino (incl. FTP com senha) e troque pelo seletor na home.
- **Ações**: *Substituir*, *Substituir se for mais recente*, *Não Substituir*.
- **Painéis de Origem/Destino** listando arquivos, com botão *Atualizar* e navegador de pastas FTP.
- **Modo escuro** (padrão) + claro, janela redimensionável, atalho de teclado configurável.
- Interface **bilíngue** (PT/EN) selecionável no Configurar.
- Senha do FTP guardada **criptografada** (DPAPI do Windows, por usuário).

### Baixar e usar
1. Baixe o **[Release](https://github.com/samaBR85/1clicktransfer/releases/latest)** mais recente (`1clickTransfer-x.y.z.zip`).
2. Descompacte e execute **`1clickTransfer.exe`**. Não precisa instalar.
3. Na primeira execução o **SmartScreen** pode alertar (o app não é assinado): clique em
   *Mais informações → Executar assim mesmo*. É seguro — o código está todo aqui.

O `settings.json` é criado **ao lado do .exe** (portátil).

### Rodar pelo código-fonte
Requer o Windows PowerShell 5.1 (já vem no Windows). Dê dois cliques em **`Iniciar.vbs`** (roda o
script sem janela de console), ou `powershell -ExecutionPolicy Bypass -File TransferApp.ps1`.

### Compilar o .exe
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\make-icon.ps1   # gera assets\app.ico
powershell -NoProfile -ExecutionPolicy Bypass -File tools\build-exe.ps1    # -> dist\1clickTransfer.exe (+ zip)
```
Usa o [PS2EXE](https://github.com/MScholtes/PS2EXE) (instalado automaticamente).

### Licença
[MIT](LICENSE) © 2026 samaBR85.
