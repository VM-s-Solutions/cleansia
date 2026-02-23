@echo off
REM ============================================================
REM Cleansia Partner - Reset App Data
REM ============================================================
REM Clears all local storage (DataStore, SharedPreferences, tokens)
REM so the app behaves like a fresh install (onboarding, login, etc.)
REM
REM Usage: scripts\reset-app.bat [variant]
REM   variant: mock-debug (default), debug, staging, release
REM
REM Examples:
REM   scripts\reset-app.bat              -> clears mock debug build
REM   scripts\reset-app.bat debug        -> clears debug build
REM   scripts\reset-app.bat staging      -> clears staging build
REM ============================================================

set ADB=%LOCALAPPDATA%\Android\Sdk\platform-tools\adb.exe
set BASE_PKG=cz.cleansia.partner

set VARIANT=%~1
if "%VARIANT%"=="" set VARIANT=mock-debug

if "%VARIANT%"=="mock-debug" (
    set PKG=%BASE_PKG%.mock.debug
) else if "%VARIANT%"=="debug" (
    set PKG=%BASE_PKG%.debug
) else if "%VARIANT%"=="staging" (
    set PKG=%BASE_PKG%.staging
) else if "%VARIANT%"=="release" (
    set PKG=%BASE_PKG%
) else (
    echo Unknown variant: %VARIANT%
    echo Valid variants: mock-debug, debug, staging, release
    exit /b 1
)

echo Clearing data for: %PKG%
"%ADB%" shell pm clear %PKG%

if %ERRORLEVEL%==0 (
    echo.
    echo App data cleared successfully.
    echo The app will show the onboarding screen on next launch.
) else (
    echo.
    echo Failed to clear app data. Is the device connected and the app installed?
    echo Run: adb shell pm list packages ^| findstr cleansia
)
