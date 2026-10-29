# Builds the portable BIMBot-Control.exe into mcp-dashboard\dist\.
# Needs: python -m pip install pyinstaller pywebview pystray pillow
#
# Two passes (see mcp-control.spec): the one-folder tool-filter proxy first,
# then the onefile window exe that bundles it. Work and dist folders live in
# %TEMP% so OneDrive can't lock files mid-build.
# PyInstaller logs to stderr; with "Stop", Windows PowerShell 5.1 would treat
# that as a failure. Exit codes are checked after each pass instead.
$ErrorActionPreference = "Continue"
$here = $PSScriptRoot
$work = Join-Path $env:TEMP "mcpctl-build"
$dist = Join-Path $env:TEMP "mcpctl-dist"

Push-Location $here
try {
    python -m PyInstaller proxy.spec --noconfirm --workpath "$work\proxy" --distpath $dist
    if ($LASTEXITCODE -ne 0) { throw "Proxy build failed" }

    $env:BIMBOT_PROXY_STAGING = Join-Path $dist "bimbot-mcp-proxy"
    python -m PyInstaller mcp-control.spec --noconfirm --workpath "$work\control" --distpath $dist
    if ($LASTEXITCODE -ne 0) { throw "Control panel build failed" }

    New-Item -ItemType Directory -Force (Join-Path $here "dist") | Out-Null
    Copy-Item -Force (Join-Path $dist "BIMBot-Control.exe") (Join-Path $here "dist\BIMBot-Control.exe")
    $exe = Get-Item (Join-Path $here "dist\BIMBot-Control.exe")
    Write-Host ("Built {0} ({1:N1} MB)" -f $exe.FullName, ($exe.Length / 1MB))
}
finally {
    Remove-Item Env:BIMBOT_PROXY_STAGING -ErrorAction SilentlyContinue
    Pop-Location
}
