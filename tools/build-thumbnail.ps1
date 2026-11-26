# Renders tools\thumbnail.html to preview.jpg (1440x1440, square: Steam and the launcher show it in a square) with headless Edge.
# It shows workshop\overview.jpg: run tools\build-screenshots.ps1 first.
# Usage: .\tools\build-thumbnail.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$html = 'file:///' + ((Join-Path $PSScriptRoot 'thumbnail.html') -replace '\\', '/')
$png = Join-Path $env:TEMP 'dw2-overview-preview.png'
$jpg = Join-Path $root 'preview.jpg'
if (-not (Test-Path (Join-Path $root 'workshop\overview.jpg'))) { throw 'workshop\overview.jpg is missing: run tools\build-screenshots.ps1 first.' }

& $edge --headless=new --disable-gpu --hide-scrollbars --allow-file-access-from-files --force-device-scale-factor=2 --window-size=720,720 `
    --virtual-time-budget=2000 "--screenshot=$png" $html 2>$null | Out-Null
if (-not (Test-Path $png)) { throw 'Edge did not write the screenshot.' }

Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile($png)
try {
    $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
    $params = New-Object System.Drawing.Imaging.EncoderParameters 1
    $params.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 90L
    $img.Save($jpg, $codec, $params)
    Write-Host "Wrote preview.jpg ($($img.Width)x$($img.Height), $([math]::Round((Get-Item $jpg).Length / 1KB)) KB)"
} finally { $img.Dispose(); Remove-Item $png -ErrorAction SilentlyContinue }
