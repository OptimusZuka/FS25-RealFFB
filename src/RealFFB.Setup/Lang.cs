// FS25 RealFFB - idiomas da interface. Ingles por omissao; portugues (Brasil), espanhol e frances.
// Chave = texto em ingles. Texto sem traducao aparece em ingles.
using System;
using System.Collections.Generic;

namespace RealFFBSetup
{
    internal static class L
    {
        public static readonly string[] Codes = { "en", "pt", "es", "fr" };
        public static readonly string[] Names = { "English", "Português (Brasil)", "Español", "Français" };
        public static string Current = "en";
        private static Dictionary<string, string> map;

        public static void Set(string code)
        {
            int i = Array.IndexOf(Codes, code ?? "");
            Current = i < 0 ? "en" : Codes[i];
            map = null;
            if (Current == "en") return;
            int col = Array.IndexOf(Codes, Current);
            map = new Dictionary<string, string>();
            for (int r = 0; r + 3 < T4.Length; r += 4) map[T4[r]] = T4[r + col];
        }

        public static string T(string en)
        {
            string t;
            return map != null && map.TryGetValue(en, out t) ? t : en;
        }

        public static string F(string en, params object[] args) { return string.Format(System.Globalization.CultureInfo.InvariantCulture, T(en), args); }

        // en, pt-BR, es, fr
        private static readonly string[] T4 = {
            "Device setup", "Configuração dos dispositivos", "Configuración de los dispositivos", "Configuration des périphériques",
            "Continue", "Continuar", "Continuar", "Continuer",
            "Cancel", "Cancelar", "Cancelar", "Annuler",
            "Apply", "Aplicar", "Aplicar", "Appliquer",
            "Don't change", "Não alterar", "No cambiar", "Ne pas modifier",
            "RealFFB.exe not found:\n{0}", "RealFFB.exe não encontrado:\n{0}", "No se encontró RealFFB.exe:\n{0}", "RealFFB.exe introuvable :\n{0}",
            "HidHide is not installed.\nRun the FS25 RealFFB installer again to install it.", "O HidHide não está instalado.\nExecute de novo o instalador do FS25 RealFFB para instalá-lo.", "HidHide no está instalado.\nEjecuta de nuevo el instalador de FS25 RealFFB para instalarlo.", "HidHide n'est pas installé.\nRelancez l'installateur de FS25 RealFFB pour l'installer.",
            "The HidHide driver is not responding.\n\nIf you just installed it, restart Windows.\nIf the 'HidHide Configuration Client' is open, close it and try again.", "O driver HidHide não responde.\n\nSe acabou de instalá-lo, reinicie o Windows.\nSe o 'HidHide Configuration Client' estiver aberto, feche-o e tente de novo.", "El controlador HidHide no responde.\n\nSi acabas de instalarlo, reinicia Windows.\nSi el 'HidHide Configuration Client' está abierto, ciérralo y vuelve a intentarlo.", "Le pilote HidHide ne répond pas.\n\nSi vous venez de l'installer, redémarrez Windows.\nSi 'HidHide Configuration Client' est ouvert, fermez-le et réessayez.",
            "No supported wheel was found (Fanatec in PC mode or Logitech G29/G920/G923/G27/G25).", "Nenhum volante suportado foi encontrado (Fanatec em modo PC ou Logitech G29/G920/G923/G27/G25).", "No se encontró ningún volante compatible (Fanatec en modo PC o Logitech G29/G920/G923/G27/G25).", "Aucun volant compatible trouvé (Fanatec en mode PC ou Logitech G29/G920/G923/G27/G25).",
            "Connect the wheel (Fanatec: red LED = PC mode) and use the 'FS25 RealFFB - Configure devices' shortcut.\nNothing was changed.", "Conecte o volante (Fanatec: LED vermelho = modo PC) e use o atalho 'FS25 RealFFB - Configure devices'.\nNada foi alterado.", "Conecta el volante (Fanatec: LED rojo = modo PC) y usa el acceso directo 'FS25 RealFFB - Configure devices'.\nNo se cambió nada.", "Branchez le volant (Fanatec : LED rouge = mode PC) et utilisez le raccourci 'FS25 RealFFB - Configure devices'.\nRien n'a été modifié.",
            "Settings were already applied to {0}.", "A configuração já estava aplicada ao {0}.", "La configuración ya estaba aplicada a {0}.", "La configuration était déjà appliquée à {0}.",
            "Several wheels were found. Choose the one you will use in FS25.\nOnly that one will be hidden from the game.", "Foram encontrados vários volantes. Escolha o que vai usar no FS25.\nSó esse será escondido do jogo.", "Se encontraron varios volantes. Elige el que usarás en FS25.\nSolo ese se ocultará al juego.", "Plusieurs volants trouvés. Choisissez celui que vous utiliserez dans FS25.\nSeul celui-ci sera masqué au jeu.",
            "The HidHide entries of the {0} could not be identified safely.\nNothing was changed.\n\nCreate the diagnostic report and open an issue on GitHub.", "Não foi possível identificar com segurança as entradas do HidHide do {0}.\nNada foi alterado.\n\nGere o relatório de diagnóstico e abra um Issue no GitHub.", "No se pudieron identificar con seguridad las entradas de HidHide de {0}.\nNo se cambió nada.\n\nGenera el informe de diagnóstico y abre un Issue en GitHub.", "Impossible d'identifier de façon sûre les entrées HidHide de {0}.\nRien n'a été modifié.\n\nCréez le rapport de diagnostic et ouvrez un ticket sur GitHub.",
            "These changes will be made in HidHide:", "Estas alterações serão feitas no HidHide:", "Se harán estos cambios en HidHide:", "Ces modifications seront faites dans HidHide :",
            "1) Allow the application:", "1) Autorizar o aplicativo:", "1) Autorizar la aplicación:", "1) Autoriser l'application :",
            "2) Hide only the {0} from games:", "2) Esconder dos jogos apenas o {0}:", "2) Ocultar a los juegos solo {0}:", "2) Masquer aux jeux uniquement {0} :",
            "No other controller is changed. Hiding is only active while RealFFB is open (with 'AutoHide').", "Nenhum outro controle é alterado. A ocultação só fica ativa enquanto o RealFFB estiver aberto (com 'AutoHide').", "No se cambia ningún otro mando. La ocultación solo está activa mientras RealFFB esté abierto (con 'AutoHide').", "Aucun autre périphérique n'est modifié. Le masquage n'est actif que lorsque RealFFB est ouvert (avec 'AutoHide').",
            "The previous configuration is saved and restored on uninstall.", "A configuração anterior é guardada e reposta ao desinstalar.", "La configuración anterior se guarda y se restaura al desinstalar.", "La configuration précédente est sauvegardée et restaurée à la désinstallation.",
            "FS25 is open: game.xml will be adjusted next time (close the game and use this shortcut again).", "O FS25 está aberto: o game.xml será ajustado na próxima vez (feche o jogo e use este atalho de novo).", "FS25 está abierto: game.xml se ajustará la próxima vez (cierra el juego y vuelve a usar este acceso directo).", "FS25 est ouvert : game.xml sera ajusté la prochaine fois (fermez le jeu et réutilisez ce raccourci).",
            "Settings applied to {0}.", "Configuração aplicada ao {0}.", "Configuración aplicada a {0}.", "Configuration appliquée à {0}.",
            "Installation complete. Open FS25 RealFFB from the shortcut.", "Instalação concluída. Abra o FS25 RealFFB pelo atalho.", "Instalación completada. Abre FS25 RealFFB desde el acceso directo.", "Installation terminée. Ouvrez FS25 RealFFB depuis le raccourci.",
            "Installs in your user account, no administrator needed (only HidHide asks for UAC)", "Instala na sua conta, sem administrador (só o HidHide pede UAC)", "Se instala en tu cuenta, sin administrador (solo HidHide pide UAC)", "S'installe dans votre compte, sans administrateur (seul HidHide demande l'UAC)",
            "1. System check", "1. Verificação do sistema", "1. Comprobación del sistema", "1. Vérification du système",
            "2. Farming Simulator 25 detection", "2. Detecção do Farming Simulator 25", "2. Detección de Farming Simulator 25", "2. Détection de Farming Simulator 25",
            "3. Wheel check", "3. Verificação do volante", "3. Comprobación del volante", "3. Vérification du volant",
            "4. HidHide installation", "4. Instalação do HidHide", "4. Instalación de HidHide", "4. Installation de HidHide",
            "5. Device setup", "5. Configuração dos dispositivos", "5. Configuración de los dispositivos", "5. Configuration des périphériques",
            "6. RealFFB installation", "6. Instalação do RealFFB", "6. Instalación de RealFFB", "6. Installation de RealFFB",
            "7. Final check", "7. Verificação final", "7. Comprobación final", "7. Vérification finale",
            "Install", "Instalar", "Instalar", "Installer",
            "Close", "Fechar", "Cerrar", "Fermer",
            "Finish", "Concluir", "Finalizar", "Terminer",
            "Install folder: {0}", "Pasta de instalação: {0}", "Carpeta de instalación: {0}", "Dossier d'installation : {0}",
            "Close Farming Simulator 25 and RealFFB before continuing.", "Feche o Farming Simulator 25 e o RealFFB antes de continuar.", "Cierra Farming Simulator 25 y RealFFB antes de continuar.", "Fermez Farming Simulator 25 et RealFFB avant de continuer.",
            "Requires 64-bit Windows 10 or 11.", "Precisa do Windows 10 ou 11 de 64 bits.", "Requiere Windows 10 u 11 de 64 bits.", "Nécessite Windows 10 ou 11 64 bits.",
            "FS25 is open. Close it and click Install again.", "O FS25 está aberto. Feche-o e clique em Instalar de novo.", "FS25 está abierto. Ciérralo y pulsa Instalar de nuevo.", "FS25 est ouvert. Fermez-le et cliquez à nouveau sur Installer.",
            "FS25 not found automatically ({0}). Choose the folder.", "FS25 não encontrado automaticamente ({0}). Indique a pasta.", "FS25 no se encontró automáticamente ({0}). Indica la carpeta.", "FS25 introuvable automatiquement ({0}). Indiquez le dossier.",
            "Farming Simulator 25 folder (the one with FarmingSimulator2025.exe)", "Pasta do Farming Simulator 25 (a que tem FarmingSimulator2025.exe)", "Carpeta de Farming Simulator 25 (la que tiene FarmingSimulator2025.exe)", "Dossier de Farming Simulator 25 (celui avec FarmingSimulator2025.exe)",
            "Cannot continue without the FS25 folder.", "Sem a pasta do FS25 não é possível continuar.", "No se puede continuar sin la carpeta de FS25.", "Impossible de continuer sans le dossier de FS25.",
            "No supported wheel connected. You can set it up later (Start > FS25 RealFFB - Configure devices).", "Nenhum volante suportado conectado. Pode configurar depois (Iniciar > FS25 RealFFB - Configure devices).", "Ningún volante compatible conectado. Puedes configurarlo después (Inicio > FS25 RealFFB - Configure devices).", "Aucun volant compatible connecté. Vous pourrez le configurer plus tard (Démarrer > FS25 RealFFB - Configure devices).",
            "HidHide {0} already installed.", "HidHide {0} já instalado.", "HidHide {0} ya instalado.", "HidHide {0} déjà installé.",
            "HidHide installation", "Instalação do HidHide", "Instalación de HidHide", "Installation de HidHide",
            "RealFFB needs HidHide (Nefarius Software Solutions, free, open source), a driver that hides the wheel from FS25.\n\nThe official installer will be downloaded from github.com/nefarius/HidHide. Its digital signature is checked before running and the file is not modified.\n\nWindows will ask for administrator permission (UAC). A restart may be needed.", "O RealFFB precisa do HidHide (Nefarius Software Solutions, gratuito, código aberto), um driver que esconde o volante do FS25.\n\nO instalador oficial será baixado de github.com/nefarius/HidHide. A assinatura digital é verificada antes de executar e o arquivo não é alterado.\n\nO Windows vai pedir autorização de administrador (UAC). Pode ser preciso reiniciar.", "RealFFB necesita HidHide (Nefarius Software Solutions, gratuito, código abierto), un controlador que oculta el volante a FS25.\n\nSe descargará el instalador oficial de github.com/nefarius/HidHide. Su firma digital se comprueba antes de ejecutarlo y el archivo no se modifica.\n\nWindows pedirá permiso de administrador (UAC). Puede ser necesario reiniciar.", "RealFFB a besoin de HidHide (Nefarius Software Solutions, gratuit, open source), un pilote qui masque le volant à FS25.\n\nL'installateur officiel sera téléchargé depuis github.com/nefarius/HidHide. Sa signature numérique est vérifiée avant l'exécution et le fichier n'est pas modifié.\n\nWindows demandera l'autorisation administrateur (UAC). Un redémarrage peut être nécessaire.",
            "HidHide not installed. RealFFB does not work without it.", "HidHide não instalado. O RealFFB não funciona sem ele.", "HidHide no instalado. RealFFB no funciona sin él.", "HidHide non installé. RealFFB ne fonctionne pas sans lui.",
            "HidHide installed.", "HidHide instalado.", "HidHide instalado.", "HidHide installé.",
            "HidHide installed: Windows needs a restart.", "HidHide instalado: é preciso reiniciar o Windows.", "HidHide instalado: hay que reiniciar Windows.", "HidHide installé : Windows doit redémarrer.",
            "HidHide failed (code {0}). See the log at %APPDATA%\\FS25 RealFFB\\setup_log.txt.", "HidHide falhou (código {0}). Veja o registro em %APPDATA%\\FS25 RealFFB\\setup_log.txt.", "HidHide falló (código {0}). Mira el registro en %APPDATA%\\FS25 RealFFB\\setup_log.txt.", "HidHide a échoué (code {0}). Voir le journal %APPDATA%\\FS25 RealFFB\\setup_log.txt.",
            "Missing {0} in {1}. Run build.bat first.", "Falta {0} em {1}. Execute o build.bat primeiro.", "Falta {0} en {1}. Ejecuta build.bat primero.", "{0} manquant dans {1}. Lancez d'abord build.bat.",
            "Close FS25 RealFFB and click OK.", "Feche o FS25 RealFFB e clique em OK.", "Cierra FS25 RealFFB y pulsa Aceptar.", "Fermez FS25 RealFFB et cliquez sur OK.",
            "RealFFB is still open.", "O RealFFB continua aberto.", "RealFFB sigue abierto.", "RealFFB est toujours ouvert.",
            "Create a desktop shortcut?\n(Opens RealFFB and FS25)", "Criar atalho na área de trabalho?\n(Abre o RealFFB e o FS25)", "¿Crear acceso directo en el escritorio?\n(Abre RealFFB y FS25)", "Créer un raccourci sur le bureau ?\n(Ouvre RealFFB et FS25)",
            "Installed in {0}", "Instalado em {0}", "Instalado en {0}", "Installé dans {0}",
            "Device setup continues automatically after restarting Windows.", "A configuração dos dispositivos continua sozinha depois de reiniciar o Windows.", "La configuración de dispositivos continúa sola después de reiniciar Windows.", "La configuration des périphériques continue après le redémarrage de Windows.",
            "Devices: configured", "Dispositivos: configurados", "Dispositivos: configurados", "Périphériques : configurés",
            "Devices: not configured (use 'Configure devices' in the Start menu)", "Dispositivos: não configurados (use 'Configure devices' no menu Iniciar)", "Dispositivos: no configurados (usa 'Configure devices' en el menú Inicio)", "Périphériques : non configurés (utilisez 'Configure devices' dans le menu Démarrer)",
            "Devices not configured yet: connect the wheel and use Start > FS25 RealFFB - Configure devices.", "Dispositivos por configurar: conecte o volante e use Iniciar > FS25 RealFFB - Configure devices.", "Dispositivos sin configurar: conecta el volante y usa Inicio > FS25 RealFFB - Configure devices.", "Périphériques à configurer : branchez le volant et utilisez Démarrer > FS25 RealFFB - Configure devices.",
            "Restart Windows to finish.", "Reinicie o Windows para concluir.", "Reinicia Windows para terminar.", "Redémarrez Windows pour terminer.",
            "Installed and ready. Open FS25 RealFFB from the Start menu or the shortcut.", "Instalado e pronto. Abra o FS25 RealFFB pelo menu Iniciar ou pelo atalho.", "Instalado y listo. Abre FS25 RealFFB desde el menú Inicio o el acceso directo.", "Installé et prêt. Ouvrez FS25 RealFFB depuis le menu Démarrer ou le raccourci.",
            "Installed with {0} problem(s). Use 'Diagnostic report' to ask for help.", "Instalado com {0} problema(s). Use 'Diagnostic report' para pedir ajuda.", "Instalado con {0} problema(s). Usa 'Diagnostic report' para pedir ayuda.", "Installé avec {0} problème(s). Utilisez 'Diagnostic report' pour demander de l'aide.",
            "Uninstall FS25 RealFFB?\n\nHidHide and game.xml go back to how they were. HidHide stays installed.", "Desinstalar o FS25 RealFFB?\n\nO HidHide e o game.xml voltam ao estado anterior. O HidHide continua instalado.", "¿Desinstalar FS25 RealFFB?\n\nHidHide y game.xml vuelven a como estaban. HidHide sigue instalado.", "Désinstaller FS25 RealFFB ?\n\nHidHide et game.xml reviennent à leur état précédent. HidHide reste installé.",
            "Close FS25 RealFFB first.", "Feche o FS25 RealFFB primeiro.", "Cierra FS25 RealFFB primero.", "Fermez d'abord FS25 RealFFB.",
            "Also delete your settings and profiles (FFB tuning, pedals, buttons)?\n\nNo = keep them (recommended if you will reinstall).", "Apagar também seus ajustes e perfis (FFB, pedais, comandos)?\n\nNão = manter (recomendado se for reinstalar).", "¿Borrar también tus ajustes y perfiles (FFB, pedales, botones)?\n\nNo = conservarlos (recomendado si vas a reinstalar).", "Supprimer aussi vos réglages et profils (FFB, pédales, boutons) ?\n\nNon = les garder (recommandé si vous réinstallez).",
            "FS25 RealFFB uninstalled.", "FS25 RealFFB desinstalado.", "FS25 RealFFB desinstalado.", "FS25 RealFFB désinstallé.",
            "FS25 RealFFB utility.\nUse the 'Configure devices' shortcut or the diagnostics button in the app.", "Utilitário do FS25 RealFFB.\nUse o atalho 'Configure devices' ou o botão de diagnóstico do app.", "Utilidad de FS25 RealFFB.\nUsa el acceso directo 'Configure devices' o el botón de diagnóstico de la app.", "Utilitaire FS25 RealFFB.\nUtilisez le raccourci 'Configure devices' ou le bouton de diagnostic de l'app.",
            "Language", "Idioma", "Idioma", "Langue",
            "Could not get HidHide from GitHub: {0}", "Não foi possível obter o HidHide do GitHub: {0}", "No se pudo obtener HidHide de GitHub: {0}", "Impossible d'obtenir HidHide depuis GitHub : {0}",
            "Could not get HidHide from GitHub:\n{0}\n\nCheck your internet connection and try again.", "Não foi possível obter o HidHide do GitHub:\n{0}\n\nVerifique a ligação à internet e tente de novo.", "No se pudo obtener HidHide de GitHub:\n{0}\n\nComprueba la conexión a internet y vuelve a intentarlo.", "Impossible d'obtenir HidHide depuis GitHub :\n{0}\n\nVérifiez la connexion internet et réessayez.",
        };
    }
}
