@echo off
chcp 65001 >nul
echo [Orbis Web Manager] Compilando...

set CSC_PATH=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC_PATH%" set CSC_PATH=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

if not exist "%CSC_PATH%" (
    echo [ERRO] O compilador .NET CSC nao foi encontrado. Instale o .NET Framework 4.x.
    pause
    exit /b 1
)

"%CSC_PATH%" /nologo /optimize+ /r:System.Web.Extensions.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:System.Xml.dll /out:"%~dp0OrbisManager.exe" "%~dp0src\*.cs"

if %ERRORLEVEL% equ 0 (
    echo [+] Compilacao concluida: %~dp0OrbisManager.exe
) else (
    echo [ERRO] Falha durante a compilacao.
    pause
    exit /b 1
)
