@echo off
setlocal
call "%~dp0libraries\SkrGui\LaunchSkrGui.cmd" %*
exit /b %errorlevel%
