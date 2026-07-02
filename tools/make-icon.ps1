# Gera o icone do app (aviao de papel branco sobre quadrado azul arredondado).
# Saidas: assets/app.ico (multi-resolucao) e assets/app-icon-256.png
# Uso:  powershell -NoProfile -ExecutionPolicy Bypass -File tools\make-icon.ps1

Add-Type -AssemblyName System.Drawing

$root    = Split-Path -Parent $PSScriptRoot
$assets  = Join-Path $root 'assets'
if (-not (Test-Path $assets)) { New-Item -ItemType Directory -Path $assets -Force | Out-Null }
$icoPath = Join-Path $assets 'app.ico'
$pngPath = Join-Path $assets 'app-icon-256.png'

$blue  = [System.Drawing.Color]::FromArgb(0x25,0x63,0xEB)   # #2563EB
$white = [System.Drawing.Color]::White

function New-RoundedPath($x, $y, $w, $h, $r) {
    $gp = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $gp.AddArc($x, $y, $d, $d, 180, 90)
    $gp.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $gp.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $gp.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $gp.CloseAllFigures()
    return $gp
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    # Fundo azul arredondado
    $r = [int]($size * 0.22)
    $bg = New-RoundedPath 0 0 ($size - 1) ($size - 1) $r
    $brushBlue = New-Object System.Drawing.SolidBrush($blue)
    $g.FillPath($brushBlue, $bg)

    # Aviao de papel (coordenadas base 64x64)
    $sc = $size / 64.0
    $pts = @(
        (New-Object System.Drawing.PointF((12 * $sc), (33 * $sc))),
        (New-Object System.Drawing.PointF((52 * $sc), (14 * $sc))),
        (New-Object System.Drawing.PointF((41 * $sc), (50 * $sc))),
        (New-Object System.Drawing.PointF((33 * $sc), (39 * $sc))),
        (New-Object System.Drawing.PointF((24 * $sc), (45 * $sc)))
    )
    $brushWhite = New-Object System.Drawing.SolidBrush($white)
    $g.FillPolygon($brushWhite, $pts)

    # Vinco (linha da ponta ate o corpo) em azul, so em tamanhos maiores
    if ($size -ge 32) {
        $pen = New-Object System.Drawing.Pen($blue, [float]([Math]::Max(1.0, $sc * 1.4)))
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawLine($pen, (52 * $sc), (14 * $sc), (33 * $sc), (39 * $sc))
        $pen.Dispose()
    }

    $brushBlue.Dispose(); $brushWhite.Dispose(); $bg.Dispose(); $g.Dispose()
    return $bmp
}

# --- Monta o .ico com PNGs embutidos (suportado no Windows Vista+) ---
$sizes = 16,24,32,48,64,128,256
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,@{ size = $s; bytes = $ms.ToArray() }
    if ($s -eq 256) { $bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png) }
    $ms.Dispose(); $bmp.Dispose()
}

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
# ICONDIR
$bw.Write([UInt16]0)                 # reserved
$bw.Write([UInt16]1)                 # type = icon
$bw.Write([UInt16]$pngs.Count)       # count
# Offset dos dados: 6 (header) + 16 por entrada
$offset = 6 + (16 * $pngs.Count)
foreach ($p in $pngs) {
    $dim = if ($p.size -ge 256) { 0 } else { $p.size }   # 0 = 256
    $bw.Write([Byte]$dim)            # width
    $bw.Write([Byte]$dim)            # height
    $bw.Write([Byte]0)              # color count
    $bw.Write([Byte]0)              # reserved
    $bw.Write([UInt16]1)            # planes
    $bw.Write([UInt16]32)           # bit count
    $bw.Write([UInt32]$p.bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $p.bytes.Length
}
foreach ($p in $pngs) { $bw.Write($p.bytes) }
$bw.Flush(); $bw.Close(); $fs.Close()

Write-Output "OK: $icoPath ($([Math]::Round((Get-Item $icoPath).Length/1KB,1)) KB, $($pngs.Count) tamanhos)"
Write-Output "OK: $pngPath"
