@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-Tomatotodo.ps1" -PayloadPath "%~dp0TomatotodoPayload.zip"
exit /b %errorlevel%
