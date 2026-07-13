# Calendario REST & cBot API

O calendario economico e exposto como uma **API REST versionada, protegida por JWT e com rate limiting** — a superfice
de integracao principal. Qualquer servico externo, dashboard ou cBot se integra contra ele como um produto. Tem
paridade de funcionalidades com a FXStreet Calendar API e passa dela: ponto-no-tempo `asOf`, cadeias de revisao completas,
justificativa de impacto deterministica, analiticas de surprise, resolucao pais→simbolo e matematica de blackout
que outras APIs de calendario nao expoem.

> **Status.** A seguranca JWT (emissao de cliente + troca de token), o gating e os endpoints de leitura principais —
> `token`, `events`, `events/{id}`, `history`, `series`, `surprises`, `next`, `blackout`,
> `affected-symbols`, `health` — estao **implementados e testados em integracao** (auth, aplicacao de escopo,
> feature/white-label 404), mais **`events/batch`** (multiplex bounded) e um documento **`/openapi.json`** descobrivel,
> **`ETag`/`If-None-Match` 304** nos reads de evento/historico, **keyset cursor pagination**
> (`Link: rel="next"`), o **stream SSE** (push ao vivo `event: release`,
> poll-backed), **webhooks HMAC-signed** (`X-CMind-Signature: sha256=…`, registrados pelo owner, delivered por um
> worker controlado por config para fora de uma watermark persistida), e o **cliente tipado shipped**
> (`CmindCalendarClient`). A superfice publica da API completa esta implementada.

## Seguranca — JWT

A API reutiliza a maquinaria existente de token HS256 do repo (o mesmo padrao que os agentes CtraderCliNode
usam), nao um novo esquema:

- Um admin do app emite um **Cliente de API do Calendario** (nome + escopos + expiracao). O cliente troca seu id
  e secret em `POST /api/calendar/v1/token` por um **JWT HS256 short-lived**
  (`iss=cmind-calendar`, `aud=calendar-api`, `exp` ~15 min, `scope` claim). Apenas o JWT curto viaja
  nas requisicoes (`Authorization: Bearer <jwt>`).
- O secret do cliente e armazenado **criptografado** via `ISecretProtector` — nunca em texto puro, nunca logado.
- **Escopos** (menor privilegio): `calendar:read`, `calendar:blackout`, `calendar:surprises`,
  `calendar:stream`. Um token cBot tipicamente recebe `read` + `blackout` apenas.
- Validacao `JwtBearer` padrao (issuer, audience, lifetime, signing key; `alg=none` rejeitado; skew de relogio apertado). Rate limit por cliente com bucket de token + limitador global; `429` com `Retry-After`. Todos
  as falhas de auth sao auditadas.
- Desabilitar o cliente para emissao futura de token imediatamente; o curto tempo de vida do JWT limita um token vazado. Toda a arvore `/api/calendar/**` da **404** quando a funcionalidade esta desabilitada.

## Convencoes

- **Caminho base e versao:** `/api/calendar/v1/...` (URL-versioned; mudancas aditivas nao bumpam).
- **Formato:** JSON; instantes UTC RFC 3339 mais um `sourceTimeZone` explicito; opcional `tz=` renderiza
  uma hora local conveniencia sem perder a ancora UTC.
- **Paginacao:** baseada em cursor (`cursor`, `limit` <= 1000); cursor `next` no body e um header `Link`.
- **Cache:** `ETag` + `If-None-Match`; ranges historicos ganham um TTL longo, os proximos um curto.
- **Erros:** RFC 7807 `problem+json`, nunca um 500 nu.
- **Reads degradados:** uma falha de fonte/DB retorna `200` com melhores dados conhecidos mais um sinal
  `X-Calendar-Freshness` / `stale=true` (ou `503 Retry-After` somente se verdadeiramente nada e conhecido) — o cBot decide.

## Endpoints

| Metodo e caminho | Proposito | Params chave |
|---|---|---|
| `POST /v1/token` | Troca id+secret do cliente → JWT curto | body: `clientId`, `clientSecret` |
| `GET /v1/events` | Eventos em uma janela (proximo ou historico) | `from`,`to`,`countries`,`currencies`,`series`,`minImpact`,`category`,`q`,`asOf`,`cursor`,`limit`,`tz` |
| `GET /v1/events/{id}` | Um evento: cadeia de revisao completa, surprise, justificativa de impacto, simbolos afetados | `watchlist?`,`asOf?` |
| `GET /v1/events/{id}/revisions` | Historico ordenado de revisoes | — |
| `GET /v1/history` | Pull historico profundo para uma serie (>=10y) | `series`,`from`,`to`,`asOf`,`cursor`,`limit` |
| `GET /v1/series` | Catalogo de indicadores rastreados + cadencia + fonte | `countries`,`currencies`,`q` |
| `GET /v1/surprises` | Serie historica de actual/forecast/surprise z-score | `series`,`count`/`from,to` |
| `GET /v1/next` | Proximo release relevante para um simbolo (pais→simbolo mapeado) | `symbol`,`minImpact` |
| `GET /v1/blackout` | Esta um simbolo dentro de uma janela de alto impacto agora/em T | `symbol`,`at?`,`minImpact`,`before`,`after` |
| `GET /v1/affected-symbols` | Resolver um evento → simbolos em uma watchlist | `eventId`,`watchlist` |
| `POST /v1/events:batch` | Multiplex varias queries em uma ida | body: array de queries |
| `GET /v1/stream` (SSE) | Push ao vivo: releases/revisoes/entrada de janela | `currencies`,`minImpact` (escopo `calendar:stream`) |
| `POST /v1/webhooks` | Registrar callback HMAC-signed para release/revisao/blackout | body: url, filters, secret |
| `GET /v1/health` | Frescor por fonte + cobertura | — |

## Blackout — o filtro de noticias cBot

`GET /v1/blackout` retorna `{ inBlackout, event, startsAt, endsAt, stale }`. Em incertezaDefaults
para a **resposta conservadora configurada** (fail-closed por padrao: "assuma em blackout" para bots risk-off)
, mais uma flag `stale` — uma lacuna de dados nunca green-lights trading attravers NFP. O endpoint e uma
leitura pura de DB/cache com um hard server timeout; nao ha busca sincrona de origem no hot path.

Um cliente tipado shipped (`Infrastructure.Calendar.CmindCalendarClient`) embrulha isso: aponta seu `HttpClient`
para a raiz da API, chama `GetTokenAsync(clientId, clientSecret)` uma vez, entao `GetBlackoutAsync(token, symbol)`
antes de cada ordem — e **fail-safe por construcao** (qualquer non-success ou parse error retorna
`InBlackout = true, Stale = true`, entao uma lacuna de dados nunca green-lights trading). Um cBot pausa em torno de noticias assim:

```csharp
// Pseudocode for a cTrader cBot using WebRequest + a Calendar API client token.
var jwt = CalendarApi.GetToken(clientId, clientSecret);           // POST /v1/token
var res = CalendarApi.Blackout(jwt, symbol: SymbolName,           // GET  /v1/blackout
                               minImpact: "High", before: 15, after: 15);
if (res.InBlackout || res.Stale)                                  // fail-safe: stale ⇒ treat as blackout
    return;                                                       // skip new entries in the news window
// ...otherwise proceed to place the order
```

## Ponto-no-tempo para backtests

Passe `asOf` em qualquer read para obter o calendario exatamente como estava em um instante passado — os atuals,
previsoes e revisoes *como eram entao*. Porque reads `asOf` sao puros e cacheaveis, um backtest
martelando o historico recebe bytes identicos toda vez, e uma regra de noticia backtestada se comporta exatamente como a
live (sem visao de futuro de valores revisados).

## Resiliencia para chamadores algo

A API esta em um hot path de trading, entao nunca lana em um bot live: todo caminho retorna um
`problem+json` bem-formed ou um body degradado typed. Ela reutiliza as primitivas de resiliencia de copy-trading —
o handler HTTP resiliente padrao em cada cliente de fonte, um circuit breaker de dominio por fonte, um
worker de ingestao singleton lease-guardado com reconciliacao de inicializacao, e health checks wireados em
`/health`. O snippet do cliente tipado shipped vem com retry + timeout + circuit breaker pre-configurados
para que autores de bots herdem resiliencia.

## Sibling: forca de moeda AI (`market:read`)

O modelo de leitura de [forca de moeda AI](./currency-strength.md) usa a **mesma** maquinaria JWT —
um esquema, um secret de assinatura, um rate-limiter — adicionando apenas um escopo `market:read`. Registre um cliente de API com
aquele escopo, troque por um token exatamente como acima, e chame:

```
GET /api/market/v1/currency-strength/latest?horizon=3M&tier=Majors
GET /api/market/v1/currency-strength/history?days=30
GET /api/market/v1/currency-strength/pair/EUR/USD?horizon=3M
```

```csharp
// obtain a token via POST /api/calendar/v1/token as above, then:
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
var view = await http.GetFromJsonAsync<JsonElement>(
    baseUrl + "/api/market/v1/currency-strength/latest?horizon=3M");
// view.ranking[], view.forecasts[], view.pairs[] (bias/conviction), view.narrative
```

Um token faltando `market:read` obtem `403`; um token expirado/tampered obtem `401`. Os endpoints sao controlados
pelo flag de funcionalidade AI e servidos em `/api/market/v1` para permanecerem independentes do gate de funcionalidade
do calendario. Em dispatch de run/backtest um deployment pode injetar `CMIND_API_BASEURL` + um token short-lived
`market:read` para que um cBot callback com zero registro de cliente.
