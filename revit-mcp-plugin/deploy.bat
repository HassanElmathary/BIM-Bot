@echo off
echo Deploying BIM-Bot DLL...
echo.

REM Kill Revit if still running
tasklist /fi "imagename eq Revit.exe" 2>nul | find /i "Revit.exe" >nul
if %errorlevel%==0 (
    echo ERROR: Revit is still running! Please close Revit first.
    pause
    exit /b 1
)

echo Copying files from staging to deployment...
xcopy /Y /E /Q "c:\Users\hassa\OneDrive\01-me\Revit MCP\revit-mcp-plugin\BIMBotPlugin\bin\Release\staging\*" "c:\Users\hassa\OneDrive\01-me\Revit MCP\revit-mcp-plugin\BIMBotPlugin\bin\Release\net8.0-windows-v3\"

if %errorlevel%==0 (
    echo.
    echo SUCCESS: DLL deployed! You can now open Revit.
) else (
    echo.
    echo FAILED: Could not copy files.
)
pause
