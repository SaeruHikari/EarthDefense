@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\launch_debug.ps1" -NewCampaign
if errorlevel 1 (
  echo Campaign launch failed. See .runtime\new-campaign\launcher.log and import.log
  pause
)
