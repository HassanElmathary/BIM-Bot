@echo off
echo ============================================
echo   RevitMCP - Deploy New Build
echo ============================================
echo.

:: Update .addin manifests for 2025, 2026, and 2027
echo Updating .addin manifests...
(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<RevitAddIns^>
echo   ^<AddIn Type="Application"^>
echo     ^<Name^>BIM-Bot Plugin^</Name^>
echo     ^<Assembly^>c:\Users\hassa\OneDrive\01-me\Revit MCP\revit-mcp-plugin\RevitMCPPlugin\bin\Release\net8.0-windows\RevitMCPPlugin.dll^</Assembly^>
echo     ^<FullClassName^>RevitMCPPlugin.Core.Application^</FullClassName^>
echo     ^<ClientId^>A1B2C3D4-E5F6-7890-ABCD-EF1234567890^</ClientId^>
echo     ^<VendorId^>RevitMCP^</VendorId^>
echo     ^<VendorDescription^>AI-Powered BIM-Bot Plugin^</VendorDescription^>
echo   ^</AddIn^>
echo ^</RevitAddIns^>
) > "C:\ProgramData\Autodesk\Revit\Addins\2025\RevitMCP.addin"

(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<RevitAddIns^>
echo   ^<AddIn Type="Application"^>
echo     ^<Name^>BIM-Bot Plugin^</Name^>
echo     ^<Assembly^>c:\Users\hassa\OneDrive\01-me\Revit MCP\revit-mcp-plugin\RevitMCPPlugin\bin\Release\net8.0-windows\RevitMCPPlugin.dll^</Assembly^>
echo     ^<FullClassName^>RevitMCPPlugin.Core.Application^</FullClassName^>
echo     ^<ClientId^>A1B2C3D4-E5F6-7890-ABCD-EF1234567890^</ClientId^>
echo     ^<VendorId^>RevitMCP^</VendorId^>
echo     ^<VendorDescription^>AI-Powered BIM-Bot Plugin^</VendorDescription^>
echo   ^</AddIn^>
echo ^</RevitAddIns^>
) > "C:\ProgramData\Autodesk\Revit\Addins\2026\RevitMCP.addin"

(
echo ^<?xml version="1.0" encoding="utf-8"?^>
echo ^<RevitAddIns^>
echo   ^<AddIn Type="Application"^>
echo     ^<Name^>BIM-Bot Plugin^</Name^>
echo     ^<Assembly^>c:\Users\hassa\OneDrive\01-me\Revit MCP\revit-mcp-plugin\RevitMCPPlugin\bin\Release\net10.0-windows\RevitMCPPlugin.dll^</Assembly^>
echo     ^<FullClassName^>RevitMCPPlugin.Core.Application^</FullClassName^>
echo     ^<ClientId^>A1B2C3D4-E5F6-7890-ABCD-EF1234567890^</ClientId^>
echo     ^<VendorId^>RevitMCP^</VendorId^>
echo     ^<VendorDescription^>AI-Powered BIM-Bot Plugin^</VendorDescription^>
echo   ^</AddIn^>
echo ^</RevitAddIns^>
) > "C:\ProgramData\Autodesk\Revit\Addins\2027\RevitMCP.addin"

echo Done!
echo.
echo 2025 and 2026 .addin files point to: bin\Release\net8.0-windows\RevitMCPPlugin.dll
echo 2027 .addin file points to:          bin\Release\net10.0-windows\RevitMCPPlugin.dll
echo.
echo Launch Revit to see the new design!
echo.
pause

