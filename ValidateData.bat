@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\validate_data.ps1"
set "validation_result=%errorlevel%"
if /I not "%~1"=="--no-pause" pause
exit /b %validation_result%
