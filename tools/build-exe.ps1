# Compila o TransferApp.ps1 em dist\1clickTransfer.exe usando PS2EXE
# e gera o zip de distribuicao. Uso:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\build-exe.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$ps1  = Join-Path $root 'TransferApp.ps1'
$ico  = Join-Path $root 'assets\app.ico'
$dist = Join-Path $root 'dist'
$exe  = Join-Path $dist '1clickTransfer.exe'
$ver  = '1.0.0'

if (-not (Test-Path $dist)) { New-Item -ItemType Directory -Path $dist -Force | Out-Null }

# --- Garante o PS2EXE ---
if (-not (Get-Command Invoke-ps2exe -ErrorAction SilentlyContinue)) {
    Write-Output "PS2EXE nao encontrado. Instalando..."
    $ok = $false
    try {
        Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope CurrentUser -ErrorAction Stop | Out-Null
        try { Set-PSRepository -Name PSGallery -InstallationPolicy Trusted -ErrorAction Stop } catch {}
        Install-Module -Name ps2exe -Scope CurrentUser -Force -AllowClobber -ErrorAction Stop
        Import-Module ps2exe -Force
        $ok = $true
        Write-Output "PS2EXE instalado via PSGallery."
    } catch {
        Write-Output "Install-Module falhou ($($_.Exception.Message)). Baixando o script do PS2EXE..."
    }
    if (-not $ok) {
        $tmp = Join-Path $env:TEMP 'ps2exe.ps1'
        Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/MScholtes/PS2EXE/master/Module/ps2exe.ps1' -OutFile $tmp -UseBasicParsing
        . $tmp
        Write-Output "PS2EXE carregado do script baixado."
    }
}

# --- Compila ---
Write-Output "Compilando o .exe..."
Invoke-ps2exe -inputFile $ps1 -outputFile $exe -noConsole -STA `
    -iconFile $ico `
    -title 'Transferencia 1-Clique / 1-Click Transfer' `
    -description 'Transferencia 1-Clique / 1-Click Transfer' `
    -product '1clickTransfer' -company 'samaBR85' `
    -version '1.0.0.0' -copyright '(c) 2026 samaBR85'

if (-not (Test-Path $exe)) { throw "Falha: o exe nao foi gerado." }
Write-Output "OK: $exe ($([Math]::Round((Get-Item $exe).Length/1KB,1)) KB)"

# --- Zip de distribuicao (exe + docs que existirem) ---
$zip = Join-Path $dist "1clickTransfer-$ver.zip"
$items = @()
foreach ($f in @($exe, (Join-Path $root 'LEIA-ME.txt'), (Join-Path $root 'README-EN.txt'))) {
    if (Test-Path $f) { $items += $f }
}
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $items -DestinationPath $zip -Force
Write-Output "OK: $zip"
