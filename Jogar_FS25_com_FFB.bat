@echo off
rem FS25 Real FFB - abre a app (que esconde a Fanatec e liga o FFB) e depois o FS25.
cd /d "%~dp0app"
if not exist RealFFB.exe ( echo Corra primeiro app\build.bat & pause & exit /b 1 )
start "" RealFFB.exe --jogar
