@echo off
chcp 65001 >nul
title Orbis Web Manager - Liberar acesso pela rede

rem Libera o Orbis Web Manager para ser acessado por outros aparelhos da rede
rem (celular e o proprio PS4, para instalar PKGs direto do PC).
rem Precisa ser executado UMA vez, como administrador.

net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo Solicitando permissao de administrador...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

set PORT=5000
for /f "usebackq delims=" %%P in (`powershell -NoProfile -Command "try { (Get-Content '%~dp0config.json' -Raw | ConvertFrom-Json).http_port } catch { 5000 }"`) do set PORT=%%P
if "%PORT%"=="" set PORT=5000

echo Porta: %PORT%
echo.
echo [1/2] Reservando a URL http://+:%PORT%/ para usuarios comuns...
netsh http delete urlacl url=http://+:%PORT%/ >nul 2>&1
rem SDDL "WD" = Everyone (funciona em Windows de qualquer idioma)
netsh http add urlacl url=http://+:%PORT%/ sddl=D:(A;;GX;;;WD)

echo [2/2] Liberando a porta %PORT% no Firewall do Windows (apenas redes privadas)...
netsh advfirewall firewall delete rule name="Orbis Web Manager" >nul 2>&1
netsh advfirewall firewall add rule name="Orbis Web Manager" dir=in action=allow protocol=TCP localport=%PORT% profile=private

echo.
echo Pronto! Feche e abra o Orbis Web Manager de novo (start.bat).
echo Obs: a sua rede Wi-Fi/cabo precisa estar como "Rede privada" no Windows.
pause
