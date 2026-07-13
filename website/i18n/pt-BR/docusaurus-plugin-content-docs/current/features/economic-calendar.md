# Calendario economico

cMind ships seu **proprio** calendario economico — agenda de releases, atuals, previsoes, revisoes e um
modelo de impacto baseado em dados — originado de **autoridades primarias** (bancos centrais e agencias
estatisticas nacionais), com **zero dependencia** de ForexFactory, FXStreet, Investing.com ou qualquer
agregador. E ponto-no-tempo correto, mantem >= 10 anos de historico, e conectado a trading, a API
publica, MCP, cBots, AI, alertas e backtests. E um modulo desacoplado: pode ser desabilitado com
zero efeito no nucleo de trading.

> **Status.** O nucleo de dominio (modelo de impacto, mapeamento pais→simbolo, politica de janela de noticias, cadeias de revisao ponto-no-tempo em duas camadas) **e** persistencia (o schema Postgres `calendar`, o lado append-only de leitura/escrita, o conector FRED e o worker de ingestao controlado por config) estao implementados e testados
> (unidade + integracao Testcontainers). A JWT REST API, as ferramentas MCP e a UI aterrizam nas
> fases subsequentes descritas abaixo.

## O que o diferencia

As queixas recorrentes contra os principais calendarios se tornaram nossas restricoes de design:

- **Sem mudancas silenciosas de rating de impacto.** Nosso rating de impacto e **deterministico, versionado e
  auditavel**. Toda mudanca e uma revisao gravada com timestamp — nunca uma sobrescrita silenciosa. Um
  usuario pode ver exatamente *porque* um evento e Alto.
- **Uma ancora UTC por evento.** Todo evento e ancorado a um instante UTC unico do horario oficial da fonte
  primaria; o timezone proprio da fonte e armazenado, e a renderizacao por usuario usa um timezone IANA explicito com DST tratado pelo banco de dados de zona — nunca um toggle manual de ±1h.
- **Cadeias de revisao completas, em todo lugar.** O valor original e cada revisao sao first-class, expostos
  identicamente pela API, MCP e superfices cBot.
- **>= 10 anos de historico, sem muro.** Range de navegacao irrestrito; sem tampa de 60 dias, sem gate de registro.
- **Ponto-no-tempo por construcao.** Todo fato carrega `KnownAt` (quando *nós* aprendemos) e
  `EffectiveAt` (o instante do evento). "Como o calendario parecia no tempo T" e uma query first-class, entao uma
  regra de noticia backtestada se comporta exatamente como live — sem visao de futuro de valores revisados no historico.

## O modelo de impacto

O score de impacto e uma funcao pura e deterministica em `[0, 100]`, dividida em Low / Medium / High /
Critical. Seus inputs sao apenas dados conhecidos no tempo de scoring (sem vazamento futuro):

- **Serie prior** — um peso base por classe de indicador (decisao de taxa supera CPI, que
  supera uma pesquisa menor).
- **Pega de volatilidade realizada** — o retorno absoluto mediano dos simbolos afetados primarios na janela
  apos os *passados* releases deste indicador: "este release historicamente move o preco assim."
- **Sensibilidade de surpresa** — quao fortemente a surpresa absoluta (um z-score) historicamente
  correlacionou com o movimento pos-release.

O score combina estes com pesos fixos e carimba um `ImpactModelVersion`. Recomputar e uma
operacao explicita e logada que produz uma **nova revisao** — nunca uma mutacao — entao o score e sempre
reproduzivel de seus inputs.

## Pais → moeda → mapeamento de simbolo

O pepino de integracao de algoritmo mais citado e resolvido uma vez, como uma funcao pura: um pais mapeia para
sua moeda (cada membro da area do euro fan out para EUR), e uma moeda mapeia para os simbolos de watchlist que a
cotam em qualquer perna. Entao **EURUSD e afetado por eventos da EU e dos US**; XAUUSD e exposto a USD;
US500 mapeia para USD. Isso conduz o filtro de noticias, a resolucao de simbolos afetados e a matematica de blackout.

## Politica de janela de noticias

Um `NewsWindowRule` e `{ minImpact, beforeMinutes, afterMinutes, currencies?, series? }`. Uma implementacao unica e
compartilhada e pura responde "esta instante T dentro de um blackout para simbolo S?" — usada pelo filtro de noticias cBot,
o pause de copy-trade e o guarda de risco AI, entao nunca podem divergir. Em incerteza a resposta de blackout
e por padrao o valor conservador configurado (fail-closed por padrao) entao uma lacuna de dados
nunca green-lights trading atraves de um release de alto impacto.

## Ponto-no-tempo e revisoes

Atuals, previsoes e scores de impacto sao **append-only**. Cada evento possui uma cadeia ordenada de
revisoes, monotonica em `KnownAt`:

- `Scheduled` — o evento foi primeiramente agendado (impacto previo, sem actual).
- `Released` — o primeiro actual chegou.
- `Revised` — uma revisao posterior chegou.
- `Rescheduled` — a fonte moveu o instante do release (auditavel, alertavel).
- `Rescored` — o score de impacto foi recomputado sob uma nova versao de modelo.

Query `as of` um instante passado retorna exatamente a revisao conhecida entao — a garantia que mata
visao de futuro em regras de noticias backtestadas.

## Previsoes / consenso

A mediana do consenso de economistas **nao** e publicada gratuitamente por fontes primarias — e o valor agregado
proprietario dos agregadores, e nos nao a fabricamos. O schema de evento carrega um `Forecast` nullable;
um deployment pode conectar um feed de consenso licenciado atraves da porta opcional `IForecastProvider`
(trazer sua propria chave, off por padrao). Valores anteriores e revisoes sempre vem da fonte oficial.

## Fontes de dados

Duas camadas desacopladas, todas primarias — nunca um agregador:

- **Agenda / timing:** calendario de releases FRED; agencias estatisticas nacionais (BLS, BEA, Census,
  Eurostat, ONS, Destatis, INSEE, e-Stat, ABS, StatCan); calendarios de reunioes de bancos centrais (Fed, ECB,
  BoE, BoJ, RBA, BoC, SNB, RBNZ).
- **Valores atuais:** FRED (com datas de vintage para revisoes e ponto-no-tempo), mais BLS, BEA, Census,
  ECB SDW, Eurostat e APIs SDMX do OECD.

Uma fonte morta degrada cobertura **para aquela fonte apenas**; o calendario continua servindo tudo mais
e surfaceia a lacuna como uma metrica de frescor.

## Rate limiting e plano de backup

Provedores externos publicam limites de taxa (FRED permite ~120 requisicoes/minuto). O calendario e construido para
**nunca tripotar o limite de um provedor**, e para que ser throttled ou cortado nunca degrade reads:

- **Throttling proativo.** Todo cliente HTTP de fonte passa por um gate de taxa compartilhado e thread-safe
  que espaça requisicoes outbound para um budget configurado (`App:Calendar:FredRequestsPerMinute`, padrao
  100 — deliberadamente abaixo do teto do provedor). Requisicoes sao enfileiradas e pacingadas, nunca em burst.
- **Honra `429 Retry-After`.** Se um provedor retorna `429 Too Many Requests`, o gate backa a fonte
  inteira pelo cooldown solicitado pelo servidor (ou `App:Calendar:RateLimitBackoff`, padrao 60s)
  antes da proxima chamada — nenhum loop de retry apertado.
- **Resiliencia padrao.** Cada cliente de fonte tambem herda o handler de resiliencia do app (retry com
  backoff + jitter, circuit breaker, timeouts), entao blips transitórios sao absorvidos e uma
  fonte persistentemente falhando e estacionada (sua cobertura vai estagnar) sem afetar as outras.
- **O plano de backup — o cache read-through duravel.** Reads **nunca** sao servidos chamando um
  provedor. Uma vez que uma range e buscada e persistida append-only no Postgres e servida de lah
  para sempre (veja §"Load on-demand"). Entao mesmo quando uma fonte e rate-limited ou down, o calendario
  continua respondendo de dados cacheados, ponto-no-tempo-corretos; o span faltante simplesmente permanece
  descoberto e e retried no proximo ciclo de ingestao. Respostas de blackout adicionalmente falham para o padrao
  conservador sob incerteza, entao uma lacuna de dados nunca green-lights trading attravers de um release.
- **Polling barato.** Fetch condicional (ETag / If-Modified-Since / cursores de vintage da fonte) e o
  "busque uma span uma vez, nunca de novo" cache mantem o volume de requisicao real bem abaixo de qualquer limite em operacao
  normal — o rate gate e uma rede de seguranca, nao o caminho comum.

## Habilitar / desabilitar

Duas camadas independentes, exatamente como outras funcionalidades cMind:

- **Camada 1 — toggle de funcionalidade de runtime** (`Feature.EconomicCalendar`) virado da UI admin de Features;
  sem redeploy, pega efeito live.
- **Camada 2 — gate hard white-label** (`App:Branding:EnableEconomicCalendar`, padrao `true`). Um
  revendedor o define `false` para remover a funcionalidade inteiramente; um operador entao nao pode re-abilita-lo.

Estado efetivo e `Branding.EnableEconomicCalendar && FeatureToggle.EconomicCalendar`. Quando desabilitado,
a entrada nav e escondida e `/economic-calendar`, `/api/calendar/**` e as ferramentas MCP do calendario retornam
um 404 limpo de funcionalidade-desabilitada — nunca um 500. Historico persistido e retido em toggle-off de runtime
para re-abilitacao instantanea.

## Fases de rollout

- **P0 — nucleo de dominio** *(implementado)*: agregados, objetos de valor, portas, modelo de impacto,
  mapeamento pais→simbolo, politica de janela de noticias, gating de duas camadas, suite de unidade completa.
- **P1 — persistencia + uma fonte** *(implementado)*: schema EF `calendar` (tabelas proprias, append-only,
  indices hot), o leitor `IEconomicCalendar` read-through com `asOf` ponto-no-tempo, o servico de escrita idempotente
  append-only, o conector FRED tras um cliente tipado resiliente, e o worker de ingestao controlado por config;
  testes de integracao Testcontainers (persistencia, PIT, idempotencia, blackout).
- **P2 — JWT REST API publica + UI Web** *(implementado)*: API `/api/calendar/v1` versionada e protegida por JWT —
  emissao de cliente, troca de token e os endpoints de leitura principais (events, history, series,
  surprises, next, blackout, affected-symbols, health) com aplicacao de escopo e gating de duas camadas,
  testada em integracao. Mais a pagina **mobil-first `/economic-calendar`** — uma agenda de releases
  futuras como cartoes amigaveis para telefone com chips de impacto coloridos e um **dialogo de filtro MudBlazor**
  (moedas + impacto minimo + um **seletor De data** para pular para
  **qualquer** data passada em todo o historico — sem tampa de 60 dias, sem muro); entrada nav, testes smoke/mobile/a11y/E2E.
  Uma **pagina de serie historica por indicador** (`/economic-calendar/series/{code}`, linkada de cada
  evento) lista o historico completo de prints de uma serie. Os graficos de surpresa e scroll infinito vem depois.
- **P3 — mais fontes & warm-up** *(iniciado)*: um **catalogo de series core** (CPI, Core CPI, NFP,
  desemprego, PIB, PCE, Fed funds, vendas no varejo → seus ids FRED) e seedeado automaticamente na inicializacao,
  e um **backfill proativo idempotente** de uma vez puxa >= 10 anos de historico para que o
  caso comum seja quente sem esperar um usuario perder. **Ingestao e ligada por padrao**
  (`App:Calendar:IngestionEnabled`, padrao `true`): a **fonte de calendario de banco central** precisa **de nenhuma chave de API**,
  entao o calendario FOMC / ECB / BoE popula out of the box — o backfill seedeia aquelas datas de reuniao
  atraves de **tanto historico recente quanto do horizonte futuro**, entao navegar *o mes passado* (ou qualquer
  janela passada) mostra as reunioes mesmo antes de qualquer chave FRED/BLS ser configurada; as series de valores preenchem
  uma vez que suas chaves sao configuradas. Os workers honram o gate de duas camadas do calendario — um deployment white-label ou
  o owner desabilitando a funcionalidade calendario-economico para a ingestao, e `App:Calendar:IngestionEnabled=false`
  o desliga explicitamente. **Frescor por fonte** tambem e real agora: o worker registra o ultimo poll
  bem-sucedido de cada fonte, contagem de falhas consecutivas e uma flag de circuit tripped (persisitido em app settings,
  cross-process), e o endpoint `/health` e a ferramenta MCP `calendar_health` reportam um veredicto `stale`
  verdadeiro por fonte. **BLS** (uma segunda fonte de valores) e a **fonte de calendario de banco central** (datas de decisao FOMC / ECB / BoE, backfilled atraves de historico e sincronizado para frente em uma janela de horizonte pelo worker)
  estao inclusos. Ainda por vir: fontes de valor BEA/Census/ECB-SDW/Eurostat/OECD e a passagem de reconciliacao.
- **P4 — integracao profunda**: **ferramentas MCP** *(implementado — paridade completa com API de leitura: `calendar_events`,
  `calendar_event`, `calendar_history`, `calendar_series`, `calendar_surprises`, `calendar_next`,
  `calendar_blackout`, `calendar_affected_symbols`, `calendar_health`, controlado pela funcionalidade)* e o
  **trigger `EconomicEvent` de alertas** *(implementado — um `AlertRule` que dispara N minutos antes de um
  release iminente em/acima de um impacto escolhido, opcionalmente estreitado para moedas; avaliado pelo
  worker de alertas existente sem AI, deduplicado por release; criado via
  `POST /api/alerts/rules/economic-event`)*. O gate de blackout de noticias do prop-guard **e o
  pause de copy-trade** estao inclusos (§5.1 — um `App:Copy:NewsPauseEnabled` opt-in, padrao off: uma fonte
  aberta cujo simbolo esta em um blackout de impacto Critical e pulada, hot path byte-identico quando off). O
  **overlay de evento de backtest** esta incluso — `GET /api/calendar/v1/for-symbol` e a
  ferramenta `calendar_events_for_symbol` MCP retornam os eventos ponto-no-tempo-corretos afetando um simbolo em uma
  janela, e a **pagina de instancia/relatorio de backtest** renderiza os releases de alto impacto que ocorreram dentro da
  janela de backtest abaixo da curva de equity (entao um autor ve quais trades pousaram em NFP), controlado e
  localizado. O plano completo agora esta implementado.
- **P5 — extras**: analiticas de surprise, exportacao iCal/CSV, busca por palavra-chave, consenso conectavel.

Veja a [referencia cBot & REST API](calendar-cbot-api.md) para a superfice de integracao.
