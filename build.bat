@echo off
rem FS25 RealFFB - compila tudo a partir do codigo-fonte (sem Visual Studio).
rem Usa o compilador C# que vem com o Windows (.NET Framework 4.x) e, se existir, o Inno Setup 6.
rem   build.bat           compila app, ajudante, testes, mod e instalador -> dist\
rem   build.bat notest    sem correr os testes
setlocal EnableDelayedExpansion
cd /d "%~dp0"

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" ( echo [ERRO] csc.exe nao encontrado: %CSC% & exit /b 1 )

set /p VER=<VERSION
for /f "tokens=* delims= " %%a in ("%VER%") do set VER=%%a
if "%VER%"=="" ( echo [ERRO] ficheiro VERSION vazio & exit /b 1 )
echo == FS25 RealFFB %VER% ==

if exist build rmdir /s /q build
if exist dist rmdir /s /q dist
mkdir build
mkdir dist\app

> build\Version.g.cs echo // gerado pelo build.bat a partir de VERSION
>>build\Version.g.cs echo using System.Reflection;
>>build\Version.g.cs echo [assembly: AssemblyVersion("%VER%.0")]
>>build\Version.g.cs echo [assembly: AssemblyFileVersion("%VER%.0")]
>>build\Version.g.cs echo [assembly: AssemblyInformationalVersion("%VER%")]
>>build\Version.g.cs echo [assembly: AssemblyProduct("FS25 RealFFB")]
>>build\Version.g.cs echo [assembly: AssemblyCompany("OptimusZuka")]
>>build\Version.g.cs echo [assembly: AssemblyCopyright("MIT License - OptimusZuka")]

set COMMON=/nologo /optimize+ /platform:x64 /win32icon:assets\realffb.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll

echo [1/5] RealFFB.exe
"%CSC%" %COMMON% /target:winexe /r:System.Core.dll /resource:assets\logo.png,RealFFB.logo.png /out:dist\app\RealFFB.exe build\Version.g.cs src\RealFFB\*.cs > build\app_log.txt 2>&1
if errorlevel 1 ( type build\app_log.txt & echo [ERRO] RealFFB.exe & exit /b 1 )

echo [2/5] RealFFB.Setup.exe
"%CSC%" %COMMON% /target:winexe /r:System.Management.dll /resource:assets\logo.png,RealFFB.logo.png /out:dist\app\RealFFB.Setup.exe build\Version.g.cs src\RealFFB.Setup\*.cs > build\setup_log.txt 2>&1
if errorlevel 1 ( type build\setup_log.txt & echo [ERRO] RealFFB.Setup.exe & exit /b 1 )

echo [3/5] testes
"%CSC%" /nologo /platform:x64 /target:exe /main:RealFFBSetupTests.TestMain /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Management.dll /out:build\SetupTests.exe src\RealFFB.Setup\*.cs tests\SetupTests.cs > build\tests_log.txt 2>&1
if errorlevel 1 ( type build\tests_log.txt & echo [ERRO] compilacao dos testes & exit /b 1 )
if /i "%1"=="notest" goto mod
build\SetupTests.exe
if errorlevel 1 ( echo [ERRO] ha testes a falhar & exit /b 1 )

:mod
echo [4/5] mod
tar -a -c -f dist\app\FS25_RealFFBTelemetry.zip -C mod\FS25_RealFFBTelemetry modDesc.xml RealFFBTelemetry.lua icon_RealFFB.dds
if errorlevel 1 ( echo [ERRO] zip do mod & exit /b 1 )

echo [5/5] instalador
set ISCC=
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not defined ISCC (
  echo [AVISO] Inno Setup 6 nao encontrado: so foram gerados os ficheiros em dist\app.
  echo         Instale-o de https://jrsoftware.org/isdl.php para gerar o instalador.
  goto sums
)
"%ISCC%" /Q /DAppVersion=%VER% installer\RealFFB.iss
if errorlevel 1 ( echo [ERRO] Inno Setup & exit /b 1 )

:sums
pushd dist
> SHA256SUMS.txt (
  for %%F in (FS25_RealFFB_Setup_*.exe app\RealFFB.exe app\RealFFB.Setup.exe app\FS25_RealFFBTelemetry.zip) do (
    if exist "%%F" for /f "skip=1 delims=" %%H in ('certutil -hashfile "%%F" SHA256 ^| findstr /v /i /c:"certutil"') do (
      set H=%%H
      set H=!H: =!
      echo !H!  %%~nxF
    )
  )
)
type SHA256SUMS.txt
popd
echo.
echo OK: dist\
exit /b 0
