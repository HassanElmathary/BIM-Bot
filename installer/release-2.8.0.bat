@echo off
cd /d "%~dp0"
echo Building BIM-Bot 2.8.0 release (takes several minutes)...
powershell -ExecutionPolicy Bypass -File "%~dp0release-2.8.0.ps1" > "%~dp0release-2.8.0.log" 2>&1
type "%~dp0release-2.8.0.log"
echo.
echo Done. Tell Claude "built".
pause
