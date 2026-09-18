<#
.SYNOPSIS
Captures one window of this browser to a PNG.

.DESCRIPTION
Two details matter and both are easy to get wrong.

The capture is of the window alone, never of the screen: PrintWindow asks the
window to draw itself into a bitmap, so whatever else is on the desktop stays
out of the picture. The flag is 2, PW_RENDERFULLCONTENT, without which anything
the graphics card composited - which is the whole page - comes out blank.

The crop comes from DwmGetWindowAttribute rather than from GetWindowRect,
because a resizable window's rectangle includes an invisible border that is
several pixels wide. Cropping to the window rectangle leaves a black band down
the left, the right and the bottom of every screenshot.
#>
param(
    [string]$TitleMatch = "Chromium Browser",
    [string]$Out = "docs/screenshot.png",
    [int]$DelaySeconds = 0
)

Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;

public static class WindowShot
{
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr dc, uint flags);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out RECT value, int size);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public const int ExtendedFrameBounds = 9;
    public const uint RenderFullContent = 2;
}
'@

if ($DelaySeconds -gt 0) { Start-Sleep -Seconds $DelaySeconds }

$window = Get-Process |
    Where-Object { $_.MainWindowTitle -like "*$TitleMatch*" -and $_.MainWindowHandle -ne 0 } |
    Select-Object -First 1

if (-not $window) { throw "No window whose title contains '$TitleMatch'." }

$hwnd = $window.MainWindowHandle

$outer = New-Object WindowShot+RECT
[void][WindowShot]::GetWindowRect($hwnd, [ref]$outer)
$width = $outer.Right - $outer.Left
$height = $outer.Bottom - $outer.Top

$bitmap = New-Object System.Drawing.Bitmap $width, $height
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$dc = $graphics.GetHdc()
[void][WindowShot]::PrintWindow($hwnd, $dc, [WindowShot]::RenderFullContent)
$graphics.ReleaseHdc($dc)
$graphics.Dispose()

# The visible window is inset from its own rectangle by the invisible border.
$frame = New-Object WindowShot+RECT
$got = [WindowShot]::DwmGetWindowAttribute($hwnd, [WindowShot]::ExtendedFrameBounds, [ref]$frame, 16)

if ($got -eq 0) {
    $crop = New-Object System.Drawing.Rectangle(
        ($frame.Left - $outer.Left),
        ($frame.Top - $outer.Top),
        ($frame.Right - $frame.Left),
        ($frame.Bottom - $frame.Top))

    $trimmed = $bitmap.Clone($crop, $bitmap.PixelFormat)
    $bitmap.Dispose()
    $bitmap = $trimmed
}

$path = if ([System.IO.Path]::IsPathRooted($Out)) { $Out } else { Join-Path (Get-Location) $Out }
New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($path)) | Out-Null
$bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()

"$path  ($($bitmap.Width)x$($bitmap.Height))"
