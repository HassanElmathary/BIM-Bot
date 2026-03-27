# Deploy RevitMCPPlugin to all installed Revit versions
# Routes net48 build to Revit 2020-2024, net8.0-windows build to Revit 2025-2026

$sourceNet48 = "c:\Users\hassa\OneDrive\01-me\Revit MCP\revit-mcp-plugin\RevitMCPPlugin\bin\Release\net48"
$sourceNet8 = "c:\Users\hassa\OneDrive\01-me\Revit MCP\revit-mcp-plugin\RevitMCPPlugin\bin\Release\net8.0-windows"
$destBase = "C:\Program Files\Revit MCP\plugin"
$destNet48 = "$destBase\net48"
$destNet8 = "$destBase\net8"

# Create plugin subdirectories
foreach ($dir in @($destNet48, $destNet8)) {
  if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
}

# Copy build outputs
Copy-Item "$sourceNet48\*" "$destNet48\" -Force -Recurse
Copy-Item "$sourceNet8\*" "$destNet8\" -Force -Recurse

# Auto-detect installed Revit versions and deploy addin for each
$revitVersions = Get-ChildItem "C:\Program Files\Autodesk" -Directory |
Where-Object { $_.Name -match "^Revit (\d{4})$" } |
ForEach-Object { [int]$Matches[1] }

foreach ($year in $revitVersions) {
  # Route: 2020-2024 -> net48, 2025+ -> net8
  if ($year -le 2024) {
    $dllPath = "$destNet48\RevitMCPPlugin.dll"
  }
  else {
    $dllPath = "$destNet8\RevitMCPPlugin.dll"
  }

  $addinContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Revit MCP Plugin</Name>
    <Assembly>$dllPath</Assembly>
    <FullClassName>RevitMCPPlugin.Core.Application</FullClassName>
    <ClientId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</ClientId>
    <VendorId>RevitMCP</VendorId>
    <VendorDescription>AI-Powered Revit MCP Plugin</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
  $addinDir = "C:\ProgramData\Autodesk\Revit\Addins\$year"
  if (-not (Test-Path $addinDir)) { New-Item -ItemType Directory -Path $addinDir -Force | Out-Null }
  $addinContent | Out-File -Encoding utf8 "$addinDir\RevitMCP.addin" -Force
  $fw = if ($year -le 2024) { "net48" } else { "net8" }
  Write-Host "Deployed addin for Revit $year ($fw)"
}

# Verify
Write-Host ""
Write-Host "=== net48 DLL ==="
$dll48 = Get-Item "$destNet48\RevitMCPPlugin.dll"
Write-Host "  Size: $($dll48.Length) bytes | Date: $($dll48.LastWriteTime)"
Write-Host "=== net8 DLL ==="
$dll8 = Get-Item "$destNet8\RevitMCPPlugin.dll"
Write-Host "  Size: $($dll8.Length) bytes | Date: $($dll8.LastWriteTime)"
Write-Host ""
Write-Host "Deployed to Revit versions: $($revitVersions -join ', ')"
Write-Host "Deploy complete!"
