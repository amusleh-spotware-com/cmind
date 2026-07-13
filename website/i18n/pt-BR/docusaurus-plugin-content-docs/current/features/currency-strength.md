# Forca de moeda AI macro e perspectiva futura

cMind ships um motor macro de forca de moeda **com assistencia AI e matematicamente deterministico**. Ele classifica um
universo configuravel de moedas — as 8 majors mais moedas de mercados emergentes e exoticos — por
**forca** fundamental **atual**, e projeta uma **perspectiva direcional futura** para cada par sobre um
horizonte escolhido (1M / 3M / 6M / 12M). Cada classificacao, cada vies de par e cada numero e computado por matematica
deterministica pura no nucleo de dominio; o LLM apenas **reune** os inputs futuros que os dados
nao publicam e **explica** o resultado em portugues. Ele nunca inventa uma classificacao, direcao ou
numero.

> **Limitacao honesta.** Fundamentos predictam valor de medio-a-longo prazo bem e valor de curto prazo mal. Trate isto como um filtro de posicionamento / confluencia, **nao** um sinal de timing de curto prazo. Leituras
> perto de releases de alto impacto (NFP/CPI/banco central) sao ruidosas. Nao e conselho financeiro.

## Como funciona

1. **Fundamentos atuais vem do Calendario Economico, nao do LLM.** Os numeros concretos — taxas de policy,
   CPI vs meta, PIB, emprego, balanca comercial — e seus **z-scores de surpresa** sao originados
   **ponto-no-tempo** do modulo de [calendario economico](./economic-calendar.md) (FRED/BLS/BEA/ECB e
   calendarios de bancos centrais). Um snapshot historico nunca vaza visao de futuro.
2. **O LLM reúne apenas o que o calendario nao pode publicar** — por moeda: a trajetoria **futura**
   (caminho de taxa de policy esperada em bp, tendencia de inflacao-vs-meta, momentum de crescimento) e uma perspectiva **geopolitica**
   (risk-on/off, tarifas, fiscal/divida, eleicoes), mais quaisquer figuras EM/exoticas atuais que
   o calendario carece. JSON estrito, validacao tier-aware, web search ligado.
3. **O dominio computa a classificacao e a matriz futura deterministicamente.** Cada driver e pontuado como um
   **z-score within-tier** (entao uma inflacao de 50% exotica nunca distorce os majors), winsorizado,
   weighted-sum em um composite, e classificado strongest→weakest com um tie-break ISO estavel. A camada futura carrega cada composite ao longo de sua trajetoria —
   `projected = current + horizonScale · Σ trajectoryDriver·weight` — e mapeia o diferencial projetado de cada par a um **vies direcional** (▲ appreciate / ▬ neutral / ▼ depreciate) com uma conviccao.
4. **O LLM explica** a classificacao e as principais chamadas de par em linguagem simples.

## Os drivers

| Driver | Efeito na forca | Notas |
|---|---|---|
| Taxa de policy e trajetoria | Maior / hawkish ⇒ mais forte | Maior peso; divergencia de banco central guia as maiores lacunas. |
| Inflacao (CPI vs meta) | Acima da meta ⇒ mais fraco | Pontuado inversamente (arrasto de poder de compra). |
| Crescimento do PIB | Crescimento relativo maior ⇒ mais forte | Diferencial vs o painel. |
| Emprego | Mercado de trabalho mais forte ⇒ mais forte | Alimenta o caminho de policy. |
| Balanca comercial / conta corrente | Superavit ⇒ mais forte | Demanda estrutural. |
| Postura de policy | Hawkish ⇒ mais forte | O driver primario de longo prazo. |
| Momentum de surpresa | Wins recentes ⇒ mais forte | Dos z-scores de surpresa do calendario. |
| Geopolitico / risco | Risk-off ⇒ safe havens (USD/JPY/CHF) mais fortes | Delta de risco futuro limitado. |
| Real yield / carry *(EM/exotic)* | Taxa real positiva ⇒ mais forte | Driver dominante EM em regimes calmos. |
| Vulnerabilidade externa *(EM/exotic)* | Deficits / reservas baixas / divida em USD ⇒ mais fraco | Pressao de depreciacao estrutural. |
| Termos de troca *(exportadores de commodities)* | Precos de exportacao em alta ⇒ mais forte | BRL, ZAR, CLP, NOK, AUD, CAD. |
| Risco politico/institucional *(EM/exotic)* | Instabilidade ⇒ mais fraco | Banda morta mais larga, conviccao capped. |

## Universo em camadas (majors + EM + exotics)

O universo e **configuravel por deployment** (`App:CurrencyStrength:Universe`) — adicionar uma moeda e
config, nao codigo. Cada moeda carrega uma **tier** (`Major` / `EmergingMarket` / `Exotic`) que ajusta
ponderacao, largura de banda morta e tampa de conviccao:

- **Majors** — USD, EUR, GBP, JPY, AUD, NZD, CAD, CHF (led por nivel de taxa).
- **Mercados emergentes** — CNH, INR, BRL, MXN, ZAR, KRW, SGD, PLN (+ Scandi NOK/SEK); carry + risco +
  vulnerabilidade externa ponderados para cima, confianca media.
- **Exotics** — TRY, HUF, CZK, mais HKD/SAR USD-pegged; baixa confianca, banda morta mais larga, conviccao capped.
  **Moedas fixadas/fortemente gerenciadas** (HKD, SAR, CNH) sao sinalizadas, sua trajetoria e
  down-weighted, e a perspectiva de par e clamped para `Neutral` para que uma fixa nunca seja lida como um
  sinal de free-floating.

Porque oficiales EM/exotic stats sao de menor frequencia, revisados e as vezes opacos, as figuras reunidas pelo AI carregam uma **confianca por tier** mostrada como badge de confiabilidade.

## Degradacao harmonica

| Calendario | AI | Resultado |
|---|---|---|
| ✅ | ✅ | Classificacao completa + projecao futura + narrativa (`CalendarAndAi`). |
| ✅ | ❌ | Classificacao atual baseada apenas em calendario, sem projecao futura (`CalendarOnly`). |
| ❌ | ✅ | Figuras atuais reunidas por AI + futura, confianca menor (`AiOnly`). |
| ❌ | ❌ | Nenhum snapshot — o widget se esconde e a pagina mostra estado vazio. |

O app executa inalterado de qualquer forma. AI e controlada pela chave AI; a perna do calendario respeita seu
proprio gate white-label + toggle de runtime.

## Usando

- **Habilite AI** (Settings → AI) e **ative o widget** do seu proprio dialogo de **Customize** do dashboard
  ("Currency strength" — opt-in, escondido por padrao). O widget mostra as moedas mais fortes/fracas e a principal chamada de par 3M; link para a pagina completa.
- **Pagina completa** — `/ai/currency-strength`: um seletor de horizonte (1M/3M/6M/12M), um filtro de tier
  (All/Majors/EM/Exotics), a classificacao atual, a previsao futura, a matriz de perspectiva de par (vies +
  conviccao, paridade/low-confidence sinalizada), e a narrativa AI. Pressione **Refresh now** (owner) para
  regenerar. Um worker background (`App:CurrencyStrength:RefreshEnabled`, **padrao `true`**) atualiza em um
  schedule para que a pagina seja populada out of the box; um deployment ou owner o desliga (ou desabilita
  a funcionalidade AI / calendario economico, que o atualizador honra degradando para nenhum snapshot).

## Acesso programatico

Um modelo de leitura compartilhado (`ICurrencyStrengthQuery`) e alcancavel de tres formas:

- **AI in-app** — injetado diretamente (in-process) em funcionalidades AI.
- **MCP** — a ferramenta `currency_strength` (params `horizon`, `tier`) para clientes/agentes AI.
- **cBot REST** — `GET /api/market/v1/currency-strength/{latest,history,pair/{base}/{quote}}`, protegido
  pela mesma maquinaria de `CalendarJwt` do [calendario cBot API](./calendar-cbot-api.md) com um
  escopo adicional **`market:read`**. Um cBot registra um cliente de API com `market:read`, troca seu
  id + secret por um JWT short-lived em `POST /api/calendar/v1/token`, e chama os endpoints com um
  token `Bearer`. Nenhum segundo esquema de JWT, nenhum segundo secret — um token vazado e
  somente leitura, scoped no mercado, short-lived e revogavel.

Veja o [calendario cBot API](./calendar-cbot-api.md) para o fluxo de token e um exemplo copy-paste.
