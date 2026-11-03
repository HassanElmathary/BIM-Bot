# BIM-Bot 2.8.0 release build. Run via installer\release-2.8.0.bat (log: installer\release-2.8.0.log)
# 1. Navisworks 2024 build with compiler errors shown (build-installer.ps1 hides them)
# 2. Full installer build (all Revit bands, Navisworks 2020-2027, MCP servers, Inno Setup)
# 3. Copy to BIMBot-Setup-2.8.0.exe and print its SHA-256 for version.json
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$version = "2.8.0"

Write-Host "[0/4] Navisworks 2024 compile check..."
Push-Location "$root\navis-mcp-plugin\BIMBotNavisPlugin"
cmd /c "dotnet build -c Release -p:NavisYear=2024 -clp:ErrorsOnly;Summary 2>&1"
$code = $LASTEXITCODE
Pop-Location
if ($code -ne 0) { Write-Host "FAILED: Navisworks plugin does not compile - see errors above."; exit 1 }

& powershell -ExecutionPolicy Bypass -File "$root\installer\build-installer.ps1"
if ($LASTEXITCODE -ne 0) { Write-Host "FAILED: build-installer.ps1"; exit 1 }

$src = "$root\installer\output\BIM-Bot.exe"
$dst = "$root\installer\output\BIMBot-Setup-$version.exe"
Copy-Item $src $dst -Force
$sha = (Get-FileHash $dst -Algorithm SHA256).Hash.ToLower()
Write-Host ""
Write-Host "RELEASE FILE: $dst"
Write-Host "SHA256: $sha"
