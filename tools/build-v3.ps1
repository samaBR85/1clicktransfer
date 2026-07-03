# Publica o app v3 (Avalonia) single-file self-contained por RID -> dist-v3/.
# Uso:  powershell -NoProfile -ExecutionPolicy Bypass -File tools\build-v3.ps1 [-Rid win-x64]
#       -Rid all  publica os 4 RIDs (win-x64, linux-x64, osx-x64, osx-arm64).
# Nomes contratuais: Windows = 1clickTransfer.exe (o auto-update v2 filtra por .exe);
#                    demais  = 1clickTransfer-<rid>.
# NUNCA usar PublishTrimmed/AOT (Avalonia quebra).

param([string]$Rid = 'win-x64')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'src\OneClickTransfer.Avalonia\OneClickTransfer.Avalonia.csproj'
$dist = Join-Path $root 'dist-v3'

$rids = if ($Rid -eq 'all') { @('win-x64','linux-x64','osx-x64','osx-arm64') } else { @($Rid) }

# Evita lock do exe em execucao (Windows).
Get-Process -Name '1clickTransfer','1clickTransfer.v3' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

foreach ($r in $rids) {
    $outDir = Join-Path $dist $r
    Write-Output "==> Publicando $r (single-file, self-contained)..."
    & dotnet publish $proj -c Release -r $r `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:EnableCompressionInSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=none `
        -o $outDir
    if ($LASTEXITCODE -ne 0) { throw "publish falhou para $r" }

    # Nome do artefato final (contratual).
    if ($r -eq 'win-x64') {
        $src = Join-Path $outDir '1clickTransfer.exe'
        $final = Join-Path $dist '1clickTransfer.exe'
    } else {
        $src = Join-Path $outDir '1clickTransfer'
        $final = Join-Path $dist ("1clickTransfer-$r")
    }
    if (-not (Test-Path $src)) { throw "artefato nao gerado: $src" }

    Copy-Item $src $final -Force
    $mb = [Math]::Round((Get-Item $final).Length / 1MB, 1)
    if ((Get-Item $final).Length -lt 1MB) { throw "artefato $final < 1MB (guard do SwapExe)" }
    Write-Output "OK: $final ($mb MB)"
}

Write-Output ""
Write-Output "Artefatos em: $dist"
Get-ChildItem $dist -File | Select-Object Name, @{n='MB';e={[Math]::Round($_.Length/1MB,1)}}
