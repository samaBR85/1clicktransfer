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
  <a href="README.pt-BR.md"><b>🇧🇷 Ler em Português</b></a>
</p>

<p align="center">
  <a href="https://ko-fi.com/E5N7227AO5"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="ko-fi"></a>
</p>

<hr>

A **cross-platform** desktop app (**C# / .NET 8, Avalonia UI**) for **Windows, Linux and macOS**.
One big **TRANSFER** button sends your pre-chosen **file(s)** to your pre-chosen **destination(s)** —
a **local/network folder**, an **FTP/FTPS** server, or **SFTP**. Set up multiple independent
**tasks** (each with its own source and destinations), send them all with one click or **one at a
time**, or let it **watch** a file and send it automatically when it changes.

<p align="center">
  <img src="screenshots/v3.0/home.png" width="860" alt="1-Click Transfer — main window">
</p>

## Features
- **Multiple tasks** — each task is its own *source → destinations* pair. Toggle any on/off and
  transfer all the enabled ones with one click, or hit **Send this task** to fire just one.
- **Multiple source files** per task, sent to **multiple destinations**: local/network folders,
  **FTP/FTPS**, and **SFTP**. Destinations are a saved library with per-item checkboxes, reusable
  **named groups**, **presets**, and a library of **saved FTP/SFTP servers**.
- **Transfer queue** — a live panel shows what's queued, in progress, failed and succeeded, with
  per-item progress and a configurable **parallel destinations** limit.
- **Folder sources with exclude patterns** — pick a whole folder (recursive) as the source and
  exclude subfolders/files with simple `.gitignore`-style patterns.
- **Right-click on a destination** — create folder, rename, delete, or copy path, on local
  *and* FTP/SFTP destinations.
- **Watch (auto-send), per task** — when the source file changes, that task uploads automatically
  (great for build outputs).
- **System tray icon** — open the window, send all enabled tasks, or exit from the tray; optional
  **minimize to tray on close**.
- **Desktop notifications** on transfer completion or failure (Windows, Linux, macOS).
- **Command line** — run headless from a script, cron or Task Scheduler:
  `1clickTransfer --task "Name"`, `--all`, `--list`, `--silent`.
- **Auto-update** — checks GitHub Releases; on Windows it downloads and swaps itself, on Linux/macOS
  it shows what's new and opens the release page.
- **Action modes**: *Replace*, *Replace if newer*, *Don't replace*.
- **Navigable Source/Destination browsers** (incl. an FTP/SFTP folder browser), resizable columns
  and panels; the window **remembers its size and position**.
- **Dark / light** theme, **Portuguese / English** UI (switch in Settings).
- Passwords stored **encrypted** — Windows **DPAPI** (per user); Linux/macOS use a local AES key
  next to the settings (obfuscation, not strong security). Single **portable** executable per OS.

<p align="center">
  <img src="screenshots/v3.0/edit-task.png" width="380" alt="Edit task — folder source with exclude patterns, destinations, presets">
  &nbsp;
  <img src="screenshots/v3.0/settings.png" width="380" alt="Settings — parallel destinations, minimize to tray, language, theme, shortcut, updates">
</p>

## Download & run
Grab the latest **[Release](https://github.com/samaBR85/1clicktransfer/releases/latest)** for your OS —
each asset is a `.zip` containing a single **self-contained** executable (a `.app` bundle on macOS,
so it gets a proper Dock icon), no runtime or install needed:

| OS | Download | How to run |
|---|---|---|
| **Windows 10/11** | `1clickTransfer-win-x64.zip` | unzip, double-click `1clickTransfer.exe`. SmartScreen may warn (not code-signed): *More info → Run anyway* |
| **Linux (x64)** | `1clickTransfer-linux-x64.zip` | unzip, then `chmod +x 1clickTransfer-linux-x64 && ./1clickTransfer-linux-x64` |
| **macOS (Intel)** | `1clickTransfer-osx-x64.zip` | unzip to get `1clickTransfer.app`, then right-click → *Open* (Gatekeeper, unsigned build) |
| **macOS (Apple Silicon)** | `1clickTransfer-osx-arm64.zip` | same as Intel, arm64 build |

`settings.json` is created **next to the executable** (portable — on macOS, next to
`1clickTransfer.app/Contents/MacOS/1clickTransfer`). On Linux/macOS, if that folder
isn't writable it falls back to `~/.config/1clicktransfer/settings.json`.

## Command line
Run a transfer without opening the window — handy for scripts, cron and Task Scheduler:

| Command | What it does |
|---|---|
| `1clickTransfer --task "Name"` | send that task (repeat `--task` for several) |
| `1clickTransfer --all` | send all enabled tasks |
| `1clickTransfer --list` | list saved tasks |
| `1clickTransfer --silent` | no console output (exit code only) |
| `1clickTransfer --help` | help |

No arguments → opens the normal window. Exit codes: `0` = ok, `1` = some failure, `2` = usage error.
On macOS the binary for CLI use is `1clickTransfer.app/Contents/MacOS/1clickTransfer`.

## Run from source / build
Requires the **.NET 8 SDK**.
```bash
dotnet run --project src/OneClickTransfer.Avalonia        # run from source
dotnet test 1clickTransfer.sln -c Release                 # run the tests
# publish self-contained single-file binaries into dist-v3/ :
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-v3.ps1 -Rid all
```
`build-v3.ps1` produces `win-x64`, `linux-x64`, `osx-x64` and `osx-arm64` binaries with the
contractual names above.

> **Project layout.** `src/OneClickTransfer.Core` holds all the logic (models, services, i18n) with
> no UI; `src/OneClickTransfer.Avalonia` is the cross-platform UI (v3). `src/OneClickTransfer` is the
> frozen Windows-only WPF v2. The root `TransferApp.ps1` and the two `.vbs` files are the original
> **v1** (PowerShell/VBScript) — kept for history only and **not part of the distribution**.

## License
[MIT](LICENSE) © 2026 samaBR85.

## Credits
UI/UX and feature ideas inspired by **[Cyberduck](https://cyberduck.io)**, the open-source
file transfer browser. This project shares **no code** with Cyberduck and is independently
licensed under MIT.
