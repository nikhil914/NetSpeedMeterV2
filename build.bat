@echo off
title NetMonitor Pro — Build
color 0A

echo.
echo   ========================================
echo        NetMonitor Pro — Build ^& Publish
echo   ========================================
echo.

set "PROJECT=%~dp0src\NetMonitorPro.App\NetMonitorPro.App.csproj"
set "OUTPUT=%~dp0dist"

:: Clean previous build
if exist "%OUTPUT%" (
    echo   [1/3] Cleaning previous build...
    rmdir /s /q "%OUTPUT%"
)
mkdir "%OUTPUT%"

:: Publish self-contained single-file EXE
echo   [2/3] Publishing (Release ^| win-x64 ^| self-contained)...
echo.
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%OUTPUT%"

if %ERRORLEVEL% neq 0 (
    echo.
    echo   [FAIL] Build failed! See errors above.
    echo.
    pause
    exit /b 1
)

:: Show result
echo.
echo   ========================================
echo            Build Complete!
echo   ========================================
echo.
echo   Output: %OUTPUT%\NetMonitorPro.exe
echo.

:: Show file size
for %%A in ("%OUTPUT%\NetMonitorPro.exe") do (
    set /a SIZE=%%~zA / 1048576
    echo   Size  : %%~zA bytes
)

echo.
echo   Press any key to open the output folder...
pause >nul
explorer "%OUTPUT%"
