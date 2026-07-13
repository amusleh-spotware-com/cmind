---
title: Agent Studio
description: "Az Agent Studio a cMind AI-alapú ügynökrendszere, ahol autonóm ügynököket hozhatsz létre, konfigurálhatsz és kezelhetsz különböző feladatokra - kereskedési stratégiák, kockázatkezelés, riasztások és még sok más."
---

# Agent Studio

Az Agent Studio a cMind AI-alapú ügynökrendszere. Itt hozol letre, konfigurálsz és kezelsz autonóm ügynököket, akik a nevedben dolgoznak - kereskedési stratégiákat építenek, kockázatot figyelnek, riasztásokat kezelnek, és még sok más.

## Ahhoz valo hozzaferes

Az Agent Studio csak azoknak érhető el, akiknek a telepítés engedélyezi az AI funkciókat (`App:Ai:Enabled` + `AppOptions.Ai.ApiKey` beállítva). Ha az AI nincs konfigurálva, az ügynökök oldal üres üzenetet mutat.

## Mi az az Agent

Egy **Agent** egy autonóm egység, amely:
- egy `Mandate`-vel rendelkezik (mit csináljon)
- egy `AgentType`-pel rendelkezik (milyen típusú)
- képes önállóan döntéseket hozni a rábízott feladatokról
- képes más ügynökökkel együttműködni

## Agent tipusok

| Tipus | Feladata |
|-------|---------|
| `Trading` | Kereskedési döntések, stratégia-építés |
| `Risk` | Kockázatfigyelés és -kezelés |
| `Alert` | Riasztások kezelése és továbbítása |
| `Coach` | Kereskedési napló elemzés és javaslatok |
| `Research` | Piaci kutatás és információgyűjtés |

## Agent letrehozasa

1. Kattints az **Új Agent** gombra.
2. Válaszd ki az **Agent típusát**.
3. Add meg az **Agent nevét** és **leírását**.
4. Állítsd be a **Mandate**-et (utasítások az ügynöknek).
5. Konfiguráld a **paramétereket** (típus-függő).
6. Kattints a **Létrehozás** gombra.

## Agent kezelés

- **Szerkesztés:** kattints az ügynök nevére → szerkesztő oldal.
- **Törlés:** szerkesztő oldalon kattints a **Törlés** gombra.
- **Aktiválás/deaktiválás:** kapcsoló az ügynök kártyáján.
- **Logok:** minden ügynök logja a **Agent Logs** oldalon érhető el.

## Kapcsolódasi pontok

Az ügynökök más cMind funkciókkal integrálódnak:
- **Kereskedési ügynökök** → Másolási kereskedés, Backtesting
- **Kockázati ügynökök** → Prop-Firm szabályok, Alert Rules
- **Coach ügynökök** → Trading Journal, Strategy Health
- **Riasztási ügynökök** → Economic Calendar, Position Sizing

## API endpointok

```http
GET    /api/agents           # Lista
POST   /api/agents           # Létrehozás
GET    /api/agents/{id}      # Részletek
PUT    /api/agents/{id}      # Frissítés
DELETE /api/agents/{id}      # Törlés
POST   /api/agents/{id}/run  # Futtatás
GET    /api/agents/{id}/logs # Logok
```
