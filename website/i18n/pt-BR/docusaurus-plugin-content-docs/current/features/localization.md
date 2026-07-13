# Localizacao (i18n)

cMind e totalmente localizavel e ships nos **mesmos 23 idiomas que o cTrader suporta**, entao um trader
usa a plataforma — e le estes docs — em seu proprio idioma. Ingles e o fallback; qualquer traducao faltante
degrada gracefulmente para Ingles ao inves de mostrar um espaco em branco ou chave bruta.

## Idiomas suportados

Arabe (RTL), Chines (Simplificado), Checo, Ingles, Frances, Alemao, Grego, Hungaro, Indonesio,
Italiano, Japones, Coreano, Malasio, Polones, Portugues (Brasil), Russo, Servio, Eslovaco, Slovenio,
Espanhol, Tailandes, Turco, Vietn Nam.

A unica fonte de verdade e `Core.Constants.SupportedCultures` — o middleware de cultura de requisicao, o
seletor de idioma, o teste de paridade de recursos e o gate de texto sem codificacaohard-coded todos leem dela. Adicionar um
idioma e uma mudanca de uma linha ali mais seus arquivos de recursos.

## Como funciona (Blazor Server)

- **Recursos.** Strings de UI vivem em `src/Web/Resources/Ui.resx` (base em Ingles) mais um
  `Ui.<culture>.resx` por idioma. Componentes os leem atraves de `IStringLocalizer<Ui>` — `@L["key"]`,
  nunca um literal. Os arquivos `.resx` sao gerados de `tools/i18n/ui-translations.json`
  (`pwsh tools/i18n/gen-resx.ps1`), a fonte de verdade amigavel para tradutores.
- **Resolucao de cultura.** `RequestLocalizationMiddleware` escolhe a cultura do cookie `.AspNetCore.Culture`
  primeiro, entao `Accept-Language` do navegador, entao Ingles.
- **Trocando.** O seletor de idioma na barra de apps (e a secao **Settings → Language**) navega para
  o endpoint `GET /set-culture` — um reload completo fora do circuito Blazor, porque um circuito nao pode
  mudar cultura ao vivo. Ele escreve o cookie e, para um usuario logado, persiste a escolha em seu
  perfil (`UserProfile.Locale`); o reload inicializa um circuito fresco no idioma escolhido.
- **Persistncia e login.** O locale do perfil salvo e escrito de volta no cookie de cultura no login,
  entao um usuario aterrissa em seu idioma em todo dispositivo.
- **Direita-para-esquerda.** Arabe (e qualquer idioma futuro RTL) define `<html dir="rtl">` e embala o layout em
  `MudRTLProvider` do MudBlazor, espelhando todo o shell.
- **ICU.** O host Web executa com ICU habilitada (`InvariantGlobalization=false`); codigo wire/parse permanece em
  `CultureInfo.InvariantCulture`, entao apenas formatacao de UI por cultura e afetada — nunca um backtest ou CSV.

## O gate — sem texto de UI codificado

Novas strings user-facing **nao podem** ser mescladas sem localizacao no escopo coberto:

- Um teste arch-guard que falha build (`NoHardcodedUiTextTests`) escaneia arquivos `.razor` migrados e falha em
  qualquer literal, atributo que carrega texto (`Label`, `Text`, `Title`, `Placeholder`, `HelperText`,
  `aria-label`, `alt`) que nao e um lookup `@L["..."]`.
- Um teste de paridade de recursos (`ResourceParityTests`) falha o build se qualquer idioma esta faltando uma chave ou ships
  um valor em branco — todo idioma sempre tem toda chave.

## Adicionando ou mudando uma string

1. Adicione/edite a chave em `tools/i18n/ui-translations.json` para **cada** cultura.
2. Regenere os `.resx`: `pwsh tools/i18n/gen-resx.ps1`.
3. Referencie no componente com `@L["your.key"]`.
4. `dotnet test` — os gates de paridade e texto-hardcoded mantem voce honesto.

## Localizacao de docs

Estes docs tambem sao localizados. i18n do Docusaurus e configurado para todos os 23 locales (`website/i18n/`), com um
dropdown de locale na navbar e RTL para Arabe. Scaffold arquivos de traducao de um locale com
`npm run write-translations -- --locale <code>` e traduz sob `website/i18n/<code>/`. Conforme o
mandato de localizacao, **adicionar ou mudar qualquer doc significa atualizar cada locale na mesma mudanca**.
