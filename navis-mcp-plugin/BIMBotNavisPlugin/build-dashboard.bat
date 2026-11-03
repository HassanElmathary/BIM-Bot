@echo off
cd /d "%~dp0"
echo Building BIM-Bot Navis plugin (Navisworks 2024)...
dotnet build -c Release -p:NavisYear=2024 -clp:ErrorsOnly;Summary > build.log 2>&1
type build.log
echo.
echo Log saved to build.log - tell Claude "built".
pause
