---
title: Dev Credentials
description: Sdílený dev credentials soubor pro lokální / E2E / live testování přes všechny úrovně.
---

# Dev Credentials

Lokální vývoj, E2E testy, a live testy všechny potřebují cTrader sandbox pověření (demo účty). cMind čte z **jednoho centralizovaného souboru**.

## Soubor

Umístění: `src/Nodes/CtraderCliNode/dev-credentials.json` (`.gitignore`d)

Obsah:

```json
{
  "CTraderDemoAccounts": [
    {
      "Host": "sandbox-api.ctrader.com",
      "Port": 443,
      "ClientId": "1234567",
      "ClientSecret": "...",
      "DemoAccountId": 12345678,
      "DemoPassword": "password",
      "DemoEmail": "email@example.com"
    }
  ],
  "CTraderOpenApi": {
    "AppId": "...",
    "AppSecret": "..."
  }
}
```

## Jak je to používáno

- **Lokální dev** — Web host čte pro live backtest + copy demo  
- **E2E testy** — fixture `FakeTradingSession` pro deterministic test, nebo real sandbox když credentials k dispozici
- **Live copy testy** — skutečné cTrader demo účty

Bez souboru — E2E běží s fake trades; s souborem — E2E běží live.

## Setup

Vytvořte `.json` z cTrader sandbox console (API keys, account credentials). Nikdy commit.

Viz [testing/live-copy-trading.md](live-copy-trading.md) pro live test procedury.
