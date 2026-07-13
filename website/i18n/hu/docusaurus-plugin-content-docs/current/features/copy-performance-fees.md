---
title: Masolasi teljesitmenydijak
description: "Terhelj dijat a masolasi szolgaltatasert - magas vizjel stilusan, a masolt hozam egy reszet kovetelve, csak nyereseg felett."
---

# Masolasi teljesitmenydijak

Terhelj díjat a másolási szolgáltatásért - magas vízjel stílusban, a másolt hozam egy részét követelve, csak nyereség felett.

## hogyan mukodik

A szolgáltató beállít egy **high-water mark** (HWM) stratégiát:
- **Belépési díj** (opcionális) - egyszeri díj a masolás indításakor
- **Teljesítménydíj** - % a másolt nyereségből, csak a HWM felett
- **HWM** - a legmagasabb elért érték; a díj csak az új csúcs felett kerül felszámításra

## Példa

```
Szolgáltató HWM: $10,000
Jelenlegi egyenleg: $11,000
Nyereség a HWM felett: $1,000
Teljesítménydíj: 20%
Fizetendő díj: $200
```

## Beallitas

1. **Beallitasok → Masolasi dijak**
2. Allitsd be a **Belépési díjat** (vagy 0)
3. Allitsd be a **Teljesítménydíjat** (%)
4. Allitsd be a **HWM Reset feltételeket** (opcionális)
5. Mentsd a beállításokat

## API

```http
GET  /api/copy/fees
PUT  /api/copy/fees
POST /api/copy/fees/accrue
GET  /api/copy/fees/history
```

## Kapcsolodo

- **[Copy Trading](./copy-trading.md)**
- **[Copy Notifications](./copy-notifications.md)**
- **[AI Copy Recommender](./ai-copy-recommender.md)**
