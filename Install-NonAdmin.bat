@echo off
title BIM-Bot User-Level Installer
echo.
echo Launching non-admin PowerShell installation script...
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-NonAdmin.ps1"
echo.
pause
