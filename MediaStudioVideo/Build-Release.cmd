@echo off
chcp 65001 >nul
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Package.ps1" -Configuration Release %*
if errorlevel 1 (
  echo.
  echo Build or packaging failed. See the error above.
  pause
  exit /b 1
)
echo.
echo Build and packaging completed.
pause
