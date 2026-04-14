# ============================================================
#  BIM-Bot — Uninstaller
#  Usage: irm https://raw.githubusercontent.com/HassanElmathary/Revit-MCP/main/uninstall.ps1 | iex
# ============================================================

$ErrorActionPreference = "Stop"

function Write-Banner {
    Write-Host ""
    Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "  ║        🗑️  BIM-Bot — Uninstaller                 ║" -ForegroundColor Red
    Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor Red
    Write-Host ""
}

Write-Banner

# ── Confirm ──────────────────────────────────────────────────
$confirm = Read-Host "  Are you sure you want to uninstall BIM-Bot? (y/N)"
if ($confirm -notin @("y", "Y", "yes", "Yes")) {
    Write-Host "  Cancelled." -ForegroundColor Gray
    return
}

Write-Host ""

# ── Remove Revit .addin files ────────────────────────────────
Write-Host "  [1/4] Removing Revit plugin files..." -ForegroundColor Yellow

for ($year = 2020; $year -le 2027; $year++) {
    $addinsDir = Join-Path $env:ProgramData "Autodesk\Revit\Addins\$year"
    $addinFile = Join-Path $addinsDir "BIMBot.addin"
    $mcpDir = Join-Path $addinsDir "BIMBot"

    if (Test-Path $addinFile) {
        Remove-Item $addinFile -Force -ErrorAction SilentlyContinue
        Write-Host "        ✅ Removed .addin for Revit $year" -ForegroundColor Green
    }
    if (Test-Path $mcpDir) {
        Remove-Item $mcpDir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "        ✅ Removed plugin files for Revit $year" -ForegroundColor Green
    }

    # Also check user-level addins
    $userAddinsDir = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$year"
    $userAddinFile = Join-Path $userAddinsDir "BIMBot.addin"
    $userMcpDir = Join-Path $userAddinsDir "BIMBot"
    if (Test-Path $userAddinFile) {
        Remove-Item $userAddinFile -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $userMcpDir) {
        Remove-Item $userMcpDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ── Remove Claude Desktop config entry ───────────────────────
Write-Host "  [2/4] Removing Claude Desktop config..." -ForegroundColor Yellow

$claudeConfig = Join-Path $env:APPDATA "Claude\claude_desktop_config.json"
if (Test-Path $claudeConfig) {
    try {
        $json = Get-Content $claudeConfig -Raw | ConvertFrom-Json
        if ($json.mcpServers -and $json.mcpServers.PSObject.Properties["bim-bot"]) {
            $json.mcpServers.PSObject.Properties.Remove("bim-bot")
            $json | ConvertTo-Json -Depth 10 | Set-Content $claudeConfig -Encoding UTF8
            Write-Host "        ✅ Removed bim-bot from Claude Desktop config" -ForegroundColor Green
        } else {
            Write-Host "        ⏭️  No bim-bot entry found in Claude Desktop config" -ForegroundColor DarkGray
        }
    } catch {
        Write-Host "        ⚠️  Could not update Claude Desktop config: $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "        ⏭️  Claude Desktop config not found" -ForegroundColor DarkGray
}

# ── Remove install directory ─────────────────────────────────
Write-Host "  [3/4] Removing installation files..." -ForegroundColor Yellow

$installDirs = @(
    (Join-Path $env:ProgramFiles "BIMBot"),
    (Join-Path $env:LOCALAPPDATA "BIMBot")
)

foreach ($dir in $installDirs) {
    if (Test-Path $dir) {
        Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "        ✅ Removed $dir" -ForegroundColor Green
    }
}

# ── Restart Claude Desktop ───────────────────────────────────
Write-Host "  [4/4] Cleaning up..." -ForegroundColor Yellow

$claudeProcess = Get-Process -Name "Claude" -ErrorAction SilentlyContinue
if ($claudeProcess) {
    Write-Host "        Restarting Claude Desktop..." -ForegroundColor DarkGray
    try {
        $claudeExe = $claudeProcess[0].Path
        Stop-Process -Name "Claude" -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Start-Process $claudeExe
        Write-Host "        ✅ Claude Desktop restarted" -ForegroundColor Green
    } catch {
        Write-Host "        ⚠️  Please restart Claude Desktop manually." -ForegroundColor Yellow
    }
}

# ── Done ─────────────────────────────────────────────────────
Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "  ║        ✅  BIM-Bot Uninstalled Successfully      ║" -ForegroundColor Green
Write-Host "  ╚══════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "  Restart Revit to complete removal." -ForegroundColor Gray
Write-Host ""
