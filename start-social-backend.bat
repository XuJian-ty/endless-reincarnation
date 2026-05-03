@echo off
setlocal
title SocialBackend Server

cd /d "%~dp0Backend\SocialBackend"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\run-backend.ps1"

echo.
echo SocialBackend server has stopped. Press any key to close this window.
pause >nul
endlocal
