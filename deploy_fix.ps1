$addinContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Revit MCP Plugin</Name>
    <Assembly>C:\ProgramData\Autodesk\Revit\Addins\2025\RevitMCP\RevitMCPPlugin.dll</Assembly>
    <FullClassName>RevitMCPPlugin.Core.Application</FullClassName>
    <ClientId>A1B2C3D4-E5F6-7890-ABCD-EF1234567890</ClientId>
    <VendorId>RevitMCP</VendorId>
    <VendorDescription>AI-Powered Revit MCP Plugin</VendorDescription>
  </AddIn>
</RevitAddIns>
"@

[System.IO.File]::WriteAllText('C:\ProgramData\Autodesk\Revit\Addins\2025\RevitMCP.addin', $addinContent, [System.Text.Encoding]::UTF8)

# Also copy the latest DLL
Copy-Item 'C:\Users\Hassan Elmthary\OneDrive\01-me\Revit MCP\revit-mcp-plugin\RevitMCPPlugin\bin\Release\net48\RevitMCPPlugin.dll' 'C:\ProgramData\Autodesk\Revit\Addins\2025\RevitMCP\RevitMCPPlugin.dll' -Force

Write-Host "Done - addin and DLL deployed"
