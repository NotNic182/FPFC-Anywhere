@echo off
if not exist "%~dp0FpfcAnywhere-Setup.ps1" echo Unzip the whole zip first, then run this again.
if exist "%~dp0FpfcAnywhere-Setup.ps1" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0FpfcAnywhere-Setup.ps1" -Uninstall %*
pause
