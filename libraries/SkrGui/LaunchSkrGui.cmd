@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\LaunchSkrGui.ps1" %*
exit /b %errorlevel%
