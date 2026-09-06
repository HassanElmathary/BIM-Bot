# ============================================================
#  BIM-Bot Doctor — diagnose (and optionally repair) an install
#
#  Answers, in one pass, the questions that a failed install leaves
#  ambiguous: is the plugin deployed for the Revit versions on this
#  machine, is the MCP server present and launchable, does every MCP
#  client point at it, and is anything squatting on the service port.
#
#  Usage:
#    powershell -ExecutionPolicy Bypass -File scripts\bimbot-doctor.ps1
#    powershell -ExecutionPolicy Bypass -File scripts\bimbot-doctor.ps1 -Fix
#
#  -Fix re-runs the bundled configure-claude script, which repairs stale
#  or missing MCP client entries. Everything else is read-only.
# ============================================================

param(
    [switch]$Fix
)

$ErrorActionPreference = "Continue"

$script:Problems = 0

function Write-Head($text) {
    Write-Host ""
    Write-Host "  $text" -ForegroundColor Cyan
    Write-Host "  $('-' * $text.Length)" -ForegroundColor DarkGray
}
function Write-Ok($text)   { Write-Host "  [ OK ] $text" -ForegroundColor Green }
function Write-Bad($text)  { Write-Host "  [FAIL] $text" -ForegroundColor Red; $script:Problems++ }
function Write-Warn($text) { Write-Host "  [WARN] $text" -ForegroundColor Yellow }
function Write-Info($text) { Write-Host "         $text" -ForegroundColor DarkGray }

Write-Host ""
Write-Host "  ==========================================" -ForegroundColor Cyan
Write-Host "    BIM-Bot Doctor" -ForegroundColor Cyan
Write-Host "  ==========================================" -ForegroundColor Cyan

# ── 1. Install root ─────────────────────────────────────────
Write-Head "Installation"

$candidates = @(
    "$env:ProgramFiles\BIMBot",
    "${env:ProgramFiles(x86)}\BIMBot",
    "$env:LOCALAPPDATA\Programs\BIMBot"
)
$appDir = $candidates | Where-Object { Test-Path (Join-Path $_ "server\build\index.js") } | Select-Object -First 1

if (-not $appDir) {
    Write-Bad "BIM-Bot is not installed (no server\build\index.js under any known location)."
    Write-Info "Looked in: $($candidates -join '; ')"
    Write-Info "Run the BIM-Bot installer, then re-run this script."
    Write-Host ""
    exit 1
}
Write-Ok "Install root: $appDir"

$serverJs = Join-Path $appDir "server\build\index.js"
$nodeExe  = Join-Path $appDir "nodejs\node.exe"

if (Test-Path $nodeExe) {
    Write-Ok "Bundled Node.js: $nodeExe"
} else {
    Write-Bad "Bundled Node.js missing: $nodeExe"
    Write-Info "Reinstall BIM-Bot with the 'Portable Node.js Runtime' component selected."
}

# Prove the server actually starts and speaks MCP, rather than only that the
# file exists. This is the check that distinguishes "Claude shows BIM-Bot as
# failed" caused by a broken server from one caused by a bad config path.
if (Test-Path $nodeExe) {
    $probe   = '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"doctor","version":"1"}}}'
    $stdin   = [System.IO.Path]::GetTempFileName()
    $stdout  = [System.IO.Path]::GetTempFileName()
    $stderrF = [System.IO.Path]::GetTempFileName()
    try {
        # ASCII, and the trailing newline the transport needs to see a full frame.
        [System.IO.File]::WriteAllText($stdin, $probe + "`n", [System.Text.Encoding]::ASCII)

        $proc = Start-Process -FilePath $nodeExe -ArgumentList "`"$serverJs`"" `
            -RedirectStandardInput $stdin -RedirectStandardOutput $stdout `
            -RedirectStandardError $stderrF -NoNewWindow -PassThru
        if (-not $proc.WaitForExit(30000)) {
            try { $proc.Kill() } catch {}
        }

        $reply = Get-Content $stdout -Raw -ErrorAction SilentlyContinue
        if ($reply -and $reply -match '"serverInfo"') {
            Write-Ok "MCP server responds to an initialize handshake"
        } else {
            Write-Bad "MCP server did not answer an initialize handshake"
            $errTail = (Get-Content $stderrF -Tail 5 -ErrorAction SilentlyContinue) -join "; "
            if ($errTail) { Write-Info "stderr: $errTail" }
            Write-Info "Run it by hand to see the error: & `"$nodeExe`" `"$serverJs`""
        }
    } finally {
        Remove-Item $stdin, $stdout, $stderrF -Force -ErrorAction SilentlyContinue
    }
}

# ── 2. Revit plugin deployment ──────────────────────────────
Write-Head "Revit plugin"

$years = 2020..2027
$revitFound = @()
foreach ($y in $years) {
    $installed = $false
    foreach ($drive in [char[]](67..90)) {
        $root = "${drive}:\"
        if ((Test-Path "$root`Program Files\Autodesk\Revit $y") -or (Test-Path "$root`Autodesk\Revit $y")) {
            $installed = $true; break
        }
    }
    if ($installed) { $revitFound += $y }
}

if ($revitFound.Count -eq 0) {
    Write-Warn "No Revit installation detected on this machine."
} else {
    Write-Ok "Revit detected: $($revitFound -join ', ')"
}

foreach ($y in $revitFound) {
    # A manifest in either scope will load; the machine-wide one wins if both exist.
    $manifests = @(
        "$env:ProgramData\Autodesk\Revit\Addins\$y\BIMBot.addin",
        "$env:APPDATA\Autodesk\Revit\Addins\$y\BIMBot.addin"
    ) | Where-Object { Test-Path $_ }

    if ($manifests.Count -eq 0) {
        Write-Bad "Revit ${y}: no BIMBot.addin — the BIM-Bot tab will not appear"
        Write-Info "Re-run the installer and tick 'Revit $y' under 'Deploy Revit plugin to'."
        continue
    }
    if ($manifests.Count -gt 1) {
        Write-Warn "Revit ${y}: BIMBot.addin exists in BOTH ProgramData and AppData"
        Write-Info "The per-user copy shadows the machine-wide one. Delete the stale one:"
        $manifests | ForEach-Object { Write-Info "  $_" }
    }

    foreach ($m in $manifests) {
        try {
            $asm = ([xml](Get-Content $m -Raw)).RevitAddIns.AddIn.Assembly
        } catch {
            Write-Bad "Revit ${y}: BIMBot.addin is not readable XML ($m)"
            continue
        }
        if (Test-Path $asm) {
            Write-Ok "Revit ${y}: plugin -> $asm"
        } else {
            Write-Bad "Revit ${y}: manifest points at a missing DLL"
            Write-Info "Manifest: $m"
            Write-Info "Missing:  $asm"
            Write-Info "This is what a moved or partly-removed install looks like. Reinstall BIM-Bot."
        }
    }
}

# ── 3. MCP client configuration ─────────────────────────────
Write-Head "MCP clients"

$clients = @(
    @{ Name = "Claude Desktop";   Path = "$env:APPDATA\Claude\claude_desktop_config.json";        Key = "mcpServers" },
    @{ Name = "Claude Code";      Path = "$env:USERPROFILE\.claude.json";                        Key = "mcpServers" },
    @{ Name = "Cursor";           Path = "$env:USERPROFILE\.cursor\mcp.json";                    Key = "mcpServers" },
    @{ Name = "Windsurf";         Path = "$env:USERPROFILE\.codeium\windsurf\mcp_config.json";   Key = "mcpServers" },
    @{ Name = "VS Code";          Path = "$env:APPDATA\Code\User\mcp.json";                      Key = "servers"    }
)

$anyClient = $false
foreach ($c in $clients) {
    if (-not (Test-Path $c.Path)) { continue }
    $anyClient = $true

    $raw = Get-Content $c.Path -Raw -Encoding UTF8
    try {
        $json = $raw -replace "^﻿", "" | ConvertFrom-Json
    } catch {
        Write-Bad "$($c.Name): config is not valid JSON — the client will refuse to load any server"
        Write-Info "File: $($c.Path)"
        Write-Info "Run this script with -Fix to back it up and rebuild it."
        continue
    }

    $entry = $json.($c.Key).'BIM-Bot'
    if (-not $entry) {
        Write-Bad "$($c.Name): no BIM-Bot entry"
        Write-Info "Run this script with -Fix, or click 'Connect Claude' on the BIM-Bot ribbon."
        continue
    }

    $cmd = $entry.command
    $arg = if ($entry.args) { $entry.args[0] } else { $null }
    if (-not $cmd -or -not (Test-Path $cmd)) {
        Write-Bad "$($c.Name): BIM-Bot entry points at a missing command: '$cmd'"
        Write-Info "Run this script with -Fix."
    } elseif (-not $arg -or -not (Test-Path $arg)) {
        Write-Bad "$($c.Name): BIM-Bot entry points at a missing server script: '$arg'"
        Write-Info "Run this script with -Fix."
    } else {
        Write-Ok "$($c.Name): configured -> $arg"
    }
}

if (-not $anyClient) {
    Write-Warn "No MCP client config found for this user."
    Write-Info "Install Claude Desktop, then run this script with -Fix."
}

# ── 4. Revit-side service ───────────────────────────────────
Write-Head "BIM-Bot service (Revit side)"

$handshake = "$env:LOCALAPPDATA\BIMBot\service.json"
if (Test-Path $handshake) {
    try {
        $hs = Get-Content $handshake -Raw | ConvertFrom-Json
        $listening = Get-NetTCPConnection -State Listen -LocalPort $hs.port -ErrorAction SilentlyContinue
        if ($listening) {
            Write-Ok "Service listening on 127.0.0.1:$($hs.port) (Revit PID $($hs.pid))"
        } else {
            Write-Warn "Handshake says port $($hs.port), but nothing is listening there."
            Write-Info "Revit probably closed without shutting the service down. Reopen Revit."
        }
    } catch {
        Write-Warn "Handshake file exists but could not be read: $handshake"
    }
} else {
    Write-Warn "No handshake file — the Revit-side service has not started."
    Write-Info "Open Revit with a project, then check the BIM-Bot ribbon reads 'BIM-Bot ON'."
}

# Whoever holds the preferred port, even if BIM-Bot moved off it.
$holder = Get-NetTCPConnection -State Listen -LocalPort 8080 -ErrorAction SilentlyContinue | Select-Object -First 1
if ($holder) {
    $proc = Get-Process -Id $holder.OwningProcess -ErrorAction SilentlyContinue
    Write-Info "Port 8080 is held by PID $($holder.OwningProcess) ($($proc.ProcessName))."
    Write-Info "That is fine — BIM-Bot moves to the next free port and publishes it in the handshake file."
}

# ── 5. Logs ─────────────────────────────────────────────────
Write-Head "Logs"

$logDir = "$env:APPDATA\BIMBot\logs"
if (Test-Path $logDir) {
    $latest = Get-ChildItem $logDir -Filter *.log | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest) {
        Write-Ok "Latest log: $($latest.FullName)"
        $errs = Select-String -Path $latest.FullName -Pattern "ERROR" | Select-Object -Last 5
        if ($errs) {
            Write-Info "Last errors:"
            $errs | ForEach-Object { Write-Info "  $($_.Line.Trim())" }
        }
    }
} else {
    Write-Warn "No log directory yet ($logDir) — the plugin has never run."
}

# ── 6. Repair ───────────────────────────────────────────────
if ($Fix) {
    Write-Head "Repair"
    $configure = Join-Path $appDir "server\scripts\configure-claude.cjs"
    if ((Test-Path $nodeExe) -and (Test-Path $configure)) {
        & $nodeExe $configure
    } else {
        Write-Bad "Cannot repair: missing $configure or $nodeExe"
    }
}

# ── Summary ─────────────────────────────────────────────────
Write-Host ""
if ($script:Problems -eq 0) {
    Write-Host "  No problems found." -ForegroundColor Green
    Write-Host "  If Claude still cannot see BIM-Bot, fully quit it (File -> Exit, not just" -ForegroundColor DarkGray
    Write-Host "  the window X) and reopen — it only reads its config at startup." -ForegroundColor DarkGray
} else {
    Write-Host "  $($script:Problems) problem(s) found — see [FAIL] lines above." -ForegroundColor Red
    if (-not $Fix) {
        Write-Host "  Many of these are fixed by re-running with -Fix." -ForegroundColor Yellow
    }
}
Write-Host ""
