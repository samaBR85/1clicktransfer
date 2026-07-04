# Publica o app v3 (Avalonia) single-file self-contained por RID -> dist-v3/.
# Uso:  powershell -NoProfile -ExecutionPolicy Bypass -File tools\build-v3.ps1 [-Rid win-x64]
#       -Rid all  publica os 4 RIDs (win-x64, linux-x64, osx-x64, osx-arm64).
# Nomes contratuais: Windows = 1clickTransfer.exe; demais = 1clickTransfer-<rid>.
# Cada executavel tambem eh empacotado em 1clickTransfer-<rid>.zip -> sao esses .zip
# que viram os assets da release (o auto-update busca um .zip com "win-x64" no nome
# e extrai o .exe de dentro; ver UpdateService.ExtractExeFromZip).
# osx-x64/osx-arm64: o executavel solto nao tem bundle, entao o macOS nao mostra
# icone nenhum no Dock (nem generico). O zip desses dois RIDs empacota um
# 1clickTransfer.app (Contents/MacOS + Resources/AppIcon.icns via assets/app.icns)
# em vez do executavel solto, so pra ter icone/identidade no Dock; o CLI dentro do
# .app continua o mesmo binario (Contents/MacOS/1clickTransfer).
# NUNCA usar PublishTrimmed/AOT (Avalonia quebra).

param([string]$Rid = 'win-x64')

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'src\OneClickTransfer.Avalonia\OneClickTransfer.Avalonia.csproj'
$dist = Join-Path $root 'dist-v3'
$icns = Join-Path $root 'assets\app.icns'
$appVersion = ([xml](Get-Content $proj)).Project.PropertyGroup.Version | Select-Object -First 1

function New-MacAppBundle([string]$exePath, [string]$bundleDir) {
    if (Test-Path $bundleDir) { Remove-Item $bundleDir -Recurse -Force }
    $macosDir = Join-Path $bundleDir 'Contents\MacOS'
    $resDir = Join-Path $bundleDir 'Contents\Resources'
    New-Item -ItemType Directory -Path $macosDir -Force | Out-Null
    New-Item -ItemType Directory -Path $resDir -Force | Out-Null
    Copy-Item $exePath (Join-Path $macosDir '1clickTransfer') -Force
    Copy-Item $icns (Join-Path $resDir 'AppIcon.icns') -Force
    $plist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>1-Click Transfer</string>
  <key>CFBundleDisplayName</key><string>1-Click Transfer</string>
  <key>CFBundleIdentifier</key><string>com.samabr85.1clicktransfer</string>
  <key>CFBundleVersion</key><string>$appVersion</string>
  <key>CFBundleShortVersionString</key><string>$appVersion</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleExecutable</key><string>1clickTransfer</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSPrincipalClass</key><string>NSApplication</string>
</dict>
</plist>
"@
    Set-Content -Path (Join-Path $bundleDir 'Contents\Info.plist') -Value $plist -Encoding UTF8
}

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
    # Nos RIDs mac, zip empacota um .app (Contents/MacOS/1clickTransfer) pra ter
    # icone/identidade no Dock; nos demais RIDs zip empacota o executavel solto.
    $zip = Join-Path $dist "1clickTransfer-$r.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    if ($r -like 'osx-*') {
        $bundleDir = Join-Path $outDir '1clickTransfer.app'
        New-MacAppBundle -exePath $final -bundleDir $bundleDir
        Compress-Archive -Path $bundleDir -DestinationPath $zip -CompressionLevel Optimal
    } else {
        Compress-Archive -Path $final -DestinationPath $zip -CompressionLevel Optimal
    }
    $zipMb = [Math]::Round((Get-Item $zip).Length / 1MB, 1)
    Write-Output "OK: $zip ($zipMb MB)"
}

Write-Output ""
Write-Output "Artefatos em: $dist"
Get-ChildItem $dist -File | Select-Object Name, @{n='MB';e={[Math]::Round($_.Length/1MB,1)}}
