@echo off
if not exist "%~dp0FpfcAnywhere-Setup.ps1" echo The other files aren't next to this one. Right-click the zip, pick Extract All, then run Install.cmd from the folder it makes.
if exist "%~dp0FpfcAnywhere-Setup.ps1" powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0FpfcAnywhere-Setup.ps1" %*
pause
