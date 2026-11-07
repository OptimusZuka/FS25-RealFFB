@echo off
rem FS25 RealFFB - compila neste PC (compilador C# do proprio Windows) e abre o instalador.
rem Nao precisa de instalar nada.
cd /d "%~dp0"
call build.bat notest
if errorlevel 1 ( echo. & echo A compilacao falhou. & pause & exit /b 1 )
start "" "dist\app\RealFFB.Setup.exe" install
