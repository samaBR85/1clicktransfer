# CLAUDE.md — 1-Click Transfer

## O que é
App desktop: envia arquivos pré-escolhidos a destinos (local/FTP/FTPS/SFTP) em 1 clique.
- src/OneClickTransfer.Core — Models + Services + Security + i18n (SEM UI). Toda lógica de negócio.
- src/OneClickTransfer.Avalonia — UI Avalonia 11 MVVM (v3, branch v3).
- src/OneClickTransfer — WPF v2 CONGELADO (não editar; só referencia o Core).
- TransferApp.ps1, *.vbs — legado v1 (histórico; NUNCA editar nem distribuir).

## Comandos
- Build:   dotnet build 1clickTransfer.sln -c Release
- Run v3:  dotnet run --project src/OneClickTransfer.Avalonia
- CLI:     dotnet run --project src/OneClickTransfer.Avalonia -- --list
- Testes:  dotnet test 1clickTransfer.sln -c Release
- Publish: powershell -NoProfile -ExecutionPolicy Bypass -File tools/build-v3.ps1 -Rid win-x64
           (RIDs: win-x64, linux-x64, osx-x64, osx-arm64 → dist-v3/)
- SEMPRE antes de build/publish: taskkill /IM 1clickTransfer.exe /F (ignorar erro)

## Estilo C#
- PascalCase p/ tipos/métodos/propriedades; _camelCase p/ campos privados.
- ViewModels: CommunityToolkit.Mvvm ([ObservableProperty]/[RelayCommand]); proibido INPC manual;
  lógica de negócio mora no Core, não no VM.
- Views (.axaml.cs): SÓ InitializeComponent + wiring impossível em XAML. Zero lógica.
- Bindings compilados SEMPRE: x:DataType em toda Window/DataTemplate (o projeto já força por padrão).
- ViewModels/ não pode ter `using Avalonia` (grep de revisão); UI thread só via IUiDispatcher.
- Async: UI nunca bloqueia; Core síncrono via Task.Run; proibido .Result/.Wait() na UI thread.
- Strings de UI: sempre L.T("chave") (PT+EN em I18n/L.cs). Nada hardcoded em View/VM.
- Comentários curtos em português (padrão do projeto). Arquivos UTF-8.

## Contratos imutáveis (quebrar = bug)
- settings.json: chaves camelCase atuais, JsonSerializerOptions e migrações de SettingsService.Normalize.
- CLI: --task/-t --all/-a --list/-l --silent/-s --help; exit codes 0 ok / 1 falha / 2 uso;
  --all = só tarefas marcadas; --task roda mesmo desmarcada. Não trocar por System.CommandLine.
- Releases: assets são .zip por RID (1clickTransfer-win-x64.zip / -linux-x64.zip / -osx-x64.zip /
  -osx-arm64.zip). Win/Linux: zip com o executável solto dentro (1clickTransfer.exe no Windows).
  macOS: zip com 1clickTransfer.app (Contents/MacOS/1clickTransfer + Contents/Resources/AppIcon.icns
  gerado de assets/app.icns) — sem bundle o binário solto não ganha ícone/identidade no Dock.
  Auto-update busca o asset .zip cujo nome contém "win-x64" e extrai o 1clickTransfer.exe de dentro
  dele (UpdateService.ExtractExeFromZip) — não trocar essa convenção de nome sem atualizar as duas
  pontas.
- UpdateService.Current usa GetEntryAssembly (versão vem do csproj do APP, não do Core).
- Números fixos: debounce watch 1200ms; throttle progresso 200ms/1pt; TasksHeight 140–600; SplitRatio 0.15–0.85.
- Versões lockadas: FluentFTP 52.1.0, SSH.NET 2024.2.0, ProtectedData 8.0.0; sem upgrades "de carona".

## Regras de segurança
- settings.json NUNCA no git (caminhos pessoais + senhas). Conferir git check-ignore; nunca git add -A sem revisar.
- Screenshots de app: só PrintWindow no HWND do próprio processo + checagem de PID (nunca capturar
  tela/foreground — já vazou conteúdo privado 2×; regra rígida).
- Senhas nunca em logs, testes, commits.

## Git
- Branch v3; 1 commit por etapa do plano (v3(N): resumo) + trailer
  Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>. Push de v3 ok.
- PROIBIDO tocar main, criar tag ou release sem ordem explícita do usuário.

## Armadilhas Avalonia (não alucinar API de WPF)
- DataGrid: pacote Avalonia.Controls.DataGrid + StyleInclude do tema no App.axaml, SENÃO RENDERIZA INVISÍVEL.
  Sem ElementStyle (usar DataGridTemplateColumn) e sem DataTrigger (usar Classes.xxx="{Binding ...}").
- Não existem: MessageBox (→ MessageDialog próprio via IDialogService), OpenFileDialog (→ IFilePickerService/
  StorageProvider async), DialogResult (→ Close(result); ShowDialog<T> é await), Items.Refresh()
  (→ JobItemViewModel notificável), SetResourceReference (→ Classes + styles), PasswordBox (→ TextBox
  PasswordChar), RestoreBounds (→ rastrear bounds Normal na mão), DwmSetWindowAttribute direto
  (→ helper WindowsDarkTitleBar com TryGetPlatformHandle, guard IsWindows).
- Window.Position é PixelPoint (pixels FÍSICOS — DPI!). MouseDoubleClick → DoubleTapped.
  F4/F5: AddHandler(KeyDownEvent, ..., RoutingStrategies.Tunnel). Dispatcher.UIThread, não Dispatcher.Invoke.
- Ícones funcionais: PathIcon/StreamGeometry (Segoe MDL2 e emoji não existem no Linux).
- Publish: single-file self-contained; NUNCA PublishTrimmed/AOT.
- **NUNCA escreva um `InitializeComponent` manual** (`AvaloniaXamlLoader.Load(this)`) em janelas/controles
  com `x:Name`: use o `InitializeComponent()` GERADO (só chame no ctor). O manual compila mas NÃO
  atribui os campos x:Name -> NullReferenceException em runtime (engolido por `async void`; o diálogo
  simplesmente não abre).
