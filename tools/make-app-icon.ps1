# Draws the application icon and builds the Windows .ico from it. Windows only.
#
# The icon is the thing the program is: a window with a tab on it, drawn in the
# colours the browser draws itself with. Not a globe, which every browser has
# had, and deliberately not a coloured wheel, which is somebody else's.
#
# One tab rather than three, and no text anywhere: at sixteen pixels a second
# tab is two grey smudges and lettering is a grey bar. What has to survive that
# size is the silhouette — a light page with a blue tab sitting on its corner.
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
$band = [System.Drawing.Color]::FromArgb(0xC2, 0xC7, 0xCF)
$dots = [System.Drawing.Color]::FromArgb(0x6E, 0x74, 0x7E)
$rule = [System.Drawing.Color]::FromArgb(0xB6, 0xBC, 0xC6)

function New-RoundedPath {
    param([System.Drawing.RectangleF] $bounds, [float] $radius)

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $radius * 2
    $path.AddArc($bounds.X, $bounds.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($bounds.Right - $diameter, $bounds.Y, $diameter, $diameter, 270, 90)
    $path.AddArc($bounds.Right - $diameter, $bounds.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($bounds.X, $bounds.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

# A tab: rounded at the top, square at the bottom where it meets the page.
function New-TabPath {
    param([System.Drawing.RectangleF] $bounds, [float] $radius)

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $radius * 2
    $path.AddArc($bounds.X, $bounds.Y, $diameter, $diameter, 180, 90)
    $path.AddArc($bounds.Right - $diameter, $bounds.Y, $diameter, $diameter, 270, 90)
    $path.AddLine($bounds.Right, $bounds.Bottom, $bounds.X, $bounds.Bottom)
    $path.CloseFigure()
    return $path
}

$bitmap = [System.Drawing.Bitmap]::new($canvas, $canvas, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)

# The tile.
$tilePath = New-RoundedPath -bounds ([System.Drawing.RectangleF]::new(0, 0, $canvas, $canvas)) -radius 190
$tileBrush = [System.Drawing.SolidBrush]::new($chrome)
$edgePen = [System.Drawing.Pen]::new($edge, 10)
$graphics.FillPath($tileBrush, $tilePath)
$graphics.DrawPath($edgePen, $tilePath)

# The window: a band of chrome across the top with the page below it. Drawn as
# one rounded shape in the band's colour, with the page laid over its lower
# part, so the corners stay round without cutting two shapes to fit.
$windowBounds = [System.Drawing.RectangleF]::new(128, 250, 768, 634)
$windowPath = New-RoundedPath -bounds $windowBounds -radius 56
$bandBrush = [System.Drawing.SolidBrush]::new($band)
$graphics.FillPath($bandBrush, $windowPath)

$pageBounds = [System.Drawing.RectangleF]::new(128, 392, 768, 492)
$pagePath = New-RoundedPath -bounds $pageBounds -radius 56
$pageBrush = [System.Drawing.SolidBrush]::new($page)
$graphics.FillPath($pageBrush, $pagePath)

# Square off the page's top corners, which belong to the band above them.
$graphics.FillRectangle($pageBrush, [System.Drawing.RectangleF]::new(128, 392, 768, 60))

# The three buttons every window has, and the bar an address goes in.
$dotBrush = [System.Drawing.SolidBrush]::new($dots)
foreach ($x in @(186, 246, 306)) {
    $graphics.FillEllipse($dotBrush, [System.Drawing.RectangleF]::new($x, 305, 34, 34))
}

$addressPath = New-RoundedPath -bounds ([System.Drawing.RectangleF]::new(372, 296, 452, 52)) -radius 26
$addressBrush = [System.Drawing.SolidBrush]::new($page)
$graphics.FillPath($addressBrush, $addressPath)

# The tab, sitting on the window's top edge where the first one sits. Inset
# from the window's left edge rather than flush with it: flush, at sixteen
# pixels, is the silhouette of a folder.
$tabBounds = [System.Drawing.RectangleF]::new(196, 146, 252, 126)
$tabPath = New-TabPath -bounds $tabBounds -radius 52
$tabBrush = [System.Drawing.SolidBrush]::new($accent)
$graphics.FillPath($tabBrush, $tabPath)

# Two lines of a page. They vanish into the white below about 32 pixels, which
# is the point: what is left there is the silhouette.
$lineBrush = [System.Drawing.SolidBrush]::new($rule)
foreach ($line in @(
    [System.Drawing.RectangleF]::new(206, 520, 560, 44),
    [System.Drawing.RectangleF]::new(206, 626, 396, 44))) {

    $linePath = New-RoundedPath -bounds $line -radius 22
    $graphics.FillPath($lineBrush, $linePath)
    $linePath.Dispose()
}

$bitmap.Save($sourcePath, [System.Drawing.Imaging.ImageFormat]::Png)

$lineBrush.Dispose()
$tabBrush.Dispose()
$tabPath.Dispose()
$addressBrush.Dispose()
$addressPath.Dispose()
$dotBrush.Dispose()
$pageBrush.Dispose()
$pagePath.Dispose()
$bandBrush.Dispose()
$windowPath.Dispose()
$edgePen.Dispose()
$tileBrush.Dispose()
$tilePath.Dispose()
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
