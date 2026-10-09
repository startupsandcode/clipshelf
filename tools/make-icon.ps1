# Renders src\icon.ico (multi-size, PNG-encoded) with System.Drawing. Run once; the .ico is checked in.
Add-Type -AssemblyName System.Drawing
$out = Join-Path $PSScriptRoot '..\src\icon.ico'

function Fill-RoundRect($g, $brush, [float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = 2 * $r
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath($brush, $path)
    $path.Dispose()
}

function Render([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $u = $size / 32.0
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (
        (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF $size, $size),
        [System.Drawing.Color]::FromArgb(255, 76, 154, 255), [System.Drawing.Color]::FromArgb(255, 124, 77, 255))
    Fill-RoundRect $g $bg 0 0 $size $size (7 * $u)
    $paper = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    Fill-RoundRect $g $paper (8 * $u) (7 * $u) (16 * $u) (19 * $u) (2.5 * $u)
    $clip = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 34, 46, 120))
    Fill-RoundRect $g $clip (11.5 * $u) (4.5 * $u) (9 * $u) (5 * $u) (1.6 * $u)
    $line = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 120, 136, 200))
    foreach ($row in @(@(13, 10), @(17, 10), @(21, 6))) {
        Fill-RoundRect $g $line (11 * $u) ($row[0] * $u) ($row[1] * $u) ([Math]::Max(1.8 * $u, 1)) (0.9 * $u)
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return , $ms.ToArray()
}

$sizes = 16, 20, 24, 32, 48, 64, 256
$images = New-Object 'System.Collections.Generic.List[byte[]]'
foreach ($s in $sizes) { $images.Add((Render $s)) }

$fs = [System.IO.File]::Create($out)
$w = New-Object System.IO.BinaryWriter $fs
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $dim = $sizes[$i] % 256
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32)
    $w.Write([uint32]$images[$i].Length); $w.Write([uint32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $w.Write($img) }
$w.Dispose()
Write-Host "Wrote $out"
