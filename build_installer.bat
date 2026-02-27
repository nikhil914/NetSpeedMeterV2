@echo off
title NetMonitor Pro — Build Installer
color 0B

echo.
echo   ========================================
echo     NetMonitor Pro — Build + Installer
echo   ========================================
echo.

set "ROOT=%~dp0"
set "PROJECT=%ROOT%src\NetMonitorPro.App\NetMonitorPro.App.csproj"
set "DIST=%ROOT%dist"
set "ISS=%ROOT%scripts\installer.iss"
set "INSTALLER_OUT=%ROOT%installer"

:: ─── Locate Inno Setup ───
set "ISCC="
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
) else if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
) else (
    echo   [ERROR] Inno Setup 6 not found!
    echo   Please install from: https://jrsoftware.org/isdl.php
    echo.
    pause
    exit /b 1
)

echo   Found Inno Setup: %ISCC%
echo.

:: ─── Step 1: Clean ───
echo   [1/4] Cleaning previous builds...
if exist "%DIST%" rmdir /s /q "%DIST%"
if exist "%INSTALLER_OUT%" rmdir /s /q "%INSTALLER_OUT%"
mkdir "%DIST%"
mkdir "%INSTALLER_OUT%"

:: ─── Step 2: Publish EXE ───
echo   [2/4] Publishing (Release ^| win-x64 ^| self-contained)...
echo.
dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%DIST%"

if %ERRORLEVEL% neq 0 (
    echo.
    echo   [FAIL] Build failed! See errors above.
    pause
    exit /b 1
)

if not exist "%DIST%\NetMonitorPro.exe" (
    echo   [FAIL] NetMonitorPro.exe not found in dist folder!
    pause
    exit /b 1
)

echo.
echo   [OK] EXE built successfully.
echo.

:: ─── Step 3: Build Installer ───
echo   [3/4] Building Inno Setup installer...
echo.
"%ISCC%" "%ISS%"

if %ERRORLEVEL% neq 0 (
    echo.
    echo   [FAIL] Installer build failed!
    pause
    exit /b 1
)

echo.

:: ─── Step 4: Summary ───
echo   [4/4] Done!
echo.
echo   ========================================
echo          Build + Installer Complete!
echo   ========================================
echo.
echo   EXE       : %DIST%\NetMonitorPro.exe
echo   Installer : %INSTALLER_OUT%\NetMonitorPro_Setup.exe
echo.
echo   Press any key to open the installer folder...
pause >nul
explorer "%INSTALLER_OUT%"
