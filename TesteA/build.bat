@echo off
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo csc.exe nao encontrado em %CSC%
  pause
  exit /b 1
)
echo A compilar o Teste A...
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /out:"%~dp0TesteA_CSLDD.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0TesteA.cs"
if errorlevel 1 (
  echo.
  echo FALHOU A COMPILACAO. Envie esta janela ao Claude.
  pause
  exit /b 1
)
echo OK. A abrir o teste...
start "" "%~dp0TesteA_CSLDD.exe"
