# Deploy RevitMCPPlugin to Revit

$source = "c:\Users\hassa\OneDrive\01-me\Revit MCP\revit-mcp-plugin\RevitMCPPlugin\bin\Release\net48"
$dest = "C:\Program Files\Revit MCP\plugin"

# Copy all build output
Copy-Item "$source\*" "$dest\" -Force -Recurse

# Update addin file to point to correct DLL
$addinContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Revit MCP Plugin</Name>
    <Assembly>C:\Program Files\Revit MCP\plugin\RevitMCPPlugin.dll</Assembly>
    <FullClassName>RevitMCPPlugin.Core.Application</FullClassName>
    <ClientId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</ClientId>
    <VendorId>RevitMCP</VendorId>
    <VendorDescription>AI-Powered Revit MCP Plugin</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
$addinContent | Out-File -Encoding utf8 "C:\ProgramData\Autodesk\Revit\Addins\2025\RevitMCP.addin" -Force

# Verify
$dll = Get-Item "$dest\RevitMCPPlugin.dll"
Write-Host "DLL Size: $($dll.Length) bytes"
Write-Host "DLL Date: $($dll.LastWriteTime)"
Write-Host "Deploy complete!"
