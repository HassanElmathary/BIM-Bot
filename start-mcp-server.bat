@echo off
title Revit MCP Server
echo ============================================
echo   Revit MCP Server v1.0.0
echo ============================================
echo.
cd /d "%~dp0revit-mcp-server"
echo Starting MCP server...
node build/index.js
pause
