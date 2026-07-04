@echo off
:: ============================================================
::  BIM-Bot v2.1.0 — Fix & Correct Deployment
::  Self-elevating — no need to right-click "Run as Admin"
:: ============================================================
title BIM-Bot v2.1.0 — Fix Installation

:: Auto-elevate to Administrator
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting Administrator privileges...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"%~f0\"' -Verb RunAs -Wait"
    exit /b
)

setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
set "SRC_PLUGIN_NET48=%SCRIPT_DIR%revit-mcp-plugin\BIMBotPlugin\bin\Release\net48"
set "SRC_PLUGIN_NET8=%SCRIPT_DIR%revit-mcp-plugin\BIMBotPlugin\bin\Release\net8.0-windows"
set "SRC_PLUGIN_NET10=%SCRIPT_DIR%revit-mcp-plugin\BIMBotPlugin\bin\Release\net10.0-windows"
set "SRC_SERVER=%SCRIPT_DIR%revit-mcp-server"
set "INSTALL_DIR=C:\Program Files\BIMBot"
set "PLUGIN_DEST=%INSTALL_DIR%\plugin"
set "SERVER_DEST=%INSTALL_DIR%\server"
set "NODE_EXE=%INSTALL_DIR%\nodejs\node.exe"
set "SERVER_JS=%INSTALL_DIR%\server\build\index.js"
set "CLAUDE_CONFIG=%APPDATA%\Claude\claude_desktop_config.json"

echo.
echo   ==========================================
echo     BIM-Bot v2.1.0 - Installation Fix
echo     by Hassan Ahmed Elmathary
echo   ==========================================
echo.

:: ── STEP 1: Take ownership of BIMBot install dir ────────────
echo   [1/7] Taking ownership of install directory...
takeown /f "%INSTALL_DIR%" /r /d y >nul 2>&1
icacls "%INSTALL_DIR%" /grant Administrators:F /t /q >nul 2>&1
echo         Done.

:: ── STEP 2: Remove old conflicting installations ─────────────
echo   [2/7] Removing old RevitMCP files and conflicts...
if exist "C:\Program Files\RevitMCP" (
    rd /s /q "C:\Program Files\RevitMCP" 2>nul
    echo         Removed C:\Program Files\RevitMCP
)

for %%Y in (2020 2021 2022 2023 2024 2025 2026 2027) do (
    set "ADDIN_DIR=C:\ProgramData\Autodesk\Revit\Addins\%%Y"
    if exist "!ADDIN_DIR!" (
        if exist "!ADDIN_DIR!\RevitMCP.addin" (
            del /f /q "!ADDIN_DIR!\RevitMCP.addin"
            echo         Removed RevitMCP.addin for Revit %%Y
        )
        if exist "!ADDIN_DIR!\RevitMCP" (
            rd /s /q "!ADDIN_DIR!\RevitMCP"
            echo         Removed RevitMCP\ folder for Revit %%Y
        )
        if exist "!ADDIN_DIR!\BIMBot" (
            rd /s /q "!ADDIN_DIR!\BIMBot"
            echo         Removed BIMBot\ subfolder for Revit %%Y
        )
    )
)
echo         Done.

:: ── STEP 3: Deploy v2.1.0 Plugin DLLs ───────────────────────
echo   [3/7] Deploying BIMBotPlugin v2.1.0 DLLs...
set COPIED_ANY=0

if exist "%SRC_PLUGIN_NET48%\BIMBotPlugin.dll" (
    if not exist "%PLUGIN_DEST%\net48" mkdir "%PLUGIN_DEST%\net48"
    robocopy "%SRC_PLUGIN_NET48%" "%PLUGIN_DEST%\net48" /MIR /R:2 /W:1 /NP /NFL /NDL >nul 2>&1
    set COPIED_ANY=1
    echo         Installed net48 plugin.
)
if exist "%SRC_PLUGIN_NET8%\BIMBotPlugin.dll" (
    if not exist "%PLUGIN_DEST%\net8" mkdir "%PLUGIN_DEST%\net8"
    robocopy "%SRC_PLUGIN_NET8%" "%PLUGIN_DEST%\net8" /MIR /R:2 /W:1 /NP /NFL /NDL >nul 2>&1
    set COPIED_ANY=1
    echo         Installed net8 plugin.
)
if exist "%SRC_PLUGIN_NET10%\BIMBotPlugin.dll" (
    if not exist "%PLUGIN_DEST%\net10" mkdir "%PLUGIN_DEST%\net10"
    robocopy "%SRC_PLUGIN_NET10%" "%PLUGIN_DEST%\net10" /MIR /R:2 /W:1 /NP /NFL /NDL >nul 2>&1
    set COPIED_ANY=1
    echo         Installed net10 plugin.
)

if %COPIED_ANY%==0 (
    echo   [ERROR] No plugin DLLs found to copy!
    echo           Please rebuild the plugin in Visual Studio first.
    pause & exit /b 1
)
echo         Done.

:: ── STEP 4: Deploy v2.1.0 MCP Server ─────────────────────────
echo   [4/7] Deploying MCP Server v2.1.0...
if not exist "%SRC_SERVER%\build\index.js" (
    echo   [ERROR] Server build not found at: %SRC_SERVER%\build
    pause & exit /b 1
)

if not exist "%SERVER_DEST%" mkdir "%SERVER_DEST%"

:: Deploy server build (the compiled JS - critical)
robocopy "%SRC_SERVER%\build" "%SERVER_DEST%\build" /MIR /R:2 /W:1 /NP /NFL /NDL >nul 2>&1
:: Deploy node_modules
robocopy "%SRC_SERVER%\node_modules" "%SERVER_DEST%\node_modules" /MIR /R:2 /W:1 /NP /NFL /NDL >nul 2>&1
:: Copy package.json
copy /y "%SRC_SERVER%\package.json" "%SERVER_DEST%\package.json" >nul
echo         Server files deployed.
echo         Done.

:: ── STEP 5: Write correct .addin files ──────────────────────
echo   [5/7] Writing .addin manifest files for Revit...
set FOUND_REVIT=0

for %%Y in (2020 2021 2022 2023 2024 2025 2026 2027) do (
    if exist "C:\Program Files\Autodesk\Revit %%Y" (
        set FOUND_REVIT=1
        set "ADDIN_DIR=C:\ProgramData\Autodesk\Revit\Addins\%%Y"
        if not exist "!ADDIN_DIR!" mkdir "!ADDIN_DIR!"

        set "FW=net8"
        if %%Y LEQ 2024 set "FW=net48"
        if %%Y GEQ 2027 set "FW=net10"
        
        set "DLL_PATH=%PLUGIN_DEST%\!FW!\BIMBotPlugin.dll"

        (
            echo ^<?xml version="1.0" encoding="utf-8"?^>
            echo ^<RevitAddIns^>
            echo   ^<AddIn Type="Application"^>
            echo     ^<Name^>BIM-Bot Plugin^</Name^>
            echo     ^<Assembly^>!DLL_PATH!^</Assembly^>
            echo     ^<FullClassName^>BIMBotPlugin.Core.Application^</FullClassName^>
            echo     ^<ClientId^>A1B2C3D4-E5F6-7890-ABCD-EF1234567890^</ClientId^>
            echo     ^<VendorId^>HassanElmathary^</VendorId^>
            echo     ^<VendorDescription^>AI-Powered BIM-Bot Plugin v2.1.0 by Hassan Ahmed Elmathary^</VendorDescription^>
            echo   ^</AddIn^>
            echo ^</RevitAddIns^>
        ) > "!ADDIN_DIR!\BIMBot.addin"
        echo         Revit %%Y [!FW!]: !ADDIN_DIR!\BIMBot.addin
    )
)
if %FOUND_REVIT%==0 echo         [WARN] No Revit installation found in C:\Program Files\Autodesk\
echo         Done.

:: ── STEP 6: Fix Claude Desktop Config ───────────────────────
echo   [6/7] Configuring Claude Desktop (auto-mode)...
if not exist "%APPDATA%\Claude" mkdir "%APPDATA%\Claude"

:: Write config using PowerShell for proper JSON
powershell -Command ^
    "$config = @{ mcpServers = @{ 'BIM-Bot' = @{ command = '%NODE_EXE:\=\\%'; args = @('%SERVER_JS:\=\\%'); env = @{} } } }; $config | ConvertTo-Json -Depth 10 | Out-File -Encoding utf8 '%CLAUDE_CONFIG:\=\\%' -Force; Write-Host 'Claude config written.'"

echo         Done.

:: ── STEP 7: Create launcher ──────────────────────────────────
echo   [7/7] Creating launcher shortcut...
(
    echo @echo off
    echo title BIM-Bot MCP Server v2.1.0
    echo echo.
    echo echo   Starting BIM-Bot MCP Server v2.1.0...
    echo echo   Press Ctrl+C to stop.
    echo echo.
    echo "%NODE_EXE%" "%SERVER_JS%"
    echo pause
) > "%INSTALL_DIR%\Start MCP Server.bat"
echo         Done.

:: ── VERIFICATION ─────────────────────────────────────────────
echo.
echo   ==========================================
echo     Verification
echo   ==========================================
echo.

set PASS=1

:: Check DLL exists
set DLL_PASS=0
if exist "%PLUGIN_DEST%\net48\BIMBotPlugin.dll" (
    echo   [OK] net48\BIMBotPlugin.dll installed
    set DLL_PASS=1
)
if exist "%PLUGIN_DEST%\net8\BIMBotPlugin.dll" (
    echo   [OK] net8\BIMBotPlugin.dll installed
    set DLL_PASS=1
)
if exist "%PLUGIN_DEST%\net10\BIMBotPlugin.dll" (
    echo   [OK] net10\BIMBotPlugin.dll installed
    set DLL_PASS=1
)

if %DLL_PASS%==0 (
    echo   [FAIL] BIMBotPlugin.dll MISSING from all framework folders!
    set PASS=0
)

:: Check addin content for Revit 2026
if exist "C:\ProgramData\Autodesk\Revit\Addins\2026\BIMBot.addin" (
    findstr /c:"net8\BIMBotPlugin.dll" "C:\ProgramData\Autodesk\Revit\Addins\2026\BIMBot.addin" >nul 2>&1
    if !errorlevel! equ 0 (
        echo   [OK] Revit 2026 addin points to net8\BIMBotPlugin.dll
    ) else (
        echo   [FAIL] Revit 2026 addin has wrong DLL path!
        set PASS=0
    )
)

:: Check old RevitMCP.addin is gone
if exist "C:\ProgramData\Autodesk\Revit\Addins\2026\RevitMCP.addin" (
    echo   [FAIL] Old RevitMCP.addin still exists!
    set PASS=0
) else (
    echo   [OK] Old RevitMCP.addin removed
)

:: Check server build
if exist "%SERVER_DEST%\build\index.js" (
    echo   [OK] Server build\index.js present
) else (
    echo   [FAIL] Server build\index.js MISSING!
    set PASS=0
)

:: Check Claude config
if exist "%CLAUDE_CONFIG%" (
    findstr /c:"BIM-Bot" "%CLAUDE_CONFIG%" >nul 2>&1
    if !errorlevel! equ 0 (
        echo   [OK] Claude Desktop configured with BIM-Bot
    ) else (
        echo   [FAIL] Claude config missing BIM-Bot entry!
        set PASS=0
    )
) else (
    echo   [FAIL] Claude config not found!
    set PASS=0
)

:: Check node.exe
if exist "%NODE_EXE%" (
    echo   [OK] Node.js runtime: %NODE_EXE%
) else (
    echo   [FAIL] Node.js not found at %NODE_EXE%!
    set PASS=0
)

echo.
if %PASS%==1 (
    echo   ==========================================
    echo     SUCCESS! BIM-Bot v2.1.0 is ready!
    echo   ==========================================
    echo.
    echo   NEXT STEPS:
    echo     1. CLOSE and RESTART Revit 2026
    echo        - Look for "BIM-Bot" tab in the ribbon
    echo        - Click "Start MCP Service" to enable socket
    echo.
    echo     2. CLOSE and RESTART Claude Desktop
    echo        - BIM-Bot tools appear automatically
    echo        - Look for the hammer icon (tools)
    echo.
) else (
    echo   ==========================================
    echo     Some checks FAILED - see errors above
    echo   ==========================================
)

echo.
pause
