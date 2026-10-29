@echo off
REM =============================================
REM BIM-BOT: Fix Missing Revit Core Engine (RCE)
REM =============================================
REM This script creates a junction from the missing
REM Revit Interoperability 2023 path to the existing
REM 2027 version, allowing RevitExtractor.exe to work.
REM
REM RIGHT-CLICK THIS FILE AND SELECT "Run as administrator"
REM =============================================

echo BIM-BOT - Revit Core Engine Fix
echo ================================
echo.

set "TARGET=C:\Program Files\Common Files\Autodesk Shared\Revit Interoperability 2023"
set "SOURCE=C:\Program Files\Common Files\Autodesk Shared\Revit Interoperability 2027"

if exist "%TARGET%" (
    echo [OK] Revit Interoperability 2023 already exists.
    echo Nothing to do.
    goto :end
)

if not exist "%SOURCE%" (
    echo [ERROR] Revit Interoperability 2027 not found!
    echo Please install Revit Interoperability components.
    goto :end
)

echo Creating junction: %TARGET%
echo Pointing to: %SOURCE%
echo.

mklink /J "%TARGET%" "%SOURCE%"

if errorlevel 1 (
    echo.
    echo [ERROR] Failed to create junction.
    echo Make sure you are running this as Administrator!
) else (
    echo.
    echo [SUCCESS] Junction created successfully!
    echo RevitExtractor.exe should now work for Revit 2023 files.
)

:end
echo.
pause
