@echo off
chcp 65001 >nul
echo [Orbis Web Manager] Compilando suite...

set CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC_PATH%" (
    echo [ERRO] O compilador .NET CSC nao foi encontrado em %CSC_PATH%
    pause
    exit /b 1
)

"%CSC_PATH%" /nologo /optimize+ /r:System.Web.Extensions.dll /out:"%~dp0OrbisManager.exe" "%~dp0src\Program.cs"

if %ERRORLEVEL% equ 0 (
    echo [+] Compilacao concluida com sucesso!
    echo     Executavel gerado em: %~dp0OrbisManager.exe
) else (
    echo [ERRO] Falha durante a compilacao.
    pause
    exit /b 1
)
