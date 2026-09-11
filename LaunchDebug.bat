@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\launch_debug.ps1"
if errorlevel 1 (
  echo Development launch failed. See .runtime\debug-cleared\launcher.log and .runtime\debug-cleared\import.log
  pause
)
