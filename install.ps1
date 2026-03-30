# ============================================================
#  Revit MCP — One-Line Global Installer
#  Usage: irm https://raw.githubusercontent.com/HassanElmathary/Revit-MCP/main/install.ps1 | iex
# ============================================================

param(
    [string]$InstallPath = "",
    [switch]$SkipClaudeConfig,
    [switch]$SkipRevitPlugin,
    [switch]$Uninstall
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"  # Faster downloads

# ── Branding ─────────────────────────────────────────────────
$VERSION = "2.0.1"
$REPO = "HassanElmathary/Revit-MCP"
$RELEASE_URL = "https://github.com/$REPO/releases/latest/download/RevitMCP-v$VERSION.zip"
$RAW_BASE = "https://raw.githubusercontent.com/$REPO/main"

function Write-Banner {
    Write-Host ""
    Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "  ║                                                  ║" -ForegroundColor Cyan
    Write-Host "  ║        🏗️  Revit MCP — AI-Powered BIM           ║" -ForegroundColor Cyan
    Write-Host "  ║           179 Tools for Autodesk Revit           ║" -ForegroundColor Cyan
    Write-Host "  ║                                                  ║" -ForegroundColor Cyan
    Write-Host "  ║        by Hassan Ahmed Elmathary                 ║" -ForegroundColor Cyan
    Write-Host "  ║        v$VERSION                                    ║" -ForegroundColor Cyan
    Write-Host "  ║                                                  ║" -ForegroundColor Cyan
    Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step {
    param([string]$Step, [string]$Message)
    Write-Host "  [$Step]" -ForegroundColor Yellow -NoNewline
    Write-Host " $Message" -ForegroundColor White
}

function Write-OK {
    param([string]$Message)
    Write-Host "        ✅ $Message" -ForegroundColor Green
}

function Write-Skip {
    param([string]$Message)
    Write-Host "        ⏭️  $Message" -ForegroundColor DarkGray
}

function Write-Warn {
    param([string]$Message)
    Write-Host "        ⚠️  $Message" -ForegroundColor Yellow
}

function Write-Err {
    param([string]$Message)
    Write-Host "        ❌ $Message" -ForegroundColor Red
}

# ── Helpers ──────────────────────────────────────────────────

function Test-Admin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-InstallDir {
    if ($InstallPath) { return $InstallPath }
    if (Test-Admin) {
        return Join-Path $env:ProgramFiles "RevitMCP"
    } else {
        return Join-Path $env:LOCALAPPDATA "RevitMCP"
    }
}

function Get-InstalledRevitVersions {
    $versions = @()
    $pf = $env:ProgramFiles
    for ($year = 2020; $year -le 2027; $year++) {
        $revitPath = Join-Path $pf "Autodesk\Revit $year"
        if (Test-Path $revitPath) {
            $versions += $year
        }
    }
    return $versions
}

function Get-FrameworkForYear([int]$Year) {
    if ($Year -le 2024) { return "net48" } else { return "net8" }
}

function Get-AddinsDir([int]$Year) {
    return Join-Path $env:ProgramData "Autodesk\Revit\Addins\$Year"
}

function Get-ClaudeConfigPath {
    return Join-Path $env:APPDATA "Claude\claude_desktop_config.json"
}

# ── Uninstall ────────────────────────────────────────────────

function Invoke-Uninstall {
    Write-Banner
    Write-Host "  Uninstalling Revit MCP..." -ForegroundColor Yellow
    Write-Host ""

    $installDir = Get-InstallDir

    # Remove .addin files for all Revit versions
    for ($year = 2020; $year -le 2027; $year++) {
        $addinsDir = Get-AddinsDir $year
        $addinFile = Join-Path $addinsDir "RevitMCP.addin"
        $mcpDir = Join-Path $addinsDir "RevitMCP"
        if (Test-Path $addinFile) {
            Remove-Item $addinFile -Force -ErrorAction SilentlyContinue
            Write-OK "Removed .addin for Revit $year"
        }
        if (Test-Path $mcpDir) {
            Remove-Item $mcpDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    # Remove Claude Desktop config entry
    $claudeConfig = Get-ClaudeConfigPath
    if (Test-Path $claudeConfig) {
        try {
            $json = Get-Content $claudeConfig -Raw | ConvertFrom-Json
            if ($json.mcpServers.PSObject.Properties["revit-mcp"]) {
                $json.mcpServers.PSObject.Properties.Remove("revit-mcp")
                $json | ConvertTo-Json -Depth 10 | Set-Content $claudeConfig -Encoding UTF8
                Write-OK "Removed revit-mcp from Claude Desktop config"
            }
        } catch {
            Write-Warn "Could not update Claude Desktop config: $_"
        }
    }

    # Remove install directory
    if (Test-Path $installDir) {
        Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-OK "Removed $installDir"
    }

    Write-Host ""
    Write-Host "  ✅ Revit MCP has been uninstalled." -ForegroundColor Green
    Write-Host "     Restart Revit and Claude Desktop to complete removal." -ForegroundColor Gray
    Write-Host ""
    return
}

# ── Main Install ─────────────────────────────────────────────

if ($Uninstall) {
    Invoke-Uninstall
    return
}

Write-Banner

$installDir = Get-InstallDir
$isAdmin = Test-Admin

if (-not $isAdmin) {
    Write-Warn "Running without admin — installing to $installDir"
    Write-Host "        Run as Administrator for system-wide install to Program Files." -ForegroundColor DarkGray
    Write-Host ""
}

# ────────────────────────────────────
# STEP 1: Download release
# ────────────────────────────────────
Write-Step "1/5" "Downloading Revit MCP v$VERSION..."

$tempZip = Join-Path $env:TEMP "RevitMCP-v$VERSION.zip"
$tempExtract = Join-Path $env:TEMP "RevitMCP-extract"

try {
    # Try GitHub Release first
    Invoke-WebRequest -Uri $RELEASE_URL -OutFile $tempZip -UseBasicParsing
    Write-OK "Downloaded from GitHub Releases"
} catch {
    # Fallback: try latest release API
    try {
        $releaseApi = "https://api.github.com/repos/$REPO/releases/latest"
        $release = Invoke-RestMethod -Uri $releaseApi -UseBasicParsing
        $asset = $release.assets | Where-Object { $_.name -like "RevitMCP-*.zip" } | Select-Object -First 1
        if ($asset) {
            Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $tempZip -UseBasicParsing
            Write-OK "Downloaded from GitHub Releases (latest)"
        } else {
            throw "No ZIP asset found in latest release"
        }
    } catch {
        Write-Err "Could not download release. Please download manually from:"
        Write-Host "        https://github.com/$REPO/releases" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "  Alternative: Install the MCP server via npm:" -ForegroundColor Yellow
        Write-Host "    npm install -g revit-mcp-server" -ForegroundColor Cyan
        Write-Host ""
        return
    }
}

# ────────────────────────────────────
# STEP 2: Extract and install files
# ────────────────────────────────────
Write-Step "2/5" "Installing to $installDir..."

# Clean previous extract
if (Test-Path $tempExtract) { Remove-Item $tempExtract -Recurse -Force }

# Extract ZIP
Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force

# Find the content root (might be nested in a folder)
$extractedRoot = $tempExtract
$subdirs = Get-ChildItem $tempExtract -Directory
if ($subdirs.Count -eq 1 -and (Test-Path (Join-Path $subdirs[0].FullName "server"))) {
    $extractedRoot = $subdirs[0].FullName
}

# Create install directory
if (Test-Path $installDir) { Remove-Item $installDir -Recurse -Force }
New-Item -ItemType Directory -Path $installDir -Force | Out-Null

# Copy all contents
Copy-Item "$extractedRoot\*" -Destination $installDir -Recurse -Force

Write-OK "Files installed to $installDir"

# Cleanup temp
Remove-Item $tempZip -Force -ErrorAction SilentlyContinue
Remove-Item $tempExtract -Recurse -Force -ErrorAction SilentlyContinue

# ────────────────────────────────────
# STEP 3: Detect & install Revit plugin
# ────────────────────────────────────
Write-Step "3/5" "Detecting installed Revit versions..."

$installedRevit = @()

if (-not $SkipRevitPlugin) {
    $installedRevit = Get-InstalledRevitVersions

    if ($installedRevit.Count -eq 0) {
        Write-Warn "No Revit installations found (2020–2026)"
        Write-Host "        The MCP server will still be installed. Add the Revit plugin later." -ForegroundColor DarkGray
    } else {
        Write-OK "Found: Revit $($installedRevit -join ', Revit ')"
        Write-Host ""

        foreach ($year in $installedRevit) {
            $fw = Get-FrameworkForYear $year
            $addinsDir = Get-AddinsDir $year
            $pluginDir = Join-Path $addinsDir "RevitMCP"

            # Create directories
            if (!(Test-Path $addinsDir)) { New-Item -ItemType Directory -Path $addinsDir -Force | Out-Null }
            if (!(Test-Path $pluginDir)) { New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null }

            # Copy the correct framework build
            $sourcePluginDir = Join-Path $installDir "plugin\$fw"
            if (Test-Path $sourcePluginDir) {
                Copy-Item "$sourcePluginDir\*" -Destination $pluginDir -Recurse -Force
            } else {
                # Fallback: copy whatever plugin dir exists
                $fallbackDir = Join-Path $installDir "plugin"
                if (Test-Path $fallbackDir) {
                    $firstFw = Get-ChildItem $fallbackDir -Directory | Select-Object -First 1
                    if ($firstFw) {
                        Copy-Item "$($firstFw.FullName)\*" -Destination $pluginDir -Recurse -Force
                    }
                }
            }

            # Write .addin manifest
            $assemblyPath = Join-Path $pluginDir "RevitMCPPlugin.dll"
            $addinContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Revit MCP Plugin</Name>
    <Assembly>$assemblyPath</Assembly>
    <FullClassName>RevitMCPPlugin.Core.Application</FullClassName>
    <ClientId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</ClientId>
    <VendorId>HassanElmathary</VendorId>
    <VendorDescription>AI-Powered Revit Plugin by Hassan Ahmed Elmathary</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
            Set-Content -Path (Join-Path $addinsDir "RevitMCP.addin") -Value $addinContent -Encoding UTF8
            Write-OK "Installed plugin for Revit $year ($fw)"
        }
    }
} else {
    Write-Skip "Revit plugin installation skipped"
}

# ────────────────────────────────────
# STEP 4: Auto-configure Claude Desktop
# ────────────────────────────────────
Write-Step "4/5" "Configuring Claude Desktop..."

if (-not $SkipClaudeConfig) {
    $claudeConfig = Get-ClaudeConfigPath
    $claudeDir = Split-Path $claudeConfig

    # Determine node path and server path
    $nodeExe = Join-Path $installDir "nodejs\node.exe"
    $serverJs = Join-Path $installDir "server\build\index.js"

    # Fallback: if no bundled node, use system node
    if (-not (Test-Path $nodeExe)) {
        $systemNode = Get-Command node -ErrorAction SilentlyContinue
        if ($systemNode) {
            $nodeExe = $systemNode.Source
        } else {
            Write-Warn "No Node.js found. Install Node.js 18+ or use the full installer."
            Write-Host "        Download: https://nodejs.org" -ForegroundColor Cyan
            $nodeExe = "node"
        }
    }

    # Build the MCP server config entry
    $mcpEntry = @{
        command = $nodeExe
        args = @($serverJs)
        env = @{}
    }

    if (Test-Path $claudeConfig) {
        # Merge into existing config
        try {
            $configText = Get-Content $claudeConfig -Raw
            $config = $configText | ConvertFrom-Json

            # Ensure mcpServers exists
            if (-not $config.mcpServers) {
                $config | Add-Member -NotePropertyName "mcpServers" -NotePropertyValue ([PSCustomObject]@{})
            }

            # Add/update revit-mcp entry
            if ($config.mcpServers.PSObject.Properties["revit-mcp"]) {
                $config.mcpServers."revit-mcp" = [PSCustomObject]$mcpEntry
            } else {
                $config.mcpServers | Add-Member -NotePropertyName "revit-mcp" -NotePropertyValue ([PSCustomObject]$mcpEntry)
            }

            $config | ConvertTo-Json -Depth 10 | Set-Content $claudeConfig -Encoding UTF8
            Write-OK "Updated Claude Desktop config (merged revit-mcp server)"
        } catch {
            Write-Warn "Could not update existing Claude config: $_"
            Write-Host "        You may need to add the MCP server manually." -ForegroundColor DarkGray
        }
    } else {
        # Create new config
        try {
            if (!(Test-Path $claudeDir)) { New-Item -ItemType Directory -Path $claudeDir -Force | Out-Null }

            $newConfig = @{
                mcpServers = @{
                    "revit-mcp" = $mcpEntry
                }
            }
            $newConfig | ConvertTo-Json -Depth 10 | Set-Content $claudeConfig -Encoding UTF8
            Write-OK "Created Claude Desktop config with revit-mcp server"
        } catch {
            Write-Warn "Could not create Claude config: $_"
        }
    }

    # Try to restart Claude Desktop so the new config takes effect
    $claudeProcess = Get-Process -Name "Claude" -ErrorAction SilentlyContinue
    if ($claudeProcess) {
        Write-Host "        Restarting Claude Desktop to apply config..." -ForegroundColor DarkGray
        try {
            $claudeExe = $claudeProcess[0].Path
            Stop-Process -Name "Claude" -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
            Start-Process $claudeExe
            Write-OK "Claude Desktop restarted"
        } catch {
            Write-Warn "Please restart Claude Desktop manually to load the new MCP server."
        }
    }
} else {
    Write-Skip "Claude Desktop configuration skipped"
}

# ────────────────────────────────────
# STEP 5: Create launcher scripts
# ────────────────────────────────────
Write-Step "5/5" "Creating launcher scripts..."

$nodeExeFinal = Join-Path $installDir "nodejs\node.exe"
$serverJsFinal = Join-Path $installDir "server\build\index.js"

# Start MCP Server batch file
$startBat = @"
@echo off
title Revit MCP Server v$VERSION
echo.
echo   ======================================
echo     Revit MCP Server v$VERSION
echo     by Hassan Ahmed Elmathary
echo   ======================================
echo.
echo   Starting MCP Server...
echo   Press Ctrl+C to stop.
echo.
"$nodeExeFinal" "$serverJsFinal"
pause
"@
Set-Content -Path (Join-Path $installDir "Start MCP Server.bat") -Value $startBat

# Save config reference
$mcpConfigRef = @{
    mcpServers = @{
        "revit-mcp" = @{
            command = $nodeExeFinal
            args = @($serverJsFinal)
            env = @{}
        }
    }
} | ConvertTo-Json -Depth 10
Set-Content -Path (Join-Path $installDir "mcp-config.json") -Value $mcpConfigRef -Encoding UTF8

Write-OK "Launcher scripts created"

# ────────────────────────────────────
# DONE
# ────────────────────────────────────
Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "  ║                                                  ║" -ForegroundColor Green
Write-Host "  ║        ✅  Installation Complete!                ║" -ForegroundColor Green
Write-Host "  ║                                                  ║" -ForegroundColor Green
Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "  📁 Installed to: $installDir" -ForegroundColor Gray

if ($installedRevit.Count -gt 0) {
    Write-Host "  🏗️  Revit plugin: $($installedRevit -join ', ')" -ForegroundColor Gray
}

$claudeConfigCheck = Get-ClaudeConfigPath
if (Test-Path $claudeConfigCheck) {
    Write-Host "  🤖 Claude Desktop: ✅ Configured" -ForegroundColor Gray
}

Write-Host ""
Write-Host "  Next Steps:" -ForegroundColor Yellow
Write-Host "  1. Open Revit → look for 'Chat with me' in the Add-ins tab" -ForegroundColor White
Write-Host "  2. Open Claude Desktop → Revit MCP tools are ready to use" -ForegroundColor White
Write-Host "  3. Click ⚙️ Settings in Revit to configure AI providers" -ForegroundColor White
Write-Host ""
Write-Host "  📖 Docs: https://github.com/$REPO" -ForegroundColor Cyan
Write-Host "  🐛 Issues: https://github.com/$REPO/issues" -ForegroundColor Cyan
Write-Host ""
Write-Host "  To uninstall:" -ForegroundColor DarkGray
Write-Host "  irm https://raw.githubusercontent.com/$REPO/main/install.ps1 | iex -Uninstall" -ForegroundColor DarkGray
Write-Host ""
