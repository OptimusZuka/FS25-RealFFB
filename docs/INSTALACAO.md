# Guia de instalação

## 1. Descarregar

Descarregue o instalador **apenas** da página oficial de Releases:
`https://github.com/OptimusZuka/FS25-RealFFB/releases/latest` → `FS25_RealFFB_Setup_x.y.z.exe`

## Verificar o ficheiro

Cada Release publica o `SHA256SUMS.txt` e repete os valores nas notas da versão. Se o valor calculado no seu PC for igual, o ficheiro é exatamente o que foi compilado pelo GitHub Actions a partir do código deste repositório.

1. Abra a pasta Transferências, clique com o botão direito num espaço vazio → **Abrir no Terminal** (ou PowerShell).
2. Execute:
   ```powershell
   Get-FileHash .\FS25_RealFFB_Setup_0.5.1.exe -Algorithm SHA256
   ```
3. Compare o `Hash` com a linha do `SHA256SUMS.txt`. Maiúsculas e minúsculas não interessam.
   - **Igual** → pode executar.
   - **Diferente** → não execute. Apague o ficheiro e descarregue de novo da página oficial.

Alternativa sem PowerShell: `certutil -hashfile FS25_RealFFB_Setup_0.5.1.exe SHA256` na Linha de Comandos.

## 2. Avisos do Windows (o que é normal)

O instalador **não tem assinatura digital** (veja [ASSINATURA.md](ASSINATURA.md)). Por isso:

- **SmartScreen** — "O Windows protegeu o PC": depois de confirmar o SHA-256, carregue em **Mais informações → Executar assim mesmo**. O aviso diminui à medida que a versão ganha reputação.
- **Smart App Control** (Windows 11), se estiver **ligado**, bloqueia aplicações sem assinatura e não oferece exceção. **Não o desative por causa do RealFFB.** Nesse caso há duas opções:
  1. esperar por uma versão assinada (ver ASSINATURA.md);
  2. compilar no seu próprio PC (`build.bat`, ver README). Isto não é garantido: o Smart App Control também pode bloquear ficheiros compilados localmente.
- **UAC** — só aparece para instalar o **HidHide** (é um driver). O resto do RealFFB instala-se na sua conta, sem administrador. O pedido de UAC mostra "Nefarius Software Solutions" como editor verificado.
- **Antivírus** — se o seu antivírus acusar o ficheiro, verifique o SHA-256 e abra um Issue com o nome da deteção.

## 3. Etapas do instalador

| Etapa | O que faz | Altera alguma coisa? |
|---|---|---|
| 1. Sistema | Windows 10/11 de 64 bits | não |
| 2. FS25 | procura em todas as bibliotecas Steam (todos os discos) e nas pastas habituais de outras lojas; se não encontrar, pede a pasta | não |
| 3. Fanatec | lista as bases Fanatec ligadas em modo PC | não |
| 4. HidHide | se faltar ou estiver desatualizado: descarrega o instalador oficial de `github.com/nefarius/HidHide`, confirma a assinatura digital da Nefarius e o SHA-256 publicado pelo GitHub, e executa-o sem alterações (UAC) | instala o driver |
| 5. Dispositivos | mostra exatamente o que vai mudar no HidHide e só aplica com o seu **Aplicar** | autoriza o RealFFB.exe e esconde só a base escolhida; ajusta o `game.xml` (com cópia) |
| 6. RealFFB | copia a app, o ajudante e o mod | ficheiros |
| 7. Verificação | confirma ficheiros, mod, game.xml, HidHide e Fanatec | não |

**Se for preciso reiniciar** depois do HidHide, o instalador avisa. Depois de reiniciar, abre-se sozinha a janela da etapa 5.

**Se a Fanatec não estiver ligada**, pode concluir a instalação. Depois ligue a base e use **Iniciar → FS25 RealFFB - Configurar dispositivos**.

## 4. Primeira utilização

1. Abra **FS25 RealFFB** (atalho no ambiente de trabalho). A app abre o FS25 pela Steam.
2. No jogo, em *Mods*, ative **Real FFB Telemetry** no save.
3. Na app, confirme a direção e os pedais (o separador de pedais deteta os eixos) e afine a força.

## Atualizar

Quando houver versão nova, aparece um botão amarelo na app. Também pode descarregar o instalador novo e executá-lo por cima. As definições (`%APPDATA%\FS25 RealFFB`) e a configuração do HidHide mantêm-se.

## Desinstalar

**Definições → Aplicações → FS25 RealFFB → Desinstalar.** O desinstalador repõe o `game.xml` e o HidHide como estavam antes. O HidHide fica instalado, porque outros programas podem usá-lo; se quiser removê-lo, desinstale-o à parte. No início, o desinstalador pergunta se quer manter as suas definições.
