---
title: Masolasi ertekitesek
description: "Ertesulj a masolasi szamladod es masolt poziciod allapot valtozasairol - masolas inditasa, megallitasa, uj pozicio, pozicio zarasa, egyenleg visszaallitas."
---

# Masolasi ertekitesek

Értesülj a másolási számlád és másolt pozícióid állapot változásairól.

## Tipusok

| Esemény | Mikor tunik fel |
|---------|----------------|
| `CopyStart` | Amikor egy masolási profil masolása elindul |
| `CopyStop` | Amikor egy masolási profil masolása megall |
| `NewPosition` | Amikor a masolt szamlan uj pozicio nyilik |
| `PositionClosed` | Amikor egy masolt pozicio zarul |
| `BalanceReset` | Amikor a masolt szamla egyenlege visszaall egy ismert ertekre |

## Beallitas

1. **Beallitasok → Masolasi ertekitesek**
2. Kapcsold be a kivant esemeny-tipusokat
3. Valaszd ki az erteitesi csatornat (email, SignalR, webhook)
4. Mentsd a beallitasokat

## API

```http
GET  /api/notifications/copy
POST /api/notifications/copy
PUT  /api/notifications/copy/{id}
DELETE /api/notifications/copy/{id}
```

## Kapcsolodo

- **[Copy Trading](./copy-trading.md)**
- **[Copy Performance Fees](./copy-performance-fees.md)**
- **[AI Copy Recommender](./ai-copy-recommender.md)**
