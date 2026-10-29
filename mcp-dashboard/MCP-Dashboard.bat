@echo off
rem Runs BIM-Bot MCP Control from source (desktop window + tray icon).
rem For browser mode instead:  python server.py
cd /d "%~dp0"
python app.py %*
if errorlevel 1 pause
