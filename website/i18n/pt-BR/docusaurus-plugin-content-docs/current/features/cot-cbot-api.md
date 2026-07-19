# COT cBot API

Os dados do Commitment of Traders são expostos para cBots e clientes externos sobre uma API REST autenticada,
para que uma estratégia possa extrair posicionamento (posição líquida, % de interesse em aberto, índice COT)
como entrada de sinal. Reutiliza o **mesmo mecanismo JWT e escopo `market:read`** que a API de mercado de pares —
um token, um esquema.

## Autenticação

1. No aplicativo, emita um cliente de API de dados de mercado (proprietário) e conceda a ele o escopo **`market:read`**.
2. Troque o id/segredo do cliente por um token portador de curta duração:

   ```http
   POST /api/calendar/v1/token
   Content-Type: application/json

   { "clientId": "…", "clientSecret": "…" }
   ```

   A resposta contém `token`, `expiresAt` e os `scopes` concedidos.
3. Envie o token a cada chamada COT:

   ```http
   Authorization: Bearer <token>
   ```

Um token ausente/inválido retorna `401`; um token sem `market:read` retorna `403`.

## Endpoints

Caminho base `/api/market/v1/cot`. Todas as respostas são JSON.

| Método e caminho | Propósito |
|---------------|---------|
| `GET /markets` | O catálogo de mercado de contrato rastreado. Opcional `group` (Fx, Metals, Energy, Agriculture, Softs, Rates, Indices, Crypto) e palavra-chave `q`. |
| `GET /latest?code={code}&kind={kind}&combined={bool}` | O snapshot semanal mais recente para um mercado. |
| `GET /history/{code}?kind={kind}&combined={bool}&from={iso}&to={iso}` | Histórico semanal em uma janela. |

Parâmetros:

- `code` — o código de mercado de contrato CFTC (ex: `099741` para Euro FX; obtenha-o em `/markets`).
- `kind` — `Legacy` (padrão), `Disaggregated` ou `Tff`.
- `combined` — `true` para futuros + opções, `false` (padrão) para apenas futuros.
- `asOf` (ISO-8601, opcional) — âncora ponto no tempo: apenas relatórios públicos naquele instante são retornados,
  então um backtest não vê look-ahead.

### Exemplo

```http
GET /api/market/v1/cot/latest?code=088691&kind=Legacy HTTP/1.1
Authorization: Bearer <token>
```

```json
{
  "contractCode": "088691",
  "marketName": "Gold",
  "kind": "Legacy",
  "combined": false,
  "reportDate": "2024-01-02T00:00:00+00:00",
  "knownAt": "2024-01-05T20:30:00+00:00",
  "openInterest": 450000,
  "cotIndex": 82.4,
  "extreme": "LongExtreme",
  "categories": [
    { "category": "NonCommercial", "long": 250000, "short": 90000, "net": 160000, "longPercentOfOi": 55.5 }
  ]
}
```

## Ferramentas MCP

O mesmo modelo de leitura está disponível para clientes de IA como ferramentas MCP: `CotMarkets`, `CotLatest`, `CotHistory`
e `CotHealth` — cada um correto ponto no tempo através de um `asOf` opcional. Veja
[recurso Commitment of Traders](./cot-report.md) para a visão geral completa.

## Gating

A API está atrás da mesma porta de dois níveis que a página: `App:Branding:EnableCot` e `App:Features:Cot`.
Com qualquer um desligado, cada rota em `/api/market/v1/cot` retorna `404`.
