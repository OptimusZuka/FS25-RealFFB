# Resolução de problemas

Primeiro passo, sempre: **Iniciar → FS25 RealFFB - Relatório de diagnóstico** (ou o botão na app). O relatório não inclui o nome de utilizador, o nome do PC nem números de série. Anexe-o a um [Issue](https://github.com/OptimusZuka/FS25-RealFFB/issues).

Os registos ficam em `%APPDATA%\FS25 RealFFB\` (`RealFFB_log.txt`, `setup_log.txt`). As cópias de segurança ficam na subpasta `backup\`.

| Sintoma / linha do diagnóstico | Causa provável | O que fazer |
|---|---|---|
| `[ERRO] HidHide: nao instalado` | instalação do HidHide cancelada ou falhada | executar de novo o instalador do RealFFB |
| `Driver HidHide ... nao responde` | o driver ainda não carregou, ou o *HidHide Configuration Client* está aberto | reiniciar o Windows; fechar o Configuration Client |
| `RealFFB autorizada no HidHide: ERRO` | a app foi movida ou reinstalada noutra pasta | Iniciar → *Configurar dispositivos* |
| `Fanatec escondida do jogo: ERRO` | configuração não aplicada ou cancelada | Iniciar → *Configurar dispositivos* |
| `Fanatec: nenhuma base ligada` | base desligada, em modo de compatibilidade ou sem driver Fanatec | ligar em modo PC (LED vermelho) e instalar o driver Fanatec |
| `Outros controladores escondidos: AVISO` | entradas que não são Fanatec na lista do HidHide (o RealFFB nunca as cria) | rever no *HidHide Configuration Client* |
| `Farming Simulator 25: nao encontrado` | instalação fora da Steam numa pasta pouco habitual | executar o instalador e indicar a pasta |
| `Mod RealFFB: instalado na pasta errada` | o zip foi copiado à mão para outro sítio | apagar essa cópia; executar o instalador |
| `game.xml: configuracao invalida` | o jogo ou outro programa repôs o `game.xml` | fechar o FS25 e usar *Configurar dispositivos* |
| `game.xml: ainda nao existe` | o FS25 nunca foi aberto nesta conta | abrir o jogo uma vez, fechar e usar *Configurar dispositivos* |
| `Permissoes (pasta de dados): ERRO` | antivírus ou pasta sincronizada a bloquear `%APPDATA%` | autorizar a pasta no antivírus |

## Outros casos

- **A app abre mas não há força no jogo.** Confirme que o mod *Real FFB Telemetry* está ativo no save, que a app mostra telemetria a chegar e que o FS25 foi aberto **depois** da app.
- **O volante não vira no jogo.** A ponte de comandos está desligada, ou o FS25 ainda vê a base. Use *Configurar dispositivos* e reinicie o jogo.
- **A base não funciona noutros jogos.** Feche o RealFFB: a ocultação desliga-se quando a app fecha. Se a app terminou de forma inesperada, abra-a e feche-a outra vez.
- **O instalador ficou a meio (falta de luz, PC desligado).** Execute-o de novo. Cada passo verifica o estado antes de agir: não reinstala o HidHide se já existir e não duplica entradas.
- **"O Windows protegeu o PC" / Smart App Control.** Veja [INSTALACAO.md](INSTALACAO.md#2-avisos-do-windows-o-que-é-normal).

## Repor tudo à mão

1. Desinstale o FS25 RealFFB pelas Definições do Windows. O `game.xml` e o HidHide voltam ao estado anterior.
2. Se for preciso, o `game.xml` original está em `%APPDATA%\FS25 RealFFB\backup\game.xml.*.bak`, e a configuração original do HidHide em `backup\hidhide-original-*.txt` (cada linha é uma opção do `HidHideCLI.exe`).
