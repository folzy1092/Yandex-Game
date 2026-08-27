@echo off
setlocal enabledelayedexpansion

cd /d "%~dp0"

git add -A

git diff --cached --quiet
if %errorlevel%==0 (
    echo Nechego commitit - net izmeneniy.
    pause
    exit /b 0
)

if "%~1"=="" (
    set /p MSG="Commit message: "
) else (
    set MSG=%*
)

git commit -m "!MSG!"
if errorlevel 1 (
    echo Commit ne udalsya.
    pause
    exit /b 1
)

git push origin main
if errorlevel 1 (
    echo Push ne udalsya - proveryay konflikty/set.
    pause
    exit /b 1
)

echo Gotovo.
pause
