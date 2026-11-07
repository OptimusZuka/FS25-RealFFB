@echo off
rem FS25 RealFFB - builds on this PC (with the C# compiler built into Windows) and opens the installer.
rem Nothing needs to be installed.
cd /d "%~dp0"
call build.bat notest
if errorlevel 1 ( echo. & echo Build failed. & pause & exit /b 1 )
start "" "dist\app\RealFFB.Setup.exe" install
