$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repoRoot = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $repoRoot 'docs/assets'
$cards = Join-Path $repoRoot 'docs/official-cards'
$branding = Join-Path $repoRoot 'Installer/Branding'

function Export-Image {
    param([string]$Source, [string]$Destination, [int]$Width, [int]$Height, [int]$JpegQuality = 86)
    $inputImage = [Drawing.Image]::FromFile($Source)
    $bitmap = [Drawing.Bitmap]::new($Width, $Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $isJpeg = [IO.Path]::GetExtension($Destination) -eq '.jpg'
        $background = if ($isJpeg) { [Drawing.Color]::FromArgb(8, 19, 22) } else { [Drawing.Color]::Transparent }
        $graphics.Clear($background)
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $scale = [Math]::Min($Width / $inputImage.Width, $Height / $inputImage.Height)
        $targetWidth = [int][Math]::Round($inputImage.Width * $scale)
        $targetHeight = [int][Math]::Round($inputImage.Height * $scale)
        $rectangle = [Drawing.Rectangle]::new([int](($Width - $targetWidth) / 2), [int](($Height - $targetHeight) / 2), $targetWidth, $targetHeight)
        $graphics.DrawImage($inputImage, $rectangle)
        if ($isJpeg) {
            $encoder = [Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
            $parameters = [Drawing.Imaging.EncoderParameters]::new(1)
            try {
                $parameters.Param[0] = [Drawing.Imaging.EncoderParameter]::new([Drawing.Imaging.Encoder]::Quality, [long]$JpegQuality)
                $bitmap.Save($Destination, $encoder, $parameters)
            } finally { $parameters.Dispose() }
        } else { $bitmap.Save($Destination, [Drawing.Imaging.ImageFormat]::Png) }
    } finally { $graphics.Dispose(); $bitmap.Dispose(); $inputImage.Dispose() }
}

Export-Image -Source (Join-Path $assets 'ELKA-EventLens-Header-v1.png') -Destination (Join-Path $assets 'github-social-preview.jpg') -Width 1280 -Height 640
Export-Image -Source (Join-Path $cards 'ELKA-EventLens-Vertical.png') -Destination (Join-Path $cards 'ELKA-EventLens-Vertical-Web.jpg') -Width 720 -Height 1280
Export-Image -Source (Join-Path $cards 'ELKA-EventLens-Vertical.png') -Destination (Join-Path $branding 'wizard-left.png') -Width 432 -Height 768
Export-Image -Source (Join-Path $repoRoot 'src/ELKA.EventLens.App/Assets/EventLens-v1.png') -Destination (Join-Path $branding 'wizard-small.png') -Width 128 -Height 128

$preview = Get-Item -LiteralPath (Join-Path $assets 'github-social-preview.jpg')
if ($preview.Length -gt 1MB) { throw 'GitHub preview exceeds the 1 MB budget.' }
Get-Item -LiteralPath $preview.FullName,(Join-Path $cards 'ELKA-EventLens-Vertical-Web.jpg'),(Join-Path $branding 'wizard-left.png'),(Join-Path $branding 'wizard-small.png') | Select-Object Name,Length
