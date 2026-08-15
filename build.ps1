$ErrorActionPreference = "Stop"

function New-AppIcon {
    param(
        [Parameter(Mandatory)] [string] $PngPath,
        [Parameter(Mandatory)] [string] $IcoPath,
        [int[]] $Sizes = @(16, 24, 32, 48, 256)
    )

    Add-Type -AssemblyName System.Drawing

    $icoDir = Split-Path -Parent $IcoPath
    if (-not (Test-Path -LiteralPath $icoDir)) {
        New-Item -ItemType Directory -Path $icoDir -Force | Out-Null
    }

    $source = [System.Drawing.Image]::FromFile($PngPath)
    try {
        $images = [System.Collections.Generic.List[object]]::new()
        foreach ($s in $Sizes) {
            $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $g = [System.Drawing.Graphics]::FromImage($bmp)
            $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $g.Clear([System.Drawing.Color]::Transparent)
            $g.DrawImage($source, 0, 0, $s, $s)
            $g.Dispose()
            $images.Add($bmp)
        }

        $fileStream = [System.IO.File]::Create($IcoPath)
        try {
            $writer = New-Object System.IO.BinaryWriter($fileStream)

            # ICONDIR header
            $writer.Write([UInt16]0)                    # reserved
            $writer.Write([UInt16]1)                    # type: icon
            $writer.Write([UInt16]$images.Count)

            $offset = 6 + 16 * $images.Count
            $blob = [System.IO.MemoryStream]::new()
            $blobWriter = New-Object System.IO.BinaryWriter($blob)

            for ($i = 0; $i -lt $images.Count; $i++) {
                $bmp = $images[$i]
                $size = $bmp.Width
                $len = Get-DibEntry -Bitmap $bmp -Writer $blobWriter

                $b = if ($size -ge 256) { 0 } else { $size }
                $writer.Write([Byte]$b)                 # width (0 = 256)
                $writer.Write([Byte]$b)                 # height (0 = 256)
                $writer.Write([Byte]0)                  # color count
                $writer.Write([Byte]0)                  # reserved
                $writer.Write([UInt16]1)                # color planes
                $writer.Write([UInt16]32)               # bits per pixel
                $writer.Write([UInt32]$len)             # bytes in resource
                $writer.Write([UInt32]$offset)          # image offset
                $offset += $len

                $bmp.Dispose()
            }

            $blobWriter.Flush()
            $finalBlob = $blob.ToArray()
            $blobWriter.Dispose()
            $writer.Write($finalBlob)
            $writer.Dispose()
        } finally {
            $fileStream.Dispose()
        }
    } finally {
        $source.Dispose()
    }
}

function Get-DibEntry {
    param(
        [System.Drawing.Bitmap] $Bitmap,
        [System.IO.BinaryWriter] $Writer
    )

    $w = $Bitmap.Width
    $h = $Bitmap.Height
    $rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $stride = $data.Stride
        $bytes = New-Object byte[] ($stride * $h)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)

        # BITMAPINFOHEADER
        $Writer.Write([UInt32]40)                       # biSize
        $Writer.Write([Int32]$w)                        # biWidth
        $Writer.Write([Int32]($h * 2))                  # biHeight (XOR + AND)
        $Writer.Write([Int16]1)                         # biPlanes
        $Writer.Write([Int16]32)                        # biBitCount
        $Writer.Write([UInt32]0)                        # biCompression
        $Writer.Write([UInt32]($w * $h * 4))            # biSizeImage
        $Writer.Write([UInt32]0)                        # biXPelsPerMeter
        $Writer.Write([UInt32]0)                        # biYPelsPerMeter
        $Writer.Write([UInt32]0)                        # biClrUsed
        $Writer.Write([UInt32]0)                        # biClrImportant

        # XOR data: BGRA rows bottom-up (ICO wants top-down)
        for ($y = $h - 1; $y -ge 0; $y--) {
            $row = New-Object byte[] ($w * 4)
            [System.Array]::Copy($bytes, $y * $stride, $row, 0, $w * 4)
            $Writer.Write($row)
        }

        # AND mask: all zeros (alpha channel is used), rows padded to 4 bytes
        $maskRowLen = [int]([Math]::Floor(($w + 31) / 32) * 4)
        $maskRow = New-Object byte[] $maskRowLen
        for ($y = 0; $y -lt $h; $y++) {
            $Writer.Write($maskRow)
        }

        return (40 + $w * $h * 4 + $maskRowLen * $h)
    } finally {
        $Bitmap.UnlockBits($data)
    }
}

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $Root "src\Umvc3MusicTool\Umvc3MusicTool.csproj"
$Out = Join-Path $Root "publish\win-x64-single"

$IconPng = Join-Path $Root "Music_Tool_icon_by_Alucard.png"
$AppIcon = Join-Path $Root "src\Umvc3MusicTool\obj\app.ico"

if (-not (Test-Path -LiteralPath $IconPng)) {
    throw "App icon PNG not found: $IconPng"
}

Write-Host "Generating app icon (.ico) from $IconPng..."
New-AppIcon -PngPath $IconPng -IcoPath $AppIcon

Write-Host "Publishing single-file exe (win-x64, self-contained)..."
Write-Host "  Icon:     $AppIcon"

dotnet publish $Project -c Release -r win-x64 --self-contained true -o $Out `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:DebugType=None `
    /p:DebugSymbols=false `
    "/p:ApplicationIcon=$AppIcon" `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

$Exe = Join-Path $Out "Umvc3MusicTool.exe"
$Ffmpeg = Join-Path $Out "tools\ffmpeg.exe"
$Oggenc = Join-Path $Out "tools\oggenc2.exe"

if (-not (Test-Path -LiteralPath $Exe)) {
    throw "Expected exe not found: $Exe"
}
if (-not (Test-Path -LiteralPath $Ffmpeg)) {
    throw "Expected tools\ffmpeg.exe not found: $Ffmpeg"
}
if (-not (Test-Path -LiteralPath $Oggenc)) {
    throw "Expected tools\oggenc2.exe not found: $Oggenc"
}

Write-Host ""
Write-Host "Done."
Write-Host "  Exe:     $Exe"
Write-Host "  ffmpeg:  $Ffmpeg"
Write-Host "  oggenc2: $Oggenc"
Write-Host ""
Write-Host "Credits: App by RED1 - App icon by Alucard"
