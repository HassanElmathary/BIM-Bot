# Build & Package the BIM-Bot Installer for distribution
# Run: powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
#
# Output: installer\output\BIMBot-Setup-<version>.exe  (full Inno Setup installer:
#         Revit plugin + MCP server + portable Node.js + Claude auto-config)

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

# 1. Build plugin - both frameworks the installer bundles (net48 for Revit
#    2020-2024, net8.0-windows for 2025+)
Write-Host "[1/4] Building Revit Plugin (net48 + net8.0-windows)..."
Push-Location "$root\revit-mcp-plugin\BIMBotPlugin"
# cmd /c keeps native stderr (warnings, notices) from becoming terminating
# errors under Windows PowerShell 5.1 + ErrorActionPreference=Stop
cmd /c "dotnet build -c Release -f net48 >nul 2>&1"
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "FAILED to build plugin (net48)!"; exit 1 }
cmd /c "dotnet build -c Release -f net8.0-windows >nul 2>&1"
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "FAILED to build plugin (net8.0-windows)!"; exit 1 }
Pop-Location
Write-Host "  [OK] Plugin built"

# 2. Build MCP server
Write-Host "[2/4] Building MCP Server..."
Push-Location "$root\revit-mcp-server"
cmd /c "npm run build >nul 2>&1"
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "FAILED to build MCP server!"; exit 1 }
Pop-Location
Write-Host "  [OK] MCP server built"

# 3. Compile the Inno Setup installer (bundles plugin, server, nodejs, assets)
Write-Host "[3/4] Compiling Inno Setup installer..."
$iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { $iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $iscc)) { Write-Host "FAILED: ISCC.exe not found - install Inno Setup 6"; exit 1 }
& $iscc $issPath /Q
if ($LASTEXITCODE -ne 0) { Write-Host "FAILED to compile installer!"; exit 1 }
$setupExe = "$root\installer\output\BIMBot-Setup-$version.exe"
if (-not (Test-Path $setupExe)) { Write-Host "FAILED: $setupExe was not produced!"; exit 1 }
$exeSizeMB = [math]::Round((Get-Item $setupExe).Length / 1048576, 1)
Write-Host "  [OK] BIMBot-Setup-$version.exe compiled ($exeSizeMB MB)"

# 4. Done
Write-Host ""
Write-Host "============================================"
Write-Host "  BUILD COMPLETE!"
Write-Host "  Installer: $setupExe"
Write-Host "============================================"
