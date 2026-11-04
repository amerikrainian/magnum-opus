# build.ps1 - Build Magnum Opus (Debug) and deploy it into the game. The deploy is the Debug
# post-build targets of the host (MagnumOpus.dll, Contracts, Names, Mono.Cecil, 0Harmony,
# prism.dll, Lightning.exe.config, steam_appid.txt, MagnumOpus\mappings) and the module
# (MagnumOpus\MagnumOpus.Core.dll, MagnumOpus.Module.dll, lang\, mappings\fingerprint.tsv).
# Close the game first, or the HOST copy is skipped (file locked) and you run a stale host;
# the module deploys with the game running (it is byte-loaded: POST /reload or F6).
#
# Adapted from the Non-Visual Calculus installer by Rashad Naqeeb (MIT),
# https://github.com/rashadnaqeeb/NonVisualCalculus - by way of the Harkest Dungeon and
# Echopunks tooling.
param(
    [switch]$ModuleOnly,
    [switch]$Help
)

if ($Help) {
    Write-Host "Usage: .\build.ps1 [-ModuleOnly] [-Help]"
    Write-Host "  Builds the solution (Debug) and deploys the mod into the game folder."
    Write-Host "  -ModuleOnly builds Core + Module only (hot-reloadable with the game running)."
    Write-Host "  Set MAGNUMOPUS_DIR to point at a non-default game folder."
    exit 0
}

$ErrorActionPreference = "Stop"
$GameExe = "Lightning.exe"
$GameFolder = "Opus Magnum"

# --- Locate the game install: MAGNUMOPUS_DIR, then every Steam library ---
$Game = $env:MAGNUMOPUS_DIR
if (-not $Game) {
    $RegSteam = (Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name InstallPath -ErrorAction SilentlyContinue).InstallPath
    $DefaultSteam = if ($RegSteam) { $RegSteam } else { "C:\Program Files (x86)\Steam" }
    $SteamPaths = @()
    if (Test-Path "$DefaultSteam\steamapps") { $SteamPaths += $DefaultSteam }
    $LibFolders = "$DefaultSteam\steamapps\libraryfolders.vdf"
    if (Test-Path $LibFolders) {
        $content = Get-Content $LibFolders -Raw
        [regex]::Matches($content, '"path"\s+"([^"]+)"') | ForEach-Object {
            $p = $_.Groups[1].Value -replace '\\\\', '\'
            if ($p -ne $DefaultSteam -and (Test-Path "$p\steamapps")) { $SteamPaths += $p }
        }
    }
    foreach ($steam in $SteamPaths) {
        $candidate = "$steam\steamapps\common\$GameFolder"
        if (Test-Path "$candidate\$GameExe") { $Game = $candidate; break }
    }
    if (-not $Game) { $Game = "C:\Program Files (x86)\Steam\steamapps\common\$GameFolder" }
}
if (-not (Test-Path "$Game\$GameExe")) {
    Write-Host "ERROR: Opus Magnum not found at: $Game" -ForegroundColor Red
    Write-Host "Set the MAGNUMOPUS_DIR environment variable to the game folder." -ForegroundColor Red
    exit 1
}

$target = if ($ModuleOnly) { "$PSScriptRoot\src\MagnumOpus.Module\MagnumOpus.Module.csproj" } else { "$PSScriptRoot\MagnumOpus.slnx" }
Write-Host "Building $(Split-Path -Leaf $target) (game: $Game)..." -ForegroundColor Cyan
dotnet build $target -c Debug -p:GameDir="$Game"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED." -ForegroundColor Red
    exit 1
}
Write-Host ""
Write-Host "Done. Launch Opus Magnum (community-edition branch) and listen for `"Magnum Opus ... loaded`"." -ForegroundColor Cyan
