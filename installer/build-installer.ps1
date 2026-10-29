# Build & Package the BIM-Bot Installer for distribution
# Run: powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
#
# Output: installer\output\BIM-Bot.exe  (full Inno Setup installer: Revit +
#         Navisworks plugins, MCP server, Unreal MCP server, portable Node.js,
#         Claude auto-config)

Write-Host "============================================"
Write-Host "  BIM-Bot Installer - Build Script"
Write-Host "============================================"
Write-Host ""

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path "$root\revit-mcp-plugin")) { $root = $PSScriptRoot | Split-Path }

# Read version from setup.iss so exe/zip names always match
$issPath = "$root\installer\setup.iss"
$version = (Select-String -Path $issPath -Pattern '#define\s+MyAppVersion\s+"([^"]+)"').Matches[0].Groups[1].Value
Write-Host "Version: $version"
Write-Host ""

# 1. Build the plugin once per Revit version. Each band compiles against that
#    year's Revit API with its own compat symbols — see BIMBotPlugin.csproj.
$revitBands = @('2020', '2021', '2022', '2023', '2024', '2025', '2026', '2027')
Write-Host "[1/4] Building Revit Plugin ($($revitBands.Count) bands: $($revitBands[0])-$($revitBands[-1]))..."
Push-Location "$root\revit-mcp-plugin\BIMBotPlugin"
foreach ($band in $revitBands) {
  # cmd /c keeps native stderr (warnings, notices) from becoming terminating
  # errors under Windows PowerShell 5.1 + ErrorActionPreference=Stop
  cmd /c "dotnet build -c Release -p:RevitVersion=$band >nul 2>&1"
  if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "FAILED to build plugin for Revit $band!"; exit 1 }
  Write-Host "  [OK] Revit $band"
}
Pop-Location
Write-Host "  [OK] All plugin bands built"

# 1b. Build the Navisworks plugin once PER Manage year (2020-2027): each year's
#     API is strong-named with its own version, so one DLL cannot serve every year.
#     API references come from the Speckle.Navisworks.API NuGet package per year.
Write-Host "[1b/4] Building Navisworks Plugin (8 years: 2020-2027)..."
Push-Location "$root\navis-mcp-plugin\BIMBotNavisPlugin"
foreach ($navisYear in 2020..2027) {
    cmd /c "dotnet build -c Release -p:NavisYear=$navisYear >nul 2>&1"
    if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "FAILED to build Navisworks plugin for $navisYear!"; exit 1 }
    if (-not (Test-Path "bin\N$navisYear\Release\net48\BIMBotNavisPlugin.dll")) { Pop-Location; Write-Host "Navisworks $navisYear build produced no DLL!"; exit 1 }
    Write-Host "  [OK] Navisworks $navisYear"
}
Pop-Location
Write-Host "  [OK] All Navisworks plugin years built"

# 2. Build MCP server
Write-Host "[2/4] Building MCP Server..."
Push-Location "$root\revit-mcp-server"
cmd /c "npm run build >nul 2>&1"
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "FAILED to build MCP server!"; exit 1 }
Pop-Location
Write-Host "  [OK] MCP server built"

# 2b. Build the Unreal MCP server as a single-file exe (Node SEA). The exe
#     embeds the node.exe that runs this script, so that node must be 20+.
Write-Host "[2b/4] Building Unreal MCP Server..."
Push-Location "$root\unreal-mcp-server"
if (-not (Test-Path "node_modules")) { cmd /c "npm ci >nul 2>&1" }
cmd /c "npm test >nul 2>&1"
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "FAILED: Unreal MCP server tests!"; exit 1 }
$unrealStart = Get-Date
cmd /c "npm run build:exe >nul 2>&1"
$unrealExe = "$root\unreal-mcp-server\dist\BIM-Bot-Unreal.exe"
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $unrealExe) -or (Get-Item $unrealExe).LastWriteTime -lt $unrealStart) {
    Pop-Location; Write-Host "FAILED to build BIM-Bot-Unreal.exe!"; exit 1
}
Pop-Location
Write-Host "  [OK] BIM-Bot-Unreal.exe built"

# 3. Compile the Inno Setup installer (bundles plugin, server, nodejs, assets)
Write-Host "[3/4] Compiling Inno Setup installer..."
$iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { $iscc = "$env:LOCALAPPDATA\Programs\Inno\ISCC.exe" }
if (-not (Test-Path $iscc)) { $iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $iscc)) { Write-Host "FAILED: ISCC.exe not found - install Inno Setup 6"; exit 1 }
$isccStart = Get-Date
& $iscc $issPath /Q
if ($LASTEXITCODE -ne 0) { Write-Host "FAILED to compile installer!"; exit 1 }
$setupExe = "$root\installer\output\BIM-Bot.exe"
if (-not (Test-Path $setupExe) -or (Get-Item $setupExe).LastWriteTime -lt $isccStart) { Write-Host "FAILED: $setupExe was not produced!"; exit 1 }
$exeSizeMB = [math]::Round((Get-Item $setupExe).Length / 1048576, 1)
Write-Host "  [OK] BIM-Bot.exe v$version compiled ($exeSizeMB MB)"

# 4. Done
Write-Host ""
Write-Host "============================================"
Write-Host "  BUILD COMPLETE!"
Write-Host "  Installer: $setupExe"
Write-Host "============================================"
