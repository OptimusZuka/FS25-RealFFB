@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
set CSC=%FW%\csc.exe
set WM=%WINDIR%\System32\WinMetadata
set GAC=%WINDIR%\Microsoft.NET\assembly\GAC_MSIL
if not exist "%CSC%" ( echo csc.exe nao encontrado & pause & exit /b 1 )

echo ==== build %DATE% %TIME% ==== > build_log.txt
set REFS=/r:System.Numerics.dll /r:System.Core.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll
set WREFS=/r:"%WM%\Windows.Foundation.winmd" /r:"%WM%\Windows.Gaming.winmd"
for %%N in (System.Runtime System.Runtime.InteropServices.WindowsRuntime System.Numerics.Vectors System.ObjectModel) do (
  set FOUND=
  if exist "%FW%\%%N.dll" set FOUND=%FW%\%%N.dll
  if not defined FOUND for /f "delims=" %%F in ('dir /s /b "%GAC%\%%N\%%N.dll" 2^>nul') do if not defined FOUND set FOUND=%%F
  if defined FOUND ( set WREFS=!WREFS! /r:"!FOUND!" & echo ref %%N = !FOUND! >> build_log.txt ) else ( echo ref %%N = NAO ENCONTRADA >> build_log.txt )
)

echo A compilar FS25 Real FFB (com Windows.Gaming.Input)...
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /out:RealFFB.exe %REFS% %WREFS% *.cs >> build_log.txt 2>&1
if not errorlevel 1 goto ok

echo Falhou com WGI. A compilar versao so DirectInput... (detalhes em build_log.txt)
echo ---- fallback sem WGI ---- >> build_log.txt
set FILES=
for %%F in (*.cs) do if /i not "%%F"=="WgiDevice.cs" set FILES=!FILES! "%%F"
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /define:NO_WGI /out:RealFFB.exe %REFS% %FILES% >> build_log.txt 2>&1
if errorlevel 1 ( echo FALHOU A COMPILACAO. & type build_log.txt & pause & exit /b 1 )

:ok
echo OK. A abrir...
echo BUILD OK >> build_log.txt
start "" RealFFB.exe
