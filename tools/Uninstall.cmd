@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0FpfcAnywhere-Setup.ps1" -Uninstall %*
pause
