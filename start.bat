@echo off
chcp 65001 >nul
title Orbis Web Manager - PlayStation 4 Suite

cd /d "%~dp0"

if not exist "%~dp0OrbisManager.exe" (
    echo [i] Executavel nao encontrado. Compilando automaticamente...
    call "%~dp0build.bat"
    if %ERRORLEVEL% neq 0 (
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
