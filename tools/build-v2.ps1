# Publica o app v2 (WPF) como .exe unico, self-contained e portatil (win-x64).
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\build-v2.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root 'src\OneClickTransfer\OneClickTransfer.csproj'
$out  = Join-Path $root 'dist-v2'

Write-Output "Publicando (single-file, self-contained, comprimido)..."
& dotnet publish $proj -c Release -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -o $out

$exe = Join-Path $out '1clickTransfer.exe'
if (Test-Path $exe) {
    $mb = [Math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Output "OK: $exe ($mb MB)"
} else {
    throw "Falha: exe nao gerado."
}
