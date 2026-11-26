# Captures the live page for the Workshop gallery, with headless Edge (the game must be running with a save loaded).
# Output: workshop\<tab>.jpg, 1920x1080 (1440x810 CSS at 4/3), the page's default settings (a fresh browser profile).
# Usage: .\tools\build-screenshots.ps1 [-Tabs overview,ships] [-Port 4722]
param([string[]]$Tabs = @('overview', 'construction', 'resources', 'ships'), [int]$Port = 4722)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$outDir = Join-Path $root 'workshop'
$userData = Join-Path $env:TEMP 'dw2-overview-shots'
New-Item -ItemType Directory -Force $outDir | Out-Null

try { Invoke-WebRequest "http://localhost:$Port/api/info" -UseBasicParsing -TimeoutSec 5 | Out-Null }
catch { throw "Nothing answers on http://localhost:$Port. Start the game with the mod and load a save." }

$codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
$params = New-Object System.Drawing.Imaging.EncoderParameters 1
$params.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 90L

foreach ($tab in $Tabs) {
    $png = Join-Path $env:TEMP "dw2-overview-$tab.png"
    & $edge --headless=new --disable-gpu --hide-scrollbars "--user-data-dir=$userData" --window-size=1440,810 `
        --force-device-scale-factor=1.3333 --virtual-time-budget=6000 "--screenshot=$png" "http://localhost:$Port/#$tab" 2>$null | Out-Null
    if (-not (Test-Path $png)) { throw "Edge did not write the $tab screenshot." }
    $img = [System.Drawing.Image]::FromFile($png)
    try {
        $jpg = Join-Path $outDir "$tab.jpg"
        $img.Save($jpg, $codec, $params)
        Write-Host "Wrote workshop\$tab.jpg ($($img.Width)x$($img.Height), $([math]::Round((Get-Item $jpg).Length / 1KB)) KB)"
    } finally { $img.Dispose(); Remove-Item $png -ErrorAction SilentlyContinue }
}
Remove-Item $userData -Recurse -Force -ErrorAction SilentlyContinue
