// FS25 RealFFB - idiomas da interface. Ingles por omissao; portugues (Brasil), espanhol e frances.
// Chave = texto em ingles. Texto sem traducao aparece em ingles.
using System;
using System.Collections.Generic;

namespace FS25RealFFB
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
            "STOP (Esc)", "PARAR (Esc)", "PARAR (Esc)", "ARRÊT (Échap)",
            "Pin on top", "Fixar no topo", "Fijar arriba", "Épingler",
            "Unpin", "Desafixar", "Desfijar", "Désépingler",
            "Open FS25", "Abrir FS25", "Abrir FS25", "Lancer FS25",
            "FFB ON", "FFB ATIVO", "FFB ACTIVO", "FFB ACTIVÉ",
            "FFB OFF", "FFB DESLIGADO", "FFB APAGADO", "FFB DÉSACTIVÉ",
            "Wheel connected", "Volante conectado", "Volante conectado", "Volant connecté",
            "Wheel not open", "Volante não aberto", "Volante no abierto", "Volant non ouvert",
            "Telemetry {0:0} Hz", "Telemetria {0:0} Hz", "Telemetría {0:0} Hz", "Télémétrie {0:0} Hz",
            "No telemetry", "Sem telemetria", "Sin telemetría", "Pas de télémétrie",
            "Bridge {0:0}/s", "Ponte {0:0}/s", "Puente {0:0}/s", "Pont {0:0}/s",
            "Bridge off", "Ponte desligada", "Puente apagado", "Pont désactivé",
            "Vehicle profile", "Perfil do veículo", "Perfil del vehículo", "Profil du véhicule",
            "Wheel", "Volante", "Volante", "Volant",
            "Force", "Força", "Fuerza", "Force",
            "Terrain and engine", "Terreno e motor", "Terreno y motor", "Terrain et moteur",
            "Steering and pedals", "Direção e pedais", "Dirección y pedales", "Direction et pédales",
            "Buttons -> keys (optional)", "Comandos -> teclas (opcional)", "Botones -> teclas (opcional)", "Boutons -> touches (optionnel)",
            "System", "Sistema", "Sistema", "Système",
            "Log", "Registro", "Registro", "Journal",
            "Separate settings for each vehicle", "Ajustes próprios para cada veículo", "Ajustes propios para cada vehículo", "Réglages propres à chaque véhicule",
            "Delete this vehicle's profile (back to default)", "Apagar o perfil deste veículo (volta ao padrão)", "Borrar el perfil de este vehículo (vuelve al predeterminado)", "Supprimer le profil de ce véhicule (retour au défaut)",
            "Automatic (first wheel found)", "Automático (primeiro volante encontrado)", "Automático (primer volante encontrado)", "Automatique (premier volant trouvé)",
            "Looking for a wheel...", "Procurando volante...", "Buscando volante...", "Recherche du volant...",
            "Wheel torque (peak)", "Torque do volante (pico)", "Par del volante (pico)", "Couple du volant (crête)",
            "Maximum force", "Força máxima", "Fuerza máxima", "Force maximale",
            "Centering / self-aligning", "Centragem / alinhamento", "Centrado / autoalineación", "Centrage / auto-alignement",
            "Damping", "Amortecimento", "Amortiguación", "Amortissement",
            "Standstill weight (friction)", "Peso parado (atrito)", "Peso en parado (fricción)", "Poids à l'arrêt (friction)",
            "Reference speed", "Velocidade de referência", "Velocidad de referencia", "Vitesse de référence",
            "Suspension bumps", "Pancadas da suspensão", "Golpes de suspensión", "Chocs de suspension",
            "Terrain texture", "Textura do terreno", "Textura del terreno", "Texture du terrain",
            "Engine vibration", "Vibração do motor", "Vibración del motor", "Vibration moteur",
            "Steering sensitivity", "Sensibilidade da direção", "Sensibilidad de la dirección", "Sensibilité de direction",
            "Invert bump direction", "Inverter sentido das pancadas", "Invertir sentido de los golpes", "Inverser le sens des chocs",
            "Send wheel and pedals to FS25", "Enviar volante e pedais ao FS25", "Enviar volante y pedales a FS25", "Envoyer volant et pédales à FS25",
            "Invert steering", "Inverter direção", "Invertir dirección", "Inverser la direction",
            "Throttle", "Acelerador", "Acelerador", "Accélérateur",
            "Brake", "Freio", "Freno", "Frein",
            "Clutch", "Embreagem", "Embrague", "Embrayage",
            "Detect {0}", "Detectar {0}", "Detectar {0}", "Détecter {0}",
            "(not set)", "(não definido)", "(no definido)", "(non défini)",
            "axis {0}", "eixo {0}", "eje {0}", "axe {0}",
            "Only needed without HidHide. Add -> press the controller button -> press the FS25 key.", "Só é preciso sem HidHide. Adicionar -> aperte o botão do controle -> aperte a tecla do FS25.", "Solo hace falta sin HidHide. Añadir -> pulsa el botón del mando -> pulsa la tecla de FS25.", "Seulement sans HidHide. Ajouter -> appuyez sur le bouton -> appuyez sur la touche de FS25.",
            "Enable button-to-key translation", "Ativar tradução de botões em teclas", "Activar traducción de botones a teclas", "Activer la traduction boutons -> touches",
            "Add button", "Adicionar botão", "Añadir botón", "Ajouter un bouton",
            "Remove selected", "Remover selecionado", "Quitar seleccionado", "Retirer la sélection",
            "Import all from FS25 (inputBinding.xml)", "Importar tudo do FS25 (inputBinding.xml)", "Importar todo de FS25 (inputBinding.xml)", "Tout importer de FS25 (inputBinding.xml)",
            "Turn FFB on automatically when the app opens", "Ligar o FFB automaticamente ao abrir o app", "Activar el FFB automáticamente al abrir la app", "Activer le FFB automatiquement à l'ouverture",
            "Manage HidHide automatically (wheel hidden only while the app is open)", "Gerenciar o HidHide sozinho (volante escondido só com o app aberto)", "Gestionar HidHide automáticamente (volante oculto solo con la app abierta)", "Gérer HidHide automatiquement (volant masqué seulement app ouverte)",
            "Use the wheel's 2nd interface", "Usar a 2ª interface do volante", "Usar la 2.ª interfaz del volante", "Utiliser la 2e interface du volant",
            "Save settings", "Salvar configuração", "Guardar configuración", "Enregistrer les réglages",
            "Create diagnostic report", "Gerar relatório de diagnóstico", "Generar informe de diagnóstico", "Créer un rapport de diagnostic",
            "Create desktop shortcut", "Criar atalho na área de trabalho", "Crear acceso directo en el escritorio", "Créer un raccourci sur le bureau",
            "Language", "Idioma", "Idioma", "Langue",
            "Restart the app to apply the language.", "Reinicie o app para aplicar o idioma.", "Reinicia la app para aplicar el idioma.", "Redémarrez l'app pour appliquer la langue.",
            "Off: the same settings for every vehicle.", "Desligado: os mesmos ajustes para todos os veículos.", "Apagado: los mismos ajustes para todos los vehículos.", "Désactivé : les mêmes réglages pour tous les véhicules.",
            "No vehicle in game: changes edit the default. {0} profile(s) saved.", "Sem veículo no jogo: as alterações mudam o padrão. {0} perfil(is) salvo(s).", "Sin vehículo en el juego: los cambios editan el predeterminado. {0} perfil(es) guardado(s).", "Aucun véhicule en jeu : les changements modifient le défaut. {0} profil(s) enregistré(s).",
            "{0}: own settings (saved automatically).", "{0}: ajustes próprios (salvos automaticamente).", "{0}: ajustes propios (guardados automáticamente).", "{0} : réglages propres (enregistrés automatiquement).",
            "{0}: uses the default. Move a control to create this vehicle's profile.", "{0}: usa o padrão. Mexa num controle para criar o perfil deste veículo.", "{0}: usa el predeterminado. Mueve un control para crear el perfil de este vehículo.", "{0} : utilise le défaut. Bougez un réglage pour créer le profil de ce véhicule.",
            "In use: {0}", "Em uso: {0}", "En uso: {0}", "Utilisé : {0}",
            "No wheel open.", "Nenhum volante aberto.", "Ningún volante abierto.", "Aucun volant ouvert.",
            "Choice saved. Close and reopen RealFFB to switch wheels.", "Escolha salva. Feche e abra o RealFFB de novo para trocar de volante.", "Elección guardada. Cierra y vuelve a abrir RealFFB para cambiar de volante.", "Choix enregistré. Fermez et rouvrez RealFFB pour changer de volant.",
            "{0} (chosen, not connected)", "{0} (escolhido, não conectado)", "{0} (elegido, no conectado)", "{0} (choisi, non connecté)",
            "New version available: v{0}  (install)", "Nova versão disponível: v{0}  (instalar)", "Nueva versión disponible: v{0}  (instalar)", "Nouvelle version disponible : v{0}  (installer)",
            "Install version v{0}?\n\nYour settings, pedals and buttons are kept.\nRelease notes: {1}", "Instalar a versão v{0}?\n\nSeus ajustes, pedais e comandos são mantidos.\nNotas da versão: {1}", "¿Instalar la versión v{0}?\n\nTus ajustes, pedales y botones se conservan.\nNotas de la versión: {1}", "Installer la version v{0} ?\n\nVos réglages, pédales et boutons sont conservés.\nNotes de version : {1}",
            "Update FS25 RealFFB", "Atualizar FS25 RealFFB", "Actualizar FS25 RealFFB", "Mettre à jour FS25 RealFFB",
            "COMMANDS", "COMANDOS", "MANDOS", "COMMANDES",
            "(BRIDGE OFF)", "(PONTE DESLIGADA)", "(PUENTE APAGADO)", "(PONT DÉSACTIVÉ)",
            "Steering", "Direção", "Dirección", "Direction",
            "waiting for FS25...", "aguardando o FS25...", "esperando a FS25...", "en attente de FS25...",
            "FORCE AT THE WHEEL  (gain {0} %)", "FORÇA NO VOLANTE  (ganho {0} %)", "FUERZA EN EL VOLANTE  (ganancia {0} %)", "FORCE AU VOLANT  (gain {0} %)",
            "Centering", "Centragem", "Centrado", "Centrage",
            "Friction", "Atrito", "Fricción", "Friction",
            "Bumps", "Pancadas", "Golpes", "Chocs",
            "Texture", "Textura", "Textura", "Texture",
            "Engine", "Motor", "Motor", "Moteur",
            "surface {0}", "piso {0}", "suelo {0}", "sol {0}",
            "FFB off", "FFB desligado", "FFB apagado", "FFB désactivé",
            "waiting for FS25 telemetry", "aguardando telemetria do FS25", "esperando telemetría de FS25", "en attente de la télémétrie FS25",
            "paused / out of vehicle", "pausa / fora do veículo", "pausa / fuera del vehículo", "pause / hors du véhicule",
            "ACTIVE", "ATIVO", "ACTIVO", "ACTIF",
        };
    }
}
