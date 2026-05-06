@echo off
setlocal
title OnlineDungeonServer

cd /d "%~dp0Backend\OnlineDungeonServer"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\run-online-dungeon-server.ps1"

echo.
echo OnlineDungeonServer has stopped. Press any key to close this window.
pause >nul
endlocal
