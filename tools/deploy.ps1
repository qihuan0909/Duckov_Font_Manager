# Build DuckovFontManager and copy it into the game's local Mods folder.
# Usage: powershell -ExecutionPolicy Bypass -File tools\deploy.ps1 [-GamePath "X:\...\Escape from Duckov"] [-Configuration Release] [-SkipFonts]
param(
    [string]$GamePath = "E:\SteamLibrary\steamapps\common\Escape from Duckov",
    [string]$Configuration = "Release",
    [switch]$SkipFonts
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$proj = Join-Path $root "src\DuckovFontManager\DuckovFontManager.csproj"

dotnet build $proj -c $Configuration -p:DuckovPath="$GamePath" --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed" }

$out = Join-Path $root "src\DuckovFontManager\bin\$Configuration"
$modDir = Join-Path $GamePath "Duckov_Data\Mods\DuckovFontManager"
New-Item -ItemType Directory -Force $modDir | Out-Null

Copy-Item (Join-Path $out "DuckovFontManager.dll") $modDir -Force
Copy-Item (Join-Path $root "lib\0Harmony.dll") $modDir -Force
Copy-Item (Join-Path $root "assets\info.ini") $modDir -Force
Copy-Item (Join-Path $root "assets\preview.png") $modDir -Force
Copy-Item (Join-Path $root "assets\THIRD-PARTY-NOTICES.txt") $modDir -Force

# Package copy for distribution: exactly the shipped files (nothing the game or a user added to the mod folder).
$release = Join-Path $root "release\DuckovFontManager"
if (Test-Path $release) { Remove-Item -Recurse -Force $release }
New-Item -ItemType Directory -Force $release | Out-Null
foreach ($f in "DuckovFontManager.dll", "0Harmony.dll", "info.ini", "preview.png", "THIRD-PARTY-NOTICES.txt") {
    Copy-Item (Join-Path $modDir $f) $release -Force
}

if (-not $SkipFonts -and (Test-Path (Join-Path $root "customfont"))) {
    $fontDir = Join-Path $GamePath "ModConfigs\DuckovFontManager\Fonts"
    New-Item -ItemType Directory -Force $fontDir | Out-Null
    Get-ChildItem (Join-Path $root "customfont") -File | Where-Object { $_.Extension -in ".ttf", ".otf", ".ttc" } |
        ForEach-Object { Copy-Item $_.FullName $fontDir -Force }
}
Write-Host "Deployed to $modDir"
