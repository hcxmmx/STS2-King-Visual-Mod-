@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp005_Preview\start-preview.ps1" -Settings
if errorlevel 1 pause
