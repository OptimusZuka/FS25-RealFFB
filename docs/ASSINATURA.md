# Assinatura digital: estado e plano

## Estado atual

**O instalador e os executáveis do FS25 RealFFB não estão assinados.** Nunca são apresentados como assinados. A autenticidade verifica-se pelo SHA-256 publicado em cada Release ([INSTALACAO.md](INSTALACAO.md#verificar-o-ficheiro)). O código é aberto e cada versão é compilada pelo GitHub Actions, por isso qualquer pessoa pode conferir o processo.

## O que cada proteção do Windows faz com um ficheiro sem assinatura

| Proteção | Efeito | Gratuito resolve? |
|---|---|---|
| SmartScreen | aviso com "Executar assim mesmo"; reduz com a reputação do ficheiro | só uma assinatura reconhecida elimina o aviso logo à partida |
| Smart App Control | **bloqueia** sem exceção possível | só com assinatura de uma AC confiável (ou reputação na nuvem da Microsoft) |
| Windows Defender | analisa como qualquer outro ficheiro; um falso positivo pode ser enviado à Microsoft para revisão | sim: submissão em `https://www.microsoft.com/wdsi/filesubmission` |
| UAC | só aparece para o HidHide, que **é assinado** pela Nefarius | — |

Medidas já aplicadas para reduzir falsos positivos: nenhum script PowerShell no instalador, nenhum código ofuscado, nenhum compressor de executáveis (UPX e semelhantes), instalação sem administrador, e o HidHide é descarregado da fonte oficial e executado sem alterações.

## Opção gratuita: SignPath Foundation

A [SignPath Foundation](https://signpath.org) assina gratuitamente projetos de código aberto. O certificado é emitido em nome da "SignPath Foundation" e cada assinatura fica ligada a uma compilação feita a partir deste repositório público.

Condições principais (confirmar sempre na página da SignPath, porque podem mudar):

1. repositório **público** com licença OSI (MIT ✔);
2. compilação a partir do código-fonte num serviço de CI (GitHub Actions ✔);
3. MFA ativo nas contas do GitHub e da SignPath;
4. aprovação manual de cada versão (os rascunhos de Release já obrigam a isso ✔);
5. página do projeto com a política de assinatura de código.

Passos para o mantenedor:

1. Tornar o repositório público (Settings → General → Danger Zone → Change visibility).
2. Pedir a adesão em https://signpath.org/apply.
3. Depois de aprovado, acrescentar ao `release.yml`, entre "Compilar" e "Notas da versão", o passo de assinatura da [action oficial](https://github.com/SignPath/github-action-submit-signing-request). Assinar a `RealFFB.exe` e a `RealFFB.Setup.exe` antes do ISCC, e o instalador depois dele. Os segredos ficam em `SIGNPATH_API_TOKEN`.
4. Tirar o aviso "não tem assinatura" das notas da versão.

Alternativas pagas (certificado OV/EV, Azure Trusted Signing) estão fora do âmbito: o projeto é 100% gratuito.
