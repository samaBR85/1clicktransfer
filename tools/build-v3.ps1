# Publica o app v3 (Avalonia) single-file self-contained por RID -> dist-v3/.
# Uso:  powershell -NoProfile -ExecutionPolicy Bypass -File tools\build-v3.ps1 [-Rid win-x64]
#       -Rid all  publica os 4 RIDs (win-x64, linux-x64, osx-x64, osx-arm64).
# Nomes contratuais: Windows = 1clickTransfer.exe; demais = 1clickTransfer-<rid>.
# Cada executavel tambem eh empacotado em 1clickTransfer-<rid>.zip -> sao esses .zip
# que viram os assets da release (o auto-update busca um .zip com "win-x64" no nome
# e extrai o .exe de dentro; ver UpdateService.ExtractExeFromZip).
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

    # Asset de release: zip contendo o executavel (nome do zip carrega o RID).
    $zip = Join-Path $dist "1clickTransfer-$r.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path $final -DestinationPath $zip -CompressionLevel Optimal
    $zipMb = [Math]::Round((Get-Item $zip).Length / 1MB, 1)
    Write-Output "OK: $zip ($zipMb MB)"
}

Write-Output ""
Write-Output "Artefatos em: $dist"
Get-ChildItem $dist -File | Select-Object Name, @{n='MB';e={[Math]::Round($_.Length/1MB,1)}}
