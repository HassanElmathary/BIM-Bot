# Build & Package the Revit MCP Installer for distribution
# Run: powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1

Write-Host "============================================"
Write-Host "  Revit MCP Installer - Build Script"
Write-Host "============================================"
Write-Host ""

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not (Test-Path "$root\revit-mcp-plugin")) { $root = $PSScriptRoot | Split-Path }

# 1. Build plugin
Write-Host "[1/4] Building Revit Plugin..."
Push-Location "$root\revit-mcp-plugin\RevitMCPPlugin"
dotnet build -c Release 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "FAILED to build plugin!"; exit 1 }
Pop-Location
Write-Host "  [OK] Plugin built"

# 2. Build installer
Write-Host "[2/4] Building Installer..."
Push-Location "$root\installer\RevitMCPInstaller"
dotnet build -c Release 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host "FAILED to build installer!"; exit 1 }
Pop-Location
Write-Host "  [OK] Installer built"

# 3. Create distribution package
Write-Host "[3/4] Packaging distribution..."
$distDir = "$root\installer\output\RevitMCP-Setup"
if (Test-Path $distDir) { Remove-Item $distDir -Recurse -Force }
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

# Copy installer EXE
Copy-Item "$root\installer\RevitMCPInstaller\bin\Release\net48\RevitMCP-Setup.exe" -Destination $distDir
Copy-Item "$root\installer\RevitMCPInstaller\bin\Release\net48\Newtonsoft.Json.dll" -Destination $distDir

# Copy plugin files
$pluginDir = "$distDir\plugin"
New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
Copy-Item "$root\revit-mcp-plugin\RevitMCPPlugin\bin\Release\net48\RevitMCPPlugin.dll" -Destination $pluginDir
Copy-Item "$root\revit-mcp-plugin\RevitMCPPlugin\bin\Release\net48\Newtonsoft.Json.dll" -Destination $pluginDir

# Copy MCP server (build + node_modules + package.json)
$serverDest = "$distDir\server"
New-Item -ItemType Directory -Path $serverDest -Force | Out-Null
if (Test-Path "$root\revit-mcp-server\build") {
    Copy-Item "$root\revit-mcp-server\build" -Destination "$serverDest\build" -Recurse
}
if (Test-Path "$root\revit-mcp-server\node_modules") {
    Copy-Item "$root\revit-mcp-server\node_modules" -Destination "$serverDest\node_modules" -Recurse
}
if (Test-Path "$root\revit-mcp-server\package.json") {
    Copy-Item "$root\revit-mcp-server\package.json" -Destination $serverDest
}

# Copy Node.js portable
if (Test-Path "$root\installer\nodejs") {
    Copy-Item "$root\installer\nodejs" -Destination "$distDir\nodejs" -Recurse
}

Write-Host "  [OK] Distribution packaged"

# 4. Create ZIP
Write-Host "[4/4] Creating ZIP archive..."
$zipPath = "$root\installer\output\RevitMCP-Setup-v1.1.0.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$distDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
$zipSizeBytes = (Get-Item $zipPath).Length
$zipSizeMB = [math]::Round($zipSizeBytes / 1048576, 1)
Write-Host "  [OK] ZIP created: $zipSizeMB MB"

Write-Host ""
Write-Host "============================================"
Write-Host "  BUILD COMPLETE!"
Write-Host "  Output: $zipPath"
Write-Host "============================================"
