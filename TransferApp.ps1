#Requires -Version 5.1
<#
    Transferencia 1-Clique  -  Windows 11
    App nativo (PowerShell + WinForms). Copia um arquivo pre-escolhido
    para uma pasta local/rede ou para um servidor FTP/FTPS.
    Visual moderno com modo escuro/claro. Nada precisa ser instalado.
#>

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName Microsoft.VisualBasic   # InputBox para nomear perfis
[System.Windows.Forms.Application]::EnableVisualStyles()

# Ativa double-buffering (via reflexao, pois a propriedade e protegida) para
# eliminar artefatos de repintura ao redimensionar grids/paineis.
function Set-DoubleBuffered($ctrl) {
    try {
        [System.Windows.Forms.Control].GetProperty('DoubleBuffered',
            [System.Reflection.BindingFlags]'Instance,NonPublic').SetValue($ctrl, $true, $null)
    } catch {}
}

# Barra de titulo escura no Windows 11 (via Reflection.Emit para NAO compilar C#,
# que seria lento no cold start por causa do antivirus escaneando o csc.exe)
$script:DwmType = $null
try {
    $an = New-Object System.Reflection.AssemblyName('DwmDynAsm')
    $ab = [AppDomain]::CurrentDomain.DefineDynamicAssembly($an, [System.Reflection.Emit.AssemblyBuilderAccess]::Run)
    $mob = $ab.DefineDynamicModule('DwmDynMod')
    $tbd = $mob.DefineType('DwmNative', 'Public, Class')
    $pin = $tbd.DefinePInvokeMethod('DwmSetWindowAttribute', 'dwmapi.dll',
        ([System.Reflection.MethodAttributes]'Public,Static'),
        [System.Reflection.CallingConventions]::Standard,
        [int], @([IntPtr], [int], [int].MakeByRefType(), [int]),
        [System.Runtime.InteropServices.CallingConvention]::Winapi,
        [System.Runtime.InteropServices.CharSet]::Auto)
    $pin.SetImplementationFlags([System.Reflection.MethodImplAttributes]::PreserveSig)
    $script:DwmType = $tbd.CreateType()
} catch { $script:DwmType = $null }

# ------------------------------------------------------------------
#  Caminhos / Configuracao
# ------------------------------------------------------------------
# Resolve a pasta do app tanto rodando como .ps1 quanto compilado como .exe (PS2EXE)
$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot }
             elseif ($MyInvocation.MyCommand.Path) { Split-Path -Parent $MyInvocation.MyCommand.Path }
             else { Split-Path -Parent ([System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName) }
$SettingsPath = Join-Path $ScriptDir 'settings.json'

function New-DefaultSettings {
    [PSCustomObject]@{
        sourceFile = ''
        destType   = 'local'
        destFolder = ''
        overwriteMode = 'always'   # 'always' = Substituir | 'ifNewer' = Substituir se for mais recente
        theme      = 'dark'
        shortcut   = 'F5'
        language   = 'pt'          # 'pt' | 'en'
        ftp        = [PSCustomObject]@{
            host = ''; port = 21; path = '/'; username = ''; password = ''; useTls = $false
        }
        profiles      = @()   # lista de perfis salvos (origem + destino nomeados)
        activeProfile = ''    # nome do perfil atualmente selecionado
    }
}

function New-FtpObject { param($src)
    [PSCustomObject]@{
        host     = if ($src) { [string]$src.host } else { '' }
        port     = if ($src -and $src.port) { [int]$src.port } else { 21 }
        path     = if ($src -and $src.path) { [string]$src.path } else { '/' }
        username = if ($src) { [string]$src.username } else { '' }
        password = if ($src) { [string]$src.password } else { '' }
        useTls   = if ($src) { [bool]$src.useTls } else { $false }
    }
}

function New-ProfileObject($name, $s) {
    [PSCustomObject]@{
        name       = [string]$name
        sourceFile = [string]$s.sourceFile
        destType   = [string]$s.destType
        destFolder = [string]$s.destFolder
        ftp        = New-FtpObject $s.ftp
    }
}

function Find-Profile($s, $name) {
    foreach ($p in $s.profiles) { if ($p.name -eq $name) { return $p } }
    return $null
}

function Apply-ProfileToCurrent($s, $prof) {
    $s.sourceFile = [string]$prof.sourceFile
    $s.destType   = [string]$prof.destType
    $s.destFolder = [string]$prof.destFolder
    $s.ftp        = New-FtpObject $prof.ftp
    $s.activeProfile = [string]$prof.name
}

function Clear-CurrentConfig($s) {
    $s.sourceFile    = ''
    $s.destType      = 'local'
    $s.destFolder    = ''
    $s.ftp           = New-FtpObject $null
    $s.activeProfile = ''
}

function Load-Settings {
    $s = New-DefaultSettings
    if (Test-Path $SettingsPath) {
        try {
            $raw = Get-Content -Path $SettingsPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($raw.sourceFile) { $s.sourceFile = $raw.sourceFile }
            if ($raw.destType)   { $s.destType   = $raw.destType }
            if ($raw.destFolder) { $s.destFolder = $raw.destFolder }
            if ($raw.overwriteMode) { $s.overwriteMode = $raw.overwriteMode }
            if ($raw.theme)      { $s.theme    = $raw.theme }
            if ($raw.shortcut)   { $s.shortcut = $raw.shortcut }
            if ($raw.language)   { $s.language = $raw.language }
            if ($raw.ftp) {
                if ($raw.ftp.host)     { $s.ftp.host     = $raw.ftp.host }
                if ($raw.ftp.port)     { $s.ftp.port     = [int]$raw.ftp.port }
                if ($raw.ftp.path)     { $s.ftp.path     = $raw.ftp.path }
                if ($raw.ftp.username) { $s.ftp.username = $raw.ftp.username }
                if ($raw.ftp.password) { $s.ftp.password = $raw.ftp.password }
                if ($null -ne $raw.ftp.useTls) { $s.ftp.useTls = [bool]$raw.ftp.useTls }
            }
            if ($raw.activeProfile) { $s.activeProfile = $raw.activeProfile }
            if ($raw.profiles) {
                $s.profiles = @(foreach ($p in $raw.profiles) {
                    [PSCustomObject]@{
                        name       = [string]$p.name
                        sourceFile = [string]$p.sourceFile
                        destType   = if ($p.destType) { [string]$p.destType } else { 'local' }
                        destFolder = [string]$p.destFolder
                        ftp        = New-FtpObject $p.ftp
                    }
                })
            }
        } catch {}
    }
    return $s
}

function Save-Settings($s) {
    $s | ConvertTo-Json -Depth 6 | Set-Content -Path $SettingsPath -Encoding UTF8
}

function Protect-Text([string]$plain) {
    if ([string]::IsNullOrEmpty($plain)) { return '' }
    ConvertFrom-SecureString -SecureString (ConvertTo-SecureString -String $plain -AsPlainText -Force)
}
function Unprotect-Text([string]$enc) {
    if ([string]::IsNullOrEmpty($enc)) { return '' }
    try {
        $sec  = ConvertTo-SecureString -String $enc
        $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec)
        try   { [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
        finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
    } catch { '' }
}

# ------------------------------------------------------------------
#  Textos / i18n (PT + EN)
# ------------------------------------------------------------------
$script:Strings = @{
    pt = @{
        appTitle='Transferência 1-Clique'; refresh='Atualizar'; lightMode='Modo claro'; darkMode='Modo escuro'
        profile='Perfil:'; noneItem='(nenhum)'; source='ORIGEM'; destination='DESTINO'
        folderPrefix='Pasta: '; ftpPrefix='FTP: '
        noFile='(nenhum arquivo - clique em Configurar)'; noDest='(nenhum destino - clique em Configurar)'
        clickRefreshFtp='  (clique em Atualizar para listar o FTP)'; loadingFtp='  Carregando lista do FTP...'
        emptyFolder='  (pasta vazia)'; cantListFtp='  (não foi possível listar o FTP)'
        colName='Nome'; colSize='Tamanho'; colModified='Modificado'
        action='Ação:'; replace='Substituir'; replaceIfNewer='Substituir se for mais recente'; dontReplace='Não Substituir'
        transfer='TRANSFERIR'; settings='Configurar'; shortcutHint='Atalho do teclado: {0}'
        clickSettingsStart='Clique em "Configurar" para começar.'; refreshing='Atualizando...'
        profileLoaded='Perfil carregado: {0}'; fieldsCleared='Campos limpos.'; settingsSaved='Configurações salvas.'
        checkingDest='Verificando destino...'; uploading='Enviando...'; copying='Copiando...'; connectingFtp='Conectando ao FTP...'
        completed='Concluído com sucesso!'; transferFailed='Falha na transferência.'
        nothingNewer='Nada a enviar: o destino já está igual ou mais novo.'
        notSentExists='Não enviado: "{0}" já existe no destino.'
        srcNotFound='Arquivo de origem não encontrado!'; srcNotFoundBox="O arquivo não existe mais:`n{0}"
        errorTitle='Erro'; transferErrorTitle='Erro na transferência'
        settingsTitle='Configurações'; sec1File='1) Arquivo a ser transferido'; browse='Procurar'
        sec2Where='2) Para onde enviar'; localFolder='Pasta local / rede'; ftpServer='Servidor FTP'
        destFolderLabel='Pasta de destino:'; ftpHost='Servidor (host):'; ftpPort='Porta:'; ftpRemote='Pasta remota:'
        ftpUser='Usuário:'; ftpPass='Senha:'; ftpTls='Usar TLS (FTPS)'; ftpSearch='Pesquisar'
        testConn='Testar conexão'; testing='Testando...'
        sec3Options='3) Opções'; shortcutLabel='Atalho do teclado para TRANSFERIR:'; themeLabel='Tema:'
        themeDark='Escuro'; themeLight='Claro'; langLabel='Idioma / Language:'; scNone='Nenhum'; scSpace='Espaço'
        profSaved='Perfis salvos:'; selectItem='(selecione)'; saveAs='Salvar atual como...'; rename='Renomear'
        delete='Excluir'; resetFields='Resetar campos'
        profHint='Selecione um perfil para carregar os campos acima; "Salvar atual como..." cria/atualiza um perfil.'
        save='Salvar'; cancel='Cancelar'; profileNamePrompt='Nome do perfil:'; saveProfileTitle='Salvar perfil'
        newNamePrompt='Novo nome:'; renameProfileTitle='Renomear perfil'; profilesTitle='Perfis'
        selectProfileWarn='Selecione um perfil na lista.'; profileSaved="Perfil '{0}' salvo."
        confirmTitle='Confirmar'; deleteProfileConfirm="Excluir o perfil '{0}'?"; warnTitle='Atenção'
        enterHostFirst='Informe o servidor (host) primeiro.'; connOkMsg='Conexão OK! O FTP respondeu corretamente.'
        successTitle='Sucesso'; failPrefix='Falha: {0}'; connErrorTitle='Erro de conexão'
        ftpBrowserTitle='Escolher pasta no FTP'; currentFolder='Pasta atual: {0}'; selectThisFolder='Selecionar esta pasta'
        dblClickEnter='Duplo-clique para entrar na pasta.'; upFolder='..  (voltar)'; listErrorPrefix='(erro ao listar: {0})'
        langPtItem='Português'; langEnItem='English'
    }
    en = @{
        appTitle='1-Click Transfer'; refresh='Refresh'; lightMode='Light mode'; darkMode='Dark mode'
        profile='Profile:'; noneItem='(none)'; source='SOURCE'; destination='DESTINATION'
        folderPrefix='Folder: '; ftpPrefix='FTP: '
        noFile='(no file - click Settings)'; noDest='(no destination - click Settings)'
        clickRefreshFtp='  (click Refresh to list the FTP)'; loadingFtp='  Loading FTP list...'
        emptyFolder='  (empty folder)'; cantListFtp='  (could not list the FTP)'
        colName='Name'; colSize='Size'; colModified='Modified'
        action='Action:'; replace='Replace'; replaceIfNewer='Replace if newer'; dontReplace="Don't replace"
        transfer='TRANSFER'; settings='Settings'; shortcutHint='Keyboard shortcut: {0}'
        clickSettingsStart='Click "Settings" to start.'; refreshing='Refreshing...'
        profileLoaded='Profile loaded: {0}'; fieldsCleared='Fields cleared.'; settingsSaved='Settings saved.'
        checkingDest='Checking destination...'; uploading='Uploading...'; copying='Copying...'; connectingFtp='Connecting to FTP...'
        completed='Completed successfully!'; transferFailed='Transfer failed.'
        nothingNewer='Nothing to send: the destination is already the same or newer.'
        notSentExists='Not sent: "{0}" already exists at the destination.'
        srcNotFound='Source file not found!'; srcNotFoundBox="The file no longer exists:`n{0}"
        errorTitle='Error'; transferErrorTitle='Transfer error'
        settingsTitle='Settings'; sec1File='1) File to transfer'; browse='Browse'
        sec2Where='2) Where to send'; localFolder='Local / network folder'; ftpServer='FTP server'
        destFolderLabel='Destination folder:'; ftpHost='Server (host):'; ftpPort='Port:'; ftpRemote='Remote folder:'
        ftpUser='Username:'; ftpPass='Password:'; ftpTls='Use TLS (FTPS)'; ftpSearch='Browse'
        testConn='Test connection'; testing='Testing...'
        sec3Options='3) Options'; shortcutLabel='Keyboard shortcut for TRANSFER:'; themeLabel='Theme:'
        themeDark='Dark'; themeLight='Light'; langLabel='Idioma / Language:'; scNone='None'; scSpace='Space'
        profSaved='Saved profiles:'; selectItem='(select)'; saveAs='Save current as...'; rename='Rename'
        delete='Delete'; resetFields='Reset fields'
        profHint='Select a profile to load the fields above; "Save current as..." creates/updates a profile.'
        save='Save'; cancel='Cancel'; profileNamePrompt='Profile name:'; saveProfileTitle='Save profile'
        newNamePrompt='New name:'; renameProfileTitle='Rename profile'; profilesTitle='Profiles'
        selectProfileWarn='Select a profile from the list.'; profileSaved="Profile '{0}' saved."
        confirmTitle='Confirm'; deleteProfileConfirm="Delete profile '{0}'?"; warnTitle='Attention'
        enterHostFirst='Enter the server (host) first.'; connOkMsg='Connection OK! The FTP responded correctly.'
        successTitle='Success'; failPrefix='Failed: {0}'; connErrorTitle='Connection error'
        ftpBrowserTitle='Choose FTP folder'; currentFolder='Current folder: {0}'; selectThisFolder='Select this folder'
        dblClickEnter='Double-click to enter the folder.'; upFolder='..  (up)'; listErrorPrefix='(list error: {0})'
        langPtItem='Português'; langEnItem='English'
    }
}

function T($k) {
    $lang = if ($script:settings -and $script:settings.language) { $script:settings.language } else { 'pt' }
    $tbl = $script:Strings[$lang]; if (-not $tbl) { $tbl = $script:Strings['pt'] }
    $v = $tbl[$k]; if ($null -eq $v) { $v = $script:Strings['pt'][$k] }
    if ($null -eq $v) { $k } else { $v }
}

# ------------------------------------------------------------------
#  Paletas de cor / Tema
# ------------------------------------------------------------------
function C($r,$g,$b) { [System.Drawing.Color]::FromArgb($r,$g,$b) }

function Get-Palette($theme) {
    if ($theme -eq 'light') {
        [PSCustomObject]@{
            Bg=(C 245 245 247); Card=(C 255 255 255); Input=(C 255 255 255)
            Flat=(C 240 240 243); FlatHover=(C 228 228 233); Border=(C 222 222 228)
            Text=(C 24 24 27); SubText=(C 113 113 122)
            Accent=(C 37 99 235); AccentHover=(C 59 130 246); AccentText=(C 255 255 255)
            Success=(C 22 163 74); Error=(C 220 38 38)
            GridHeader=(C 243 244 246); Selection=(C 219 234 254); Dark=0
        }
    } else {
        [PSCustomObject]@{
            Bg=(C 24 24 27); Card=(C 39 39 42); Input=(C 32 32 36)
            Flat=(C 45 45 50); FlatHover=(C 60 60 66); Border=(C 63 63 70)
            Text=(C 244 244 245); SubText=(C 161 161 170)
            Accent=(C 37 99 235); AccentHover=(C 59 130 246); AccentText=(C 255 255 255)
            Success=(C 74 222 128); Error=(C 248 113 113)
            GridHeader=(C 32 32 36); Selection=(C 55 65 81); Dark=1
        }
    }
}

function Set-DwmDark($form, $dark) {
    if (-not $script:DwmType) { return }
    try {
        $v = [int]$dark
        $script:DwmType::DwmSetWindowAttribute($form.Handle, 20, [ref]$v, 4) | Out-Null
    } catch {}
}

function Set-Rounded($ctrl, $radius) {
    $gp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $w = $ctrl.Width; $h = $ctrl.Height
    $gp.AddArc(0, 0, $d, $d, 180, 90)
    $gp.AddArc($w - $d, 0, $d, $d, 270, 90)
    $gp.AddArc($w - $d, $h - $d, $d, $d, 0, 90)
    $gp.AddArc(0, $h - $d, $d, $d, 90, 90)
    $gp.CloseAllFigures()
    $ctrl.Region = New-Object System.Drawing.Region($gp)
}

# ------------------------------------------------------------------
#  Utilidades de arquivo / FTP
# ------------------------------------------------------------------
$script:PtBr = [System.Globalization.CultureInfo]::GetCultureInfo('pt-BR')
function Format-Size([long]$b) {
    # Mostra a contagem exata de bytes com separador de milhar (ex.: 2.056.819)
    ([long]$b).ToString('#,0', $script:PtBr)
}

function Get-FtpUri($h, $port, $path, [switch]$Dir) {
    $clean = ($path -replace '\\','/').Trim()
    $segs  = @($clean.Split('/') | Where-Object { $_ -ne '' } | ForEach-Object { [Uri]::EscapeDataString($_) })
    $enc   = '/' + ($segs -join '/')
    if ($Dir -and -not $enc.EndsWith('/')) { $enc += '/' }
    "ftp://${h}:${port}${enc}"
}

function Parse-FtpLine($ln) {
    if ([string]::IsNullOrWhiteSpace($ln)) { return $null }
    # IIS / Windows: MM-dd-yy hh:mmAM <DIR>|size name
    if ($ln -match '^\d{2}-\d{2}-\d{2}\s+\d{2}:\d{2}(AM|PM)\s+') {
        $t = $ln -split '\s+', 4
        if ($t.Count -lt 4) { return $null }
        $isDir = $t[2] -eq '<DIR>'
        $size = 0; if (-not $isDir) { [long]::TryParse($t[2], [ref]$size) | Out-Null }
        return [PSCustomObject]@{ Name=$t[3]; IsDir=$isDir; Size=$size; Date="$($t[0]) $($t[1])" }
    }
    # Unix: perms links owner group size Mon dd time/year name
    if ($ln -match '^[dl-][rwxsStT-]{9}') {
        $t = $ln -split '\s+', 9
        if ($t.Count -lt 9) { return $null }
        $isDir = $ln[0] -eq 'd'
        $size = 0; [long]::TryParse($t[4], [ref]$size) | Out-Null
        $name = $t[8]
        if ($name -like '* -> *') { $name = $name.Substring(0, $name.IndexOf(' -> ')) }
        if ($name -eq '.' -or $name -eq '..') { return $null }
        return [PSCustomObject]@{ Name=$name; IsDir=$isDir; Size=$(if($isDir){0}else{$size}); Date="$($t[5]) $($t[6]) $($t[7])" }
    }
    return $null
}

function Get-FtpList($h, $port, $path, $user, $pass, $tls) {
    $uri = Get-FtpUri $h $port $path -Dir
    $req = [System.Net.FtpWebRequest]::Create($uri)
    $req.Method = [System.Net.WebRequestMethods+Ftp]::ListDirectoryDetails
    $req.Credentials = New-Object System.Net.NetworkCredential($user, $pass)
    $req.Proxy = $null   # evita auto-deteccao de proxy (WPAD), que trava segundos
    $req.EnableSsl = [bool]$tls; $req.UsePassive = $true; $req.KeepAlive = $false; $req.Timeout = 8000
    $resp = $req.GetResponse()
    $sr = New-Object System.IO.StreamReader($resp.GetResponseStream())
    $lines = @()
    while (-not $sr.EndOfStream) { $lines += $sr.ReadLine() }
    $sr.Close(); $resp.Close()
    $out = @()
    foreach ($l in $lines) { $p = Parse-FtpLine $l; if ($p) { $out += $p } }
    ,($out | Sort-Object @{e={ -not $_.IsDir }}, Name)
}

# ------------------------------------------------------------------
#  Transferencia
# ------------------------------------------------------------------
function Set-Progress($pct) {
    if ($pct -lt 0) { $pct = 0 }; if ($pct -gt 100) { $pct = 100 }
    $script:progPct = $pct
    $script:progFill.Width  = [int]($script:progBg.ClientSize.Width * $pct / 100)
    $script:progFill.Height = $script:progBg.ClientSize.Height
}

function Invoke-LocalCopy {
    $src = $script:settings.sourceFile
    $dstFolder = $script:settings.destFolder
    if (-not (Test-Path -LiteralPath $dstFolder)) { New-Item -ItemType Directory -Path $dstFolder -Force | Out-Null }
    $dst = Join-Path $dstFolder ([System.IO.Path]::GetFileName($src))
    Copy-Item -LiteralPath $src -Destination $dst -Force
    Set-Progress 100
}

# Retorna as infos do arquivo ja existente no destino (ou $null se nao existir).
# Usa a LISTAGEM do diretorio (confiavel), nao o MDTM (que muitos servidores nao respondem).
function Get-ExistingDest {
    $s = $script:settings
    $fileName = [System.IO.Path]::GetFileName($s.sourceFile)
    if ($s.destType -eq 'ftp') {
        try {
            $list = Get-FtpList $s.ftp.host $s.ftp.port $s.ftp.path $s.ftp.username (Unprotect-Text $s.ftp.password) $s.ftp.useTls
            $e = $list | Where-Object { -not $_.IsDir -and $_.Name -eq $fileName } | Select-Object -First 1
            if ($e) { return [PSCustomObject]@{ Size = [long]$e.Size; DateStr = $e.Date } }
        } catch {}
        return $null
    } else {
        $dst = Join-Path $s.destFolder $fileName
        if (Test-Path -LiteralPath $dst) {
            $it = Get-Item -LiteralPath $dst
            return [PSCustomObject]@{ Size = [long]$it.Length; DateStr = $it.LastWriteTime.ToString('dd/MM/yyyy HH:mm') }
        }
        return $null
    }
}

# $true = origem mais nova; $false = destino igual/mais novo; $null = nao deu pra saber a data
function Test-SourceNewer {
    $s = $script:settings
    $fileName = [System.IO.Path]::GetFileName($s.sourceFile)
    if ($s.destType -eq 'ftp') {
        $rt = Get-FtpFileTime $s.ftp (($s.ftp.path.TrimEnd('/')) + '/' + $fileName)
        if ($null -eq $rt) { return $null }
        return ((Get-Item -LiteralPath $s.sourceFile).LastWriteTime -gt $rt)
    } else {
        $dst = Join-Path $s.destFolder $fileName
        if (-not (Test-Path -LiteralPath $dst)) { return $true }
        $srcT = (Get-Item -LiteralPath $s.sourceFile).LastWriteTimeUtc
        $dstT = (Get-Item -LiteralPath $dst).LastWriteTimeUtc
        return ($srcT -gt $dstT)
    }
}

function Get-FtpFileTime($ftp, $remotePath) {
    try {
        $uri = Get-FtpUri $ftp.host $ftp.port $remotePath
        $req = [System.Net.FtpWebRequest]::Create($uri)
        $req.Method = [System.Net.WebRequestMethods+Ftp]::GetDateTimestamp
        $req.Proxy = $null
        $req.EnableSsl = [bool]$ftp.useTls; $req.UsePassive = $true; $req.KeepAlive = $false; $req.Timeout = 8000
        $req.Credentials = New-Object System.Net.NetworkCredential($ftp.username, (Unprotect-Text $ftp.password))
        $resp = $req.GetResponse(); $t = $resp.LastModified; $resp.Close()
        return $t
    } catch { return $null }
}

function Invoke-FtpUpload {
    $ftp = $script:settings.ftp
    $src = $script:settings.sourceFile
    $fileName = [System.IO.Path]::GetFileName($src)
    $remote = ($ftp.path.TrimEnd('/')) + '/' + $fileName
    $uri = Get-FtpUri $ftp.host $ftp.port $remote
    $req = [System.Net.FtpWebRequest]::Create($uri)
    $req.Method = [System.Net.WebRequestMethods+Ftp]::UploadFile
    $req.UseBinary = $true; $req.UsePassive = $true; $req.KeepAlive = $false
    $req.EnableSsl = [bool]$ftp.useTls
    $req.Proxy = $null   # evita auto-deteccao de proxy (WPAD), que trava segundos
    $req.Credentials = New-Object System.Net.NetworkCredential($ftp.username, (Unprotect-Text $ftp.password))

    $fs = [System.IO.File]::OpenRead($src)
    try {
        $req.ContentLength = $fs.Length
        $total = $fs.Length
        $script:lblStatus.Text = (T 'connectingFtp'); $script:form.Refresh()
        $rs = $req.GetRequestStream()
        try {
            $buffer = New-Object byte[] 65536; $sent = 0
            $script:lblStatus.Text = (T 'uploading')
            while (($read = $fs.Read($buffer, 0, $buffer.Length)) -gt 0) {
                $rs.Write($buffer, 0, $read); $sent += $read
                if ($total -gt 0) { Set-Progress ([int](($sent / $total) * 100)) }
                [System.Windows.Forms.Application]::DoEvents()
            }
        } finally { $rs.Close() }
    } finally { $fs.Close() }
    $resp = $req.GetResponse(); $resp.Close()
    Set-Progress 100
}

function Test-FtpConnection($f) {
    $uri = Get-FtpUri $f.host $f.port $f.path -Dir
    $req = [System.Net.FtpWebRequest]::Create($uri)
    $req.Method = [System.Net.WebRequestMethods+Ftp]::ListDirectory
    $req.Proxy = $null   # evita auto-deteccao de proxy (WPAD), que trava segundos
    $req.UsePassive = $true; $req.KeepAlive = $false; $req.EnableSsl = [bool]$f.useTls; $req.Timeout = 10000
    $req.Credentials = New-Object System.Net.NetworkCredential($f.username, $f.password)
    $resp = $req.GetResponse(); $resp.Close()
}

# ------------------------------------------------------------------
#  Grids da tela inicial
# ------------------------------------------------------------------
function Fill-GridLocal($g, $folder, $highlight) {
    $g.Rows.Clear()
    if (-not $folder -or -not (Test-Path -LiteralPath $folder)) { return }
    $items = Get-ChildItem -LiteralPath $folder -Force -ErrorAction SilentlyContinue |
             Sort-Object @{e={ -not $_.PSIsContainer }}, Name
    foreach ($it in $items) {
        $isDir = $it.PSIsContainer
        $name  = if ($isDir) { "  " + $it.Name } else { "  " + $it.Name }
        $size  = if ($isDir) { '' } else { Format-Size $it.Length }
        $mod   = $it.LastWriteTime.ToString('dd/MM/yyyy HH:mm')
        $idx = $g.Rows.Add($name, $size, $mod)
        if ($highlight -and $it.Name -eq $highlight) {
            $g.Rows[$idx].DefaultCellStyle.BackColor          = $script:P.Accent
            $g.Rows[$idx].DefaultCellStyle.ForeColor          = $script:P.AccentText
            $g.Rows[$idx].DefaultCellStyle.SelectionBackColor = $script:P.Accent
            $g.Rows[$idx].DefaultCellStyle.SelectionForeColor = $script:P.AccentText
            $g.Rows[$idx].DefaultCellStyle.Font = New-Object System.Drawing.Font('Segoe UI', 9, [System.Drawing.FontStyle]::Bold)
        }
    }
}

function Fill-GridFtp($g, $s) {
    $g.Rows.Clear()
    try {
        $list = Get-FtpList $s.ftp.host $s.ftp.port $s.ftp.path $s.ftp.username (Unprotect-Text $s.ftp.password) $s.ftp.useTls
        foreach ($e in $list) {
            $size = if ($e.IsDir) { '' } else { Format-Size $e.Size }
            $g.Rows.Add(("  " + $e.Name), $size, $e.Date) | Out-Null
        }
        if ($list.Count -eq 0) { $g.Rows.Add((T 'emptyFolder'), "", "") | Out-Null }
    } catch {
        $g.Rows.Add((T 'cantListFtp'), "", "") | Out-Null
    }
}

# ------------------------------------------------------------------
#  Tema (aplicacao recursiva por Tag)
# ------------------------------------------------------------------
function Style-Grid($g, $p) {
    $g.BackgroundColor = $p.Card
    $g.GridColor = $p.Border
    $g.DefaultCellStyle.BackColor = $p.Card
    $g.DefaultCellStyle.ForeColor = $p.Text
    $g.DefaultCellStyle.SelectionBackColor = $p.Selection
    $g.DefaultCellStyle.SelectionForeColor = $p.Text
    $g.ColumnHeadersDefaultCellStyle.BackColor = $p.GridHeader
    $g.ColumnHeadersDefaultCellStyle.ForeColor = $p.SubText
    $g.EnableHeadersVisualStyles = $false
}

function Apply-Theme($container) {
    $p = $script:P
    foreach ($c in $container.Controls) {
        switch ("$($c.Tag)") {
            'card'     { $c.BackColor = $p.Card }
            'title'    { $c.ForeColor = $p.Text;    $c.BackColor = [System.Drawing.Color]::Transparent }
            'sub'      { $c.ForeColor = $p.SubText; $c.BackColor = [System.Drawing.Color]::Transparent }
            'text'     { $c.ForeColor = $p.Text;    $c.BackColor = [System.Drawing.Color]::Transparent }
            'check'    { $c.ForeColor = $p.Text;    $c.BackColor = [System.Drawing.Color]::Transparent }
            'input'    { $c.BackColor = $p.Input;   $c.ForeColor = $p.Text }
            'accent'   { $c.BackColor = $p.Accent;  $c.ForeColor = $p.AccentText; $c.FlatAppearance.BorderSize = 0 }
            'flat'     { $c.BackColor = $p.Flat;    $c.ForeColor = $p.Text; $c.FlatAppearance.BorderColor = $p.Border }
            'grid'     { Style-Grid $c $p }
            'progbg'   { $c.BackColor = $p.Input }
            'progfill' { $c.BackColor = $p.Accent }
            default    { }
        }
        if ($c.Controls.Count -gt 0) { Apply-Theme $c }
    }
}

function Style-InputBox($t)  { $t.BorderStyle = 'FixedSingle'; $t.Tag = 'input' }
function Style-FlatBtn($b, $p) {
    $b.FlatStyle = 'Flat'; $b.Tag = 'flat'; $b.FlatAppearance.BorderSize = 1; $b.Cursor = 'Hand'
    $b.Add_MouseEnter({ $this.BackColor = $script:P.FlatHover })
    $b.Add_MouseLeave({ $this.BackColor = $script:P.Flat })
}

# ------------------------------------------------------------------
#  Navegador de pastas FTP
# ------------------------------------------------------------------
function Show-FtpBrowser($h, $port, $startPath, $user, $pass, $tls) {
    $state = @{ cur = $(if ($startPath) { $startPath } else { '/' }); entries = @() }

    $dlg = New-Object System.Windows.Forms.Form
    $dlg.Text = (T 'ftpBrowserTitle')
    $dlg.ClientSize = New-Object System.Drawing.Size(460, 420)
    $dlg.StartPosition = 'CenterParent'; $dlg.FormBorderStyle = 'FixedDialog'
    $dlg.MaximizeBox = $false; $dlg.MinimizeBox = $false
    $dlg.Font = New-Object System.Drawing.Font('Segoe UI', 9)
    $dlg.BackColor = $script:P.Bg

    $lblCur = New-Object System.Windows.Forms.Label
    $lblCur.Location = '15,12'; $lblCur.Size = '430,20'; $lblCur.Tag = 'sub'
    $dlg.Controls.Add($lblCur)

    $lb = New-Object System.Windows.Forms.ListBox
    $lb.Location = '15,40'; $lb.Size = '430,300'
    $lb.BackColor = $script:P.Input; $lb.ForeColor = $script:P.Text; $lb.BorderStyle = 'FixedSingle'
    $lb.Font = New-Object System.Drawing.Font('Segoe UI', 10)
    $dlg.Controls.Add($lb)

    $lblHint = New-Object System.Windows.Forms.Label
    $lblHint.Location = '15,345'; $lblHint.Size = '430,18'; $lblHint.Tag = 'sub'
    $lblHint.Text = (T 'dblClickEnter')
    $dlg.Controls.Add($lblHint)

    $btnSel = New-Object System.Windows.Forms.Button
    $btnSel.Text = (T 'selectThisFolder'); $btnSel.Location = '15,375'; $btnSel.Size = '220,32'
    $btnSel.Tag = 'accent'; $btnSel.FlatStyle = 'Flat'; $btnSel.Cursor = 'Hand'
    $dlg.Controls.Add($btnSel)

    $btnCan = New-Object System.Windows.Forms.Button
    $btnCan.Text = (T 'cancel'); $btnCan.Location = '345,375'; $btnCan.Size = '100,32'
    Style-FlatBtn $btnCan $script:P
    $dlg.Controls.Add($btnCan)

    $loadDir = {
        $lblCur.Text = ((T 'currentFolder') -f $state.cur)
        $lb.Items.Clear()
        $state.entries = @()
        if ($state.cur.TrimEnd('/') -ne '') {
            $lb.Items.Add((T 'upFolder')) | Out-Null
            $state.entries += @{ Up = $true }
        }
        try {
            $list = Get-FtpList $h $port $state.cur $user $pass $tls
            foreach ($e in $list) {
                if ($e.IsDir) {
                    $lb.Items.Add('[ ] ' + $e.Name) | Out-Null
                    $state.entries += @{ Up = $false; Name = $e.Name }
                }
            }
        } catch {
            $lb.Items.Add(((T 'listErrorPrefix') -f $_.Exception.Message)) | Out-Null
            $state.entries += @{ Up = $false; Name = $null }
        }
    }

    $lb.Add_DoubleClick({
        $i = $lb.SelectedIndex
        if ($i -lt 0 -or $i -ge $state.entries.Count) { return }
        $en = $state.entries[$i]
        if ($en.Up) {
            $c = $state.cur.TrimEnd('/')
            $state.cur = $(if ($c.LastIndexOf('/') -le 0) { '/' } else { $c.Substring(0, $c.LastIndexOf('/')) })
        } elseif ($en.Name) {
            $state.cur = ($state.cur.TrimEnd('/')) + '/' + $en.Name
        }
        & $loadDir
    })

    $btnSel.Add_Click({ $dlg.Tag = $state.cur; $dlg.DialogResult = 'OK'; $dlg.Close() })
    $btnCan.Add_Click({ $dlg.DialogResult = 'Cancel'; $dlg.Close() })

    & $loadDir
    Apply-Theme $dlg
    if ($script:P.Dark) { $dlg.Add_Shown({ Set-DwmDark $dlg $true }) }

    if ($dlg.ShowDialog() -eq 'OK') { return $dlg.Tag } else { return $null }
}

# ------------------------------------------------------------------
#  Janela de Configuracao
# ------------------------------------------------------------------
function Show-SettingsDialog($settings) {
    $dlg = New-Object System.Windows.Forms.Form
    $dlg.Text = (T 'settingsTitle')
    $dlg.ClientSize = New-Object System.Drawing.Size(560, 690)
    $dlg.StartPosition = 'CenterParent'; $dlg.FormBorderStyle = 'FixedDialog'
    $dlg.MaximizeBox = $false; $dlg.MinimizeBox = $false
    $dlg.Font = New-Object System.Drawing.Font('Segoe UI', 9)
    $dlg.BackColor = $script:P.Bg

    $mkLabel = {
        param($text, $x, $y, $w, $tag)
        $l = New-Object System.Windows.Forms.Label
        $l.Text = $text; $l.Location = New-Object System.Drawing.Point($x, $y)
        $l.Size = New-Object System.Drawing.Size($w, 20); $l.Tag = $tag
        $dlg.Controls.Add($l); $l
    }

    # 1) Arquivo
    & $mkLabel (T 'sec1File') 20 18 400 'text'
    $txtSrc = New-Object System.Windows.Forms.TextBox
    $txtSrc.Location = '20,42'; $txtSrc.Size = '410,25'; $txtSrc.Text = $settings.sourceFile
    Style-InputBox $txtSrc; $dlg.Controls.Add($txtSrc)
    $btnSrc = New-Object System.Windows.Forms.Button
    $btnSrc.Text = (T 'browse'); $btnSrc.Location = '440,41'; $btnSrc.Size = '100,27'
    Style-FlatBtn $btnSrc $script:P; $dlg.Controls.Add($btnSrc)
    $btnSrc.Add_Click({
        $ofd = New-Object System.Windows.Forms.OpenFileDialog
        if ($txtSrc.Text -and (Test-Path -LiteralPath $txtSrc.Text)) { $ofd.InitialDirectory = Split-Path -Parent $txtSrc.Text }
        if ($ofd.ShowDialog() -eq 'OK') { $txtSrc.Text = $ofd.FileName }
    })

    # 2) Destino
    & $mkLabel (T 'sec2Where') 20 82 400 'text'
    $rbLocal = New-Object System.Windows.Forms.RadioButton
    $rbLocal.Text = (T 'localFolder'); $rbLocal.Location = '30,106'; $rbLocal.AutoSize = $true
    $rbLocal.Tag = 'check'; $rbLocal.Checked = ($settings.destType -ne 'ftp'); $dlg.Controls.Add($rbLocal)
    $rbFtp = New-Object System.Windows.Forms.RadioButton
    $rbFtp.Text = (T 'ftpServer'); $rbFtp.Location = '220,106'; $rbFtp.AutoSize = $true
    $rbFtp.Tag = 'check'; $rbFtp.Checked = ($settings.destType -eq 'ftp'); $dlg.Controls.Add($rbFtp)

    # Painel Local
    $pLocal = New-Object System.Windows.Forms.Panel
    $pLocal.Location = '20,136'; $pLocal.Size = '520,60'; $pLocal.Tag = 'card'
    $pLocal.Add_Paint({ param($s,$e) $pen = New-Object System.Drawing.Pen($script:P.Border,1); $e.Graphics.DrawRectangle($pen, 0,0,$s.Width-1,$s.Height-1); $pen.Dispose() })
    $dlg.Controls.Add($pLocal)
    $lblLoc = New-Object System.Windows.Forms.Label
    $lblLoc.Text = (T 'destFolderLabel'); $lblLoc.Location = '12,10'; $lblLoc.Size = '160,20'; $lblLoc.Tag = 'sub'
    $pLocal.Controls.Add($lblLoc)
    $txtDst = New-Object System.Windows.Forms.TextBox
    $txtDst.Location = '12,30'; $txtDst.Size = '385,25'; $txtDst.Text = $settings.destFolder
    Style-InputBox $txtDst; $pLocal.Controls.Add($txtDst)
    $btnDst = New-Object System.Windows.Forms.Button
    $btnDst.Text = (T 'browse'); $btnDst.Location = '407,29'; $btnDst.Size = '100,27'
    Style-FlatBtn $btnDst $script:P; $pLocal.Controls.Add($btnDst)
    $btnDst.Add_Click({
        $fbd = New-Object System.Windows.Forms.FolderBrowserDialog
        if ($txtDst.Text) { $fbd.SelectedPath = $txtDst.Text }
        if ($fbd.ShowDialog() -eq 'OK') { $txtDst.Text = $fbd.SelectedPath }
    })

    # Painel FTP
    $pFtp = New-Object System.Windows.Forms.Panel
    $pFtp.Location = '20,206'; $pFtp.Size = '520,190'; $pFtp.Tag = 'card'
    $pFtp.Add_Paint({ param($s,$e) $pen = New-Object System.Drawing.Pen($script:P.Border,1); $e.Graphics.DrawRectangle($pen, 0,0,$s.Width-1,$s.Height-1); $pen.Dispose() })
    $dlg.Controls.Add($pFtp)

    $fl = {
        param($text, $x, $y)
        $l = New-Object System.Windows.Forms.Label
        $l.Text = $text; $l.Location = New-Object System.Drawing.Point($x,$y); $l.AutoSize = $true; $l.Tag = 'sub'
        $pFtp.Controls.Add($l)
    }
    & $fl (T 'ftpHost') 12 16
    $txtHost = New-Object System.Windows.Forms.TextBox; $txtHost.Location = '120,13'; $txtHost.Size = '230,25'; $txtHost.Text = $settings.ftp.host; Style-InputBox $txtHost; $pFtp.Controls.Add($txtHost)
    & $fl (T 'ftpPort') 365 16
    $txtPort = New-Object System.Windows.Forms.TextBox; $txtPort.Location = '410,13'; $txtPort.Size = '95,25'; $txtPort.Text = [string]$settings.ftp.port; Style-InputBox $txtPort; $pFtp.Controls.Add($txtPort)
    & $fl (T 'ftpRemote') 12 48
    $txtPath = New-Object System.Windows.Forms.TextBox; $txtPath.Location = '120,45'; $txtPath.Size = '280,25'; $txtPath.Text = $settings.ftp.path; Style-InputBox $txtPath; $pFtp.Controls.Add($txtPath)
    $btnBrowse = New-Object System.Windows.Forms.Button; $btnBrowse.Text = (T 'ftpSearch'); $btnBrowse.Location = '408,44'; $btnBrowse.Size = '97,27'; Style-FlatBtn $btnBrowse $script:P; $pFtp.Controls.Add($btnBrowse)
    & $fl (T 'ftpUser') 12 80
    $txtUser = New-Object System.Windows.Forms.TextBox; $txtUser.Location = '120,77'; $txtUser.Size = '230,25'; $txtUser.Text = $settings.ftp.username; Style-InputBox $txtUser; $pFtp.Controls.Add($txtUser)
    & $fl (T 'ftpPass') 12 112
    $txtPass = New-Object System.Windows.Forms.TextBox; $txtPass.Location = '120,109'; $txtPass.Size = '230,25'; $txtPass.UseSystemPasswordChar = $true; $txtPass.Text = (Unprotect-Text $settings.ftp.password); Style-InputBox $txtPass; $pFtp.Controls.Add($txtPass)
    $chkTls = New-Object System.Windows.Forms.CheckBox; $chkTls.Text = (T 'ftpTls'); $chkTls.Location = '365,80'; $chkTls.AutoSize = $true; $chkTls.Tag = 'check'; $chkTls.Checked = [bool]$settings.ftp.useTls; $pFtp.Controls.Add($chkTls)
    $btnTest = New-Object System.Windows.Forms.Button; $btnTest.Text = (T 'testConn'); $btnTest.Location = '365,107'; $btnTest.Size = '140,29'; Style-FlatBtn $btnTest $script:P; $pFtp.Controls.Add($btnTest)

    $btnBrowse.Add_Click({
        if ([string]::IsNullOrWhiteSpace($txtHost.Text)) {
            [System.Windows.Forms.MessageBox]::Show((T 'enterHostFirst'), (T 'warnTitle'), 'OK', 'Warning') | Out-Null; return
        }
        $port = 21; [void][int]::TryParse($txtPort.Text, [ref]$port)
        $chosen = Show-FtpBrowser $txtHost.Text $port $txtPath.Text $txtUser.Text $txtPass.Text $chkTls.Checked
        if ($chosen) { $txtPath.Text = $chosen }
    })
    $btnTest.Add_Click({
        try {
            $btnTest.Enabled = $false; $btnTest.Text = (T 'testing'); $dlg.Refresh()
            $port = 21; [void][int]::TryParse($txtPort.Text, [ref]$port)
            Test-FtpConnection ([PSCustomObject]@{ host=$txtHost.Text; port=$port; path=$txtPath.Text; username=$txtUser.Text; password=$txtPass.Text; useTls=$chkTls.Checked })
            [System.Windows.Forms.MessageBox]::Show((T 'connOkMsg'), (T 'successTitle'), 'OK', 'Information') | Out-Null
        } catch {
            [System.Windows.Forms.MessageBox]::Show(((T 'failPrefix') -f $_.Exception.Message), (T 'connErrorTitle'), 'OK', 'Error') | Out-Null
        } finally { $btnTest.Enabled = $true; $btnTest.Text = (T 'testConn') }
    })

    # 3) Opcoes
    & $mkLabel (T 'sec3Options') 20 406 400 'text'
    & $mkLabel (T 'shortcutLabel') 30 434 260 'sub'
    $cmbKey = New-Object System.Windows.Forms.ComboBox
    $cmbKey.Location = '300,431'; $cmbKey.Size = '120,25'; $cmbKey.DropDownStyle = 'DropDownList'; $cmbKey.Tag = 'input'
    @((T 'scNone'),'F2','F3','F4','F5','F6','F7','F8','F9','F10','F11','F12',(T 'scSpace'),'Ctrl+Enter','Ctrl+T') | ForEach-Object { [void]$cmbKey.Items.Add($_) }
    $cmbKey.SelectedItem = $(if ($settings.shortcut -eq 'Nenhum') { (T 'scNone') } elseif ($settings.shortcut -eq 'Space') { (T 'scSpace') } elseif ($cmbKey.Items.Contains($settings.shortcut)) { $settings.shortcut } else { 'F5' })
    $dlg.Controls.Add($cmbKey)

    & $mkLabel (T 'themeLabel') 30 470 55 'sub'
    $cmbTheme = New-Object System.Windows.Forms.ComboBox
    $cmbTheme.Location = '88,467'; $cmbTheme.Size = '110,25'; $cmbTheme.DropDownStyle = 'DropDownList'; $cmbTheme.Tag = 'input'
    [void]$cmbTheme.Items.Add((T 'themeDark')); [void]$cmbTheme.Items.Add((T 'themeLight'))
    $cmbTheme.SelectedItem = $(if ($settings.theme -eq 'light') { (T 'themeLight') } else { (T 'themeDark') })
    $dlg.Controls.Add($cmbTheme)

    & $mkLabel (T 'langLabel') 210 470 140 'sub'
    $cmbLang = New-Object System.Windows.Forms.ComboBox
    $cmbLang.Location = '352,467'; $cmbLang.Size = '155,25'; $cmbLang.DropDownStyle = 'DropDownList'; $cmbLang.Tag = 'input'
    [void]$cmbLang.Items.Add((T 'langPtItem')); [void]$cmbLang.Items.Add((T 'langEnItem'))
    $cmbLang.SelectedItem = $(if ($settings.language -eq 'en') { (T 'langEnItem') } else { (T 'langPtItem') })
    $dlg.Controls.Add($cmbLang)

    # Habilita paineis conforme tipo
    $upd = {
        $isFtp = $rbFtp.Checked
        foreach ($c in $pLocal.Controls) { $c.Enabled = -not $isFtp }
        foreach ($c in $pFtp.Controls)   { $c.Enabled = $isFtp }
    }
    $rbLocal.Add_CheckedChanged($upd); $rbFtp.Add_CheckedChanged($upd)

    # Ler/escrever os campos do dialogo como um objeto (para perfis)
    $readFields = {
        $p = 21; [void][int]::TryParse($txtPort.Text, [ref]$p)
        [PSCustomObject]@{
            sourceFile = $txtSrc.Text
            destType   = if ($rbFtp.Checked) { 'ftp' } else { 'local' }
            destFolder = $txtDst.Text
            ftp = [PSCustomObject]@{
                host=$txtHost.Text; port=$p; path=$txtPath.Text
                username=$txtUser.Text; password=(Protect-Text $txtPass.Text); useTls=$chkTls.Checked
            }
        }
    }
    $loadFields = {
        param($prof)
        $txtSrc.Text  = [string]$prof.sourceFile
        if ($prof.destType -eq 'ftp') { $rbFtp.Checked = $true } else { $rbLocal.Checked = $true }
        $txtDst.Text  = [string]$prof.destFolder
        $txtHost.Text = [string]$prof.ftp.host
        $txtPort.Text = [string]$prof.ftp.port
        $txtPath.Text = [string]$prof.ftp.path
        $txtUser.Text = [string]$prof.ftp.username
        $txtPass.Text = (Unprotect-Text $prof.ftp.password)
        $chkTls.Checked = [bool]$prof.ftp.useTls
        & $upd
    }

    # 4) Perfis salvos + Resetar
    $pProf = New-Object System.Windows.Forms.Panel
    $pProf.Location = '20,506'; $pProf.Size = '520,120'; $pProf.Tag = 'card'
    $pProf.Add_Paint({ param($s,$e) $pen = New-Object System.Drawing.Pen($script:P.Border,1); $e.Graphics.DrawRectangle($pen,0,0,$s.Width-1,$s.Height-1); $pen.Dispose() })
    $dlg.Controls.Add($pProf)
    $lblProf = New-Object System.Windows.Forms.Label
    $lblProf.Text = (T 'profSaved'); $lblProf.Location = '12,14'; $lblProf.AutoSize = $true; $lblProf.Tag = 'sub'
    $pProf.Controls.Add($lblProf)
    $cmbProfiles = New-Object System.Windows.Forms.ComboBox
    $cmbProfiles.Location = '110,11'; $cmbProfiles.Size = '240,25'; $cmbProfiles.DropDownStyle = 'DropDownList'; $cmbProfiles.Tag = 'input'
    $pProf.Controls.Add($cmbProfiles)
    $btnProfSave = New-Object System.Windows.Forms.Button
    $btnProfSave.Text = (T 'saveAs'); $btnProfSave.Location = '362,10'; $btnProfSave.Size = '146,27'; Style-FlatBtn $btnProfSave $script:P
    $pProf.Controls.Add($btnProfSave)
    $btnProfRename = New-Object System.Windows.Forms.Button
    $btnProfRename.Text = (T 'rename'); $btnProfRename.Location = '110,46'; $btnProfRename.Size = '110,27'; Style-FlatBtn $btnProfRename $script:P
    $pProf.Controls.Add($btnProfRename)
    $btnProfDelete = New-Object System.Windows.Forms.Button
    $btnProfDelete.Text = (T 'delete'); $btnProfDelete.Location = '228,46'; $btnProfDelete.Size = '110,27'; Style-FlatBtn $btnProfDelete $script:P
    $pProf.Controls.Add($btnProfDelete)
    $btnReset = New-Object System.Windows.Forms.Button
    $btnReset.Text = (T 'resetFields'); $btnReset.Location = '362,46'; $btnReset.Size = '146,27'; Style-FlatBtn $btnReset $script:P
    $pProf.Controls.Add($btnReset)
    $lblProfHint = New-Object System.Windows.Forms.Label
    $lblProfHint.Text = (T 'profHint')
    $lblProfHint.Location = '12,86'; $lblProfHint.AutoSize = $true; $lblProfHint.Tag = 'sub'
    $pProf.Controls.Add($lblProfHint)

    $reloadProfiles = {
        $cmbProfiles.Items.Clear()
        [void]$cmbProfiles.Items.Add((T 'selectItem'))
        foreach ($p in $settings.profiles) { [void]$cmbProfiles.Items.Add($p.name) }
        $cmbProfiles.SelectedIndex = 0
    }
    & $reloadProfiles
    $cmbProfiles.Add_SelectedIndexChanged({
        if ($cmbProfiles.SelectedIndex -le 0) { return }   # 0 = "(selecione)"
        $prof = Find-Profile $settings ([string]$cmbProfiles.SelectedItem)
        if ($prof) { & $loadFields $prof }
    })
    $btnProfSave.Add_Click({
        $suggest = if ($cmbProfiles.SelectedIndex -gt 0) { [string]$cmbProfiles.SelectedItem } else { '' }
        $name = [Microsoft.VisualBasic.Interaction]::InputBox((T 'profileNamePrompt'), (T 'saveProfileTitle'), $suggest)
        if ([string]::IsNullOrWhiteSpace($name)) { return }
        $name = $name.Trim()
        $prof = New-ProfileObject $name (& $readFields)
        if (Find-Profile $settings $name) {
            $settings.profiles = @($settings.profiles | ForEach-Object { if ($_.name -eq $name) { $prof } else { $_ } })
        } else {
            $settings.profiles = @($settings.profiles) + $prof
        }
        Save-Settings $settings
        & $reloadProfiles
        $cmbProfiles.SelectedItem = $name
        [System.Windows.Forms.MessageBox]::Show(((T 'profileSaved') -f $name), (T 'profilesTitle'), 'OK', 'Information') | Out-Null
    })
    $btnProfRename.Add_Click({
        if ($cmbProfiles.SelectedIndex -le 0) { [System.Windows.Forms.MessageBox]::Show((T 'selectProfileWarn'), (T 'profilesTitle'), 'OK', 'Warning') | Out-Null; return }
        $name = [string]$cmbProfiles.SelectedItem
        $new = [Microsoft.VisualBasic.Interaction]::InputBox((T 'newNamePrompt'), (T 'renameProfileTitle'), $name)
        if ([string]::IsNullOrWhiteSpace($new)) { return }
        $new = $new.Trim()
        $prof = Find-Profile $settings $name
        if ($prof) {
            $prof.name = $new
            if ($settings.activeProfile -eq $name) { $settings.activeProfile = $new }
            Save-Settings $settings; & $reloadProfiles; $cmbProfiles.SelectedItem = $new
        }
    })
    $btnProfDelete.Add_Click({
        if ($cmbProfiles.SelectedIndex -le 0) { [System.Windows.Forms.MessageBox]::Show((T 'selectProfileWarn'), (T 'profilesTitle'), 'OK', 'Warning') | Out-Null; return }
        $name = [string]$cmbProfiles.SelectedItem
        if ([System.Windows.Forms.MessageBox]::Show(((T 'deleteProfileConfirm') -f $name), (T 'confirmTitle'), 'YesNo', 'Question') -ne 'Yes') { return }
        $settings.profiles = @($settings.profiles | Where-Object { $_.name -ne $name })
        if ($settings.activeProfile -eq $name) { $settings.activeProfile = '' }
        Save-Settings $settings; & $reloadProfiles
    })
    $btnReset.Add_Click({
        $txtSrc.Text = ''; $rbLocal.Checked = $true; $txtDst.Text = ''
        $txtHost.Text = ''; $txtPort.Text = '21'; $txtPath.Text = '/'; $txtUser.Text = ''; $txtPass.Text = ''; $chkTls.Checked = $false
        & $upd
    })

    # Botoes
    $btnOk = New-Object System.Windows.Forms.Button
    $btnOk.Text = (T 'save'); $btnOk.Location = '340,645'; $btnOk.Size = '100,34'; $btnOk.Tag = 'accent'; $btnOk.FlatStyle = 'Flat'; $btnOk.Cursor = 'Hand'
    $dlg.Controls.Add($btnOk)
    $btnCancel = New-Object System.Windows.Forms.Button
    $btnCancel.Text = (T 'cancel'); $btnCancel.Location = '450,645'; $btnCancel.Size = '90,34'; Style-FlatBtn $btnCancel $script:P
    $dlg.Controls.Add($btnCancel)

    $btnOk.Add_Click({
        # Sem validacao obrigatoria: pode salvar limpo/parcial. O botao TRANSFERIR
        # so habilita quando origem + destino estao preenchidos (Update-ReadyState).
        $port = 21; [void][int]::TryParse($txtPort.Text, [ref]$port)

        $settings.sourceFile = $txtSrc.Text
        $settings.destType   = if ($rbFtp.Checked) { 'ftp' } else { 'local' }
        $settings.destFolder = $txtDst.Text
        $settings.shortcut   = if ($cmbKey.SelectedIndex -eq 0) { 'Nenhum' } elseif ($cmbKey.SelectedItem -eq (T 'scSpace')) { 'Space' } else { [string]$cmbKey.SelectedItem }
        $settings.theme      = if ($cmbTheme.SelectedItem -eq (T 'themeLight')) { 'light' } else { 'dark' }
        $settings.language   = if ($cmbLang.SelectedItem -eq (T 'langEnItem')) { 'en' } else { 'pt' }
        $settings.ftp.host     = $txtHost.Text
        $settings.ftp.port     = $port
        $settings.ftp.path     = $txtPath.Text
        $settings.ftp.username = $txtUser.Text
        $settings.ftp.password = (Protect-Text $txtPass.Text)
        $settings.ftp.useTls   = $chkTls.Checked
        $settings.activeProfile = ''   # config ativa editada = "custom" (desvincula do perfil)
        Save-Settings $settings
        $dlg.DialogResult = 'OK'; $dlg.Close()
    })
    $btnCancel.Add_Click({ $dlg.DialogResult = 'Cancel'; $dlg.Close() })

    Apply-Theme $dlg
    & $upd
    if ($script:P.Dark) { $dlg.Add_Shown({ Set-DwmDark $dlg $true }) }
    return $dlg.ShowDialog()
}

# ------------------------------------------------------------------
#  Componentes reutilizaveis da Home
# ------------------------------------------------------------------
function New-FileGrid {
    $g = New-Object System.Windows.Forms.DataGridView
    $g.Tag = 'grid'; $g.ColumnCount = 3
    $g.Columns[0].Name = 'Nome'; $g.Columns[0].HeaderText = (T 'colName')
    $g.Columns[1].Name = 'Tam';  $g.Columns[1].HeaderText = (T 'colSize')
    $g.Columns[2].Name = 'Mod';  $g.Columns[2].HeaderText = (T 'colModified')
    $g.Columns[0].AutoSizeMode = 'Fill'
    $g.Columns[1].Width = 100; $g.Columns[1].DefaultCellStyle.Alignment = 'MiddleRight'
    $g.Columns[2].Width = 115; $g.Columns[2].DefaultCellStyle.Alignment = 'MiddleRight'
    $g.ReadOnly = $true; $g.AllowUserToAddRows = $false; $g.AllowUserToDeleteRows = $false
    $g.RowHeadersVisible = $false; $g.MultiSelect = $false; $g.SelectionMode = 'FullRowSelect'
    $g.BorderStyle = 'None'; $g.CellBorderStyle = 'SingleHorizontal'
    $g.AllowUserToResizeRows = $false; $g.ColumnHeadersHeightSizeMode = 'DisableResizing'
    $g.ColumnHeadersHeight = 30; $g.RowTemplate.Height = 26
    $g.ScrollBars = 'Vertical'
    Set-DoubleBuffered $g
    return $g
}

# ------------------------------------------------------------------
#  Janela Principal
# ------------------------------------------------------------------
$script:settings = Load-Settings
$script:P = Get-Palette $script:settings.theme
$script:ftpTimer = $null
$script:progPct = 0

$form = New-Object System.Windows.Forms.Form
$script:form = $form
$form.Text = 'Transferencia 1-Clique'
$form.ClientSize = New-Object System.Drawing.Size(880, 640)
$form.StartPosition = 'CenterScreen'; $form.FormBorderStyle = 'Sizable'
$form.MaximizeBox = $true
$form.MinimumSize = New-Object System.Drawing.Size(740, 600)
$form.Font = New-Object System.Drawing.Font('Segoe UI', 9)
$form.BackColor = $script:P.Bg
$form.KeyPreview = $true
Set-DoubleBuffered $form

# Icone da janela / barra de tarefas.
# No .exe compilado: extrai o icone embutido no proprio exe.
# Rodando como .ps1: usa assets\app.ico ao lado do script.
try {
    $exePath = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
    if ($exePath -and $exePath -notlike '*powershell*' -and (Test-Path -LiteralPath $exePath)) {
        $form.Icon = [System.Drawing.Icon]::ExtractAssociatedIcon($exePath)
    } else {
        $icoFile = Join-Path $ScriptDir 'assets\app.ico'
        if (Test-Path -LiteralPath $icoFile) { $form.Icon = New-Object System.Drawing.Icon($icoFile) }
    }
} catch {}

# --- Top bar ---
$lblTitle = New-Object System.Windows.Forms.Label
$lblTitle.Text = 'Transferencia 1-Clique'
$lblTitle.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 15)
$lblTitle.Location = '24,18'; $lblTitle.AutoSize = $true; $lblTitle.Tag = 'title'
$form.Controls.Add($lblTitle)

$btnTheme = New-Object System.Windows.Forms.Button
$btnTheme.Location = '686,18'; $btnTheme.Size = '150,32'; Style-FlatBtn $btnTheme $script:P
$form.Controls.Add($btnTheme)

$btnRefresh = New-Object System.Windows.Forms.Button
$btnRefresh.Text = 'Atualizar'; $btnRefresh.Location = '556,18'; $btnRefresh.Size = '120,32'; Style-FlatBtn $btnRefresh $script:P
$form.Controls.Add($btnRefresh)

# --- Seletor de perfil ---
$lblProfile = New-Object System.Windows.Forms.Label
$lblProfile.Text = 'Perfil:'; $lblProfile.AutoSize = $true; $lblProfile.Tag = 'sub'
$form.Controls.Add($lblProfile)
$cmbProfile = New-Object System.Windows.Forms.ComboBox
$cmbProfile.DropDownStyle = 'DropDownList'; $cmbProfile.Tag = 'input'
$cmbProfile.Font = New-Object System.Drawing.Font('Segoe UI', 9)
$form.Controls.Add($cmbProfile)
$script:profSync = $false

# --- Card Origem ---
$cardSrc = New-Object System.Windows.Forms.Panel
$cardSrc.Location = '20,62'; $cardSrc.Size = '400,300'; $cardSrc.Tag = 'card'
$cardSrc.Add_Paint({ param($s,$e) $pen = New-Object System.Drawing.Pen($script:P.Border,1); $e.Graphics.DrawRectangle($pen, 0,0,$s.Width-1,$s.Height-1); $pen.Dispose() })
Set-DoubleBuffered $cardSrc
$form.Controls.Add($cardSrc)
$lblSrcHdr = New-Object System.Windows.Forms.Label
$lblSrcHdr.Text = 'ORIGEM'; $lblSrcHdr.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 10)
$lblSrcHdr.Location = '14,10'; $lblSrcHdr.AutoSize = $true; $lblSrcHdr.Tag = 'title'
$cardSrc.Controls.Add($lblSrcHdr)
$lblSrcPath = New-Object System.Windows.Forms.Label
$lblSrcPath.Location = '14,34'; $lblSrcPath.Size = '372,32'; $lblSrcPath.Tag = 'sub'; $lblSrcPath.Anchor = 'Top,Left,Right'
$cardSrc.Controls.Add($lblSrcPath)
$script:gridSrc = New-FileGrid
$script:gridSrc.Location = '12,70'; $script:gridSrc.Size = '376,218'; $script:gridSrc.Anchor = 'Top,Bottom,Left,Right'
$cardSrc.Controls.Add($script:gridSrc)

# --- Card Destino ---
$cardDst = New-Object System.Windows.Forms.Panel
$cardDst.Location = '440,62'; $cardDst.Size = '400,300'; $cardDst.Tag = 'card'
$cardDst.Add_Paint({ param($s,$e) $pen = New-Object System.Drawing.Pen($script:P.Border,1); $e.Graphics.DrawRectangle($pen, 0,0,$s.Width-1,$s.Height-1); $pen.Dispose() })
Set-DoubleBuffered $cardDst
$form.Controls.Add($cardDst)
$lblDstHdr = New-Object System.Windows.Forms.Label
$lblDstHdr.Text = 'DESTINO'; $lblDstHdr.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 10)
$lblDstHdr.Location = '14,10'; $lblDstHdr.AutoSize = $true; $lblDstHdr.Tag = 'title'
$cardDst.Controls.Add($lblDstHdr)
$lblDstPath = New-Object System.Windows.Forms.Label
$lblDstPath.Location = '14,34'; $lblDstPath.Size = '372,32'; $lblDstPath.Tag = 'sub'; $lblDstPath.Anchor = 'Top,Left,Right'
$cardDst.Controls.Add($lblDstPath)
$script:gridDst = New-FileGrid
$script:gridDst.Location = '12,70'; $script:gridDst.Size = '376,218'; $script:gridDst.Anchor = 'Top,Bottom,Left,Right'
$cardDst.Controls.Add($script:gridDst)

# --- Botao TRANSFERIR ---
$btnGo = New-Object System.Windows.Forms.Button
$btnGo.Text = 'TRANSFERIR'; $btnGo.Font = New-Object System.Drawing.Font('Segoe UI', 17, [System.Drawing.FontStyle]::Bold)
$btnGo.Location = '20,378'; $btnGo.Size = '820,74'; $btnGo.Tag = 'accent'; $btnGo.FlatStyle = 'Flat'; $btnGo.Cursor = 'Hand'
$form.Controls.Add($btnGo)
Set-Rounded $btnGo 16
$btnGo.Add_MouseEnter({ if ($this.Enabled) { $this.BackColor = $script:P.AccentHover } })
$btnGo.Add_MouseLeave({ if ($this.Enabled) { $this.BackColor = $script:P.Accent } })

# --- Barra de progresso custom ---
$script:progBg = New-Object System.Windows.Forms.Panel
$script:progBg.Location = '20,468'; $script:progBg.Size = '820,8'; $script:progBg.Tag = 'progbg'
$form.Controls.Add($script:progBg)
$script:progFill = New-Object System.Windows.Forms.Panel
$script:progFill.Location = '0,0'; $script:progFill.Size = '0,8'; $script:progFill.Tag = 'progfill'
$script:progBg.Controls.Add($script:progFill)

# --- Status ---
$script:lblStatus = New-Object System.Windows.Forms.Label
$script:lblStatus.Location = '20,486'; $script:lblStatus.Size = '820,28'; $script:lblStatus.TextAlign = 'MiddleCenter'
$script:lblStatus.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 10); $script:lblStatus.Tag = 'sub'
$form.Controls.Add($script:lblStatus)

# --- Rodape ---
$lblHint = New-Object System.Windows.Forms.Label
$lblHint.Location = '24,556'; $lblHint.Size = '500,20'; $lblHint.Tag = 'sub'
$form.Controls.Add($lblHint)
$btnCfg = New-Object System.Windows.Forms.Button
$btnCfg.Text = 'Configurar'; $btnCfg.Location = '710,548'; $btnCfg.Size = '130,34'; Style-FlatBtn $btnCfg $script:P
$form.Controls.Add($btnCfg)

# --- Acao (o que fazer se ja existir no destino) ---
$lblAction = New-Object System.Windows.Forms.Label
$lblAction.Text = "A$([char]0xE7)$([char]0xE3)o:"; $lblAction.AutoSize = $true; $lblAction.Tag = 'sub'   # "Acao:"
$form.Controls.Add($lblAction)
$rbAlways = New-Object System.Windows.Forms.RadioButton
$rbAlways.Text = 'Substituir'; $rbAlways.AutoSize = $true; $rbAlways.Tag = 'check'
$form.Controls.Add($rbAlways)
$rbIfNewer = New-Object System.Windows.Forms.RadioButton
$rbIfNewer.Text = 'Substituir se for mais recente'; $rbIfNewer.AutoSize = $true; $rbIfNewer.Tag = 'check'
$form.Controls.Add($rbIfNewer)
$rbNever = New-Object System.Windows.Forms.RadioButton
$rbNever.Text = "N$([char]0xE3)o Substituir"; $rbNever.AutoSize = $true; $rbNever.Tag = 'check'   # "Nao Substituir"
$form.Controls.Add($rbNever)
$rbAlways.Checked  = ($script:settings.overwriteMode -eq 'always')
$rbIfNewer.Checked = ($script:settings.overwriteMode -eq 'ifNewer')
$rbNever.Checked   = ($script:settings.overwriteMode -eq 'never')
if (-not ($rbAlways.Checked -or $rbIfNewer.Checked -or $rbNever.Checked)) { $rbAlways.Checked = $true }
$script:syncing = $false
$saveMode = {
    if ($script:syncing) { return }
    $script:settings.overwriteMode = if ($rbIfNewer.Checked) { 'ifNewer' } elseif ($rbNever.Checked) { 'never' } else { 'always' }
    Save-Settings $script:settings
}
$rbAlways.Add_CheckedChanged($saveMode)
$rbIfNewer.Add_CheckedChanged($saveMode)
$rbNever.Add_CheckedChanged($saveMode)

# ------------------------------------------------------------------
#  Logica da Home
# ------------------------------------------------------------------
function Layout-Home {
    $W = $form.ClientSize.Width
    $H = $form.ClientSize.Height
    $M = 20; $G = 20
    # Barra superior
    $btnTheme.Location   = New-Object System.Drawing.Point(($W - 24 - $btnTheme.Width), 16)
    $btnRefresh.Location = New-Object System.Drawing.Point(($btnTheme.Left - 10 - $btnRefresh.Width), 16)
    # Linha do seletor de perfil
    $lblProfile.Location = New-Object System.Drawing.Point(($M + 2), 60)
    $cmbW = [Math]::Min(360, ($W - 2*$M - 58))
    $cmbProfile.SetBounds(($M + 58), 56, $cmbW, 25)
    # De baixo para cima
    $footerY = $H - 18 - 34
    $btnCfg.Location  = New-Object System.Drawing.Point(($W - 24 - $btnCfg.Width), $footerY)
    $lblHint.SetBounds(24, ($footerY + 7), [Math]::Max(100, ($W - 48 - $btnCfg.Width)), 20)
    $statusY = $footerY - 8 - 26
    $script:lblStatus.SetBounds($M, $statusY, ($W - 2*$M), 26)
    $progY = $statusY - 8 - 8
    $script:progBg.SetBounds($M, $progY, ($W - 2*$M), 8)
    $goY = $progY - 12 - 72
    $btnGo.SetBounds($M, $goY, ($W - 2*$M), 72)
    Set-Rounded $btnGo 16
    $actionY = $goY - 12 - 22
    $lblAction.Location  = New-Object System.Drawing.Point(($M + 2), ($actionY + 2))
    $rbAlways.Location   = New-Object System.Drawing.Point(($M + 52), $actionY)
    $rbIfNewer.Location  = New-Object System.Drawing.Point(($M + 160), $actionY)
    $rbNever.Location    = New-Object System.Drawing.Point(($M + 375), $actionY)
    # Cards preenchem o espaco restante
    $cardsTop = 90
    $cardH = ($actionY - 12) - $cardsTop
    if ($cardH -lt 120) { $cardH = 120 }
    $innerW = $W - 2*$M - $G
    $leftW = [int]($innerW / 2); $rightW = $innerW - $leftW
    $cardSrc.SetBounds($M, $cardsTop, $leftW, $cardH)
    $cardDst.SetBounds(($M + $leftW + $G), $cardsTop, $rightW, $cardH)
    $cardSrc.Invalidate($true); $cardDst.Invalidate($true)
    Set-Progress $script:progPct
}

function Start-FtpFillDeferred {
    # Deixa a janela pintar/responder primeiro; a lista do FTP chega logo depois
    if ($script:ftpTimer) { try { $script:ftpTimer.Stop(); $script:ftpTimer.Dispose() } catch {} }
    $script:ftpTimer = New-Object System.Windows.Forms.Timer
    $script:ftpTimer.Interval = 40
    $script:ftpTimer.Add_Tick({
        $script:ftpTimer.Stop()
        Fill-GridFtp $script:gridDst $script:settings
    })
    $script:ftpTimer.Start()
}

function Refresh-Home([switch]$FetchFtp) {
    $s = $script:settings
    # Origem
    if ($s.sourceFile) {
        $lblSrcPath.Text = (T 'folderPrefix') + (Split-Path -Parent $s.sourceFile)
        Fill-GridLocal $script:gridSrc (Split-Path -Parent $s.sourceFile) (Split-Path -Leaf $s.sourceFile)
    } else {
        $lblSrcPath.Text = (T 'noFile')
        $script:gridSrc.Rows.Clear()
    }
    # Destino
    if ($s.destType -eq 'ftp') {
        $lblDstPath.Text = (T 'ftpPrefix') + $s.ftp.host + $s.ftp.path
        $script:gridDst.Rows.Clear()
        if ($FetchFtp) {
            $script:gridDst.Rows.Add((T 'loadingFtp'), "", "") | Out-Null
            Start-FtpFillDeferred
        } else {
            $script:gridDst.Rows.Add((T 'clickRefreshFtp'), "", "") | Out-Null
        }
    } elseif ($s.destFolder) {
        $lblDstPath.Text = (T 'folderPrefix') + $s.destFolder
        Fill-GridLocal $script:gridDst $s.destFolder ''
    } else {
        $lblDstPath.Text = (T 'noDest')
        $script:gridDst.Rows.Clear()
    }
}

function Update-ReadyState {
    $s = $script:settings
    $ok = $s.sourceFile -and (($s.destType -eq 'ftp' -and $s.ftp.host) -or ($s.destType -eq 'local' -and $s.destFolder))
    $btnGo.Enabled = [bool]$ok
    $btnGo.BackColor = if ($ok) { $script:P.Accent } else { $script:P.Border }
    $btnGo.ForeColor = if ($ok) { $script:P.AccentText } else { $script:P.SubText }
    $lblHint.Text = if ($s.shortcut -and $s.shortcut -ne 'Nenhum') { (T 'shortcutHint') -f $(if ($s.shortcut -eq 'Space') { (T 'scSpace') } else { $s.shortcut }) } else { '' }
    $btnTheme.Text = if ($s.theme -eq 'light') { (T 'darkMode') } else { (T 'lightMode') }
    $script:syncing = $true
    $rbAlways.Checked  = ($s.overwriteMode -eq 'always')
    $rbIfNewer.Checked = ($s.overwriteMode -eq 'ifNewer')
    $rbNever.Checked   = ($s.overwriteMode -eq 'never')
    $script:syncing = $false
}

# Reaplica os textos (i18n) dos controles persistentes da janela principal.
function Apply-Language {
    $form.Text        = (T 'appTitle')
    $lblTitle.Text    = (T 'appTitle')
    $btnRefresh.Text  = (T 'refresh')
    $lblProfile.Text  = (T 'profile')
    $lblSrcHdr.Text   = (T 'source')
    $lblDstHdr.Text   = (T 'destination')
    $btnGo.Text       = (T 'transfer')
    $btnCfg.Text      = (T 'settings')
    $lblAction.Text   = (T 'action')
    $rbAlways.Text    = (T 'replace')
    $rbIfNewer.Text   = (T 'replaceIfNewer')
    $rbNever.Text     = (T 'dontReplace')
    foreach ($g in @($script:gridSrc, $script:gridDst)) {
        $g.Columns[0].HeaderText = (T 'colName')
        $g.Columns[1].HeaderText = (T 'colSize')
        $g.Columns[2].HeaderText = (T 'colModified')
    }
}

function Sync-ProfileCombo {
    $script:profSync = $true
    $cmbProfile.Items.Clear()
    [void]$cmbProfile.Items.Add((T 'noneItem'))
    foreach ($p in $script:settings.profiles) { [void]$cmbProfile.Items.Add($p.name) }
    $sel = $script:settings.activeProfile
    if ($sel -and $cmbProfile.Items.Contains($sel)) { $cmbProfile.SelectedItem = $sel }
    else { $cmbProfile.SelectedIndex = 0 }
    $script:profSync = $false
}

$script:doTransfer = {
    if (-not $btnGo.Enabled) { return }
    if (-not (Test-Path -LiteralPath $script:settings.sourceFile)) {
        $script:lblStatus.ForeColor = $script:P.Error
        $script:lblStatus.Text = (T 'srcNotFound')
        [System.Windows.Forms.MessageBox]::Show(((T 'srcNotFoundBox') -f $script:settings.sourceFile), (T 'errorTitle'), 'OK', 'Error') | Out-Null
        return
    }
    try {
        $btnGo.Enabled = $false; $btnCfg.Enabled = $false; $btnRefresh.Enabled = $false
        Set-Progress 0; $script:lblStatus.ForeColor = $script:P.SubText
        $fileName = [System.IO.Path]::GetFileName($script:settings.sourceFile)

        # Substituir = envia direto. "Nao Substituir" e "se for mais recente"
        # checam o destino primeiro (sem modal).
        $mode = $script:settings.overwriteMode
        if ($mode -ne 'always') {
            $script:lblStatus.Text = (T 'checkingDest'); $script:form.Refresh()
            if (Get-ExistingDest) {
                if ($mode -eq 'never') {
                    $script:lblStatus.Text = ((T 'notSentExists') -f $fileName)
                    return
                }
                if ($mode -eq 'ifNewer' -and (Test-SourceNewer) -eq $false) {
                    $script:lblStatus.Text = (T 'nothingNewer')
                    return
                }
            }
        }

        # Transfere
        if ($script:settings.destType -eq 'ftp') { $script:lblStatus.Text = (T 'uploading'); $script:form.Refresh(); Invoke-FtpUpload }
        else { $script:lblStatus.Text = (T 'copying'); $script:form.Refresh(); Invoke-LocalCopy }
        Set-Progress 100
        $script:lblStatus.ForeColor = $script:P.Success
        $script:lblStatus.Text = (T 'completed')
        Refresh-Home -FetchFtp
    } catch {
        Set-Progress 0
        $script:lblStatus.ForeColor = $script:P.Error
        $script:lblStatus.Text = (T 'transferFailed')
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, (T 'transferErrorTitle'), 'OK', 'Error') | Out-Null
    } finally {
        $btnCfg.Enabled = $true; $btnRefresh.Enabled = $true; Update-ReadyState
    }
}

function Apply-AllTheme {
    $script:P = Get-Palette $script:settings.theme
    $form.BackColor = $script:P.Bg
    Apply-Theme $form
    Apply-Language
    Update-ReadyState
    Sync-ProfileCombo
    Refresh-Home
    Layout-Home
    $form.Refresh()
    Set-DwmDark $form ($script:settings.theme -ne 'light')
}

$btnGo.Add_Click($script:doTransfer)
$form.Add_KeyDown({
    param($s, $e)
    $sc = $script:settings.shortcut
    if (-not $sc -or $sc -eq 'Nenhum') { return }
    $hit = $false
    if ($sc.StartsWith('Ctrl+')) {
        if ($e.Control) {
            $k = $sc.Substring(5)
            if ($k -eq 'Enter') { $hit = $e.KeyCode -eq 'Return' } else { $hit = ($e.KeyCode.ToString() -eq $k) }
        }
    } elseif (-not $e.Control) {
        if ($sc -eq 'Space') { $hit = $e.KeyCode -eq 'Space' } else { $hit = ($e.KeyCode.ToString() -eq $sc) }
    }
    if ($hit) { $e.SuppressKeyPress = $true; & $script:doTransfer }
})

$btnRefresh.Add_Click({
    $script:lblStatus.ForeColor = $script:P.SubText; $script:lblStatus.Text = (T 'refreshing'); $form.Refresh()
    Refresh-Home -FetchFtp
    $script:lblStatus.Text = ''
})

$cmbProfile.Add_SelectedIndexChanged({
    if ($script:profSync) { return }
    if ($cmbProfile.SelectedIndex -le 0) {   # 0 = "(nenhum)"
        Clear-CurrentConfig $script:settings
        Save-Settings $script:settings
        Update-ReadyState
        Refresh-Home
        $script:lblStatus.ForeColor = $script:P.SubText
        $script:lblStatus.Text = (T 'fieldsCleared')
        return
    }
    $name = [string]$cmbProfile.SelectedItem
    $prof = Find-Profile $script:settings $name
    if (-not $prof) { return }
    Apply-ProfileToCurrent $script:settings $prof
    Save-Settings $script:settings
    Update-ReadyState
    Refresh-Home
    $script:lblStatus.ForeColor = $script:P.SubText
    $script:lblStatus.Text = ((T 'profileLoaded') -f $name)
})

$btnTheme.Add_Click({
    # Trocar tema apenas recolore (nao re-busca o FTP), para ser instantaneo
    $script:settings.theme = if ($script:settings.theme -eq 'light') { 'dark' } else { 'light' }
    Save-Settings $script:settings
    $script:P = Get-Palette $script:settings.theme
    $form.BackColor = $script:P.Bg
    Apply-Theme $form
    Update-ReadyState
    $form.Refresh()
    Set-DwmDark $form ($script:settings.theme -ne 'light')
})

$btnCfg.Add_Click({
    $res = Show-SettingsDialog $script:settings
    if ($res -eq 'OK') {
        $script:settings = Load-Settings
        Apply-AllTheme
        $script:lblStatus.ForeColor = $script:P.SubText; $script:lblStatus.Text = (T 'settingsSaved')
    } else {
        # Perfis podem ter mudado mesmo sem salvar a config ativa
        Sync-ProfileCombo
    }
})

# --- Inicializacao ---
Apply-Theme $form
Apply-Language
Update-ReadyState
Sync-ProfileCombo
Layout-Home
$form.Add_Resize({ Layout-Home })
$form.Add_Shown({
    Set-DwmDark $form ($script:settings.theme -ne 'light')
    Layout-Home
    Refresh-Home
    if (-not $script:settings.sourceFile) {
        $script:lblStatus.ForeColor = $script:P.SubText
        $script:lblStatus.Text = (T 'clickSettingsStart')
    }
})

[void]$form.ShowDialog()
