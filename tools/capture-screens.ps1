# Captura screenshots da JANELA DO APP em PT e EN (estado limpo, sem dados pessoais).
# Usa PrintWindow -> renderiza apenas a janela do app (nunca o desktop), mesmo que
# esteja atras de outras janelas. Salva em screenshots\home-<lang>.png
# Uso: powershell -NoProfile -ExecutionPolicy Bypass -File tools\capture-screens.ps1

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinCap {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x,int y,int w,int ht, bool repaint);
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L,T,R,B; }
}
"@

$root = Split-Path -Parent $PSScriptRoot
$exe  = Join-Path $root 'dist\1clickTransfer.exe'
$dist = Split-Path -Parent $exe
$shots = Join-Path $root 'screenshots'
if (-not (Test-Path $exe)) { throw "Compile o exe primeiro (tools\build-exe.ps1)." }
if (-not (Test-Path $shots)) { New-Item -ItemType Directory -Path $shots -Force | Out-Null }

function Capture-Lang([string]$lang) {
    "{ ""language"": ""$lang"" }" | Set-Content (Join-Path $dist 'settings.json') -Encoding UTF8
    $p = Start-Process -FilePath $exe -PassThru
    $h = [IntPtr]::Zero
    for ($i=0; $i -lt 60; $i++) {
        Start-Sleep -Milliseconds 200
        $p.Refresh()
        if ($p.MainWindowHandle -ne [IntPtr]::Zero) { $h = $p.MainWindowHandle; break }
    }
    if ($h -eq [IntPtr]::Zero) { $p.Kill(); throw "Janela nao apareceu ($lang)." }
    [WinCap]::MoveWindow($h, 80, 80, 1040, 720, $true) | Out-Null
    Start-Sleep -Milliseconds 1200   # deixa pintar
    $r = New-Object WinCap+RECT
    [WinCap]::GetWindowRect($h, [ref]$r) | Out-Null
    $w = $r.R - $r.L; $ht = $r.B - $r.T
    $bmp = New-Object System.Drawing.Bitmap($w, $ht)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $g.GetHdc()
    $okp = [WinCap]::PrintWindow($h, $hdc, 2)   # 2 = PW_RENDERFULLCONTENT
    $g.ReleaseHdc($hdc)
    $out = Join-Path $shots "home-$lang.png"
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    try { $p.Kill() } catch {}
    Start-Sleep -Milliseconds 300
    Write-Output "OK: $out ($w x $ht, PrintWindow=$okp)"
}

Capture-Lang 'pt'
Capture-Lang 'en'
Remove-Item (Join-Path $dist 'settings.json') -Force -ErrorAction SilentlyContinue
Write-Output "Concluido."
