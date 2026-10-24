@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp005_Preview\start-preview.ps1"
if errorlevel 1 (
    echo Preview failed to start. Please send the error above.
    pause
)
