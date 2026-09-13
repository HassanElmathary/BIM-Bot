# ============================================================
#  BIM-Bot Prerequisites — strict Step 0/1 for any machine
#
#  Checks every runtime BIM-Bot needs and downloads + installs
#  whatever is missing. Safe to run repeatedly (idempotent).
#
#  Usage:
#    powershell -ExecutionPolicy Bypass -File installer\Install-Prerequisites.ps1
#
#  What it ensures (in order):
#    0. Windows 10 1809+ x64
#    1. At least one Revit 2020-2027 detected (warn only)
#    2. .NET Framework 4.7.2+ (registry check, built into Win10+)
#    3. .NET 8 Desktop Runtime (needed by Revit 2025+ plugin band;
#       Revit itself ships it, this is a fallback for helpers)
#    4. WebView2 Evergreen Runtime (PowerBI viewer / update dialogs)
#    5. VC++ 2015-2022 x64 Redist (better-sqlite3 native module)
#    6. Bundled portable Node.js present (no system Node required)
#
#  Strategy: winget first (silent, trusted), direct download to
#  $env:TEMP\BIMBot-Prereqs as fallback. Machine-wide installers
#  that need elevation abort with the exact manual command instead
#  of failing silently when running non-admin.
# ============================================================

param(
    [switch]$Quiet
)

$ErrorActionPreference = "Continue"
$script:Failed = 0
$script:PrereqDir = Join-Path $env:TEMP "BIMBot-Prereqs"
if (-not (Test-Path $script:PrereqDir)) { New-Item -ItemType Directory $script:PrereqDir -Force | Out-Null }

function Write-Step($t) { if (-not $Quiet) { Write-Host ""; Write-Host "  [$t]" -ForegroundColor Cyan } }
function Write-Ok($t)   { if (-not $Quiet) { Write-Host "    [OK] $t" -ForegroundColor Green } }
function Write-Fail($t) { Write-Host "    [FAIL] $t" -ForegroundColor Red; $script:Failed++ }
function Write-Need($t) { if (-not $Quiet) { Write-Host "    [ .. ] $t" -ForegroundColor Yellow } }

function Test-IsAdmin {
    try {
        $id = [Security.Principal.WindowsIdentity]::GetCurrent()
        return ([Security.Principal.WindowsPrincipal]$id).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    } catch { return $false }
}
$script:IsAdmin = Test-IsAdmin

function Install-WithWinget($id, $exeArgs) {
    $winget = (Get-Command winget -ErrorAction SilentlyContinue).Source
    if (-not $winget) { return $false }
    try {
        & $winget install --id $id --silent --accept-package-agreements --accept-source-agreements $exeArgs 2>&1 | Out-Null
        return ($LASTEXITCODE -eq 0)
    } catch { return $false }
}

function Install-Exe($url, $fileName, $args) {
    $out = Join-Path $script:PrereqDir $fileName
    try {
        if (-not (Test-Path $out)) {
            Write-Need "Downloading $fileName ..."
            Invoke-WebRequest -Uri $url -OutFile $out -UseBasicParsing
        }
        Write-Need "Installing $fileName ..."
        $p = Start-Process -FilePath $out -ArgumentList $args -Wait -PassThru
        return ($p.ExitCode -eq 0)
    } catch {
        Write-Fail "$fileName failed: $($_.Exception.Message)"
        return $false
    }
}

# ── 0. OS ────────────────────────────────────────────────────
Write-Step "0/6 Windows version"
$os = Get-CimInstance Win32_OperatingSystem
$arch = $env:PROCESSOR_ARCHITECTURE
if ($os.BuildNumber -ge 17763 -and $arch -eq "AMD64") { Write-Ok "$($os.Caption) build $($os.BuildNumber) x64" }
else { Write-Fail "Requires Windows 10 1809+ x64 (found $($os.Caption) $($os.BuildNumber) $arch)" }

# ── 1. Revit ─────────────────────────────────────────────────
Write-Step "1/6 Revit 2020-2027"
$found = @()
foreach ($y in 2020..2027) {
    foreach ($d in 67..90 | ForEach-Object { "$([char]$_):\" }) {
        if ((Test-Path "$d\Program Files\Autodesk\Revit $y") -or (Test-Path "$d\Autodesk\Revit $y")) { $found += $y; break }
    }
}
if ($found.Count -gt 0) { Write-Ok "Revit detected: $($found -join ', ')" }
else { Write-Host "    [WARN] No Revit detected — plugin deploys to 2023+2026 by default; install Revit first for real use." -ForegroundColor Yellow }

# ── 2. .NET Framework 4.7.2+ ─────────────────────────────────
Write-Step "2/6 .NET Framework 4.7.2+ (Revit 2020-2024 bands)"
try {
    $rel = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" -ErrorAction Stop).Release
    if ($rel -ge 461808) { Write-Ok ".NET Framework release $rel" }
    else { Write-Fail ".NET Framework too old (release $rel, need >= 461808). Enable via Windows Features." }
} catch { Write-Fail ".NET Framework 4.x not found. Enable '.NET Framework 4.8' in Windows Features." }

# ── 3. .NET 8 Desktop Runtime ─────────────────────────────────
Write-Step "3/6 .NET 8 Desktop Runtime (Revit 2025+ bands)"
$hasNet8 = $false
try {
    $runtimes = & dotnet --list-runtimes 2>$null
    if ($runtimes -match "Microsoft\.WindowsDesktop\.App 8\.") { $hasNet8 = $true }
} catch {}
if ($hasNet8) { Write-Ok ".NET 8 Desktop Runtime present" }
else {
    # Revit 2025+ ships its own .NET 8, so this is a fallback — still install for helpers.
    if (Install-WithWinget "Microsoft.DotNet.DesktopRuntime.8" @()) { Write-Ok "Installed via winget" }
    else {
        if (-not $script:IsAdmin) { Write-Fail "Missing .NET 8 Desktop Runtime and not elevated. Run as admin OR install manually: https://dotnet.microsoft.com/download/dotnet/8.0" }
        else {
            $ok = Install-Exe "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe" "windowsdesktop-runtime-8.exe" "/install /quiet /norestart"
            if ($ok) { Write-Ok "Installed .NET 8 Desktop Runtime" } else { Write-Fail "Could not install .NET 8 Desktop Runtime" }
        }
    }
}

# ── 4. WebView2 ───────────────────────────────────────────────
Write-Step "4/6 WebView2 Runtime (PowerBI viewer / dialogs)"
$wv = $false
foreach ($k in @("HKLM:\SOFTWARE\Microsoft\EdgeUpdate\ClientState\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}", "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\ClientState\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}")) {
    try { if ((Get-ItemProperty $k -ErrorAction Stop).pv) { $wv = $true } } catch {}
}
try { if (Test-Path "$env:ProgramFiles (x86)\Microsoft\EdgeWebView\Application\msedgewebview2.exe") { $wv = $true } } catch {}
if ($wv) { Write-Ok "WebView2 present" }
else {
    if (Install-WithWinget "Microsoft.EdgeWebView2Runtime" @()) { Write-Ok "Installed via winget" }
    else {
        $ok = Install-Exe "https://go.microsoft.com/fwlink/p/?LinkId=2124703" "MicrosoftEdgeWebview2Setup.exe" "/silent /install"
        if ($ok) { Write-Ok "Installed WebView2" } else { Write-Fail "Could not install WebView2" }
    }
}

# ── 5. VC++ Redist ────────────────────────────────────────────
Write-Step "5/6 VC++ 2015-2022 x64 (native sqlite module)"
$vc = Test-Path "$env:SystemRoot\System32\vcruntime140.dll"
if ($vc) { Write-Ok "VC++ runtime present" }
else {
    if (Install-WithWinget "Microsoft.VCRedist.2015+.x64" @()) { Write-Ok "Installed via winget" }
    else {
        if (-not $script:IsAdmin) { Write-Fail "Missing VC++ Redist and not elevated. Run as admin OR install manually: https://aka.ms/vs/17/release/vc_redist.x64.exe" }
        else {
            $ok = Install-Exe "https://aka.ms/vs/17/release/vc_redist.x64.exe" "vc_redist.x64.exe" "/install /quiet /norestart"
            if ($ok) { Write-Ok "Installed VC++ Redist" } else { Write-Fail "Could not install VC++ Redist" }
        }
    }
}

# ── 6. Bundled Node.js ────────────────────────────────────────
Write-Step "6/6 Portable Node.js (bundled — no system Node needed)"
$here = $PSScriptRoot
if (-not $here) { $here = Get-Location }
$bundled = Join-Path (Split-Path $here -Parent) "installer\nodejs\node.exe"
if (-not (Test-Path $bundled)) { $bundled = Join-Path $here "nodejs\node.exe" }
if (Test-Path $bundled) {
    try { $v = & $bundled --version 2>$null; Write-Ok "Bundled Node.js $v" }
    catch { Write-Fail "Bundled node.exe exists but does not run: $bundled" }
} else {
    $sys = (Get-Command node -ErrorAction SilentlyContinue).Source
    if ($sys) { Write-Host "    [WARN] Bundled node missing; system node at $sys will be used (must be >= 18)." -ForegroundColor Yellow }
    else { Write-Fail "No Node.js at all (bundled $bundled missing, none on PATH). Re-download the full BIM-Bot installer." }
}

Write-Host ""
if ($script:Failed -eq 0) { Write-Host "  Prerequisites: ALL OK" -ForegroundColor Green; exit 0 }
else { Write-Host "  Prerequisites: $script:Failed item(s) FAILED — see above." -ForegroundColor Red; exit 1 }
