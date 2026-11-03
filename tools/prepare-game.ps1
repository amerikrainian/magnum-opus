# Rebuilds the game analysis workspace (game\, gitignored; see CLAUDE.md "Analysis workspace"):
#   game\Lightning.orig.exe        byte copy of the installed (community-edition) exe
#   game\Lightning.named.ref.exe   the module's compile reference (human names, public)
#   game\Lightning.named.exe       the same names, unpublicized (input to de4dot)
#   game\Lightning.named-deob.exe  de4dot over it: strings decrypted, human names kept
#   game\decompiled\               ilspycmd project of the named-deob exe (-Decompile)
#   game\builds\<mvid>\            the previous build's exe, parked when the game updated
# The module build regenerates the reference by itself; this script is for analysis and updates.
#
#   .\tools\prepare-game.ps1              refresh what is stale
#   .\tools\prepare-game.ps1 -Decompile   also rebuild game\decompiled (needs ilspycmd)
#   .\tools\prepare-game.ps1 -Force       redo everything
param(
    [string]$GameDir = $env:MAGNUMOPUS_DIR,
    [switch]$Force,
    [switch]$Decompile
)

$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot
$game = Join-Path $repo "game"
$de4dotDir = Join-Path $repo "tools\de4dot"
$de4dotZip = Join-Path $repo "third_party\de4dot\de4dot.zip"
$de4dotZipSha = "C726CBD18B894CA63B7F6A565C6C86EF512B96E68119C6502CDF64A51F6A1C78"

if (-not $GameDir) {
    $steam = (Get-ItemProperty "HKCU:\Software\Valve\Steam" -ErrorAction SilentlyContinue).SteamPath
    if ($steam) { $GameDir = Join-Path $steam "steamapps\common\Opus Magnum" }
    if (-not $GameDir -or -not (Test-Path $GameDir)) { $GameDir = "C:\Program Files (x86)\Steam\steamapps\common\Opus Magnum" }
}
$gameExe = Join-Path $GameDir "Lightning.exe"
if (-not (Test-Path $gameExe)) { throw "Lightning.exe not found in '$GameDir'. Install the game, or pass -GameDir / set MAGNUMOPUS_DIR." }
New-Item -ItemType Directory -Force $game | Out-Null

# 0. The branch: the mod targets community-edition (the build the community maps).
$manifest = Join-Path (Split-Path (Split-Path $GameDir)) "appmanifest_558990.acf"
if (Test-Path $manifest) {
    $beta = (Select-String -Path $manifest -Pattern '"BetaKey"\s+"([^"]*)"' | Select-Object -First 1)
    $branch = if ($beta) { $beta.Matches[0].Groups[1].Value } else { "public" }
    if ($branch -ne "community-edition") { Write-Warning "Steam branch is '$branch', not 'community-edition'. The mod targets community-edition." }
}

# 1. The original exe; park the previous build when the installed one differs.
$orig = Join-Path $game "Lightning.orig.exe"
$changed = $Force -or -not (Test-Path $orig) -or ((Get-FileHash $gameExe).Hash -ne (Get-FileHash $orig).Hash)
if ($changed) {
    if (Test-Path $orig) {
        $oldMvid = & dotnet run --project (Join-Path $repo "tools\NameTool") -v q -- mvid $orig
        if ($LASTEXITCODE -eq 0 -and $oldMvid) {
            $park = Join-Path $game "builds\$($oldMvid.Trim())"
            New-Item -ItemType Directory -Force $park | Out-Null
            Copy-Item $orig (Join-Path $park "Lightning.exe") -Force
            Write-Host "parked previous build: game\builds\$($oldMvid.Trim())"
        }
    }
    Copy-Item $gameExe $orig -Force
    Write-Host "copied: Lightning.orig.exe"
} else {
    Write-Host "up to date: Lightning.orig.exe"
}
Copy-Item (Join-Path $GameDir "Steamworks.NET.dll") $game -Force

# 2. The named reference and analysis exe.
& dotnet run --project (Join-Path $repo "tools\NameTool") -v q -- refgen --exe $orig --mappings (Join-Path $repo "mappings") `
    --out (Join-Path $game "Lightning.named.ref.exe") --analysis (Join-Path $game "Lightning.named.exe")
if ($LASTEXITCODE -ne 0) { throw "refgen failed (no mapping for this build? see the message above)." }

# 3. de4dot (vendored, hash-checked) over the named exe: decrypts strings, keeps our names.
$de4dot = Join-Path $de4dotDir "de4dot-x64.exe"
if (-not (Test-Path $de4dot)) {
    $actual = (Get-FileHash $de4dotZip -Algorithm SHA256).Hash
    if ($actual -ne $de4dotZipSha) { throw "de4dot.zip SHA256 mismatch (got $actual)." }
    New-Item -ItemType Directory -Force $de4dotDir | Out-Null
    Expand-Archive $de4dotZip $de4dotDir -Force
}
$named = Join-Path $game "Lightning.named.exe"
$namedDeob = Join-Path $game "Lightning.named-deob.exe"
if ($Force -or -not (Test-Path $namedDeob) -or (Get-Item $named).LastWriteTimeUtc -gt (Get-Item $namedDeob).LastWriteTimeUtc) {
    & $de4dot $named -o $namedDeob | Select-String "Detected|ERROR|Saving" | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "de4dot failed (exit $LASTEXITCODE)." }
} else {
    Write-Host "up to date: Lightning.named-deob.exe"
}

# 4. Optional: the decompile.
if ($Decompile) {
    $ilspy = Get-Command ilspycmd -ErrorAction SilentlyContinue
    if (-not $ilspy) { throw "ilspycmd not found - install with: dotnet tool install -g ilspycmd" }
    $out = Join-Path $game "decompiled"
    if (Test-Path $out) { Remove-Item -Recurse -Force $out }
    Write-Host "decompiling..."
    & $ilspy.Source $namedDeob -p -o $out 2>&1 | Out-File (Join-Path $game "decompile.log")
    Write-Host "decompiled -> game\decompiled ($((Get-ChildItem $out -Filter *.cs).Count) files)"
}
