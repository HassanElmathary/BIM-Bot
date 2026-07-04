# Build & Package the BIM-Bot Installer for distribution
# Run: powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
#
# Output: installer\output\BIMBot-Setup-<version>.exe  (full Inno Setup installer:
#         Revit plugin + MCP server + portable Node.js + Claude auto-config)
#         installer\output\BIMBot-v<version>.zip       (the exe zipped, for hosting
#         on Firebase Spark which forbids raw .exe files)

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

# 1. Build plugin — both frameworks the installer bundles (net48 for Revit
#    2020-2024, net8.0-windows for 2025+)
Write-Host "[1/4] Building Revit Plugin (net48 + net8.0-windows)..."
Push-Location "$root\revit-mcp-plugin\BIMBotPlugin"
dotnet build -c Release -f net48 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "FAILED to build plugin (net48)!"; exit 1 }
dotnet build -c Release -f net8.0-windows 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "FAILED to build plugin (net8.0-windows)!"; exit 1 }
Pop-Location
Write-Host "  [OK] Plugin built"

# 2. Build MCP server
Write-Host "[2/4] Building MCP Server..."
Push-Location "$root\revit-mcp-server"
npm run build 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Host "FAILED to build MCP server!"; exit 1 }
Pop-Location
Write-Host "  [OK] MCP server built"

# 3. Compile the Inno Setup installer (bundles plugin, server, nodejs, assets)
Write-Host "[3/4] Compiling Inno Setup installer..."
$iscc = "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $iscc)) { $iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" }
if (-not (Test-Path $iscc)) { Write-Host "FAILED: ISCC.exe not found — install Inno Setup 6"; exit 1 }
& $iscc $issPath /Q
if ($LASTEXITCODE -ne 0) { Write-Host "FAILED to compile installer!"; exit 1 }
$setupExe = "$root\installer\output\BIMBot-Setup-$version.exe"
if (-not (Test-Path $setupExe)) { Write-Host "FAILED: $setupExe was not produced!"; exit 1 }
$exeSizeMB = [math]::Round((Get-Item $setupExe).Length / 1048576, 1)
Write-Host "  [OK] BIMBot-Setup-$version.exe compiled ($exeSizeMB MB)"

# 4. Zip the installer exe for distribution
Write-Host "[4/4] Creating ZIP archive..."
$zipPath = "$root\installer\output\BIMBot-v$version.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path $setupExe, "$root\LICENSE" -DestinationPath $zipPath -CompressionLevel Optimal
$zipSizeMB = [math]::Round((Get-Item $zipPath).Length / 1048576, 1)
Write-Host "  [OK] ZIP created: $zipSizeMB MB"

# Verify the zip actually contains the setup exe
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
$hasExe = $zip.Entries | Where-Object { $_.Name -eq "BIMBot-Setup-$version.exe" }
$zip.Dispose()
if (-not $hasExe) { Write-Host "FAILED: ZIP does not contain BIMBot-Setup-$version.exe!"; exit 1 }
Write-Host "  [OK] Verified: ZIP contains BIMBot-Setup-$version.exe"

Write-Host ""
Write-Host "============================================"
Write-Host "  BUILD COMPLETE!"
Write-Host "  Installer: $setupExe"
Write-Host "  ZIP:       $zipPath"
Write-Host "============================================"
