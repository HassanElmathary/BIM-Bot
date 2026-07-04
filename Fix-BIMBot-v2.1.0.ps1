# ============================================================
#  BIM-Bot v2.1.0 — Full Repair & Correct Deployment Script
#  Run as Administrator
# ============================================================

$ErrorActionPreference = "Stop"
$Version = "2.1.0"

Write-Host ""
Write-Host "  ========================================" -ForegroundColor Cyan
Write-Host "    BIM-Bot v$Version — Installation Repair" -ForegroundColor Cyan
Write-Host "    by Hassan Ahmed Elmathary" -ForegroundColor Cyan
Write-Host "  ========================================" -ForegroundColor Cyan
Write-Host ""

# Check admin
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "  [ERROR] Please run as Administrator!" -ForegroundColor Red
    pause
    exit 1
}

# Paths
$scriptDir       = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourcePlugin    = Join-Path $scriptDir "revit-mcp-plugin\BIMBotPlugin\bin\Release\net8.0-windows"
$sourceServer    = Join-Path $scriptDir "revit-mcp-server"
$installDir      = "C:\Program Files\BIMBot"
$pluginDest      = "$installDir\plugin\net8"
$serverDest      = "$installDir\server"
$nodejsDest      = "$installDir\nodejs"
$nodeExe         = "$nodejsDest\node.exe"
$serverJs        = "$serverDest\build\index.js"
$claudeConfig    = "$env:APPDATA\Claude\claude_desktop_config.json"

# ── STEP 1: Stop any running BIMBot/RevitMCP processes ──────
Write-Host "  [1/7] Stopping any running server processes..." -ForegroundColor Yellow
Get-Process -Name "node" -ErrorAction SilentlyContinue | Where-Object {
    $_.MainModule.FileName -like "*BIMBot*" -or $_.MainModule.FileName -like "*RevitMCP*"
} | Stop-Process -Force -ErrorAction SilentlyContinue
Write-Host "        Done." -ForegroundColor Green

# ── STEP 2: Clean up old RevitMCP installation ──────────────
Write-Host "  [2/7] Removing old RevitMCP installation..." -ForegroundColor Yellow

# Remove old RevitMCP program files
if (Test-Path "C:\Program Files\RevitMCP") {
    Remove-Item "C:\Program Files\RevitMCP" -Recurse -Force
    Write-Host "        Removed C:\Program Files\RevitMCP" -ForegroundColor DarkGray
}

# Remove conflicting addin files from all Revit versions
$revitVersions = @('2020','2021','2022','2023','2024','2025','2026','2027')
foreach ($year in $revitVersions) {
    $addinDir = "C:\ProgramData\Autodesk\Revit\Addins\$year"
    if (-not (Test-Path $addinDir)) { continue }

    # Remove old RevitMCP.addin
    $legacyAddin = "$addinDir\RevitMCP.addin"
    if (Test-Path $legacyAddin) {
        Remove-Item $legacyAddin -Force
        Write-Host "        Removed legacy RevitMCP.addin for Revit $year" -ForegroundColor DarkGray
    }
    # Remove old RevitMCP folder
    $legacyDir = "$addinDir\RevitMCP"
    if (Test-Path $legacyDir) {
        Remove-Item $legacyDir -Recurse -Force
        Write-Host "        Removed legacy RevitMCP\ folder for Revit $year" -ForegroundColor DarkGray
    }
    # Remove old BIMBot subfolder (wrong location)
    $oldBIMBotDir = "$addinDir\BIMBot"
    if (Test-Path $oldBIMBotDir) {
        Remove-Item $oldBIMBotDir -Recurse -Force
        Write-Host "        Removed old BIMBot\ subfolder for Revit $year" -ForegroundColor DarkGray
    }
}
Write-Host "        Done." -ForegroundColor Green

# ── STEP 3: Deploy v2.1.0 Plugin DLLs ───────────────────────
Write-Host "  [3/7] Deploying BIMBot v$Version plugin DLLs..." -ForegroundColor Yellow

$sourceNet48 = Join-Path $scriptDir "revit-mcp-plugin\BIMBotPlugin\bin\Release\net48"
$sourceNet8  = Join-Path $scriptDir "revit-mcp-plugin\BIMBotPlugin\bin\Release\net8.0-windows"
$sourceNet10 = Join-Path $scriptDir "revit-mcp-plugin\BIMBotPlugin\bin\Release\net10.0-windows"

$destBase = "$installDir\plugin"
$destNet48 = "$destBase\net48"
$destNet8 = "$destBase\net8"
$destNet10 = "$destBase\net10"

# Clear old plugin dir
if (Test-Path $destBase) {
    Remove-Item "$destBase\*" -Recurse -Force -ErrorAction SilentlyContinue
}

# Create plugin subdirectories and copy build outputs
$copiedAny = $false
if (Test-Path $sourceNet48) {
    New-Item -ItemType Directory -Path $destNet48 -Force | Out-Null
    Copy-Item "$sourceNet48\*" "$destNet48\" -Recurse -Force
    $copiedAny = $true
    Write-Host "        Installed net48 to $destNet48" -ForegroundColor Green
}
if (Test-Path $sourceNet8) {
    New-Item -ItemType Directory -Path $destNet8 -Force | Out-Null
    Copy-Item "$sourceNet8\*" "$destNet8\" -Recurse -Force
    $copiedAny = $true
    Write-Host "        Installed net8 to $destNet8" -ForegroundColor Green
}
if (Test-Path $sourceNet10) {
    New-Item -ItemType Directory -Path $destNet10 -Force | Out-Null
    Copy-Item "$sourceNet10\*" "$destNet10\" -Recurse -Force
    $copiedAny = $true
    Write-Host "        Installed net10 to $destNet10" -ForegroundColor Green
}

if (-not $copiedAny) {
    Write-Host "  [ERROR] No plugin build output found!" -ForegroundColor Red
    Write-Host "          Please build the plugin first (open in Visual Studio and Build -> Release)" -ForegroundColor Red
    pause; exit 1
}

# ── STEP 4: Deploy v2.1.0 MCP Server ────────────────────────
Write-Host "  [4/7] Deploying BIMBot v$Version MCP server..." -ForegroundColor Yellow

$serverBuildSrc = "$sourceServer\build"
$serverNodeModSrc = "$sourceServer\node_modules"
$serverPkgSrc = "$sourceServer\package.json"

if (-not (Test-Path $serverBuildSrc)) {
    Write-Host "  [ERROR] Server build not found: $serverBuildSrc" -ForegroundColor Red
    Write-Host "          Run: cd revit-mcp-server && npm run build" -ForegroundColor Red
    pause; exit 1
}

# Replace server files
if (Test-Path "$serverDest\build") { Remove-Item "$serverDest\build" -Recurse -Force }
if (Test-Path "$serverDest\node_modules") { Remove-Item "$serverDest\node_modules" -Recurse -Force }

New-Item -ItemType Directory -Path $serverDest -Force | Out-Null
Copy-Item $serverBuildSrc "$serverDest\build" -Recurse -Force
Copy-Item $serverNodeModSrc "$serverDest\node_modules" -Recurse -Force
Copy-Item $serverPkgSrc "$serverDest\package.json" -Force

$serverVersion = (Get-Content "$serverDest\package.json" | ConvertFrom-Json).version
Write-Host "        Server v$serverVersion installed to $serverDest" -ForegroundColor Green

# ── STEP 5: Write correct .addin files ──────────────────────
Write-Host "  [5/7] Writing correct .addin manifest files..." -ForegroundColor Yellow

$installedForRevit = @()
foreach ($year in $revitVersions) {
    $revitPath = "C:\Program Files\Autodesk\Revit $year"
    if (-not (Test-Path $revitPath)) { continue }

    if ($year -le 2024) {
        $dllPath = "$destNet48\BIMBotPlugin.dll"
        $fw = "net48"
    }
    elseif ($year -le 2026) {
        $dllPath = "$destNet8\BIMBotPlugin.dll"
        $fw = "net8"
    }
    else {
        $dllPath = "$destNet10\BIMBotPlugin.dll"
        $fw = "net10"
    }

    $addinContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>BIM-Bot Plugin</Name>
    <Assembly>$dllPath</Assembly>
    <FullClassName>BIMBotPlugin.Core.Application</FullClassName>
    <ClientId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</ClientId>
    <VendorId>HassanElmathary</VendorId>
    <VendorDescription>AI-Powered BIM-Bot Plugin v$Version by Hassan Ahmed Elmathary</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

    $addinDir = "C:\ProgramData\Autodesk\Revit\Addins\$year"
    New-Item -ItemType Directory -Path $addinDir -Force | Out-Null
    $addinContent | Out-File -Encoding utf8 "$addinDir\BIMBot.addin" -Force
    $installedForRevit += $year
    Write-Host "        Revit $year ($fw) -> $addinDir\BIMBot.addin" -ForegroundColor DarkGray
}

if ($installedForRevit.Count -eq 0) {
    Write-Host "        [WARN] No Revit installation found in C:\Program Files\Autodesk\" -ForegroundColor DarkYellow
} else {
    Write-Host "        Done for Revit: $($installedForRevit -join ', ')" -ForegroundColor Green
}

# ── STEP 6: Fix Claude Desktop Config ───────────────────────
Write-Host "  [6/7] Configuring Claude Desktop..." -ForegroundColor Yellow

$claudeDir = Split-Path $claudeConfig
New-Item -ItemType Directory -Path $claudeDir -Force | Out-Null

$nodeExeEsc = $nodeExe.Replace('\','\\')
$serverJsEsc = $serverJs.Replace('\','\\')

$newConfig = @"
{
  "mcpServers": {
    "BIM-Bot": {
      "command": "$nodeExeEsc",
      "args": ["$serverJsEsc"],
      "env": {}
    }
  }
}
"@

# Check for existing config with other entries
if (Test-Path $claudeConfig) {
    $existingRaw = Get-Content $claudeConfig -Raw -ErrorAction SilentlyContinue
    try {
        $existing = $existingRaw | ConvertFrom-Json
        # Merge: keep other servers, replace/add BIM-Bot
        if (-not $existing.mcpServers) {
            $existing | Add-Member -MemberType NoteProperty -Name "mcpServers" -Value @{}
        }
        $bimBotEntry = @{
            command = $nodeExe
            args    = @($serverJs)
            env     = @{}
        }
        $existing.mcpServers | Add-Member -MemberType NoteProperty -Name "BIM-Bot" -Value $bimBotEntry -Force
        # Remove old revit-mcp entry if present
        $existing.mcpServers.PSObject.Properties.Remove("revit-mcp")
        $existing | ConvertTo-Json -Depth 10 | Out-File $claudeConfig -Encoding utf8 -Force
        Write-Host "        Merged into existing Claude config (removed old revit-mcp entry)" -ForegroundColor Green
    } catch {
        # Config is malformed or complex — just write fresh
        $newConfig | Out-File $claudeConfig -Encoding utf8 -Force
        Write-Host "        Wrote fresh Claude config" -ForegroundColor Green
    }
} else {
    $newConfig | Out-File $claudeConfig -Encoding utf8 -Force
    Write-Host "        Created Claude config at $claudeConfig" -ForegroundColor Green
}

# ── STEP 7: Create launcher .bat ────────────────────────────
Write-Host "  [7/7] Creating launcher script..." -ForegroundColor Yellow
$launcher = @"
@echo off
title BIM-Bot MCP Server v$Version
echo.
echo   ==========================================
echo     BIM-Bot Server v$Version
echo     by Hassan Ahmed Elmathary
echo   ==========================================
echo.
echo   Starting MCP Server...
echo   Press Ctrl+C to stop.
echo.
"$nodeExe" "$serverJs"
pause
"@
$launcher | Out-File "$installDir\Start MCP Server.bat" -Encoding ascii -Force
Write-Host "        Created: $installDir\Start MCP Server.bat" -ForegroundColor Green

# ── VERIFICATION ─────────────────────────────────────────────
Write-Host ""
Write-Host "  ===========================================" -ForegroundColor Cyan
Write-Host "    Verification" -ForegroundColor Cyan
Write-Host "  ===========================================" -ForegroundColor Cyan

$ok = $true

# Check DLLs
$anyDll = $false
foreach ($fw in @("net48", "net8", "net10")) {
    $dll = Get-Item "$destBase\$fw\BIMBotPlugin.dll" -ErrorAction SilentlyContinue
    if ($dll) {
        Write-Host "  [OK] $fw\BIMBotPlugin.dll ($([Math]::Round($dll.Length/1KB)) KB)" -ForegroundColor Green
        $anyDll = $true
    }
}
if (-not $anyDll) {
    Write-Host "  [FAIL] No BIMBotPlugin.dll found in any framework folder!" -ForegroundColor Red; $ok = $false
}

# Check addin content
foreach ($year in $installedForRevit) {
    $addinFile = "C:\ProgramData\Autodesk\Revit\Addins\$year\BIMBot.addin"
    if ((Get-Content $addinFile -Raw) -match "BIMBotPlugin.dll") {
        Write-Host "  [OK] Revit $year addin -> BIMBotPlugin.dll" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] Revit $year addin is wrong!" -ForegroundColor Red; $ok = $false
    }
    # Make sure old RevitMCP.addin is gone
    if (-not (Test-Path "C:\ProgramData\Autodesk\Revit\Addins\$year\RevitMCP.addin")) {
        Write-Host "  [OK] Revit $year - old RevitMCP.addin removed" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] Revit $year - old RevitMCP.addin still exists!" -ForegroundColor Red; $ok = $false
    }
}

# Check server version
$installedPkg = Get-Content "$serverDest\package.json" -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json
if ($installedPkg.version -eq $Version) {
    Write-Host "  [OK] Server v$($installedPkg.version) installed" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Server version mismatch! Got: $($installedPkg.version)" -ForegroundColor Red; $ok = $false
}

# Check Claude config
$claudeJson = Get-Content $claudeConfig -Raw | ConvertFrom-Json
if ($claudeJson.mcpServers."BIM-Bot".command -like "*BIMBot*") {
    Write-Host "  [OK] Claude Desktop configured for BIM-Bot" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Claude Desktop config is wrong!" -ForegroundColor Red; $ok = $false
}

# Check node exe
if (Test-Path $nodeExe) {
    Write-Host "  [OK] Node.js runtime found: $nodeExe" -ForegroundColor Green
} else {
    Write-Host "  [FAIL] Node.js not found at $nodeExe!" -ForegroundColor Red; $ok = $false
}

Write-Host ""
if ($ok) {
    Write-Host "  ===========================================" -ForegroundColor Green
    Write-Host "    SUCCESS! BIM-Bot v$Version is ready!" -ForegroundColor Green
    Write-Host "  ===========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "  Next Steps:" -ForegroundColor Cyan
    Write-Host "    1. RESTART Revit 2026" -ForegroundColor White
    Write-Host "       -> Look for 'BIM-Bot' tab in the ribbon" -ForegroundColor White
    Write-Host "    2. RESTART Claude Desktop" -ForegroundColor White
    Write-Host "       -> BIM-Bot tools will appear automatically" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "  ===========================================" -ForegroundColor Red
    Write-Host "    Some checks FAILED. Review errors above." -ForegroundColor Red
    Write-Host "  ===========================================" -ForegroundColor Red
}

Write-Host ""
pause
