# Builds the mod and installs it into the game's mods folder.
# Usage: .\install.ps1 [-GameDir "..."] [-NoRegister] [-LiveWeb]
#   -LiveWeb: writes data\Logs\DW2Overview\overview.json in the game folder so the page is served from this
#             repo's web\ folder (edit the page, press F5 in the browser, no game restart). Without it,
#             the installed page is served, as players get it.
param(
    [string]$GameDir = 'D:\games\steam\steamapps\common\Distant Worlds 2',
    [switch]$NoRegister,
    [switch]$LiveWeb
)
$ErrorActionPreference = 'Stop'

$projDir = $PSScriptRoot
$manifest = Get-Content (Join-Path $projDir 'mod.json') -Raw | ConvertFrom-Json
$modName = $manifest.name

dotnet build (Join-Path $projDir 'Overview.csproj') -c Release -p:GameDir="$GameDir" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$outDir = Join-Path $projDir 'bin\Release\net8.0'
$target = Join-Path $GameDir "mods\$modName"
New-Item -ItemType Directory -Force $target | Out-Null
# Everything the build put in the output folder (DLL, mod.json, web\), except debug symbols.
Get-ChildItem $outDir -Recurse -File | Where-Object Extension -ne '.pdb' | ForEach-Object {
    $dest = Join-Path $target $_.FullName.Substring($outDir.Length + 1)
    New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
    Copy-Item $_.FullName $dest -Force
}
Write-Host "Copied $modName $($manifest.version) to $target"

# The development overview.json lives next to the log, never in the mod folder: the launcher publishes the
# whole mod folder, and 0.1.0-0.1.3 went to the Workshop with a webRoot that only existed on this machine.
$stray = Join-Path $target 'overview.json'
if (Test-Path $stray) { Remove-Item $stray; Write-Host "Removed $stray (it would be published with the mod)" }
$configDir = Join-Path $GameDir 'data\Logs\DW2Overview'
$config = Join-Path $configDir 'overview.json'
$settings = if (Test-Path $config) { Get-Content $config -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
if ($LiveWeb) {
    $settings | Add-Member -Force NoteProperty webRoot (Join-Path $projDir 'web')
    New-Item -ItemType Directory -Force $configDir | Out-Null
    ($settings | ConvertTo-Json) | Set-Content $config -Encoding utf8NoBOM
    Write-Host "overview.json: page served from $(Join-Path $projDir 'web')"
} elseif ($settings.PSObject.Properties['webRoot']) {
    # A plain install serves the installed page, as players get it.
    $settings.PSObject.Properties.Remove('webRoot')
    if (@($settings.PSObject.Properties).Count -eq 0) { Remove-Item $config } else { ($settings | ConvertTo-Json) | Set-Content $config -Encoding utf8NoBOM }
    Write-Host "overview.json: webRoot removed, the installed page is served"
}

if ($NoRegister) { return }

# The game loads the mods listed in mods.json; the launcher keeps a copy per profile (mods.<profile>.json).
$modsDir = Join-Path $GameDir 'mods'
$lists = @(Join-Path $modsDir 'mods.json')
$profileFile = Join-Path $modsDir 'currentProfile.txt'
if (Test-Path $profileFile) {
    $profileName = (Get-Content $profileFile -Raw).Trim()
    $profileList = Join-Path $modsDir "mods.$profileName.json"
    if (Test-Path $profileList) { $lists += $profileList }
}
$entry = "mods/$modName"
foreach ($list in $lists) {
    $json = Get-Content $list -Raw | ConvertFrom-Json
    if ($json.order -contains $entry) { Write-Host "Already listed in $(Split-Path $list -Leaf)"; continue }
    Copy-Item $list "$list.$(Get-Date -Format 'yyyyMMdd-HHmmss').overview_backup"
    $json.order = @($json.order) + $entry
    ($json | ConvertTo-Json -Compress -Depth 5) | Set-Content $list -NoNewline -Encoding utf8NoBOM
    Write-Host "Added $entry to $(Split-Path $list -Leaf) (backup saved next to it)"
}
