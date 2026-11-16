@echo off
chcp 65001 >nul
title Orbis Web Manager - PlayStation 4 Suite

cd /d "%~dp0"

rem Always rebuild so the .exe matches the source code (takes ~2 seconds)
call "%~dp0build.bat"
if %ERRORLEVEL% neq 0 (
    echo [ERRO] Falha ao compilar.
    pause
    exit /b 1
)

echo.
echo ========================================================
echo        ORBIS WEB MANAGER - PLAYSTATION 4 SUITE
echo ========================================================
echo.

"%~dp0OrbisManager.exe"
pause
