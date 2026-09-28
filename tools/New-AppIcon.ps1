param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Destination
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$destinationPath = [IO.Path]::GetFullPath($Destination)
$original = [Drawing.Image]::FromFile($sourcePath)
$frames = [Collections.Generic.List[object]]::new()
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)

try {
    foreach ($size in $sizes) {
        $bitmap = [Drawing.Bitmap]::new($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $attributes = [Drawing.Imaging.ImageAttributes]::new()
        $stream = [IO.MemoryStream]::new()
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.CompositingMode = [Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $attributes.SetWrapMode([Drawing.Drawing2D.WrapMode]::TileFlipXY)
            $rectangle = [Drawing.Rectangle]::new(0, 0, $size, $size)
            $graphics.DrawImage($original, $rectangle, 0, 0, $original.Width, $original.Height, [Drawing.GraphicsUnit]::Pixel, $attributes)
            $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            $frames.Add([pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() })
        }
        finally {
            $stream.Dispose()
            $attributes.Dispose()
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }

    $output = [IO.File]::Create($destinationPath)
    $writer = [IO.BinaryWriter]::new($output)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        foreach ($frame in $frames) {
            $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Bytes.Length)
            $writer.Write([uint32]$offset)
            $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
    }
    finally { $writer.Dispose() }
}
finally { $original.Dispose() }

Add-Type -AssemblyName PresentationCore
$decoder = [Windows.Media.Imaging.IconBitmapDecoder]::new(
    [Uri]::new($destinationPath),
    [Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat,
    [Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
$actualSizes = @($decoder.Frames | ForEach-Object { $_.PixelWidth })
if (($actualSizes -join ',') -ne ($sizes -join ',')) { throw 'The generated icon failed frame validation.' }
if (@($decoder.Frames | Where-Object { $_.PixelWidth -ne $_.PixelHeight }).Count -gt 0) { throw 'An icon frame is not square.' }
Write-Output "Created and validated $destinationPath with sizes: $($sizes -join ', ')."
