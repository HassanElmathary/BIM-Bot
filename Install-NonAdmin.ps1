# ============================================
# BIM-Bot User-Level (Non-Admin) Installer
# ============================================

$ErrorActionPreference = "Stop"

# Determine workspace paths
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = Get-Location }

# -- Strict Step 0: transcript + prerequisites (auto-download missing) --
$transcriptPath = Join-Path $env:TEMP "BIMBot-install.log"
try { Start-Transcript -Path $transcriptPath -Append -ErrorAction SilentlyContinue | Out-Null } catch {}
function Write-Utf8NoBom($Path, $Text) {
    # Out-File -Encoding utf8 emits a BOM on Windows PowerShell 5.1, which
    # makes Claude Desktop reject claude_desktop_config.json outright.
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($Path, $Text, $utf8)
}
$prereqScript = Join-Path $scriptDir "installer\Install-Prerequisites.ps1"
if (Test-Path $prereqScript) {
    Write-Host "  [0/4] Checking prerequisites (auto-download missing)..." -ForegroundColor Yellow
    & powershell -ExecutionPolicy Bypass -File $prereqScript
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  [!] Prerequisites reported failures - continuing anyway, but see above." -ForegroundColor Yellow
        Write-Host "      Full log: $transcriptPath" -ForegroundColor DarkGray
    }
} else {
    Write-Host "  [!] Prerequisite script not found ($prereqScript) - skipping checks." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "  =========================================" -ForegroundColor Cyan
Write-Host "       BIM-Bot Non-Admin Installer" -ForegroundColor White
Write-Host "    AI-Powered Tools for Autodesk Revit" -ForegroundColor Gray
Write-Host "  =========================================" -ForegroundColor Cyan
Write-Host ""

$sourceNet48 = Join-Path $scriptDir "dist\RevitMCP\plugin\net48"
$sourceNet8 = Join-Path $scriptDir "dist\plugin\net8"
# Strict: resolve the MCP server in installed layout first, dev layout second.
$serverCandidates = @(
    (Join-Path $scriptDir "server\build\index.js"),
    (Join-Path $scriptDir "revit-mcp-server\build\index.js")
)
$serverJs = $serverCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $serverJs) {
    Write-Host "  [FAIL] MCP server not found. Looked in:" -ForegroundColor Red
    $serverCandidates | ForEach-Object { Write-Host "         $_" -ForegroundColor DarkGray }
    Write-Host "  Run 'npm run build' in revit-mcp-server, or reinstall BIM-Bot." -ForegroundColor Yellow
    exit 1
}
Write-Host "  MCP server: $serverJs" -ForegroundColor DarkGray
# Strict: prefer the bundled portable Node, then system node. Never a bare
# "node" that GUI-launched Claude cannot resolve.
$nodeCandidates = @(
    (Join-Path $scriptDir "installer\nodejs\node.exe"),
    (Join-Path $scriptDir "nodejs\node.exe"),
    "$env:ProgramFiles\BIMBot\nodejs\node.exe"
)
$nodeCmd = $nodeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $nodeCmd) { $nodeCmd = (Get-Command node -ErrorAction SilentlyContinue).Source }
if (-not $nodeCmd) { Write-Host "  [FAIL] No Node.js found (bundled or system). Re-download the full installer." -ForegroundColor Red; exit 1 }
try { $nodeVer = & $nodeCmd --version 2>$null; Write-Host "  Node.js: $nodeCmd ($nodeVer)" -ForegroundColor DarkGray } catch {}

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

    # Clean up legacy files (per-user scope)
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
    # Strict: a machine-wide manifest with the same ClientId shadows the
    # per-user one - remove the stale scope so the new DLL actually loads.
    $machineAddin = "$env:ProgramData\Autodesk\Revit\Addins\$year\BIMBot.addin"
    if (Test-Path $machineAddin) {
        try { Remove-Item $machineAddin -Force; Write-Host "    Removed shadowing machine-wide $machineAddin" -ForegroundColor DarkGray } catch {}
    }

    # Framework mapping per Revit version
    $bandTfm = @{
        2020 = 'net47'; 2021 = 'net48'; 2022 = 'net48'; 2023 = 'net48'
        2024 = 'net48'; 2025 = 'net8.0-windows'; 2026 = 'net8.0-windows'; 2027 = 'net10.0-windows'
    }

    $sourceYear = $null
    if ($bandTfm.ContainsKey($year)) {
        $candidateYear = Join-Path $scriptDir "revit-mcp-plugin\BIMBotPlugin\bin\R$year\Release\$($bandTfm[$year])"
        if (Test-Path $candidateYear) { $sourceYear = $candidateYear }
    }

    # Determine framework and dll name
    if ($sourceYear) {
        Write-Host "    Using latest build from: $sourceYear" -ForegroundColor DarkGray
        Copy-Item "$sourceYear\*" $pluginDestDir -Recurse -Force
        $dllPath = Join-Path $pluginDestDir "BIMBotPlugin.dll"
        $className = "BIMBotPlugin.Core.Application"
    } elseif ($year -le 2024) {
        # Net48 fallback (dist\RevitMCP\plugin\net48)
        if (-not (Test-Path $sourceNet48)) {
            Write-Host "    [WARN] Source Net48 folder not found: $sourceNet48. Skipping Revit $year." -ForegroundColor Red
            continue
        }
        Copy-Item "$sourceNet48\*" $pluginDestDir -Recurse -Force
        if (Test-Path (Join-Path $pluginDestDir "BIMBotPlugin.dll")) {
            $dllPath = Join-Path $pluginDestDir "BIMBotPlugin.dll"
            $className = "BIMBotPlugin.Core.Application"
        } else {
            $dllPath = Join-Path $pluginDestDir "RevitMCPPlugin.dll"
            $className = "RevitMCPPlugin.Core.Application"
        }
    } else {
        # Net8 fallback (dist\plugin\net8)
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
    Write-Utf8NoBom "$addinDestDir\BIMBot.addin" $addinContent
    # Strict verify: manifest must parse and the DLL must exist.
    try {
        $asm = ([xml](Get-Content "$addinDestDir\BIMBot.addin" -Raw)).RevitAddIns.AddIn.Assembly
        if (-not (Test-Path $asm)) { Write-Host "    [FAIL] Manifest points at missing DLL: $asm" -ForegroundColor Red } else { Write-Host "    [OK] Deployed BIMBot.addin for Revit $year" -ForegroundColor Green }
    } catch { Write-Host "    [FAIL] BIMBot.addin is not valid XML: $($_.Exception.Message)" -ForegroundColor Red }
}

# 3. Configure Claude Desktop - STRICT: single writer (configure-claude.cjs).
# The old inline JSON editing wrote a BOM, used a bare "node", missed the
# MS-Store config path, and wiped other MCP servers on any parse error.
# configure-claude.cjs does none of that (UTF-8-no-BOM, absolute paths,
# backup + quarantine, path validation). Use it whenever possible.
Write-Host ""
Write-Host "  [3/4] Configuring Claude Desktop..." -ForegroundColor Yellow

$configureCjs = $null
foreach ($c in @((Join-Path $scriptDir "revit-mcp-server\scripts\configure-claude.cjs"), (Join-Path $scriptDir "server\scripts\configure-claude.cjs"))) {
    if (Test-Path $c) { $configureCjs = $c; break }
}
$claudeDone = $false
if ($configureCjs -and (Test-Path $nodeCmd) -and $nodeCmd -ne "node") {
    & $nodeCmd $configureCjs --server $serverJs --node $nodeCmd
    if ($LASTEXITCODE -eq 0) { $claudeDone = $true } else { Write-Host "    [!] configure-claude.cjs exited $LASTEXITCODE - using hardened fallback." -ForegroundColor Yellow }
}
if (-not $claudeDone) {
    # Hardened fallback: same guarantees as configure-claude.cjs, inline.
    $nodeCmdEscaped = $nodeCmd.Replace('\', '\\')
    $serverJsEscaped = $serverJs.Replace('\', '\\')
    $claudePaths = @("$env:APPDATA\Claude\claude_desktop_config.json")
    $storePattern = "$env:LOCALAPPDATA\Packages"
    if (Test-Path $storePattern) {
        # MS-Store Claude lives under AnthropicClaude*, not Claude_*.
        $storePaths = Get-ChildItem -Path $storePattern -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "Claude_*" -or $_.Name -like "AnthropicClaude*" } |
            ForEach-Object { Join-Path $_.FullName "LocalCache\Roaming\Claude\claude_desktop_config.json" }
        foreach ($sp in $storePaths) { $claudePaths += $sp }
    }
    $newConfig = @{
        mcpServers = @{
            "BIM-Bot" = @{
                command = $nodeCmd
                args = @($serverJs)
                env = @{}
            }
        }
    } | ConvertTo-Json -Depth 5
    foreach ($claudeConfigPath in $claudePaths) {
        $claudeDir = Split-Path $claudeConfigPath
        if (-not (Test-Path $claudeDir)) { New-Item -ItemType Directory -Path $claudeDir -Force | Out-Null }
        Write-Host "    Targeting config: $claudeConfigPath" -ForegroundColor White
        if (Test-Path $claudeConfigPath) {
            try {
                $raw = [System.IO.File]::ReadAllText($claudeConfigPath)
                if ($raw.Length -gt 0 -and $raw[0] -eq [char]0xFEFF) { $raw = $raw.Substring(1) }
                $configJson = $raw | ConvertFrom-Json
                if (-not $configJson.mcpServers) { $configJson | Add-Member -MemberType NoteProperty -Name "mcpServers" -Value @{} }
                $entry = @{ command = $nodeCmd; args = @($serverJs); env = @{} }
                $configJson.mcpServers | Add-Member -MemberType NoteProperty -Name "BIM-Bot" -Value $entry -Force
                if ($configJson.mcpServers.PSObject.Properties.Item("revit-mcp")) { $configJson.mcpServers.PSObject.Properties.Remove("revit-mcp") }
                Copy-Item $claudeConfigPath "$claudeConfigPath.bimbot-backup" -Force -ErrorAction SilentlyContinue
                Write-Utf8NoBom $claudeConfigPath ($configJson | ConvertTo-Json -Depth 10)
                Write-Host "    [OK] Merged BIM-Bot into configuration." -ForegroundColor Green
            } catch {
                $stamp = (Get-Date).ToString("yyyyMMdd-HHmmss")
                Copy-Item $claudeConfigPath "$claudeConfigPath.broken-$stamp" -Force -ErrorAction SilentlyContinue
                Write-Host "    [WARN] Old config was corrupt - quarantined, writing clean one." -ForegroundColor Yellow
                Write-Utf8NoBom $claudeConfigPath $newConfig
                Write-Host "    [OK] Wrote new configuration." -ForegroundColor Green
            }
        } else {
            Write-Utf8NoBom $claudeConfigPath $newConfig
            Write-Host "    [OK] Created configuration." -ForegroundColor Green
        }
        # Strict verify: re-parse + both paths must exist.
        try {
            $v = [System.IO.File]::ReadAllText($claudeConfigPath) | ConvertFrom-Json
            $vc = $v.mcpServers.'BIM-Bot'.command; $va = $v.mcpServers.'BIM-Bot'.args[0]
            if ((Test-Path $vc) -and (Test-Path $va)) { Write-Host "    [OK] Verified: $vc -> $va" -ForegroundColor Green } else { Write-Host "    [FAIL] Entry written but paths missing: '$vc' / '$va'" -ForegroundColor Red }
        } catch { Write-Host "    [FAIL] Written config does not parse: $($_.Exception.Message)" -ForegroundColor Red }
    }
}

# 4. Configure Gemini CLI if settings exist
$geminiDir = "$env:USERPROFILE\.gemini"
if (Test-Path $geminiDir) {
    Write-Host ""
    Write-Host "  Configuring Gemini CLI..." -ForegroundColor Yellow
    $geminiConfigPath = Join-Path $geminiDir "settings.json"
    
    $serverJsEscapedGem = $serverJs.Replace('\', '\\')
    $nodeCmdEscapedGem = $nodeCmd.Replace('\', '\\')
    $geminiNewConfig = @{
        mcpServers = @{
            "BIM-Bot" = @{
                command = $nodeCmd
                args = @($serverJs)
            }
        }
    } | ConvertTo-Json -Depth 5
    
    if (Test-Path $geminiConfigPath) {
        $gRaw = Get-Content $geminiConfigPath -Raw
        try {
            $gJson = $gRaw | ConvertFrom-Json
            if (-not $gJson.mcpServers) {
                $gJson | Add-Member -MemberType NoteProperty -Name "mcpServers" -Value @{}
            }
            $gEntry = @{
                command = $nodeCmd
                args    = @($serverJs)
            }
            $gJson.mcpServers | Add-Member -MemberType NoteProperty -Name "BIM-Bot" -Value $gEntry -Force
            if ($gJson.mcpServers.PSObject.Properties.Item("revit-mcp")) {
                $gJson.mcpServers.PSObject.Properties.Remove("revit-mcp")
            }
            Copy-Item $geminiConfigPath "$geminiConfigPath.bimbot-backup" -Force -ErrorAction SilentlyContinue
            Write-Utf8NoBom $geminiConfigPath ($gJson | ConvertTo-Json -Depth 10)
            Write-Host "    [OK] Successfully merged BIM-Bot into Gemini CLI configuration." -ForegroundColor Green
        } catch {
            $stamp = (Get-Date).ToString("yyyyMMdd-HHmmss")
            Copy-Item $geminiConfigPath "$geminiConfigPath.broken-$stamp" -Force -ErrorAction SilentlyContinue
            Write-Utf8NoBom $geminiConfigPath $geminiNewConfig
            Write-Host "    [OK] Wrote new Gemini CLI configuration." -ForegroundColor Green
        }
    } else {
        Write-Utf8NoBom $geminiConfigPath $geminiNewConfig
        Write-Host "    [OK] Created Gemini CLI configuration." -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "  =========================================" -ForegroundColor Green
Write-Host "     Non-Admin Installation Complete!" -ForegroundColor White
Write-Host "  =========================================" -ForegroundColor Green
Write-Host ""
Write-Host "  STRICT next steps (in order):" -ForegroundColor Yellow
Write-Host "  1. FULLY quit Claude Desktop (File -> Exit, not just X)" -ForegroundColor White
Write-Host "     - It only reads its config at startup." -ForegroundColor DarkGray
Write-Host "  2. Open Revit with a project -> BIM-Bot tab reads 'BIM-Bot ON'" -ForegroundColor White
Write-Host "  3. Reopen Claude Desktop -> ask: 'Use BIM-Bot to list the walls'" -ForegroundColor White
Write-Host "  4. If anything fails, run: powershell -File scripts\bimbot-doctor.ps1" -ForegroundColor White
Write-Host "     Install log: $transcriptPath" -ForegroundColor DarkGray
Write-Host ""
try { Stop-Transcript -ErrorAction SilentlyContinue | Out-Null } catch {}
