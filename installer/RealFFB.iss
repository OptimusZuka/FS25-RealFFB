; FS25 RealFFB Installer - Inno Setup 6 (gratuito). Compilar: build.bat  (ou ISCC /DAppVersion=x.y.z RealFFB.iss)
; Instala sem administrador (pasta do utilizador). So o instalador oficial do HidHide pede UAC.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#define AppName "FS25 RealFFB"
#define Repo "https://github.com/OptimusZuka/FS25-RealFFB"

[Setup]
AppId={{6C1C3B7E-4F2A-4C8E-9E51-7A0F25FFB001}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=OptimusZuka
AppPublisherURL={#Repo}
AppSupportURL={#Repo}/issues
AppUpdatesURL={#Repo}/releases
VersionInfoVersion={#AppVersion}.0
VersionInfoProductName={#AppName}
VersionInfoDescription=FS25 RealFFB Installer
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
WizardImageFile=..\assets\wizard-large.bmp,..\assets\wizard-large-200.bmp
WizardSmallImageFile=..\assets\wizard-small.bmp,..\assets\wizard-small-200.bmp
SetupIconFile=..\assets\realffb.ico
UninstallDisplayIcon={app}\RealFFB.exe
UninstallDisplayName={#AppName}
OutputDir=..\dist
OutputBaseFilename=FS25_RealFFB_Setup_{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
; a app usa este mutex: o instalador pede para a fechar antes de atualizar
AppMutex=FS25RealFFB_unica
LicenseFile=..\LICENSE
SetupLogging=yes

[Languages]
Name: "ptbr"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "pt"; MessagesFile: "compiler:Languages\Portuguese.isl"

[Messages]
SetupWindowTitle=FS25 RealFFB Installer
WelcomeLabel1=FS25 RealFFB Installer
WelcomeLabel2=Force feedback real no Farming Simulator 25 para volantes Fanatec e Logitech (G29, G920, G923).%n%nEtapas:%n  1. Verificação do sistema%n  2. Deteção do Farming Simulator 25%n  3. Verificação do volante%n  4. Instalação do HidHide%n  5. Configuração dos dispositivos%n  6. Instalação do RealFFB%n  7. Verificação final%n%nFeche o Farming Simulator 25 antes de continuar.

[Tasks]
Name: "desktopicon"; Description: "Criar atalho no ambiente de trabalho (abre o RealFFB e o FS25)"

[Files]
Source: "..\dist\app\RealFFB.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\app\RealFFB.Setup.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\app\FS25_RealFFBTelemetry.zip"; DestDir: "{code:GetModsDir}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\docs\PROBLEMAS.md"; DestDir: "{app}"; DestName: "Resolucao de problemas.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\RealFFB.exe"
Name: "{autoprograms}\{#AppName} - Configurar dispositivos"; Filename: "{app}\RealFFB.Setup.exe"; Parameters: "configure"
Name: "{autoprograms}\{#AppName} - Relatorio de diagnostico"; Filename: "{app}\RealFFB.Setup.exe"; Parameters: "diagnose"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\RealFFB.exe"; Parameters: "--jogar"; IconFilename: "{code:GetShortcutIcon}"; Tasks: desktopicon

[Run]
Filename: "{app}\RealFFB.exe"; Description: "Abrir o FS25 RealFFB agora"; Flags: nowait postinstall skipifsilent; Check: not NeedRestart

[Code]
var
  ResFile: String;
  GameDir, ProfileDir, ModsDir, HidStatus, HidVersion, GameXmlStatus: String;
  FanCount: Integer;
  OsOk, RebootNeeded, DetectDone: Boolean;
  CheckPage, FinalPage: TOutputMsgMemoWizardPage;
  GamePage: TInputDirWizardPage;
  HidPage, DevPage: TInputOptionWizardPage;
  StepLog: String;
  PurgeData: Boolean;
  OsText: String;

function R(Key: String): String;
begin
  Result := GetIniString('r', Key, '', ResFile);
end;

function Mark(Ok: Boolean): String;
begin
  if Ok then Result := '[OK]     ' else Result := '[FALTA]  ';
end;

procedure AddStep(S: String);
begin
  StepLog := StepLog + S + #13#10;
end;

// Corre o ajudante (copia em {tmp} antes de instalar, {app} depois). Devolve o codigo de saida.
function Helper(FromApp: Boolean; Params: String): Integer;
var
  Exe: String;
  Code: Integer;
begin
  if FromApp then
    Exe := ExpandConstant('{app}\RealFFB.Setup.exe')
  else
  begin
    ExtractTemporaryFile('RealFFB.Setup.exe');
    Exe := ExpandConstant('{tmp}\RealFFB.Setup.exe');
  end;
  ResFile := ExpandConstant('{tmp}\result.ini');
  DeleteFile(ResFile);
  if not Exec(Exe, Params + ' --out "' + ResFile + '"', '', SW_SHOW, ewWaitUntilTerminated, Code) then
  begin
    Log('Exec falhou: ' + SysErrorMessage(Code));
    Code := -1;
  end;
  Log('Helper ' + Params + ' -> ' + IntToStr(Code) + ' | ' + R('log'));
  Result := Code;
end;

function IsValidGame(Dir: String): Boolean;
begin
  Result := FileExists(AddBackslash(Dir) + 'FarmingSimulator2025.exe') or FileExists(AddBackslash(Dir) + 'x64\FarmingSimulator2025Game.exe');
end;

function GetModsDir(Param: String): String;
begin
  if ModsDir = '' then ModsDir := ExpandConstant('{userdocs}\My Games\FarmingSimulator2025\mods');
  Result := ModsDir;
end;

function GetShortcutIcon(Param: String): String;
begin
  if FileExists(AddBackslash(GameDir) + 'FarmingSimulator2025.exe') then
    Result := AddBackslash(GameDir) + 'FarmingSimulator2025.exe'
  else
    Result := ExpandConstant('{app}\RealFFB.exe');
end;

function NeedRestart(): Boolean;
begin
  Result := RebootNeeded;
end;

// ---------------------------------------------------------------- etapas 1-3
procedure RunDetection;
var
  T: String;
  I: Integer;
begin
  Helper(False, 'detect');
  OsOk := R('os_ok') = '1';
  OsText := R('os');
  GameDir := R('game_dir');
  ProfileDir := R('profile_dir');
  ModsDir := R('mods_dir');
  HidStatus := R('hidhide');
  HidVersion := R('hidhide_version');
  GameXmlStatus := R('gamexml');
  FanCount := StrToIntDef(R('fanatec_count'), 0);

  T := 'Etapa 1 - Sistema' + #13#10 + '  ' + Mark(OsOk) + R('os') + #13#10;
  if R('admin') = '1' then T := T + '  [AVISO]  O instalador esta a correr como administrador. Nao e necessario.' + #13#10;
  T := T + #13#10 + 'Etapa 2 - Farming Simulator 25' + #13#10;
  if GameDir <> '' then
    T := T + '  [OK]     ' + GameDir + #13#10 + '           origem: ' + R('game_source') + #13#10
  else
    T := T + '  [FALTA]  ' + R('game_source') + ' (vai poder indicar a pasta a seguir)' + #13#10;
  T := T + '  pasta de mods: ' + ModsDir + #13#10;
  if R('game_running') = '1' then T := T + '  [AVISO]  O FS25 esta aberto. Feche-o antes de continuar.' + #13#10;
  T := T + #13#10 + 'Etapa 3 - Volante' + #13#10;
  if FanCount = 0 then
    T := T + '  [FALTA]  Nenhum volante suportado ligado. Pode continuar e configurar depois.' + #13#10
  else
    for I := 0 to FanCount - 1 do T := T + '  [OK]     ' + R('fanatec_' + IntToStr(I)) + #13#10;
  if R('fanatec_other') <> '' then T := T + '  (outros dispositivos Fanatec, que nao serao alterados: ' + R('fanatec_other') + ')' + #13#10;
  T := T + #13#10 + 'HidHide (Nefarius)' + #13#10;
  if HidStatus = 'ok' then T := T + '  [OK]     versao ' + HidVersion + ' (nao sera reinstalado)' + #13#10
  else if HidStatus = 'outdated' then T := T + '  [AVISO]  versao ' + HidVersion + ' desatualizada' + #13#10
  else T := T + '  [FALTA]  nao instalado' + #13#10;
  CheckPage.RichEditViewer.Lines.Text := T;
  DetectDone := True;
end;

procedure InitializeWizard;
begin
  WizardForm.MainPanel.Color := $121212;
  WizardForm.PageNameLabel.Font.Color := $00CCFF;   // amarelo (BGR)
  WizardForm.PageDescriptionLabel.Font.Color := clWhite;

  CheckPage := CreateOutputMsgMemoPage(wpLicense, 'Etapas 1 a 3 de 7: verificação',
    'Sistema, Farming Simulator 25 e volante', 'Resultado da verificação (nada foi alterado):', '');

  GamePage := CreateInputDirPage(CheckPage.ID, 'Etapa 2 de 7: Farming Simulator 25',
    'O FS25 não foi encontrado automaticamente',
    'Indique a pasta onde está instalado o Farming Simulator 25 (a que contém FarmingSimulator2025.exe).', False, '');
  GamePage.Add('');

  HidPage := CreateInputOptionPage(GamePage.ID, 'Etapa 4 de 7: HidHide',
    'É necessário instalar um driver',
    'O RealFFB usa o HidHide (Nefarius Software Solutions, gratuito, código aberto) para esconder o volante do FS25 enquanto o RealFFB a controla.' + #13#10#13#10 +
    'O instalador oficial será descarregado do GitHub do projeto (github.com/nefarius/HidHide). A assinatura digital é verificada antes de executar; o ficheiro não é alterado.' + #13#10#13#10 +
    'O Windows vai pedir autorização de administrador (UAC). Pode ser necessário reiniciar; nesse caso a configuração continua sozinha depois de reiniciar.',
    False, False);
  HidPage.Add('Instalar o HidHide oficial (recomendado)');
  HidPage.Values[0] := True;

  DevPage := CreateInputOptionPage(HidPage.ID, 'Etapa 5 de 7: Configuração dos dispositivos',
    'Só o volante escolhido é escondido do jogo',
    'Depois de copiar os ficheiros, abre-se uma janela que mostra exatamente o que vai ser alterado no HidHide: autorizar o RealFFB.exe e esconder apenas o volante. Nenhum outro controlador é alterado.' + #13#10#13#10 +
    'Se existirem vários volantes, escolhe qual usar. Se o volante não for identificada com segurança, nada é alterado.' + #13#10#13#10 +
    'O game.xml do FS25 também é ajustado (joystick ativo, force feedback do jogo desligado), com cópia de segurança.',
    False, False);
  DevPage.Add('Configurar automaticamente (pede confirmação)');
  DevPage.Values[0] := True;

  FinalPage := CreateOutputMsgMemoPage(wpInstalling, 'Etapa 7 de 7: verificação final',
    'Resultado da instalação', 'Resumo:', '');
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = CheckPage.ID) and not DetectDone then
  begin
    WizardForm.NextButton.Enabled := False;
    CheckPage.RichEditViewer.Lines.Text := 'A verificar...';
    RunDetection;
    WizardForm.NextButton.Enabled := True;
  end;
  if CurPageID = wpInstalling then
    WizardForm.PageNameLabel.Caption := 'Etapa 6 de 7: instalação do RealFFB';
  if CurPageID = FinalPage.ID then
  begin
    FinalPage.RichEditViewer.Lines.Text := StepLog;
    WizardForm.CancelButton.Visible := False;
  end;
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := False;
  if PageID = GamePage.ID then Result := IsValidGame(GameDir);
  if PageID = HidPage.ID then Result := HidStatus = 'ok';
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = CheckPage.ID then
  begin
    if not OsOk then
    begin
      MsgBox('O FS25 RealFFB precisa do Windows 10 ou 11 de 64 bits.', mbCriticalError, MB_OK);
      Result := False;
    end
    else if R('game_running') = '1' then
    begin
      if MsgBox('O Farming Simulator 25 está aberto. Feche-o e carregue em OK para verificar de novo.', mbInformation, MB_OKCANCEL) = IDOK then
        RunDetection;
      Result := False;
    end;
  end;
  if CurPageID = GamePage.ID then
  begin
    if not IsValidGame(GamePage.Values[0]) then
    begin
      MsgBox('Essa pasta não contém o Farming Simulator 25 (FarmingSimulator2025.exe).', mbError, MB_OK);
      Result := False;
    end
    else GameDir := GamePage.Values[0];
  end;
end;

// ---------------------------------------------------------------- etapas 4, 5 e 7 (depois de copiar)
procedure PostInstall;
var
  Code, I, Errors: Integer;
  Prog: TOutputProgressWizardPage;
begin
  Prog := CreateOutputProgressPage('FS25 RealFFB Installer', 'A configurar');
  Prog.Show;
  try
    Helper(True, 'remember --game "' + GameDir + '" --mods "' + ModsDir + '"');
    AddStep('Etapa 1 - Sistema: ' + OsText);
    AddStep('Etapa 2 - FS25: ' + GameDir);
    AddStep('Etapa 3 - Volante: ' + IntToStr(FanCount) + ' detetado(s)');

    // Etapa 4
    Prog.SetText('Etapa 4 de 7: HidHide', 'A verificar / instalar o HidHide oficial...');
    Prog.SetProgress(1, 5);
    if (HidStatus = 'ok') then
    begin
      Code := Helper(True, 'hidhide-install');
      if Code = 3 then RebootNeeded := True;
      AddStep('Etapa 4 - HidHide: ja instalado (' + HidVersion + ')');
    end
    else if HidPage.Values[0] then
    begin
      Code := Helper(True, 'hidhide-install');
      if Code = 0 then AddStep('Etapa 4 - HidHide: instalado ' + R('version') + ' (assinatura verificada, SHA-256 ' + R('sha256') + ')')
      else if Code = 3 then begin RebootNeeded := True; AddStep('Etapa 4 - HidHide: instalado, e necessario reiniciar o Windows'); end
      else if Code = 2 then AddStep('Etapa 4 - HidHide: cancelado (' + R('status') + '). Volte a executar o instalador para o instalar.')
      else AddStep('Etapa 4 - HidHide: FALHOU (' + R('status') + ' ' + R('error') + '). Consulte "Resolucao de problemas".');
    end
    else AddStep('Etapa 4 - HidHide: nao instalado (opcao desmarcada). O RealFFB nao funciona sem ele.');

    // Etapa 5
    Prog.SetText('Etapa 5 de 7: dispositivos', 'Confirme as alterações na janela que se abriu...');
    Prog.SetProgress(2, 5);
    if RebootNeeded then
    begin
      Helper(True, 'schedule-resume');
      AddStep('Etapa 5 - Dispositivos: continua automaticamente depois de reiniciar o Windows');
      Helper(True, 'patch-gamexml');
    end
    else if DevPage.Values[0] then
    begin
      Code := Helper(True, 'configure');
      if R('status') = 'configured' then AddStep('Etapa 5 - Dispositivos: HidHide configurado para ' + R('device'))
      else if R('status') = 'unchanged' then AddStep('Etapa 5 - Dispositivos: ja estava configurado (' + R('device') + ')')
      else AddStep('Etapa 5 - Dispositivos: nao configurado (' + R('status') + '). Use o atalho "FS25 RealFFB - Configurar dispositivos".');
    end
    else
    begin
      Helper(True, 'patch-gamexml');
      AddStep('Etapa 5 - Dispositivos: ignorado (opcao desmarcada)');
    end;

    AddStep('Etapa 6 - RealFFB: ' + ExpandConstant('{app}') + '; mod em ' + ModsDir);

    // Etapa 7
    Prog.SetText('Etapa 7 de 7: verificação final', '');
    Prog.SetProgress(4, 5);
    Code := Helper(True, 'verify');
    Errors := StrToIntDef(R('errors'), 0);
    AddStep('');
    AddStep('Etapa 7 - Verificacao final:');
    for I := 0 to StrToIntDef(R('checks'), 0) - 1 do AddStep('  ' + R('check_' + IntToStr(I)));
    AddStep('');
    if RebootNeeded then
      AddStep('Reinicie o Windows para concluir. Depois de reiniciar abre-se a configuracao dos dispositivos.')
    else if Errors = 0 then
      AddStep('Instalado e pronto a configurar. Abra o FS25 RealFFB pelo atalho; ele abre o jogo.')
    else
      AddStep('Instalado com ' + IntToStr(Errors) + ' problema(s) acima. Use "FS25 RealFFB - Relatorio de diagnostico" para pedir ajuda.');
    Prog.SetProgress(5, 5);
  finally
    Prog.Hide;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then PostInstall;
end;

// ---------------------------------------------------------------- desinstalacao

function InitializeUninstall(): Boolean;
begin
  Result := True;
  PurgeData := MsgBox('Apagar também as suas definições e perfis do RealFFB (afinações de FFB, pedais, comandos)?' + #13#10#13#10 +
    'Sim = apagar tudo.   Não = manter (recomendado se for reinstalar).', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Code: Integer;
  P: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    P := 'restore';
    if PurgeData then P := P + ' --purge';
    // repoe o HidHide e o game.xml como estavam antes; o HidHide nao e desinstalado
    Exec(ExpandConstant('{app}\RealFFB.Setup.exe'), P, '', SW_HIDE, ewWaitUntilTerminated, Code);
  end;
end;
