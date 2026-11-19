@echo off
chcp 65001 >nul
title Orbis Web Manager - PlayStation 4 Suite

cd /d "%~dp0"

rem Rebuild from the source when it is here, so the .exe always matches the code (~2 s).
rem The release .zip already ships a compiled OrbisManager.exe, so a failed build is not fatal.
if exist "%~dp0src\Program.cs" (
    call "%~dp0build.bat"
    if errorlevel 1 if not exist "%~dp0OrbisManager.exe" (
        echo [ERRO] Falha ao compilar.
        pause
        exit /b 1
    )
)

echo.
echo ========================================================
echo        ORBIS WEB MANAGER - PLAYSTATION 4 SUITE
echo ========================================================
echo.

"%~dp0OrbisManager.exe"
pause