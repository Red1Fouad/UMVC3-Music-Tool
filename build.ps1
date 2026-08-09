$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $Root "src\Umvc3MusicTool\Umvc3MusicTool.csproj"
$Out = Join-Path $Root "publish\win-x64-single"

Write-Host "Publishing single-file exe (win-x64, self-contained)..."

dotnet publish $Project -c Release -r win-x64 --self-contained true -o $Out `
    /p:PublishSingleFile=true `
    /p:IncludeNativeLibrariesForSelfExtract=true `
    /p:DebugType=None `
    /p:DebugSymbols=false `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

$Exe = Join-Path $Out "Umvc3MusicTool.exe"
$Ffmpeg = Join-Path $Out "tools\ffmpeg.exe"

if (-not (Test-Path -LiteralPath $Exe)) {
    throw "Expected exe not found: $Exe"
}
if (-not (Test-Path -LiteralPath $Ffmpeg)) {
    throw "Expected tools\ffmpeg.exe not found: $Ffmpeg"
}

Write-Host ""
Write-Host "Done."
Write-Host "  Exe:    $Exe"
Write-Host "  ffmpeg: $Ffmpeg"
