# ============================================
# BIM-Bot User-Level (Non-Admin) Installer
# ============================================

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "  =========================================" -ForegroundColor Cyan
Write-Host "       BIM-Bot Non-Admin Installer" -ForegroundColor White
Write-Host "    AI-Powered Tools for Autodesk Revit" -ForegroundColor Gray
Write-Host "  =========================================" -ForegroundColor Cyan
Write-Host ""

# Determine workspace paths
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = Get-Location }

$sourceNet48 = Join-Path $scriptDir "dist\RevitMCP\plugin\net48"
$sourceNet8 = Join-Path $scriptDir "dist\plugin\net8"
$serverJs = Join-Path $scriptDir "revit-mcp-server\build\index.js"

# 1. Detect Revit versions
$revitVersions = @()
$autodeskDir = "C:\Program Files\Autodesk"
if (Test-Path $autodeskDir) {
    $revitVersions = Get-ChildItem $autodeskDir -Directory |
        Where-Object { $_.Name -match "^Revit (\d{4})$" } |
        ForEach-Object { [int]$Matches[1] }
}

# Fallback: check APPDATA folders if Program Files isn't accessible or is different
$appDataRevitDir = "$env:APPDATA\Autodesk\Revit\Addins"
if (Test-Path $appDataRevitDir) {
    $appDataVersions = Get-ChildItem $appDataRevitDir -Directory |
        Where-Object { $_.Name -match "^\d{4}$" } |
        ForEach-Object { [int]$_.Name }
    $revitVersions = @($revitVersions) + @($appDataVersions) | Select-Object -Unique
}

if ($revitVersions.Count -eq 0) {
    Write-Host "  [!] No Revit installation detected. Deploying to Revit 2023 and 2026 by default." -ForegroundColor Yellow
    $revitVersions = @(2023, 2026)
}

Write-Host "  Detected Revit version folders: $($revitVersions -join ', ')" -ForegroundColor White
Write-Host ""

# 2. Deploy plugin for each year
foreach ($year in $revitVersions) {
    $addinDestDir = "$env:APPDATA\Autodesk\Revit\Addins\$year"
    $pluginDestDir = "$addinDestDir\BIMBotPlugin"

    Write-Host "  Deploying plugin for Revit $year..." -ForegroundColor Yellow

    # Ensure directories exist
    if (-not (Test-Path $addinDestDir)) { New-Item -ItemType Directory -Path $addinDestDir -Force | Out-Null }
    if (-not (Test-Path $pluginDestDir)) { New-Item -ItemType Directory -Path $pluginDestDir -Force | Out-Null }

    # Clean up legacy files
    $legacyAddin = Join-Path $addinDestDir "RevitMCP.addin"
    $legacyDir = Join-Path $addinDestDir "RevitMCP"
    if (Test-Path $legacyAddin) { 
        Remove-Item $legacyAddin -Force
        Write-Host "    Cleaned up legacy $legacyAddin" -ForegroundColor DarkGray
    }
    if (Test-Path $legacyDir) { 
        Remove-Item $legacyDir -Recurse -Force
        Write-Host "    Cleaned up legacy $legacyDir" -ForegroundColor DarkGray
    }

    # Determine framework and dll name
    if ($year -le 2024) {
        # Net48 (using RevitMCPPlugin.dll from dist\RevitMCP\plugin\net48)
        if (-not (Test-Path $sourceNet48)) {
            Write-Host "    [WARN] Source Net48 folder not found: $sourceNet48. Skipping Revit $year." -ForegroundColor Red
            continue
        }
        Copy-Item "$sourceNet48\*" $pluginDestDir -Recurse -Force
        $dllPath = Join-Path $pluginDestDir "RevitMCPPlugin.dll"
        $className = "RevitMCPPlugin.Core.Application"
    } else {
        # Net8 (using BIMBotPlugin.dll from dist\plugin\net8)
        if (-not (Test-Path $sourceNet8)) {
            Write-Host "    [WARN] Source Net8 folder not found: $sourceNet8. Skipping Revit $year." -ForegroundColor Red
            continue
        }
        Copy-Item "$sourceNet8\*" $pluginDestDir -Recurse -Force
        $dllPath = Join-Path $pluginDestDir "BIMBotPlugin.dll"
        $className = "BIMBotPlugin.Core.Application"
    }

    # Write .addin manifest
    $addinContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>BIM-Bot Plugin</Name>
    <Assembly>$dllPath</Assembly>
    <FullClassName>$className</FullClassName>
    <ClientId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</ClientId>
    <VendorId>HassanElmathary</VendorId>
    <VendorDescription>AI-Powered BIM-Bot Plugin by Hassan Ahmed Elmathary</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
    $addinContent | Out-File -Encoding utf8 "$addinDestDir\BIMBot.addin" -Force
    Write-Host "    [OK] Deployed BIMBot.addin for Revit $year" -ForegroundColor Green
}

# 3. Configure Claude Desktop
Write-Host ""
Write-Host "  Configuring Claude Desktop..." -ForegroundColor Yellow

# Find absolute path of node.exe if available, fallback to "node"
$nodeCmd = (Get-Command node -ErrorAction SilentlyContinue).Source
if (-not $nodeCmd) { $nodeCmd = "node" }
$nodeCmdEscaped = $nodeCmd.Replace('\', '\\')

$serverJsEscaped = $serverJs.Replace('\', '\\')

# Determine all target Claude config paths
$claudePaths = @("$env:APPDATA\Claude\claude_desktop_config.json")
$storePattern = "$env:LOCALAPPDATA\Packages"
if (Test-Path $storePattern) {
    $storePaths = Get-ChildItem -Path $storePattern -Directory -Filter "Claude_*" -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName "LocalCache\Roaming\Claude\claude_desktop_config.json" }
    foreach ($sp in $storePaths) {
        $claudePaths += $sp
    }
}

$newConfig = @"
{
  "mcpServers": {
    "BIM-Bot": {
      "command": "$nodeCmdEscaped",
      "args": ["$serverJsEscaped"],
      "env": {}
    }
  }
}
"@

foreach ($claudeConfigPath in $claudePaths) {
    $claudeDir = Split-Path $claudeConfigPath
    if (-not (Test-Path $claudeDir)) {
        New-Item -ItemType Directory -Path $claudeDir -Force | Out-Null
    }

    Write-Host "    Targeting config: $claudeConfigPath" -ForegroundColor White

    if (Test-Path $claudeConfigPath) {
        $configRaw = Get-Content $claudeConfigPath -Raw
        try {
            $configJson = $configRaw | ConvertFrom-Json
            if (-not $configJson.mcpServers) {
                $configJson | Add-Member -MemberType NoteProperty -Name "mcpServers" -Value @{}
            }
            
            $entry = @{
                command = $nodeCmd
                args    = @($serverJs)
                env     = @{}
            }
            
            $configJson.mcpServers | Add-Member -MemberType NoteProperty -Name "BIM-Bot" -Value $entry -Force
            
            # Remove older version key if present
            if ($configJson.mcpServers.PSObject.Properties.Item("revit-mcp")) {
                $configJson.mcpServers.PSObject.Properties.Remove("revit-mcp")
            }
            
            $configJson | ConvertTo-Json -Depth 10 | Out-File $claudeConfigPath -Encoding utf8 -Force
            Write-Host "    [OK] Successfully merged BIM-Bot into configuration." -ForegroundColor Green
        } catch {
            # Malformed JSON, write new
            $newConfig | Out-File $claudeConfigPath -Encoding utf8 -Force
            Write-Host "    [OK] Wrote new configuration (old file was malformed)." -ForegroundColor Green
        }
    } else {
        $newConfig | Out-File $claudeConfigPath -Encoding utf8 -Force
        Write-Host "    [OK] Created configuration." -ForegroundColor Green
    }
}

# 4. Configure Gemini CLI if settings exist
$geminiDir = "$env:USERPROFILE\.gemini"
if (Test-Path $geminiDir) {
    Write-Host ""
    Write-Host "  Configuring Gemini CLI..." -ForegroundColor Yellow
    $geminiConfigPath = Join-Path $geminiDir "settings.json"
    
    $geminiNewConfig = @"
{
  "mcpServers": {
    "BIM-Bot": {
      "command": "node",
      "args": ["$serverJsEscaped"]
    }
  }
}
"@
    
    if (Test-Path $geminiConfigPath) {
        $gRaw = Get-Content $geminiConfigPath -Raw
        try {
            $gJson = $gRaw | ConvertFrom-Json
            if (-not $gJson.mcpServers) {
                $gJson | Add-Member -MemberType NoteProperty -Name "mcpServers" -Value @{}
            }
            $gEntry = @{
                command = "node"
                args    = @($serverJs)
            }
            $gJson.mcpServers | Add-Member -MemberType NoteProperty -Name "BIM-Bot" -Value $gEntry -Force
            if ($gJson.mcpServers.PSObject.Properties.Item("revit-mcp")) {
                $gJson.mcpServers.PSObject.Properties.Remove("revit-mcp")
            }
            $gJson | ConvertTo-Json -Depth 10 | Out-File $geminiConfigPath -Encoding utf8 -Force
            Write-Host "    [OK] Successfully merged BIM-Bot into Gemini CLI configuration." -ForegroundColor Green
        } catch {
            $geminiNewConfig | Out-File $geminiConfigPath -Encoding utf8 -Force
            Write-Host "    [OK] Wrote new Gemini CLI configuration." -ForegroundColor Green
        }
    } else {
        $geminiNewConfig | Out-File $geminiConfigPath -Encoding utf8 -Force
        Write-Host "    [OK] Created Gemini CLI configuration." -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "  =========================================" -ForegroundColor Green
Write-Host "     Non-Admin Installation Complete!" -ForegroundColor White
Write-Host "  =========================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Next steps:" -ForegroundColor Yellow
Write-Host "  1. Restart Autodesk Revit" -ForegroundColor White
Write-Host "     - Look for 'BIM-Bot' tab (or 'Revit MCP' depending on version)." -ForegroundColor White
Write-Host "  2. Restart Claude Desktop" -ForegroundColor White
Write-Host "     - The AI assistant will now have access to the model." -ForegroundColor White
Write-Host ""
