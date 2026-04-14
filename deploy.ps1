# Deploy BIM-Bot Plugin to all installed Revit versions
# Routes net48 build to Revit 2020-2024, net8.0-windows build to Revit 2025-2026, net10.0-windows build to Revit 2027+

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceNet48 = Join-Path $scriptDir "revit-mcp-plugin\BIMBotPlugin\bin\Release\net48"
$sourceNet8 = Join-Path $scriptDir "revit-mcp-plugin\BIMBotPlugin\bin\Release\net8.0-windows"
$sourceNet10 = Join-Path $scriptDir "revit-mcp-plugin\BIMBotPlugin\bin\Release\net10.0-windows"
$destBase = "C:\Program Files\BIMBot\plugin"
$destNet48 = "$destBase\net48"
$destNet8 = "$destBase\net8"
$destNet10 = "$destBase\net10"

# Create plugin subdirectories
foreach ($dir in @($destNet48, $destNet8, $destNet10)) {
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

# Copy build outputs (skip missing targets)
if (Test-Path $sourceNet48) { Copy-Item "$sourceNet48\*" "$destNet48\" -Force -Recurse }
if (Test-Path $sourceNet8) { Copy-Item "$sourceNet8\*" "$destNet8\" -Force -Recurse }
if (Test-Path $sourceNet10) { Copy-Item "$sourceNet10\*" "$destNet10\" -Force -Recurse }

# Auto-detect installed Revit versions and deploy addin for each
$revitVersions = Get-ChildItem "C:\Program Files\Autodesk" -Directory |
Where-Object { $_.Name -match "^Revit (\d{4})$" } |
ForEach-Object { [int]$Matches[1] }

foreach ($year in $revitVersions) {
  # Route: 2020-2024 -> net48, 2025-2026 -> net8, 2027+ -> net10
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

  $addinDir = "C:\ProgramData\Autodesk\Revit\Addins\$year"
  if (-not (Test-Path $addinDir)) { New-Item -ItemType Directory -Path $addinDir -Force | Out-Null }

  # Legacy cleanup: remove old RevitMCP files
  $legacyAddin = Join-Path $addinDir "RevitMCP.addin"
  $legacyDir = Join-Path $addinDir "RevitMCP"
  if (Test-Path $legacyAddin) { Remove-Item $legacyAddin -Force; Write-Host "  Removed legacy RevitMCP.addin for Revit $year" -ForegroundColor DarkYellow }
  if (Test-Path $legacyDir) { Remove-Item $legacyDir -Recurse -Force; Write-Host "  Removed legacy RevitMCP/ folder for Revit $year" -ForegroundColor DarkYellow }

  $addinContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>BIM-Bot Plugin</Name>
    <Assembly>$dllPath</Assembly>
    <FullClassName>BIMBotPlugin.Core.Application</FullClassName>
    <ClientId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</ClientId>
    <VendorId>HassanElmathary</VendorId>
    <VendorDescription>AI-Powered BIM-Bot Plugin by Hassan Ahmed Elmathary</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
  $addinContent | Out-File -Encoding utf8 "$addinDir\BIMBot.addin" -Force
  Write-Host "Deployed addin for Revit $year ($fw)"
}

# Verify
Write-Host ""
foreach ($fw in @(@{Name="net48";Path=$destNet48}, @{Name="net8";Path=$destNet8}, @{Name="net10";Path=$destNet10})) {
  $dll = Get-Item "$($fw.Path)\BIMBotPlugin.dll" -ErrorAction SilentlyContinue
  if ($dll) {
    Write-Host "=== $($fw.Name) DLL ===" -ForegroundColor Cyan
    Write-Host "  Size: $($dll.Length) bytes | Date: $($dll.LastWriteTime)"
  }
}
Write-Host ""
Write-Host "Deployed to Revit versions: $($revitVersions -join ', ')"
Write-Host "Deploy complete!" -ForegroundColor Green
