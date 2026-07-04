@echo off
echo ========================================
echo   BIM-Bot Plugin Deployment
echo ========================================
echo.

REM Kill Revit if still running
tasklist /fi "imagename eq Revit.exe" 2>nul | find /i "Revit.exe" >nul
if %errorlevel%==0 (
    echo ERROR: Revit is still running! Please close Revit first.
    pause
    exit /b 1
)

REM Derive paths from this script's location — never hardcode the repo path
set "SOURCE_DIR=%~dp0BIMBotPlugin"
set "DLL_PATH=%SOURCE_DIR%\bin\Release\net8.0-windows\BIMBotPlugin.dll"
set "ADDIN_DIR=%APPDATA%\Autodesk\Revit\Addins\2026"

echo Source:  %SOURCE_DIR%
echo Target:  %ADDIN_DIR%
echo.

REM Build first so the deployed manifest always points at a fresh DLL
echo [1/3] Building plugin (Release, net8.0-windows)...
dotnet build "%SOURCE_DIR%" -f net8.0-windows -c Release --nologo -v q
if %errorlevel% neq 0 (
    echo FAILED: Build errors — fix them and re-run.
    pause
    exit /b 1
)

REM Ensure the addins directory exists
if not exist "%ADDIN_DIR%" mkdir "%ADDIN_DIR%"

REM Write the .addin manifest with the DLL path resolved from this repo
echo [2/3] Writing .addin manifest...
(
    echo ^<?xml version="1.0" encoding="utf-8"?^>
    echo ^<RevitAddIns^>
    echo   ^<AddIn Type="Application"^>
    echo     ^<Name^>BIM-Bot Plugin^</Name^>
    echo     ^<Assembly^>%DLL_PATH%^</Assembly^>
    echo     ^<FullClassName^>BIMBotPlugin.Core.Application^</FullClassName^>
    echo     ^<ClientId^>A1B2C3D4-E5F6-7890-ABCD-EF1234567890^</ClientId^>
    echo     ^<VendorId^>BIM-Bot^</VendorId^>
    echo     ^<VendorDescription^>AI-Powered BIM-Bot Plugin^</VendorDescription^>
    echo   ^</AddIn^>
    echo ^</RevitAddIns^>
) > "%ADDIN_DIR%\BIMBot.addin"

REM Verify the DLL exists at the path referenced in the .addin
echo [3/3] Verifying DLL exists...
if exist "%DLL_PATH%" (
    echo DLL found at: %DLL_PATH%
) else (
    echo FAILED: DLL not found at %DLL_PATH%
    pause
    exit /b 1
)

echo.
echo ========================================
echo   SUCCESS: BIM-Bot deployed to Revit 2026
echo ========================================
echo.
echo Manifest: %ADDIN_DIR%\BIMBot.addin
echo DLL:      %DLL_PATH%
echo.
echo You can now open Revit — the BIM-Bot tab should appear.
echo.
pause
