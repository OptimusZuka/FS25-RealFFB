# Histórico de versões

## [0.5.0] - 2026-11-07 (beta)
Primeira versão pública, em teste.

- Primeiro instalador oficial (Inno Setup): verificação do sistema, deteção do FS25 em todas as bibliotecas Steam, deteção da Fanatec, instalação do HidHide oficial com verificação de assinatura, configuração do HidHide só para a base escolhida, ajuste do `game.xml` com cópia de segurança, verificação final.
- Desinstalação pelas Definições do Windows: repõe o HidHide e o `game.xml` como estavam; o HidHide não é desinstalado; perfis mantidos se o utilizador quiser.
- Definições passam para `%APPDATA%\FS25 RealFFB` (sobrevivem a atualizações).
- A app verifica novas versões no GitHub e instala-as só com autorização, depois de confirmar o SHA-256.
- Botão "Gerar relatório de diagnóstico" (anonimizado).
- Interface nova com a identidade OptimusZuka (preto, amarelo e branco): cabeçalho com estado, painel ao vivo com comandos e forças, definições por secções.
- Suporte a volantes Logitech (G29, G920, G923, G PRO, G27, G25): deteção, HidHide e torque de pico por modelo (ajustável em "Torque do volante").
- Perfil de FFB por veículo: cada trator/máquina guarda as suas afinações (`%APPDATA%\FS25 RealFFB\perfis`); veículos sem perfil usam o padrão.
- "Fixar no topo": janela compacta sempre visível, só com o painel ao vivo.
- A roda do rato já não altera os controlos deslizantes: desce a página.
- Motor de force feedback igual ao da v0.17 (sem alterações).
