; FS25 RealFFB Installer - Inno Setup 6 (free). Build: build.bat  (or ISCC /DAppVersion=x.y.z RealFFB.iss)
; Installs without administrator rights (user folder). Only the official HidHide installer asks for UAC.

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
; the app uses this mutex: the installer asks to close it before updating
AppMutex=FS25RealFFB_unica
LicenseFile=..\LICENSE
SetupLogging=yes
ShowLanguageDialog=yes
LanguageDetectionMethod=none

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "pt"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "es"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[Messages]
SetupWindowTitle=FS25 RealFFB Installer
WelcomeLabel1=FS25 RealFFB Installer

[CustomMessages]
en.Welcome2=Real force feedback in Farming Simulator 25 for Fanatec and Logitech wheels (G29, G920, G923).%n%nSteps:%n  1. System check%n  2. Farming Simulator 25 detection%n  3. Wheel check%n  4. HidHide installation%n  5. Device setup%n  6. RealFFB installation%n  7. Final check%n%nClose Farming Simulator 25 before continuing.
pt.Welcome2=Force feedback de verdade no Farming Simulator 25 para volantes Fanatec e Logitech (G29, G920, G923).%n%nEtapas:%n  1. Verificação do sistema%n  2. Detecção do Farming Simulator 25%n  3. Verificação do volante%n  4. Instalação do HidHide%n  5. Configuração dos dispositivos%n  6. Instalação do RealFFB%n  7. Verificação final%n%nFeche o Farming Simulator 25 antes de continuar.
es.Welcome2=Force feedback real en Farming Simulator 25 para volantes Fanatec y Logitech (G29, G920, G923).%n%nPasos:%n  1. Comprobación del sistema%n  2. Detección de Farming Simulator 25%n  3. Comprobación del volante%n  4. Instalación de HidHide%n  5. Configuración de dispositivos%n  6. Instalación de RealFFB%n  7. Comprobación final%n%nCierra Farming Simulator 25 antes de continuar.
fr.Welcome2=Un vrai force feedback dans Farming Simulator 25 pour les volants Fanatec et Logitech (G29, G920, G923).%n%nÉtapes :%n  1. Vérification du système%n  2. Détection de Farming Simulator 25%n  3. Vérification du volant%n  4. Installation de HidHide%n  5. Configuration des périphériques%n  6. Installation de RealFFB%n  7. Vérification finale%n%nFermez Farming Simulator 25 avant de continuer.
en.TaskDesktop=Create a desktop shortcut (opens RealFFB and FS25)
pt.TaskDesktop=Criar atalho na área de trabalho (abre o RealFFB e o FS25)
es.TaskDesktop=Crear acceso directo en el escritorio (abre RealFFB y FS25)
fr.TaskDesktop=Créer un raccourci sur le bureau (ouvre RealFFB et FS25)
en.RunNow=Open FS25 RealFFB now
pt.RunNow=Abrir o FS25 RealFFB agora
es.RunNow=Abrir FS25 RealFFB ahora
fr.RunNow=Ouvrir FS25 RealFFB maintenant
en.Ok=[OK]
pt.Ok=[OK]
es.Ok=[OK]
fr.Ok=[OK]
en.Missing=[MISSING]
pt.Missing=[FALTA]
es.Missing=[FALTA]
fr.Missing=[MANQUE]
en.Warn=[WARNING]
pt.Warn=[AVISO]
es.Warn=[AVISO]
fr.Warn=[ATTENTION]
en.Step1=Step 1 - System
pt.Step1=Etapa 1 - Sistema
es.Step1=Paso 1 - Sistema
fr.Step1=Étape 1 - Système
en.Step2=Step 2 - Farming Simulator 25
pt.Step2=Etapa 2 - Farming Simulator 25
es.Step2=Paso 2 - Farming Simulator 25
fr.Step2=Étape 2 - Farming Simulator 25
en.Step3=Step 3 - Wheel
pt.Step3=Etapa 3 - Volante
es.Step3=Paso 3 - Volante
fr.Step3=Étape 3 - Volant
en.Step4=Step 4 - HidHide
pt.Step4=Etapa 4 - HidHide
es.Step4=Paso 4 - HidHide
fr.Step4=Étape 4 - HidHide
en.Step5=Step 5 - Devices
pt.Step5=Etapa 5 - Dispositivos
es.Step5=Paso 5 - Dispositivos
fr.Step5=Étape 5 - Périphériques
en.Step6=Step 6 - RealFFB
pt.Step6=Etapa 6 - RealFFB
es.Step6=Paso 6 - RealFFB
fr.Step6=Étape 6 - RealFFB
en.Step7=Step 7 - Final check
pt.Step7=Etapa 7 - Verificação final
es.Step7=Paso 7 - Comprobación final
fr.Step7=Étape 7 - Vérification finale
en.AdminWarn=The installer is running as administrator. This is not needed.
pt.AdminWarn=O instalador está rodando como administrador. Não é necessário.
es.AdminWarn=El instalador se está ejecutando como administrador. No es necesario.
fr.AdminWarn=L'installateur tourne en administrateur. Ce n'est pas nécessaire.
en.Source=source: %1
pt.Source=origem: %1
es.Source=origen: %1
fr.Source=source : %1
en.GameNotFound=%1 (you can choose the folder next)
pt.GameNotFound=%1 (vai poder indicar a pasta a seguir)
es.GameNotFound=%1 (podrás indicar la carpeta a continuación)
fr.GameNotFound=%1 (vous pourrez indiquer le dossier ensuite)
en.ModsFolder=mods folder: %1
pt.ModsFolder=pasta de mods: %1
es.ModsFolder=carpeta de mods: %1
fr.ModsFolder=dossier des mods : %1
en.GameOpen=FS25 is open. Close it before continuing.
pt.GameOpen=O FS25 está aberto. Feche-o antes de continuar.
es.GameOpen=FS25 está abierto. Ciérralo antes de continuar.
fr.GameOpen=FS25 est ouvert. Fermez-le avant de continuer.
en.NoWheel=No supported wheel connected. You can continue and set it up later.
pt.NoWheel=Nenhum volante suportado conectado. Pode continuar e configurar depois.
es.NoWheel=Ningún volante compatible conectado. Puedes continuar y configurarlo después.
fr.NoWheel=Aucun volant compatible connecté. Vous pouvez continuer et le configurer plus tard.
en.OtherFanatec=(other Fanatec devices, which will not be changed: %1)
pt.OtherFanatec=(outros dispositivos Fanatec, que não serão alterados: %1)
es.OtherFanatec=(otros dispositivos Fanatec, que no se cambiarán: %1)
fr.OtherFanatec=(autres périphériques Fanatec, qui ne seront pas modifiés : %1)
en.HidOk=version %1 (will not be reinstalled)
pt.HidOk=versão %1 (não será reinstalado)
es.HidOk=versión %1 (no se reinstalará)
fr.HidOk=version %1 (ne sera pas réinstallé)
en.HidOld=version %1 is outdated
pt.HidOld=versão %1 desatualizada
es.HidOld=versión %1 desactualizada
fr.HidOld=version %1 obsolète
en.HidMissing=not installed
pt.HidMissing=não instalado
es.HidMissing=no instalado
fr.HidMissing=non installé
en.CheckTitle=Steps 1 to 3 of 7: checks
pt.CheckTitle=Etapas 1 a 3 de 7: verificação
es.CheckTitle=Pasos 1 a 3 de 7: comprobación
fr.CheckTitle=Étapes 1 à 3 sur 7 : vérifications
en.CheckDesc=System, Farming Simulator 25 and wheel
pt.CheckDesc=Sistema, Farming Simulator 25 e volante
es.CheckDesc=Sistema, Farming Simulator 25 y volante
fr.CheckDesc=Système, Farming Simulator 25 et volant
en.CheckSub=Check results (nothing has been changed):
pt.CheckSub=Resultado da verificação (nada foi alterado):
es.CheckSub=Resultado de la comprobación (no se ha cambiado nada):
fr.CheckSub=Résultat des vérifications (rien n'a été modifié) :
en.Checking=Checking...
pt.Checking=Verificando...
es.Checking=Comprobando...
fr.Checking=Vérification...
en.GameTitle=Step 2 of 7: Farming Simulator 25
pt.GameTitle=Etapa 2 de 7: Farming Simulator 25
es.GameTitle=Paso 2 de 7: Farming Simulator 25
fr.GameTitle=Étape 2 sur 7 : Farming Simulator 25
en.GameDesc=FS25 was not found automatically
pt.GameDesc=O FS25 não foi encontrado automaticamente
es.GameDesc=FS25 no se encontró automáticamente
fr.GameDesc=FS25 n'a pas été trouvé automatiquement
en.GameSub=Choose the folder where Farming Simulator 25 is installed (the one with FarmingSimulator2025.exe).
pt.GameSub=Indique a pasta onde está instalado o Farming Simulator 25 (a que contém FarmingSimulator2025.exe).
es.GameSub=Indica la carpeta donde está instalado Farming Simulator 25 (la que contiene FarmingSimulator2025.exe).
fr.GameSub=Indiquez le dossier où Farming Simulator 25 est installé (celui avec FarmingSimulator2025.exe).
en.HidTitle=Step 4 of 7: HidHide
pt.HidTitle=Etapa 4 de 7: HidHide
es.HidTitle=Paso 4 de 7: HidHide
fr.HidTitle=Étape 4 sur 7 : HidHide
en.HidDesc=A driver needs to be installed
pt.HidDesc=É necessário instalar um driver
es.HidDesc=Hay que instalar un controlador
fr.HidDesc=Un pilote doit être installé
en.HidSub=RealFFB uses HidHide (Nefarius Software Solutions, free, open source) to hide the wheel from FS25 while RealFFB controls it.%n%nThe official installer will be downloaded from the project's GitHub (github.com/nefarius/HidHide). Its digital signature is checked before running; the file is not modified.%n%nWindows will ask for administrator permission (UAC). A restart may be needed; in that case setup continues by itself after the restart.
pt.HidSub=O RealFFB usa o HidHide (Nefarius Software Solutions, gratuito, código aberto) para esconder o volante do FS25 enquanto o RealFFB o controla.%n%nO instalador oficial será baixado do GitHub do projeto (github.com/nefarius/HidHide). A assinatura digital é verificada antes de executar; o arquivo não é alterado.%n%nO Windows vai pedir autorização de administrador (UAC). Pode ser preciso reiniciar; nesse caso a configuração continua sozinha depois.
es.HidSub=RealFFB usa HidHide (Nefarius Software Solutions, gratuito, código abierto) para ocultar el volante a FS25 mientras RealFFB lo controla.%n%nSe descargará el instalador oficial desde el GitHub del proyecto (github.com/nefarius/HidHide). Su firma digital se comprueba antes de ejecutarlo; el archivo no se modifica.%n%nWindows pedirá permiso de administrador (UAC). Puede ser necesario reiniciar; en ese caso la configuración continúa sola después.
fr.HidSub=RealFFB utilise HidHide (Nefarius Software Solutions, gratuit, open source) pour masquer le volant à FS25 pendant que RealFFB le contrôle.%n%nL'installateur officiel sera téléchargé depuis le GitHub du projet (github.com/nefarius/HidHide). Sa signature numérique est vérifiée avant l'exécution ; le fichier n'est pas modifié.%n%nWindows demandera l'autorisation administrateur (UAC). Un redémarrage peut être nécessaire ; la configuration reprend alors toute seule.
en.HidOption=Install the official HidHide (recommended)
pt.HidOption=Instalar o HidHide oficial (recomendado)
es.HidOption=Instalar el HidHide oficial (recomendado)
fr.HidOption=Installer le HidHide officiel (recommandé)
en.DevTitle=Step 5 of 7: Device setup
pt.DevTitle=Etapa 5 de 7: Configuração dos dispositivos
es.DevTitle=Paso 5 de 7: Configuración de dispositivos
fr.DevTitle=Étape 5 sur 7 : Configuration des périphériques
en.DevDesc=Only the chosen wheel is hidden from the game
pt.DevDesc=Só o volante escolhido é escondido do jogo
es.DevDesc=Solo el volante elegido se oculta al juego
fr.DevDesc=Seul le volant choisi est masqué au jeu
en.DevSub=After the files are copied, a window shows exactly what will change in HidHide: allow RealFFB.exe and hide only the wheel. No other controller is changed.%n%nIf there are several wheels, you choose which one to use. If the wheel cannot be identified safely, nothing is changed.%n%nThe FS25 game.xml is also adjusted (joystick on, the game's own force feedback off), with a backup.
pt.DevSub=Depois de copiar os arquivos, abre-se uma janela que mostra exatamente o que vai mudar no HidHide: autorizar o RealFFB.exe e esconder apenas o volante. Nenhum outro controle é alterado.%n%nSe houver vários volantes, você escolhe qual usar. Se o volante não for identificado com segurança, nada é alterado.%n%nO game.xml do FS25 também é ajustado (joystick ativo, force feedback do jogo desligado), com cópia de segurança.
es.DevSub=Tras copiar los archivos se abre una ventana que muestra exactamente qué cambiará en HidHide: autorizar RealFFB.exe y ocultar solo el volante. No se cambia ningún otro mando.%n%nSi hay varios volantes, eliges cuál usar. Si el volante no se identifica con seguridad, no se cambia nada.%n%nTambién se ajusta el game.xml de FS25 (joystick activado, force feedback del juego desactivado), con copia de seguridad.
fr.DevSub=Après la copie des fichiers, une fenêtre montre exactement ce qui change dans HidHide : autoriser RealFFB.exe et masquer uniquement le volant. Aucun autre périphérique n'est modifié.%n%nS'il y a plusieurs volants, vous choisissez lequel utiliser. Si le volant ne peut pas être identifié de façon sûre, rien n'est modifié.%n%nLe game.xml de FS25 est aussi ajusté (joystick activé, force feedback du jeu désactivé), avec une sauvegarde.
en.DevOption=Set up automatically (asks for confirmation)
pt.DevOption=Configurar automaticamente (pede confirmação)
es.DevOption=Configurar automáticamente (pide confirmación)
fr.DevOption=Configurer automatiquement (demande confirmation)
en.FinalTitle=Step 7 of 7: final check
pt.FinalTitle=Etapa 7 de 7: verificação final
es.FinalTitle=Paso 7 de 7: comprobación final
fr.FinalTitle=Étape 7 sur 7 : vérification finale
en.FinalDesc=Installation result
pt.FinalDesc=Resultado da instalação
es.FinalDesc=Resultado de la instalación
fr.FinalDesc=Résultat de l'installation
en.FinalSub=Summary:
pt.FinalSub=Resumo:
es.FinalSub=Resumen:
fr.FinalSub=Résumé :
en.InstallingTitle=Step 6 of 7: installing RealFFB
pt.InstallingTitle=Etapa 6 de 7: instalação do RealFFB
es.InstallingTitle=Paso 6 de 7: instalación de RealFFB
fr.InstallingTitle=Étape 6 sur 7 : installation de RealFFB
en.NeedWin=FS25 RealFFB needs 64-bit Windows 10 or 11.
pt.NeedWin=O FS25 RealFFB precisa do Windows 10 ou 11 de 64 bits.
es.NeedWin=FS25 RealFFB necesita Windows 10 u 11 de 64 bits.
fr.NeedWin=FS25 RealFFB nécessite Windows 10 ou 11 64 bits.
en.CloseGame=Farming Simulator 25 is open. Close it and click OK to check again.
pt.CloseGame=O Farming Simulator 25 está aberto. Feche-o e clique em OK para verificar de novo.
es.CloseGame=Farming Simulator 25 está abierto. Ciérralo y pulsa Aceptar para comprobar de nuevo.
fr.CloseGame=Farming Simulator 25 est ouvert. Fermez-le et cliquez sur OK pour vérifier à nouveau.
en.BadFolder=That folder does not contain Farming Simulator 25 (FarmingSimulator2025.exe).
pt.BadFolder=Essa pasta não contém o Farming Simulator 25 (FarmingSimulator2025.exe).
es.BadFolder=Esa carpeta no contiene Farming Simulator 25 (FarmingSimulator2025.exe).
fr.BadFolder=Ce dossier ne contient pas Farming Simulator 25 (FarmingSimulator2025.exe).
en.Configuring=Configuring
pt.Configuring=Configurando
es.Configuring=Configurando
fr.Configuring=Configuration
en.ProgHid=Checking / installing the official HidHide...
pt.ProgHid=Verificando / instalando o HidHide oficial...
es.ProgHid=Comprobando / instalando el HidHide oficial...
fr.ProgHid=Vérification / installation du HidHide officiel...
en.ProgDev=Confirm the changes in the window that opened...
pt.ProgDev=Confirme as alterações na janela que se abriu...
es.ProgDev=Confirma los cambios en la ventana que se abrió...
fr.ProgDev=Confirmez les modifications dans la fenêtre ouverte...
en.SumWheels=%1 wheel(s) detected
pt.SumWheels=%1 volante(s) detectado(s)
es.SumWheels=%1 volante(s) detectado(s)
fr.SumWheels=%1 volant(s) détecté(s)
en.SumHidPresent=already installed (%1)
pt.SumHidPresent=já instalado (%1)
es.SumHidPresent=ya instalado (%1)
fr.SumHidPresent=déjà installé (%1)
en.SumHidInstalled=installed %1 (signature checked, SHA-256 %2)
pt.SumHidInstalled=instalado %1 (assinatura verificada, SHA-256 %2)
es.SumHidInstalled=instalado %1 (firma comprobada, SHA-256 %2)
fr.SumHidInstalled=installé %1 (signature vérifiée, SHA-256 %2)
en.SumHidReboot=installed, Windows needs a restart
pt.SumHidReboot=instalado, é preciso reiniciar o Windows
es.SumHidReboot=instalado, hay que reiniciar Windows
fr.SumHidReboot=installé, Windows doit redémarrer
en.SumHidCancel=cancelled (%1). Run the installer again to install it.
pt.SumHidCancel=cancelado (%1). Execute o instalador de novo para instalá-lo.
es.SumHidCancel=cancelado (%1). Ejecuta el instalador de nuevo para instalarlo.
fr.SumHidCancel=annulé (%1). Relancez l'installateur pour l'installer.
en.SumHidFail=FAILED (%1). See "Troubleshooting".
pt.SumHidFail=FALHOU (%1). Veja "Troubleshooting".
es.SumHidFail=FALLÓ (%1). Consulta "Troubleshooting".
fr.SumHidFail=ÉCHEC (%1). Voir "Troubleshooting".
en.SumHidSkip=not installed (option unchecked). RealFFB does not work without it.
pt.SumHidSkip=não instalado (opção desmarcada). O RealFFB não funciona sem ele.
es.SumHidSkip=no instalado (opción desmarcada). RealFFB no funciona sin él.
fr.SumHidSkip=non installé (option décochée). RealFFB ne fonctionne pas sans lui.
en.SumDevReboot=continues automatically after restarting Windows
pt.SumDevReboot=continua sozinho depois de reiniciar o Windows
es.SumDevReboot=continúa solo después de reiniciar Windows
fr.SumDevReboot=reprend automatiquement après le redémarrage de Windows
en.SumDevOk=HidHide set up for %1
pt.SumDevOk=HidHide configurado para %1
es.SumDevOk=HidHide configurado para %1
fr.SumDevOk=HidHide configuré pour %1
en.SumDevSame=already set up (%1)
pt.SumDevSame=já estava configurado (%1)
es.SumDevSame=ya estaba configurado (%1)
fr.SumDevSame=déjà configuré (%1)
en.SumDevNot=not set up (%1). Use the "FS25 RealFFB - Configure devices" shortcut.
pt.SumDevNot=não configurado (%1). Use o atalho "FS25 RealFFB - Configure devices".
es.SumDevNot=no configurado (%1). Usa el acceso directo "FS25 RealFFB - Configure devices".
fr.SumDevNot=non configuré (%1). Utilisez le raccourci "FS25 RealFFB - Configure devices".
en.SumDevSkip=skipped (option unchecked)
pt.SumDevSkip=ignorado (opção desmarcada)
es.SumDevSkip=omitido (opción desmarcada)
fr.SumDevSkip=ignoré (option décochée)
en.SumMod=mod in %1
pt.SumMod=mod em %1
es.SumMod=mod en %1
fr.SumMod=mod dans %1
en.SumReboot=Restart Windows to finish. After the restart, the device setup opens by itself.
pt.SumReboot=Reinicie o Windows para concluir. Depois de reiniciar, a configuração dos dispositivos abre sozinha.
es.SumReboot=Reinicia Windows para terminar. Tras reiniciar, la configuración de dispositivos se abre sola.
fr.SumReboot=Redémarrez Windows pour terminer. Après le redémarrage, la configuration des périphériques s'ouvre seule.
en.SumReady=Installed and ready. Open FS25 RealFFB from the shortcut; it opens the game.
pt.SumReady=Instalado e pronto. Abra o FS25 RealFFB pelo atalho; ele abre o jogo.
es.SumReady=Instalado y listo. Abre FS25 RealFFB desde el acceso directo; abre el juego.
fr.SumReady=Installé et prêt. Ouvrez FS25 RealFFB depuis le raccourci ; il lance le jeu.
en.SumProblems=Installed with %1 problem(s) above. Use "FS25 RealFFB - Diagnostic report" to ask for help.
pt.SumProblems=Instalado com %1 problema(s) acima. Use "FS25 RealFFB - Diagnostic report" para pedir ajuda.
es.SumProblems=Instalado con %1 problema(s) arriba. Usa "FS25 RealFFB - Diagnostic report" para pedir ayuda.
fr.SumProblems=Installé avec %1 problème(s) ci-dessus. Utilisez "FS25 RealFFB - Diagnostic report" pour demander de l'aide.
en.Purge=Also delete your RealFFB settings and profiles (FFB tuning, pedals, buttons)?%n%nYes = delete everything.   No = keep them (recommended if you will reinstall).
pt.Purge=Apagar também seus ajustes e perfis do RealFFB (FFB, pedais, comandos)?%n%nSim = apagar tudo.   Não = manter (recomendado se for reinstalar).
es.Purge=¿Borrar también tus ajustes y perfiles de RealFFB (FFB, pedales, botones)?%n%nSí = borrar todo.   No = conservarlos (recomendado si vas a reinstalar).
fr.Purge=Supprimer aussi vos réglages et profils RealFFB (FFB, pédales, boutons) ?%n%nOui = tout supprimer.   Non = les garder (recommandé si vous réinstallez).

[Tasks]
Name: "desktopicon"; Description: "{cm:TaskDesktop}"

[Files]
Source: "..\dist\app\RealFFB.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\app\RealFFB.Setup.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\app\FS25_RealFFBTelemetry.zip"; DestDir: "{code:GetModsDir}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; DestName: "LICENSE.txt"; Flags: ignoreversion
Source: "..\docs\TROUBLESHOOTING.md"; DestDir: "{app}"; DestName: "Troubleshooting.txt"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\RealFFB.exe"
Name: "{autoprograms}\{#AppName} - Configure devices"; Filename: "{app}\RealFFB.Setup.exe"; Parameters: "configure"
Name: "{autoprograms}\{#AppName} - Diagnostic report"; Filename: "{app}\RealFFB.Setup.exe"; Parameters: "diagnose"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\RealFFB.exe"; Parameters: "--jogar"; IconFilename: "{code:GetShortcutIcon}"; Tasks: desktopicon

[Run]
Filename: "{app}\RealFFB.exe"; Description: "{cm:RunNow}"; Flags: nowait postinstall skipifsilent; Check: not NeedRestart

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
  if Ok then Result := CustomMessage('Ok') + '  ' else Result := CustomMessage('Missing') + '  ';
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
    Log('Exec failed: ' + SysErrorMessage(Code));
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

  T := CustomMessage('Step1') + #13#10 + '  ' + Mark(OsOk) + R('os') + #13#10;
  if R('admin') = '1' then T := T + '  ' + CustomMessage('Warn') + '  ' + CustomMessage('AdminWarn') + #13#10;
  T := T + #13#10 + CustomMessage('Step2') + #13#10;
  if GameDir <> '' then
    T := T + '  ' + Mark(True) + GameDir + #13#10 + '        ' + FmtMessage(CustomMessage('Source'), [R('game_source')]) + #13#10
  else
    T := T + '  ' + Mark(False) + FmtMessage(CustomMessage('GameNotFound'), [R('game_source')]) + #13#10;
  T := T + '  ' + FmtMessage(CustomMessage('ModsFolder'), [ModsDir]) + #13#10;
  if R('game_running') = '1' then T := T + '  ' + CustomMessage('Warn') + '  ' + CustomMessage('GameOpen') + #13#10;
  T := T + #13#10 + CustomMessage('Step3') + #13#10;
  if FanCount = 0 then
    T := T + '  ' + Mark(False) + CustomMessage('NoWheel') + #13#10
  else
    for I := 0 to FanCount - 1 do T := T + '  ' + Mark(True) + R('fanatec_' + IntToStr(I)) + #13#10;
  if R('fanatec_other') <> '' then T := T + '  ' + FmtMessage(CustomMessage('OtherFanatec'), [R('fanatec_other')]) + #13#10;
  T := T + #13#10 + 'HidHide (Nefarius)' + #13#10;
  if HidStatus = 'ok' then T := T + '  ' + Mark(True) + FmtMessage(CustomMessage('HidOk'), [HidVersion]) + #13#10
  else if HidStatus = 'outdated' then T := T + '  ' + CustomMessage('Warn') + '  ' + FmtMessage(CustomMessage('HidOld'), [HidVersion]) + #13#10
  else T := T + '  ' + Mark(False) + CustomMessage('HidMissing') + #13#10;
  CheckPage.RichEditViewer.Lines.Text := T;
  DetectDone := True;
end;

procedure InitializeWizard;
begin
  WizardForm.MainPanel.Color := $121212;
  WizardForm.PageNameLabel.Font.Color := $00CCFF;   // yellow (BGR)
  WizardForm.PageDescriptionLabel.Font.Color := clWhite;
  WizardForm.WelcomeLabel2.Caption := CustomMessage('Welcome2');

  CheckPage := CreateOutputMsgMemoPage(wpLicense, CustomMessage('CheckTitle'), CustomMessage('CheckDesc'), CustomMessage('CheckSub'), '');

  GamePage := CreateInputDirPage(CheckPage.ID, CustomMessage('GameTitle'), CustomMessage('GameDesc'), CustomMessage('GameSub'), False, '');
  GamePage.Add('');

  HidPage := CreateInputOptionPage(GamePage.ID, CustomMessage('HidTitle'), CustomMessage('HidDesc'), CustomMessage('HidSub'), False, False);
  HidPage.Add(CustomMessage('HidOption'));
  HidPage.Values[0] := True;

  DevPage := CreateInputOptionPage(HidPage.ID, CustomMessage('DevTitle'), CustomMessage('DevDesc'), CustomMessage('DevSub'), False, False);
  DevPage.Add(CustomMessage('DevOption'));
  DevPage.Values[0] := True;

  FinalPage := CreateOutputMsgMemoPage(wpInstalling, CustomMessage('FinalTitle'), CustomMessage('FinalDesc'), CustomMessage('FinalSub'), '');
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = CheckPage.ID) and not DetectDone then
  begin
    WizardForm.NextButton.Enabled := False;
    CheckPage.RichEditViewer.Lines.Text := CustomMessage('Checking');
    RunDetection;
    WizardForm.NextButton.Enabled := True;
  end;
  if CurPageID = wpInstalling then
    WizardForm.PageNameLabel.Caption := CustomMessage('InstallingTitle');
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
      MsgBox(CustomMessage('NeedWin'), mbCriticalError, MB_OK);
      Result := False;
    end
    else if R('game_running') = '1' then
    begin
      if MsgBox(CustomMessage('CloseGame'), mbInformation, MB_OKCANCEL) = IDOK then
        RunDetection;
      Result := False;
    end;
  end;
  if CurPageID = GamePage.ID then
  begin
    if not IsValidGame(GamePage.Values[0]) then
    begin
      MsgBox(CustomMessage('BadFolder'), mbError, MB_OK);
      Result := False;
    end
    else GameDir := GamePage.Values[0];
  end;
end;

// ---------------------------------------------------------------- steps 4, 5 and 7 (after copying)
procedure PostInstall;
var
  Code, I, Errors: Integer;
  Prog: TOutputProgressWizardPage;
begin
  Prog := CreateOutputProgressPage('FS25 RealFFB Installer', CustomMessage('Configuring'));
  Prog.Show;
  try
    Helper(True, 'remember --game "' + GameDir + '" --mods "' + ModsDir + '" --lang ' + ActiveLanguage);
    AddStep(CustomMessage('Step1') + ': ' + OsText);
    AddStep(CustomMessage('Step2') + ': ' + GameDir);
    AddStep(CustomMessage('Step3') + ': ' + FmtMessage(CustomMessage('SumWheels'), [IntToStr(FanCount)]));

    // Step 4
    Prog.SetText(CustomMessage('HidTitle'), CustomMessage('ProgHid'));
    Prog.SetProgress(1, 5);
    if (HidStatus = 'ok') then
    begin
      Code := Helper(True, 'hidhide-install');
      if Code = 3 then RebootNeeded := True;
      AddStep(CustomMessage('Step4') + ': ' + FmtMessage(CustomMessage('SumHidPresent'), [HidVersion]));
    end
    else if HidPage.Values[0] then
    begin
      Code := Helper(True, 'hidhide-install');
      if Code = 0 then AddStep(CustomMessage('Step4') + ': ' + FmtMessage(CustomMessage('SumHidInstalled'), [R('version'), R('sha256')]))
      else if Code = 3 then begin RebootNeeded := True; AddStep(CustomMessage('Step4') + ': ' + CustomMessage('SumHidReboot')); end
      else if Code = 2 then AddStep(CustomMessage('Step4') + ': ' + FmtMessage(CustomMessage('SumHidCancel'), [R('status')]))
      else AddStep(CustomMessage('Step4') + ': ' + FmtMessage(CustomMessage('SumHidFail'), [R('status') + ' ' + R('error')]));
    end
    else AddStep(CustomMessage('Step4') + ': ' + CustomMessage('SumHidSkip'));

    // Step 5
    Prog.SetText(CustomMessage('DevTitle'), CustomMessage('ProgDev'));
    Prog.SetProgress(2, 5);
    if RebootNeeded then
    begin
      Helper(True, 'schedule-resume');
      AddStep(CustomMessage('Step5') + ': ' + CustomMessage('SumDevReboot'));
      Helper(True, 'patch-gamexml');
    end
    else if DevPage.Values[0] then
    begin
      Code := Helper(True, 'configure');
      if R('status') = 'configured' then AddStep(CustomMessage('Step5') + ': ' + FmtMessage(CustomMessage('SumDevOk'), [R('device')]))
      else if R('status') = 'unchanged' then AddStep(CustomMessage('Step5') + ': ' + FmtMessage(CustomMessage('SumDevSame'), [R('device')]))
      else AddStep(CustomMessage('Step5') + ': ' + FmtMessage(CustomMessage('SumDevNot'), [R('status')]));
    end
    else
    begin
      Helper(True, 'patch-gamexml');
      AddStep(CustomMessage('Step5') + ': ' + CustomMessage('SumDevSkip'));
    end;

    AddStep(CustomMessage('Step6') + ': ' + ExpandConstant('{app}') + '; ' + FmtMessage(CustomMessage('SumMod'), [ModsDir]));

    // Step 7
    Prog.SetText(CustomMessage('FinalTitle'), '');
    Prog.SetProgress(4, 5);
    Code := Helper(True, 'verify');
    Errors := StrToIntDef(R('errors'), 0);
    AddStep('');
    AddStep(CustomMessage('Step7') + ':');
    for I := 0 to StrToIntDef(R('checks'), 0) - 1 do AddStep('  ' + R('check_' + IntToStr(I)));
    AddStep('');
    if RebootNeeded then
      AddStep(CustomMessage('SumReboot'))
    else if Errors = 0 then
      AddStep(CustomMessage('SumReady'))
    else
      AddStep(FmtMessage(CustomMessage('SumProblems'), [IntToStr(Errors)]));
    Prog.SetProgress(5, 5);
  finally
    Prog.Hide;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then PostInstall;
end;

// ---------------------------------------------------------------- uninstall
function InitializeUninstall(): Boolean;
begin
  Result := True;
  PurgeData := MsgBox(CustomMessage('Purge'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
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
    // restores HidHide and game.xml as they were before; HidHide itself is not uninstalled
    Exec(ExpandConstant('{app}\RealFFB.Setup.exe'), P, '', SW_HIDE, ewWaitUntilTerminated, Code);
  end;
end;
