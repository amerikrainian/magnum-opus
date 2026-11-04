# After Steam updates Opus Magnum (community edition): what changed, and is the mod still bound?
#   1. refresh the workspace (prepare-game.ps1 parks the previous build under game\builds\<mvid>)
#   2. pull QuintessentialOM/om_mappings into third_party and import it (CE files only)
#   3. NameTool check: the fingerprint matcher on the new build, its agreement with the community
#      mapping when one exists, method bodies against the previous build, the module's references
#   4. rebuild (refgen picks up the new mapping) and run the tests
# Then: read the check output, fix the module where references moved, commit mappings/ when the
# import brought a new obf map, and release (a release bundles the new build's map).
#
#   .\tools\update-game.ps1             full run
#   .\tools\update-game.ps1 -NoPull     skip the om_mappings pull (offline)
param(
    [switch]$NoPull
)

$ErrorActionPreference = "Stop"
$repo = Split-Path $PSScriptRoot
$game = Join-Path $repo "game"
$om = Join-Path $repo "third_party\quintessential-om_mappings"
$tool = Join-Path $repo "tools\NameTool"

# The fingerprint of the build the mod was built against, before anything is refreshed.
$oldFp = Join-Path $game "fingerprint.tsv"
if (Test-Path $oldFp) { Copy-Item $oldFp (Join-Path $game "fingerprint.previous.tsv") -Force; $oldFp = Join-Path $game "fingerprint.previous.tsv" }

if (-not $NoPull) {
    if (Test-Path $om) { git -C $om pull --ff-only | Out-Host }
    else { git clone -q https://github.com/QuintessentialOM/om_mappings.git $om }
    $head = (git -C $om rev-parse HEAD).Trim()
    & dotnet run --project $tool -v q -- import $om --out (Join-Path $repo "mappings") --source "om_mappings@$head"
    if ($LASTEXITCODE -ne 0) { throw "import failed" }
}

# A build the community has not mapped yet makes refgen fail; the check below still runs.
try { & (Join-Path $PSScriptRoot "prepare-game.ps1") 2>&1 | Out-Host }
catch { Write-Warning ("prepare-game: " + $_.Exception.Message) }

$orig = Join-Path $game "Lightning.orig.exe"
$mvid = (& dotnet run --project $tool -v q -- mvid $orig).Trim()
Write-Host "installed build: $mvid"
# Keep only the current build's obf map in the repo (Steam cannot install an older CE build).
Get-ChildItem (Join-Path $repo "mappings\obf") -Filter *.tsv | Where-Object { $_.BaseName -ne $mvid } | Remove-Item

$previous = Get-ChildItem (Join-Path $game "builds") -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne $mvid -and (Test-Path (Join-Path $_.FullName "Lightning.exe")) } |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
$moduleDll = Join-Path $repo "src\MagnumOpus.Module\bin\Debug\MagnumOpus.Module.dll"
$checkArgs = @("check", "--exe", $orig, "--fingerprint", $oldFp, "--mappings", (Join-Path $repo "mappings"))
if ($previous -and (Test-Path (Join-Path $repo "mappings\obf\$($previous.Name).tsv"))) { $checkArgs += @("--old", (Join-Path $previous.FullName "Lightning.exe")) }
if (Test-Path $moduleDll) { $checkArgs += @("--module", $moduleDll) }
if (Test-Path $oldFp) {
    Write-Host "--- matcher check (fingerprint of the previous build) ---"
    & dotnet run --project $tool -v q -- @checkArgs
}

Write-Host "--- rebuild and test ---"
& dotnet build (Join-Path $repo "MagnumOpus.slnx") -v q
if ($LASTEXITCODE -ne 0) { throw "build failed: the module references something the new build lacks (see the errors), or no mapping exists for it yet" }
& dotnet test (Join-Path $repo "src\MagnumOpus.Tests") --no-build
