# Build the distributable mod zip: the Release build's shipping file set laid out as the game
# folder. The zip root IS the game folder, so the installer (and a manual user) extracts it
# straight into the game dir:
#   Lightning.exe.config, MagnumOpus.dll, MagnumOpus.Contracts.dll, MagnumOpus.Names.dll,
#   Mono.Cecil.dll, 0Harmony.dll, prism.dll, steam_appid.txt
#   MagnumOpus\MagnumOpus.Core.dll, MagnumOpus\MagnumOpus.Module.dll, MagnumOpus\lang\*.txt
#   MagnumOpus\mappings\named\** (intermediary -> human names), mappings\obf\<mvid>.tsv (the
#     current build), mappings\fingerprint.tsv (the matcher's reference for newer builds)
#   MagnumOpusDocs\ (the rendered mdbook manual, docs_src)
# A Release build carries no dev tooling (no dev server, no Mono.CSharp): refused if it leaks.
#
# Adapted from the Non-Visual Calculus installer by Rashad Naqeeb (MIT),
# https://github.com/rashadnaqeeb/NonVisualCalculus - by way of the Harkest Dungeon and
# Echopunks tooling.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
[xml]$props = Get-Content (Join-Path $root "Directory.Build.props")
$version = $props.SelectSingleNode("/Project/PropertyGroup/Version").InnerText.Trim()
if ([string]::IsNullOrWhiteSpace($version)) { throw "Could not read Version from Directory.Build.props" }

$releaseDir = Join-Path $root "releases"
$stageDir = Join-Path $root "obj\release-stage"
$hostOut = Join-Path $root "src\MagnumOpus\bin\Release"
$moduleOut = Join-Path $root "src\MagnumOpus.Module\bin\Release"
$bookDir = Join-Path $root "docs_src"
$fingerprint = Join-Path $root "game\fingerprint.tsv"
$zipPath = Join-Path $releaseDir "MagnumOpus-v$version.zip"

foreach ($dir in @($hostOut, $moduleOut)) { if (Test-Path $dir) { Remove-Item -LiteralPath $dir -Recurse -Force } }

Push-Location $root
try {
    dotnet build MagnumOpus.slnx -c Release -v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Release build failed with exit code $LASTEXITCODE (the module needs the game installed: see CLAUDE.md)" }
    if (Test-Path (Join-Path $bookDir "book.toml")) {
        mdbook build $bookDir
        if ($LASTEXITCODE -ne 0) { throw "Docs build failed with exit code $LASTEXITCODE" }
    }

    $rootFiles = @(
        (Join-Path $root "deploy\Lightning.exe.config"),
        (Join-Path $hostOut "MagnumOpus.dll"),
        (Join-Path $hostOut "MagnumOpus.Contracts.dll"),
        (Join-Path $hostOut "MagnumOpus.Names.dll"),
        (Join-Path $hostOut "Mono.Cecil.dll"),
        (Join-Path $hostOut "0Harmony.dll"),
        (Join-Path $root "third_party\prism\prism.dll")
    )
    $modFiles = @((Join-Path $moduleOut "MagnumOpus.Core.dll"), (Join-Path $moduleOut "MagnumOpus.Module.dll"))
    foreach ($f in $rootFiles + $modFiles + @($fingerprint)) { if (-not (Test-Path $f)) { throw "Release output not found: $f" } }
    foreach ($leak in @("Mono.CSharp.dll", "System.Drawing.dll")) {
        if (Test-Path (Join-Path $hostOut $leak)) { throw "$leak in the Release output: dev tooling leaked into the shipping build." }
    }

    if (Test-Path $stageDir) { Remove-Item -LiteralPath $stageDir -Recurse -Force }
    $modDir = Join-Path $stageDir "MagnumOpus"
    New-Item -ItemType Directory -Force $modDir, $releaseDir | Out-Null
    foreach ($f in $rootFiles) { Copy-Item -LiteralPath $f -Destination $stageDir }
    Set-Content -LiteralPath (Join-Path $stageDir "steam_appid.txt") -Value "558990" -NoNewline -Encoding ascii
    foreach ($f in $modFiles) { Copy-Item -LiteralPath $f -Destination $modDir }
    Copy-Item -Path (Join-Path $root "lang") -Destination (Join-Path $modDir "lang") -Recurse
    New-Item -ItemType Directory -Force (Join-Path $modDir "mappings\obf") | Out-Null
    Copy-Item -Path (Join-Path $root "mappings\named") -Destination (Join-Path $modDir "mappings\named") -Recurse
    Copy-Item -Path (Join-Path $root "mappings\obf\*.tsv") -Destination (Join-Path $modDir "mappings\obf")
    Copy-Item -LiteralPath $fingerprint -Destination (Join-Path $modDir "mappings")
    if (Test-Path (Join-Path $bookDir "book")) {
        Copy-Item -Path (Join-Path $bookDir "book") -Destination (Join-Path $stageDir "MagnumOpusDocs") -Recurse
    }

    if (Test-Path $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
    Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -Force
    Remove-Item -LiteralPath $stageDir -Recurse -Force
    Write-Host "Release zip: $zipPath"
}
finally {
    Pop-Location
}
