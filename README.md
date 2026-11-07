<p align="center"><img src="docs/logo.png" width="220" alt="ZukaForce"></p>

# FS25 RealFFB

Force feedback real no **Farming Simulator 25** para bases **Fanatec** (testado na CSL DD 8 Nm), em modo PC normal (LED vermelho), sem modo de compatibilidade.

O FFB do próprio jogo é praticamente inexistente. O RealFFB calcula a força a partir da telemetria do trator — centragem que depende da velocidade e da carga, textura do piso, buracos, impactos e vibração do motor — e envia-a diretamente para a base.

## Instalar

1. Abra **[Releases](../../releases/latest)** e descarregue `FS25_RealFFB_Setup_x.y.z.exe`.
2. (Recomendado) [Verifique o SHA-256](docs/INSTALACAO.md#verificar-o-ficheiro).
3. Execute o ficheiro e siga as 7 etapas.
4. Abra o atalho **FS25 RealFFB**: ele abre a app e o jogo.

Não precisa de compilar nada, nem de instalar Python ou Visual Studio. Guia completo: [docs/INSTALACAO.md](docs/INSTALACAO.md). Problemas: [docs/PROBLEMAS.md](docs/PROBLEMAS.md).

> **O instalador não tem assinatura digital.** O SmartScreen pode avisar ("O Windows protegeu o PC"), e o **Smart App Control**, se estiver ligado, bloqueia ficheiros sem assinatura e não deixa abrir uma exceção. Não desative nenhuma proteção por causa do RealFFB. O motivo e o plano de assinatura gratuita estão em [docs/ASSINATURA.md](docs/ASSINATURA.md).

## Como funciona

```
FS25 + mod FS25_RealFFBTelemetry ──telemetria (pipe \\.\pipe\FS25RealFFB)──► RealFFB.exe ──DirectInput FFB──► Fanatec
            ▲                                                                   │
            └──── direção e pedais (modSettings\FS25_RealFFB_input.xml) ◄────────┘
```

- O FS25 agarra a base em modo exclusivo e não deixa outro programa enviar força. O **[HidHide](https://github.com/nefarius/HidHide)** (Nefarius, gratuito e de código aberto) esconde a base do jogo. Só o RealFFB.exe fica autorizado a vê-la.
- A app passa a direção e os pedais ao jogo através do mod. Os outros controladores (button box, manetes, alavanca) continuam a funcionar diretamente no jogo e **não são tocados**.
- Com o RealFFB fechado, a ocultação desliga-se: a base volta a funcionar normalmente no iRacing, ACC, ETS2 e outros.

## Requisitos

- Windows 10 ou 11 de 64 bits
- Farming Simulator 25 (Steam ou outra loja; qualquer disco)
- Base Fanatec em modo PC: CSL DD, GT DD Pro, ClubSport DD, Podium DD1/DD2, ClubSport V2/V2.5, CSL Elite (apenas a CSL DD foi testada)
- HidHide 1.2 ou mais recente (o instalador trata disto)

## O que o instalador altera

| O quê | Onde | Na desinstalação |
|---|---|---|
| App e ajudante | `%LOCALAPPDATA%\Programs\FS25 RealFFB` | removidos |
| Mod | pasta `mods` do FS25 (respeita `modsDirectoryOverride`) | removido |
| `game.xml` | joystick ativo, FFB do jogo desligado (com cópia de segurança) | valores originais repostos |
| HidHide | autoriza o RealFFB.exe e esconde **só** a base escolhida | só o que foi acrescentado é removido; o HidHide fica instalado |
| Definições | `%APPDATA%\FS25 RealFFB` | mantidas ou apagadas, à sua escolha |

Nenhum serviço, tarefa agendada ou entrada de arranque permanente. A única exceção é uma entrada `RunOnce`, usada só quando é preciso reiniciar depois de instalar o HidHide.

## Atualizações

A app verifica a última Release ao abrir. Se houver versão nova, aparece um botão: descarrega, confirma o SHA-256 publicado e abre o instalador. As definições ficam intactas. Nada é atualizado sem autorização, nem o HidHide.

## Compilar (programadores)

```bat
build.bat
```

Usa o `csc.exe` que vem com o Windows (.NET Framework 4.x) e o [Inno Setup 6](https://jrsoftware.org/isdl.php). O resultado fica em `dist\`. As versões são publicadas pelo GitHub Actions a partir de uma tag `vX.Y.Z` igual ao ficheiro `VERSION`.

```
src/RealFFB/         app (motor FFB, ponte de comandos, atualizações)
src/RealFFB.Setup/   ajudante do instalador (deteção, HidHide, game.xml, verificação, diagnóstico)
mod/                 mod Lua do FS25
installer/           script Inno Setup
tests/               testes automáticos (sem hardware)
tools/TesteA/        teste de FFB isolado
```

## Licença

[MIT](LICENSE). O HidHide é um projeto à parte, da Nefarius Software Solutions, com licença própria. É descarregado do repositório oficial e não é redistribuído aqui. Farming Simulator é marca da GIANTS Software, e Fanatec é marca da Endor AG. Este projeto não é afiliado a nenhuma delas.
