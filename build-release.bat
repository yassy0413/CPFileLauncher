@echo off
rem Release build: bin\win-x64\CPFileLauncher.exe / bin\win-arm64\CPFileLauncher.exe
rem Usage: build-release.bat            (both win-x64 and win-arm64)
rem        build-release.bat win-x64    (one RID only)
setlocal
cd /d "%~dp0"

if "%~1"=="" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "src\build\publish-win.ps1"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "src\build\publish-win.ps1" -Rid %*
)
set "RC=%ERRORLEVEL%"

if not "%RC%"=="0" echo [build-release] FAILED (exit code %RC%)

rem Keep the window open when launched by double-click from Explorer
echo %CMDCMDLINE% | find /i "/c" >nul && pause

exit /b %RC%
