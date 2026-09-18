# Draws the application icon and builds the Windows .ico from it. Windows only.
#
# The icon is round, the way a browser's is, but the pattern is this program's
# own: an open ring with what it goes round sitting in the middle. Not a globe,
# which every browser has had, and deliberately not a coloured wheel, which is
# somebody else's.
#
# Nothing in it is smaller than a few pixels at sixteen: one ring, one gap, one
# dot. That is the whole design brief, because the taskbar and the title bar are
# where an icon is actually looked at.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$resources = Join-Path $projectRoot 'src/ChromiumBrowser/Resources'
$sourcePath = Join-Path $resources 'app-icon.png'
$outputPath = Join-Path $resources 'app.ico'

New-Item -ItemType Directory -Force -Path $resources | Out-Null

# ----------------------------------------------------------------- the drawing

$canvas = 1024

# The window's own palette: the chrome it paints behind its tabs, the accent it
# takes from the desktop, and the near-white it puts text on.
$chrome = [System.Drawing.Color]::FromArgb(0x20, 0x21, 0x24)
$edge = [System.Drawing.Color]::FromArgb(0x3C, 0x3E, 0x42)
$page = [System.Drawing.Color]::FromArgb(0xF1, 0xF3, 0xF6)
$accent = [System.Drawing.Color]::FromArgb(0x8A, 0xB4, 0xF8)



$bitmap = [System.Drawing.Bitmap]::new($canvas, $canvas, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

# The disc, with a hairline so it still has an edge on a dark taskbar.
$disc = [System.Drawing.RectangleF]::new(10, 10, $canvas - 20, $canvas - 20)
$discBrush = [System.Drawing.SolidBrush]::new($chrome)
$discPen = [System.Drawing.Pen]::new($edge, 14)
$graphics.FillEllipse($discBrush, $disc)
$graphics.DrawEllipse($discPen, $disc)

# The ring, open at the bottom. Round caps, so the gap reads as a gap rather
# than as two cut edges.
$ringPen = [System.Drawing.Pen]::new($accent, 88)
$ringPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$ringPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$graphics.DrawArc($ringPen, [System.Drawing.RectangleF]::new(214, 214, 596, 596), 128, 284)

# What it goes round.
$dotBrush = [System.Drawing.SolidBrush]::new($page)
$graphics.FillEllipse($dotBrush, [System.Drawing.RectangleF]::new(434, 434, 156, 156))

$bitmap.Save($sourcePath, [System.Drawing.Imaging.ImageFormat]::Png)

$dotBrush.Dispose()
$ringPen.Dispose()
$discPen.Dispose()
$discBrush.Dispose()
$graphics.Dispose()

# ------------------------------------------------------------------- the .ico
#
# Every size is drawn into the file rather than leaving Windows to shrink the
# largest one, because the sizes Windows actually asks for - 16 for the title
# bar, 32 for the taskbar, 256 for the big view - are the ones worth controlling.

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = [System.Collections.Generic.List[byte[]]]::new()

try {
    foreach ($size in $sizes) {
        $frame = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $frameGraphics = [System.Drawing.Graphics]::FromImage($frame)
        $stream = [IO.MemoryStream]::new()
        try {
            $frameGraphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $frameGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $frameGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $frameGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $frameGraphics.DrawImage($bitmap, [System.Drawing.Rectangle]::new(0, 0, $size, $size))
            $frame.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add($stream.ToArray())
        }
        finally {
            $stream.Dispose()
            $frameGraphics.Dispose()
            $frame.Dispose()
        }
    }

    $file = [IO.File]::Create($outputPath)
    $writer = [IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i = 0; $i -lt $sizes.Count; $i++) {
            # 256 is written as a zero, which is how the format says "256".
            $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length)
            $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }

        foreach ($frame in $frames) {
            $writer.Write($frame)
        }
    }
    finally {
        $writer.Dispose()
        $file.Dispose()
    }
}
finally {
    $bitmap.Dispose()
}

Write-Host "wrote $sourcePath and $outputPath"
